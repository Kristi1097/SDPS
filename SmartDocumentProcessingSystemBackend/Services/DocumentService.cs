using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartDocumentProcessingSystem.Configuration;
using SmartDocumentProcessingSystem.DatabaseContext;
using SmartDocumentProcessingSystem.Dtos;
using SmartDocumentProcessingSystem.Models;
using SmartDocumentProcessingSystem.Services.Processing;

namespace SmartDocumentProcessingSystem.Services;

public class DocumentService : IDocumentService
{
    private readonly SDPSContext _context;
    private readonly DocumentTextExtractor _textExtractor;
    private readonly DocumentParser _parser;
    private readonly DocumentValidator _validator;
    private readonly ProcessingOptions _options;
    private readonly IWebHostEnvironment _environment;

    public DocumentService(
        SDPSContext context,
        DocumentTextExtractor textExtractor,
        DocumentParser parser,
        DocumentValidator validator,
        IOptions<ProcessingOptions> options,
        IWebHostEnvironment environment)
    {
        _context = context;
        _textExtractor = textExtractor;
        _parser = parser;
        _validator = validator;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<IReadOnlyList<DocumentSummaryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Include(x => x.ValidationIssues)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return documents.Select(ToSummaryDto).ToList();
    }

    public async Task<DocumentDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(id, cancellationToken);
        return document is null ? null : ToDto(document);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Include(x => x.ValidationIssues)
            .ToListAsync(cancellationToken);

        var statusCounts = documents
            .GroupBy(x => x.Status)
            .ToDictionary(x => x.Key, x => x.Count());

        var totalsByCurrency = documents
            .Where(x => !string.IsNullOrWhiteSpace(x.Currency) && x.Total is not null)
            .GroupBy(x => x.Currency!)
            .ToDictionary(x => x.Key, x => x.Sum(doc => doc.Total!.Value));

        return new DashboardSummaryDto(
            statusCounts,
            documents.Sum(x => x.ValidationIssues.Count(issue => issue.Severity == ValidationSeverity.Error)),
            documents.Sum(x => x.ValidationIssues.Count(issue => issue.Severity == ValidationSeverity.Warning)),
            totalsByCurrency);
    }

    public async Task<DocumentDto> ProcessUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var document = await ProcessStreamAsync(stream, file.FileName, cancellationToken);
        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(document);
    }

    public async Task<IReadOnlyList<DocumentDto>> ImportSamplesAsync(bool refreshExisting, CancellationToken cancellationToken)
    {
        var samplePath = ResolveSampleDataPath();
        if (!Directory.Exists(samplePath))
        {
            throw new DirectoryNotFoundException($"Sample data path was not found: {samplePath}");
        }

        var existingDocuments = await _context.Documents
            .Include(x => x.LineItems)
            .Include(x => x.ValidationIssues)
            .Where(x => x.OriginalFileName != string.Empty)
            .ToDictionaryAsync(x => x.OriginalFileName, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var imported = new List<Document>();
        foreach (var file in Directory.GetFiles(samplePath)
            .Where(path => DocumentTextExtractor.IsSupported(Path.GetExtension(path)))
            .Where(path => !Path.GetFileName(path).StartsWith("Screenshot ", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            if (existingDocuments.TryGetValue(fileName, out var existingDocument))
            {
                if (!refreshExisting)
                {
                    continue;
                }

                await using var refreshStream = File.OpenRead(file);
                var refreshed = await ProcessStreamAsync(refreshStream, fileName, cancellationToken, existingDocument.Id);
                RefreshEntity(existingDocument, refreshed);
                imported.Add(existingDocument);
                continue;
            }

            await using var stream = File.OpenRead(file);
            var document = await ProcessStreamAsync(stream, fileName, cancellationToken);
            _context.Documents.Add(document);
            imported.Add(document);
            existingDocuments.Add(document.OriginalFileName, document);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return imported.Select(ToDto).ToList();
    }

    public async Task<DocumentDto?> UpdateAsync(int id, UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        document.Type = request.Type;
        document.Supplier = request.Supplier;
        document.DocumentNumber = request.DocumentNumber;
        document.IssueDate = request.IssueDate;
        document.DueDate = request.DueDate;
        document.Currency = request.Currency;
        document.Subtotal = request.Subtotal;
        document.Tax = request.Tax;
        document.Total = request.Total;
        document.UpdatedAtUtc = DateTime.UtcNow;

        document.LineItems.Clear();
        foreach (var item in request.LineItems)
        {
            document.LineItems.Add(new LineItem
            {
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                Tax = item.Tax,
                Total = item.Total
            });
        }

        await RevalidateLoadedDocumentAsync(document, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(document);
    }

    public async Task<DocumentDto?> RevalidateAsync(int id, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        await RevalidateLoadedDocumentAsync(document, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(document);
    }

    public async Task<DocumentDto?> ConfirmAsync(int id, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (document.ValidationIssues.Any(x => x.Severity == ValidationSeverity.Error))
        {
            document.Status = DocumentStatus.NeedsReview;
        }
        else
        {
            document.Status = DocumentStatus.Validated;
        }

        document.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(document);
    }

    public async Task<DocumentDto?> RejectAsync(int id, CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        document.Status = DocumentStatus.Rejected;
        document.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(document);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _context.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return false;
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Document> ProcessStreamAsync(Stream stream, string fileName, CancellationToken cancellationToken, int? currentDocumentId = null)
    {
        var extension = Path.GetExtension(fileName);
        var supported = DocumentTextExtractor.IsSupported(extension);
        var rawText = supported ? await _textExtractor.ExtractAsync(stream, fileName, cancellationToken) : string.Empty;
        var extracted = _parser.Parse(rawText, fileName, extension);

        if (!supported)
        {
            extracted.ExtractionIssues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                FieldPath = "file",
                Message = $"Unsupported file extension: {extension}"
            });
        }

        if (string.IsNullOrWhiteSpace(rawText) && extension is ".png" or ".jpg" or ".jpeg")
        {
            extracted.ExtractionIssues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                FieldPath = "rawText",
                Message = "No OCR text was extracted. Install Tesseract and ensure it is available on PATH."
            });
        }

        var duplicate = await IsDuplicateDocumentNumberAsync(extracted.DocumentNumber, currentDocumentId, cancellationToken);
        var issues = _validator.Validate(extracted, duplicate);

        return ToEntity(extracted, issues);
    }

    private async Task RevalidateLoadedDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        var extracted = new ExtractedDocument
        {
            OriginalFileName = document.OriginalFileName,
            FileExtension = document.FileExtension,
            Type = document.Type,
            Supplier = document.Supplier,
            DocumentNumber = document.DocumentNumber,
            IssueDate = document.IssueDate,
            DueDate = document.DueDate,
            Currency = document.Currency,
            Subtotal = document.Subtotal,
            Tax = document.Tax,
            Total = document.Total,
            RawText = document.RawText,
            LineItems = document.LineItems.Select(x => new LineItem
            {
                Description = x.Description,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                TaxRate = x.TaxRate,
                Tax = x.Tax,
                Total = x.Total
            }).ToList()
        };

        var duplicate = await IsDuplicateDocumentNumberAsync(document.DocumentNumber, document.Id, cancellationToken);
        var issues = _validator.Validate(extracted, duplicate);
        document.ValidationIssues.Clear();
        document.ValidationIssues.AddRange(issues);
        document.Status = issues.Any(x => x.Severity == ValidationSeverity.Error)
            ? DocumentStatus.NeedsReview
            : DocumentStatus.Validated;
        document.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<bool> IsDuplicateDocumentNumberAsync(string? documentNumber, int? currentDocumentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return false;
        }

        return await _context.Documents.AnyAsync(
            x => x.DocumentNumber == documentNumber && (currentDocumentId == null || x.Id != currentDocumentId),
            cancellationToken);
    }

    private async Task<Document?> LoadDocumentAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Documents
            .Include(x => x.LineItems)
            .Include(x => x.ValidationIssues)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private string ResolveSampleDataPath()
    {
        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.SampleDataPath));
    }

    private static Document ToEntity(ExtractedDocument extracted, List<ValidationIssue> issues)
    {
        var hasErrors = issues.Any(x => x.Severity == ValidationSeverity.Error);
        var hasWarnings = issues.Any(x => x.Severity == ValidationSeverity.Warning);

        return new Document
        {
            OriginalFileName = extracted.OriginalFileName,
            FileExtension = extracted.FileExtension,
            Type = extracted.Type,
            Supplier = extracted.Supplier,
            DocumentNumber = extracted.DocumentNumber,
            IssueDate = extracted.IssueDate,
            DueDate = extracted.DueDate,
            Currency = extracted.Currency,
            Subtotal = extracted.Subtotal,
            Tax = extracted.Tax,
            Total = extracted.Total,
            RawText = extracted.RawText,
            Status = hasErrors || hasWarnings ? DocumentStatus.NeedsReview : DocumentStatus.Validated,
            LineItems = extracted.LineItems,
            ValidationIssues = issues
        };
    }

    private static void RefreshEntity(Document target, Document source)
    {
        target.FileExtension = source.FileExtension;
        target.Type = source.Type;
        target.Supplier = source.Supplier;
        target.DocumentNumber = source.DocumentNumber;
        target.IssueDate = source.IssueDate;
        target.DueDate = source.DueDate;
        target.Currency = source.Currency;
        target.Subtotal = source.Subtotal;
        target.Tax = source.Tax;
        target.Total = source.Total;
        target.RawText = source.RawText;
        target.Status = source.Status;
        target.UpdatedAtUtc = DateTime.UtcNow;
        target.LineItems.Clear();
        target.LineItems.AddRange(source.LineItems);
        target.ValidationIssues.Clear();
        target.ValidationIssues.AddRange(source.ValidationIssues);
    }

    private static DocumentDto ToDto(Document document)
    {
        return new DocumentDto(
            document.Id,
            document.OriginalFileName,
            document.FileExtension,
            document.Type,
            document.Supplier,
            document.DocumentNumber,
            document.IssueDate,
            document.DueDate,
            document.Currency,
            document.Subtotal,
            document.Tax,
            document.Total,
            document.Status,
            document.LineItems.Select(ToLineItemDto).ToList(),
            document.ValidationIssues.Select(ToIssueDto).ToList(),
            document.CreatedAtUtc,
            document.UpdatedAtUtc);
    }

    private static DocumentSummaryDto ToSummaryDto(Document document)
    {
        return new DocumentSummaryDto(
            document.Id,
            document.OriginalFileName,
            document.Type,
            document.Supplier,
            document.DocumentNumber,
            document.Currency,
            document.Total,
            document.Status,
            document.ValidationIssues.Count(x => x.Severity == ValidationSeverity.Error),
            document.ValidationIssues.Count(x => x.Severity == ValidationSeverity.Warning),
            document.CreatedAtUtc);
    }

    private static LineItemDto ToLineItemDto(LineItem item)
    {
        return new LineItemDto(item.Id, item.Description, item.Quantity, item.UnitPrice, item.TaxRate, item.Tax, item.Total);
    }

    private static ValidationIssueDto ToIssueDto(ValidationIssue issue)
    {
        return new ValidationIssueDto(issue.Id, issue.Severity, issue.FieldPath, issue.Message, issue.ExpectedValue, issue.ActualValue);
    }
}

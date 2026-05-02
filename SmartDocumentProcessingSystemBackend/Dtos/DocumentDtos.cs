using SmartDocumentProcessingSystem.Models;

namespace SmartDocumentProcessingSystem.Dtos;

public record LineItemDto(
    int Id,
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TaxRate,
    decimal? Tax,
    decimal? Total);

public record ValidationIssueDto(
    int Id,
    ValidationSeverity Severity,
    string FieldPath,
    string Message,
    string? ExpectedValue,
    string? ActualValue);

public record DocumentDto(
    int Id,
    string OriginalFileName,
    string FileExtension,
    DocumentType Type,
    string? Supplier,
    string? DocumentNumber,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal? Total,
    DocumentStatus Status,
    IReadOnlyList<LineItemDto> LineItems,
    IReadOnlyList<ValidationIssueDto> ValidationIssues,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record DocumentSummaryDto(
    int Id,
    string OriginalFileName,
    DocumentType Type,
    string? Supplier,
    string? DocumentNumber,
    string? Currency,
    decimal? Total,
    DocumentStatus Status,
    int ErrorCount,
    int WarningCount,
    DateTime CreatedAtUtc);

public record DashboardSummaryDto(
    IReadOnlyDictionary<DocumentStatus, int> StatusCounts,
    int ErrorCount,
    int WarningCount,
    IReadOnlyDictionary<string, decimal> TotalsByCurrency);

public class UpdateDocumentRequest
{
    public DocumentType Type { get; set; }
    public string? Supplier { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Currency { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
    public List<UpdateLineItemRequest> LineItems { get; set; } = [];
}

public class UpdateLineItemRequest
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
}

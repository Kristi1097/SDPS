using SmartDocumentProcessingSystem.Models;
using Microsoft.Extensions.Options;
using SmartDocumentProcessingSystem.Configuration;
using SmartDocumentProcessingSystem.Services.Processing;

var root = FindRepoRoot();
var samplePath = Path.Combine(root, "sample-data", "resources");
var extractor = new DocumentTextExtractor(Options.Create(new ProcessingOptions()));
var parser = new DocumentParser();
var validator = new DocumentValidator();

await TestPdfWithWrongTotal(samplePath, extractor, parser, validator);
TestCsvParsing(samplePath, parser, validator);
TestTextParsing(samplePath, parser, validator);

Console.WriteLine("All parser/validator tests passed.");

static async Task TestPdfWithWrongTotal(string samplePath, DocumentTextExtractor extractor, DocumentParser parser, DocumentValidator validator)
{
    await using var stream = File.OpenRead(Path.Combine(samplePath, "invoice_1.pdf"));
    var rawText = await extractor.ExtractAsync(stream, "invoice_1.pdf", CancellationToken.None);
    var document = parser.Parse(rawText, "invoice_1.pdf", ".pdf");
    var issues = validator.Validate(document, duplicateDocumentNumber: false);

    AssertEqual(DocumentType.Invoice, document.Type, "invoice_1 type");
    AssertEqual("INV-1000", document.DocumentNumber, "invoice_1 number");
    AssertEqual("Company 0", document.Supplier, "invoice_1 supplier");
    AssertEqual(645m, document.Subtotal, "invoice_1 subtotal");
    AssertEqual(129.0m, document.Tax, "invoice_1 tax");
    AssertEqual(800.0m, document.Total, "invoice_1 total");
    AssertTrue(issues.Any(x => x.Severity == ValidationSeverity.Error && x.FieldPath == "total"), "invoice_1 should report total error");
}

static void TestCsvParsing(string samplePath, DocumentParser parser, DocumentValidator validator)
{
    var rawText = File.ReadAllText(Path.Combine(samplePath, "data_1.csv"));
    var document = parser.Parse(rawText, "data_1.csv", ".csv");
    var issues = validator.Validate(document, duplicateDocumentNumber: false);

    AssertEqual(2, document.LineItems.Count, "data_1 line item count");
    AssertEqual(246m, document.Total, "data_1 calculated total");
    AssertTrue(issues.Any(x => x.Severity == ValidationSeverity.Error && x.FieldPath == "documentNumber"), "CSV should require document number");
}

static void TestTextParsing(string samplePath, DocumentParser parser, DocumentValidator validator)
{
    var rawText = File.ReadAllText(Path.Combine(samplePath, "text_1.txt"));
    var document = parser.Parse(rawText, "text_1.txt", ".txt");
    var issues = validator.Validate(document, duplicateDocumentNumber: false);

    AssertEqual(DocumentType.Invoice, document.Type, "text_1 type");
    AssertEqual("TXT-0", document.DocumentNumber, "text_1 number");
    AssertEqual("EUR", document.Currency, "text_1 currency");
    AssertEqual(758m, document.Total, "text_1 total");
    AssertTrue(issues.Any(x => x.Severity == ValidationSeverity.Warning && x.FieldPath == "lineItems"), "TXT should warn on missing line items");
}

static string FindRepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "sample-data")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repo root with sample-data folder.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException(label);
    }
}

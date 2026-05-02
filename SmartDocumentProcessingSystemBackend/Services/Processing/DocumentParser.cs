using System.Globalization;
using System.Text.RegularExpressions;
using SmartDocumentProcessingSystem.Models;

namespace SmartDocumentProcessingSystem.Services.Processing;

public class DocumentParser
{
    private static readonly Regex InvoiceNumberRegex = new(@"\b(?:invoice\s*(?:no\.?|#)?|number:?)\s*([A-Z]*-?\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PurchaseOrderNumberRegex = new(@"\b(?:po|purchase\s+order)\s*(?:no\.?|#|number:?)?\s*([A-Z]*-?\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"\b(?:date|issue date|dated):?\s*([0-9]{4}-[0-9]{2}-[0-9]{2}|[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}|[0-9]{1,2}-[A-Za-z]{3}-[0-9]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DueDateRegex = new("\\b(?:due date|date d'ech[e\\u00e9]ance):?\\s*([0-9]{4}-[0-9]{2}-[0-9]{2}|[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}|[0-9]{1,2}\\s+[A-Za-z]+\\s+[0-9]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SupplierRegex = new(@"\bSupplier:?\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TotalRegex = new(@"\b(?:grand\s+total|total\s+due|total):?\s*\p{Sc}?\s*([0-9]+(?:[.,][0-9]+)?)\s*([A-Z]{3})?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CurrencyRegex = new(@"\b(USD|EUR|BAM|GBP|AED|INR)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ExtractedDocument Parse(string rawText, string fileName, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".csv" => ParseCsv(rawText, fileName, extension),
            ".txt" => ParseText(rawText, fileName, extension),
            _ => ParseSemiStructured(rawText, fileName, extension)
        };
    }

    private static ExtractedDocument ParseCsv(string rawText, string fileName, string extension)
    {
        var document = NewDocument(fileName, extension);
        document.Type = DocumentType.Unknown;
        document.RawText = rawText;
        document.DocumentNumber = null;
        document.Supplier = null;
        document.Currency = null;

        var lines = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Skip(1))
        {
            var columns = line.Split(',');
            if (columns.Length < 4)
            {
                continue;
            }

            var quantity = ParseDecimal(columns[1]);
            var unitPrice = ParseDecimal(columns[2]);
            var total = ParseDecimal(columns[3]);
            document.LineItems.Add(new LineItem
            {
                Description = columns[0].Trim(),
                Quantity = quantity,
                UnitPrice = unitPrice,
                Total = total
            });
        }

        document.Subtotal = document.LineItems.Sum(x => x.Total ?? 0);
        document.Tax = 0;
        document.Total = document.Subtotal;
        return document;
    }

    private static ExtractedDocument ParseText(string rawText, string fileName, string extension)
    {
        var document = NewDocument(fileName, extension);
        document.RawText = rawText;
        document.Type = rawText.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            ? DocumentType.PurchaseOrder
            : DocumentType.Invoice;

        var firstLine = rawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLine))
        {
            document.DocumentNumber = firstLine.Replace("Invoice", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        ApplyTotalAndCurrency(document, rawText);
        return document;
    }

    private static ExtractedDocument ParseSemiStructured(string rawText, string fileName, string extension)
    {
        var document = NewDocument(fileName, extension);
        document.RawText = rawText;
        document.Type = rawText.Contains("purchase order", StringComparison.OrdinalIgnoreCase)
            ? DocumentType.PurchaseOrder
            : rawText.Contains("invoice", StringComparison.OrdinalIgnoreCase)
                ? DocumentType.Invoice
                : DocumentType.Unknown;

        document.Supplier = MatchValue(SupplierRegex, rawText);
        document.DocumentNumber = document.Type == DocumentType.PurchaseOrder
            ? MatchValue(PurchaseOrderNumberRegex, rawText)
            : MatchValue(InvoiceNumberRegex, rawText) ?? MatchValue(PurchaseOrderNumberRegex, rawText);

        document.IssueDate = ParseDate(MatchValue(DateRegex, rawText));
        document.DueDate = ParseDate(MatchValue(DueDateRegex, rawText));
        document.Currency = MatchValue(CurrencyRegex, rawText)?.ToUpperInvariant();

        ParsePdfTable(document, rawText);
        ApplyTotalAndCurrency(document, rawText);
        return document;
    }

    private static void ParsePdfTable(ExtractedDocument document, string rawText)
    {
        var tokens = rawText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var descriptionIndex = Array.FindIndex(tokens, token => token.Equals("Description", StringComparison.OrdinalIgnoreCase));
        if (descriptionIndex < 0)
        {
            return;
        }

        var subtotalIndex = Array.FindIndex(tokens, token => token.Equals("Subtotal", StringComparison.OrdinalIgnoreCase));
        if (subtotalIndex < 0 || subtotalIndex < descriptionIndex + 7)
        {
            return;
        }

        var row = tokens.Skip(descriptionIndex + 4).Take(4).ToArray();
        if (row.Length == 4)
        {
            document.LineItems.Add(new LineItem
            {
                Description = row[0],
                Quantity = ParseDecimal(row[1]),
                UnitPrice = ParseDecimal(row[2]),
                Total = ParseDecimal(row[3])
            });
        }

        document.Subtotal = ParseDecimal(tokens.ElementAtOrDefault(subtotalIndex + 1));
        var taxLabelIndex = Array.FindIndex(tokens, token => token.StartsWith("Tax", StringComparison.OrdinalIgnoreCase));
        if (taxLabelIndex >= 0)
        {
            var taxRateMatch = Regex.Match(tokens[taxLabelIndex], @"([0-9]+(?:[.,][0-9]+)?)%");
            var lineItem = document.LineItems.FirstOrDefault();
            if (lineItem is not null && taxRateMatch.Success)
            {
                lineItem.TaxRate = ParseDecimal(taxRateMatch.Groups[1].Value);
            }

            document.Tax = ParseDecimal(tokens.ElementAtOrDefault(taxLabelIndex + 1));
        }

        var totalIndex = Array.FindLastIndex(tokens, token => token.Equals("Total", StringComparison.OrdinalIgnoreCase));
        if (totalIndex >= 0)
        {
            document.Total = ParseDecimal(tokens.ElementAtOrDefault(totalIndex + 1));
        }
    }

    private static void ApplyTotalAndCurrency(ExtractedDocument document, string rawText)
    {
        var totalMatch = TotalRegex.Matches(rawText).Cast<Match>().LastOrDefault(x => x.Success);
        if (totalMatch is not null)
        {
            document.Total ??= ParseDecimal(totalMatch.Groups[1].Value);
            if (totalMatch.Groups[2].Success)
            {
                document.Currency = totalMatch.Groups[2].Value.ToUpperInvariant();
            }
        }

        document.Currency ??= MatchValue(CurrencyRegex, rawText)?.ToUpperInvariant();
    }

    private static ExtractedDocument NewDocument(string fileName, string extension)
    {
        return new ExtractedDocument
        {
            OriginalFileName = fileName,
            FileExtension = extension
        };
    }

    private static string? MatchValue(Regex regex, string rawText)
    {
        var match = regex.Match(rawText);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy", "d-MMM-yyyy", "d MMMM yyyy" };
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)
            ? exact
            : DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}

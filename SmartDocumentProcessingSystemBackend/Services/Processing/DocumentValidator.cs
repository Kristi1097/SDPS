using SmartDocumentProcessingSystem.Models;

namespace SmartDocumentProcessingSystem.Services.Processing;

public class DocumentValidator
{
    public List<ValidationIssue> Validate(ExtractedDocument document, bool duplicateDocumentNumber)
    {
        var issues = new List<ValidationIssue>();
        issues.AddRange(document.ExtractionIssues);

        Require(document.DocumentNumber, "documentNumber", "Document number is required.", issues);
        if (duplicateDocumentNumber)
        {
            issues.Add(Error("documentNumber", "Duplicate document number detected.", null, document.DocumentNumber));
        }

        if (document.Type == DocumentType.Unknown)
        {
            issues.Add(Warning("type", "Document type could not be confidently detected.", "Invoice or PurchaseOrder", "Unknown"));
        }

        if (string.IsNullOrWhiteSpace(document.Supplier))
        {
            issues.Add(Warning("supplier", "Supplier/company name is missing or could not be extracted.", null, null));
        }

        if (document.IssueDate is null)
        {
            issues.Add(Warning("issueDate", "Issue date is missing or could not be extracted.", null, null));
        }

        if (document.DueDate is not null && document.IssueDate is not null && document.DueDate < document.IssueDate)
        {
            issues.Add(Error("dueDate", "Due date cannot be before issue date.", document.IssueDate.Value.ToString("yyyy-MM-dd"), document.DueDate.Value.ToString("yyyy-MM-dd")));
        }

        if (string.IsNullOrWhiteSpace(document.Currency))
        {
            issues.Add(Warning("currency", "Currency is missing or could not be extracted.", null, null));
        }

        if (document.LineItems.Count == 0)
        {
            issues.Add(Warning("lineItems", "No line items were extracted.", null, null));
        }

        ValidateLineItems(document, issues);
        ValidateTotals(document, issues);

        return issues;
    }

    private static void ValidateLineItems(ExtractedDocument document, List<ValidationIssue> issues)
    {
        for (var i = 0; i < document.LineItems.Count; i++)
        {
            var item = document.LineItems[i];
            if (item.Quantity is null || item.UnitPrice is null || item.Total is null)
            {
                issues.Add(Warning($"lineItems[{i}]", "Line item is missing quantity, unit price, or total.", null, null));
                continue;
            }

            var expected = RoundMoney(item.Quantity.Value * item.UnitPrice.Value);
            var actual = RoundMoney(item.Total.Value);
            if (expected != actual)
            {
                issues.Add(Error($"lineItems[{i}].total", "Line item total does not equal quantity times unit price.", expected.ToString("0.##"), actual.ToString("0.##")));
            }
        }
    }

    private static void ValidateTotals(ExtractedDocument document, List<ValidationIssue> issues)
    {
        if (document.LineItems.Count > 0)
        {
            var expectedSubtotal = RoundMoney(document.LineItems.Sum(x => x.Total ?? 0));
            if (document.Subtotal is null)
            {
                issues.Add(Warning("subtotal", "Subtotal is missing.", expectedSubtotal.ToString("0.##"), null));
            }
            else if (expectedSubtotal != RoundMoney(document.Subtotal.Value))
            {
                issues.Add(Error("subtotal", "Subtotal does not equal the sum of line item totals.", expectedSubtotal.ToString("0.##"), document.Subtotal.Value.ToString("0.##")));
            }
        }

        if (document.Subtotal is not null && document.Tax is not null && document.Total is not null)
        {
            var expectedTotal = RoundMoney(document.Subtotal.Value + document.Tax.Value);
            var actualTotal = RoundMoney(document.Total.Value);
            if (expectedTotal != actualTotal)
            {
                issues.Add(Error("total", "Total does not equal subtotal plus tax.", expectedTotal.ToString("0.##"), actualTotal.ToString("0.##")));
            }
        }

        foreach (var item in document.LineItems.Where(x => x.TaxRate is not null))
        {
            var expectedTax = RoundMoney((item.Total ?? 0) * item.TaxRate!.Value / 100);
            if (document.Tax is not null && document.LineItems.Count == 1 && expectedTax != RoundMoney(document.Tax.Value))
            {
                issues.Add(Error("tax", "Tax does not match extracted tax rate.", expectedTax.ToString("0.##"), document.Tax.Value.ToString("0.##")));
            }
        }
    }

    private static void Require(string? value, string fieldPath, string message, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Error(fieldPath, message, null, null));
        }
    }

    private static ValidationIssue Error(string fieldPath, string message, string? expected, string? actual)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Error,
            FieldPath = fieldPath,
            Message = message,
            ExpectedValue = expected,
            ActualValue = actual
        };
    }

    private static ValidationIssue Warning(string fieldPath, string message, string? expected, string? actual)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            FieldPath = fieldPath,
            Message = message,
            ExpectedValue = expected,
            ActualValue = actual
        };
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

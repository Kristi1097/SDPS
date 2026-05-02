namespace SmartDocumentProcessingSystem.Models;

public class ValidationIssue
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string FieldPath { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

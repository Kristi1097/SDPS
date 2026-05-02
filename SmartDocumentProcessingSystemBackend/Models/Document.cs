namespace SmartDocumentProcessingSystem.Models;

public class Document
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public DocumentType Type { get; set; } = DocumentType.Unknown;
    public string? Supplier { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Currency { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public string? RawText { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<LineItem> LineItems { get; set; } = [];
    public List<ValidationIssue> ValidationIssues { get; set; } = [];
}

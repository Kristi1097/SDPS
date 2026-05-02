namespace SmartDocumentProcessingSystem.Models;

public class LineItem
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
}

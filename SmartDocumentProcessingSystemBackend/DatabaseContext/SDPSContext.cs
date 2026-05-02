using Microsoft.EntityFrameworkCore;
using SmartDocumentProcessingSystem.Models;

namespace SmartDocumentProcessingSystem.DatabaseContext;

public class SDPSContext : DbContext
{
    public SDPSContext(DbContextOptions<SDPSContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<LineItem> LineItems => Set<LineItem>();
    public DbSet<ValidationIssue> ValidationIssues => Set<ValidationIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.FileExtension).HasMaxLength(16);
            entity.Property(x => x.Supplier).HasMaxLength(256);
            entity.Property(x => x.DocumentNumber).HasMaxLength(80);
            entity.Property(x => x.Currency).HasMaxLength(8);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.Tax).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.HasMany(x => x.LineItems)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.ValidationIssues)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.DocumentNumber);
        });

        modelBuilder.Entity<LineItem>(entity =>
        {
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
            entity.Property(x => x.Tax).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ValidationIssue>(entity =>
        {
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.FieldPath).HasMaxLength(128);
            entity.Property(x => x.Message).HasMaxLength(512);
            entity.Property(x => x.ExpectedValue).HasMaxLength(128);
            entity.Property(x => x.ActualValue).HasMaxLength(128);
        });
    }
}

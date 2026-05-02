using SmartDocumentProcessingSystem.Dtos;
using SmartDocumentProcessingSystem.Models;

namespace SmartDocumentProcessingSystem.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentSummaryDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<DocumentDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken);
    Task<DocumentDto> ProcessUploadAsync(IFormFile file, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentDto>> ImportSamplesAsync(bool refreshExisting, CancellationToken cancellationToken);
    Task<DocumentDto?> UpdateAsync(int id, UpdateDocumentRequest request, CancellationToken cancellationToken);
    Task<DocumentDto?> RevalidateAsync(int id, CancellationToken cancellationToken);
    Task<DocumentDto?> ConfirmAsync(int id, CancellationToken cancellationToken);
    Task<DocumentDto?> RejectAsync(int id, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}

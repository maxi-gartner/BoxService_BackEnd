using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services.Abstractions;

public interface IInspectionPhotoService
{
    Task<ServiceInspectionPhoto> AddPhotoAsync(int serviceId, IFormFile file,
        string photoType, string? description, CancellationToken cancellationToken);
    Task<string?> CreatePhotoUrlAsync(int serviceId, int photoId, CancellationToken cancellationToken);
    Task<bool> DeletePhotoAsync(int serviceId, int photoId, CancellationToken cancellationToken);
}

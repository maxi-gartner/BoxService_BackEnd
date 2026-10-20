using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Storage;

namespace BoxService_BackEnd.Services;

public sealed class ServiceInspectionService
{
    private readonly ServiceInspectionRepository _repository;
    private readonly SupabaseStorageService _storage;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    private static readonly HashSet<string> AllowedPhotoTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "front",
            "rear",
            "left_side",
            "right_side",
            "dashboard",
            "damage",
            "other"
        };

    public ServiceInspectionService(
        ServiceInspectionRepository repository,
        SupabaseStorageService storage
    )
    {
        _repository = repository;
        _storage = storage;
    }

    public Task<ServiceInspection?> GetByServiceIdAsync(
        int serviceId,
        CancellationToken cancellationToken
    )
    {
        if (serviceId <= 0)
        {
            return Task.FromResult<ServiceInspection?>(null);
        }

        return _repository.GetByServiceIdAsync(
            serviceId,
            cancellationToken
        );
    }

    public async Task<ServiceInspection> CreateAsync(
        int serviceId,
        ServiceInspectionUpsertRequest request,
        string inspectedBy,
        CancellationToken cancellationToken
    )
    {
        ValidateServiceId(serviceId);

        var serviceExists = await _repository.ServiceExistsAsync(
            serviceId,
            cancellationToken
        );

        if (!serviceExists)
        {
            throw new KeyNotFoundException(
                "No se encontró el service."
            );
        }

        var inspection = BuildInspection(
            serviceId,
            request,
            inspectedBy
        );

        return await _repository.CreateAsync(
            inspection,
            cancellationToken
        );
    }

    public async Task<ServiceInspection?> UpdateAsync(
        int serviceId,
        ServiceInspectionUpsertRequest request,
        string inspectedBy,
        CancellationToken cancellationToken
    )
    {
        ValidateServiceId(serviceId);

        var inspection = BuildInspection(
            serviceId,
            request,
            inspectedBy
        );

        return await _repository.UpdateAsync(
            inspection,
            cancellationToken
        );
    }

    public async Task<ServiceInspectionPhoto> AddPhotoAsync(
        int serviceId,
        IFormFile file,
        string photoType,
        string? description,
        CancellationToken cancellationToken
    )
    {
        ValidateServiceId(serviceId);

        var inspection = await _repository.GetByServiceIdAsync(
            serviceId,
            cancellationToken
        );

        if (inspection is null)
        {
            throw new KeyNotFoundException(
                "No se encontró la inspección."
            );
        }

        if (file.Length == 0)
        {
            throw new ArgumentException(
                "El archivo de la foto está vacío."
            );
        }

        const long maximumFileSize = 8 * 1024 * 1024;

        if (file.Length > maximumFileSize)
        {
            throw new ArgumentException(
                "La foto no puede superar los 8 MB."
            );
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new ArgumentException(
                "Solo se permiten imágenes JPEG, PNG o WEBP."
            );
        }

        photoType = photoType.Trim().ToLowerInvariant();

        if (!AllowedPhotoTypes.Contains(photoType))
        {
            throw new ArgumentException(
                "El tipo de foto no es válido."
            );
        }

        description = description?.Trim() ?? string.Empty;

        if (description.Length > 500)
        {
            throw new ArgumentException(
                "La descripción no puede superar los 500 caracteres."
            );
        }

        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            originalFileName = "photo";
        }

        if (originalFileName.Length > 255)
        {
            throw new ArgumentException(
                "El nombre del archivo no puede superar los 255 caracteres."
            );
        }

        var photoCount = await _repository.CountPhotosAsync(
            inspection.InspectionId,
            cancellationToken
        );

        if (photoCount >= 12)
        {
            throw new ArgumentException(
                "Una inspección no puede tener más de 12 fotos."
            );
        }

        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException(
                "El tipo de imagen no es compatible."
            )
        };

        var storagePath =
            $"services/{serviceId}/inspections/" +
            $"{inspection.InspectionId}/" +
            $"{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();

        await _storage.UploadAsync(
            stream,
            storagePath,
            file.ContentType,
            cancellationToken
        );

        var photo = new ServiceInspectionPhoto
        {
            InspectionId = inspection.InspectionId,
            StoragePath = storagePath,
            PhotoType = photoType,
            Description = description,
            OriginalFileName = originalFileName,
            ContentType = file.ContentType
        };

        try
        {
            return await _repository.CreatePhotoAsync(
                photo,
                cancellationToken
            );
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(storagePath);
            }
            catch
            {
                // Se conserva el error original de PostgreSQL.
            }

            throw;
        }
    }

    public async Task<string?> CreatePhotoUrlAsync(
        int serviceId,
        int photoId,
        CancellationToken cancellationToken
    )
    {
        var photo = await _repository.GetPhotoAsync(
            serviceId,
            photoId,
            cancellationToken
        );

        if (photo is null)
        {
            return null;
        }

        return await _storage.CreateSignedUrlAsync(
            photo.StoragePath
        );
    }

    public async Task<bool> DeletePhotoAsync(
        int serviceId,
        int photoId,
        CancellationToken cancellationToken
    )
    {
        var photo = await _repository.GetPhotoAsync(
            serviceId,
            photoId,
            cancellationToken
        );

        if (photo is null)
        {
            return false;
        }

        await _storage.DeleteAsync(photo.StoragePath);

        return await _repository.DeletePhotoAsync(
            photoId,
            cancellationToken
        );
    }

    private static ServiceInspection BuildInspection(
        int serviceId,
        ServiceInspectionUpsertRequest request,
        string inspectedBy
    )
    {
        if (request.FuelLevel is < 0 or > 100)
        {
            throw new ArgumentException(
                "El nivel de combustible debe estar entre 0 y 100."
            );
        }

        var observations =
            request.GeneralObservations?.Trim() ?? string.Empty;

        if (observations.Length > 2000)
        {
            throw new ArgumentException(
                "Las observaciones generales no pueden superar " +
                "los 2000 caracteres."
            );
        }

        inspectedBy = inspectedBy.Trim();

        if (
            string.IsNullOrEmpty(inspectedBy) ||
            inspectedBy.Length > 100
        )
        {
            throw new ArgumentException(
                "El usuario que realiza la inspección no es válido."
            );
        }

        var submittedItems = (request.Items ?? [])
            .GroupBy(
                item => item.ItemCode?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase
            );

        var unknownCodes = submittedItems.Keys
            .Where(
                code =>
                    !InspectionChecklistCatalog.Items.ContainsKey(code)
            )
            .ToArray();

        if (unknownCodes.Length > 0)
        {
            throw new ArgumentException(
                $"El punto de checklist '{unknownCodes[0]}' no existe."
            );
        }

        var items = InspectionChecklistCatalog.Items
            .Select(catalogItem =>
            {
                submittedItems.TryGetValue(
                    catalogItem.Key,
                    out var submitted
                );

                var status =
                    submitted?.Status?.Trim().ToLowerInvariant()
                    ?? InspectionStatuses.NotChecked;

                if (!InspectionStatuses.All.Contains(status))
                {
                    throw new ArgumentException(
                        "El estado del punto " +
                        $"'{catalogItem.Value}' no es válido."
                    );
                }

                var observation =
                    submitted?.Observation?.Trim() ?? string.Empty;

                if (observation.Length > 500)
                {
                    throw new ArgumentException(
                        "La observación del punto " +
                        $"'{catalogItem.Value}' no puede superar " +
                        "los 500 caracteres."
                    );
                }

                return new ServiceInspectionItem
                {
                    ItemCode = catalogItem.Key,
                    ItemName = catalogItem.Value,
                    Status = status,
                    Observation = observation
                };
            })
            .ToList();

        return new ServiceInspection
        {
            ServiceId = serviceId,
            FuelLevel = request.FuelLevel,
            GeneralObservations = observations,
            InspectedBy = inspectedBy,
            Items = items
        };
    }

    private static void ValidateServiceId(int serviceId)
    {
        if (serviceId <= 0)
        {
            throw new ArgumentException(
                "El identificador del service es obligatorio."
            );
        }
    }
}

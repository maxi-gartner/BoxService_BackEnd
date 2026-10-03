using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record ServiceInspectionItemRequest(
    string ItemCode,
    string Status,
    string? Observation
);

public sealed record ServiceInspectionUpsertRequest(
    int? FuelLevel,
    string? GeneralObservations,
    IReadOnlyList<ServiceInspectionItemRequest>? Items
);

public sealed record ServiceInspectionItemDto(
    int ItemId,
    string ItemCode,
    string ItemName,
    string Status,
    string Observation
);

public sealed record ServiceInspectionPhotoDto(
    int PhotoId,
    string StoragePath,
    string PhotoType,
    string Description,
    string OriginalFileName,
    string ContentType,
    DateTime CreatedAt
);

public sealed record ServiceInspectionDto(
    int InspectionId,
    int ServiceId,
    int? FuelLevel,
    string GeneralObservations,
    string InspectedBy,
    DateTime InspectedAt,
    IReadOnlyList<ServiceInspectionItemDto> Items,
    IReadOnlyList<ServiceInspectionPhotoDto> Photos
)
{
    public static ServiceInspectionDto FromModel(
        ServiceInspection inspection
    ) => new(
        inspection.InspectionId,
        inspection.ServiceId,
        inspection.FuelLevel,
        inspection.GeneralObservations,
        inspection.InspectedBy,
        inspection.InspectedAt,

        inspection.Items.Select(item =>
            new ServiceInspectionItemDto(
                item.ItemId,
                item.ItemCode,
                item.ItemName,
                item.Status,
                item.Observation
            )
        ).ToArray(),

        inspection.Photos.Select(photo =>
            new ServiceInspectionPhotoDto(
                photo.PhotoId,
                photo.StoragePath,
                photo.PhotoType,
                photo.Description,
                photo.OriginalFileName,
                photo.ContentType,
                photo.CreatedAt
            )
        ).ToArray()
    );
}

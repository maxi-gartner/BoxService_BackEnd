namespace BoxService_BackEnd.Models;

public sealed class ServiceInspection
{
    public int InspectionId { get; set; }

    public int ServiceId { get; set; }

    public int? FuelLevel { get; set; }

    public string GeneralObservations { get; set; } = string.Empty;

    public string InspectedBy { get; set; } = string.Empty;

    public DateTime InspectedAt { get; set; }

    public List<ServiceInspectionItem> Items { get; set; } = [];

    public List<ServiceInspectionPhoto> Photos { get; set; } = [];
}

public sealed class ServiceInspectionItem
{
    public int ItemId { get; set; }

    public int InspectionId { get; set; }

    public string ItemCode { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string Status { get; set; } = InspectionStatuses.NotChecked;

    public string Observation { get; set; } = string.Empty;
}

public sealed class ServiceInspectionPhoto
{
    public int PhotoId { get; set; }

    public int InspectionId { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string PhotoType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public static class InspectionStatuses
{
    public const string Ok = "ok";

    public const string Damaged = "damaged";

    public const string NotChecked = "not_checked";

    public static readonly HashSet<string> All =
        new(StringComparer.OrdinalIgnoreCase)
        {
            Ok,
            Damaged,
            NotChecked
        };
}

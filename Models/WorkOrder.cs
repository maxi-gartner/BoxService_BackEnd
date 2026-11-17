namespace BoxService_BackEnd.Models
{
    // Una visita del vehículo al taller: en_espera -> en_proceso ->
    // terminado -> entregado. Deliberadamente sin id_presupuesto/id_service
    // — es el progreso físico del auto, no el estado del presupuesto.
    public class WorkOrder
    {
        public int OrderId { get; set; }
        public int TenantId { get; set; }
        public int VehicleId { get; set; }
        public string Status { get; set; } = "en_espera";
        public string? Notes { get; set; }
        public string? ReceivedBy { get; set; }
        public string? DeliveredBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
    }

    // Fila ya resuelta con datos de vehículo/cliente para el tablero del
    // taller — mismo enfoque que ServiceReminder/VehicleStatusInquiryRow.
    public sealed record WorkOrderRow(
        int OrderId, int VehicleId, string Status, string? Notes,
        string ClientName, string Vehicle, string Plate, DateTime CreatedAt);

    public static class WorkOrderTransitions
    {
        private static readonly Dictionary<string, string> Next = new()
        {
            ["en_espera"] = "en_proceso",
            ["en_proceso"] = "terminado",
            ["terminado"] = "entregado",
        };

        public static bool IsValid(string current, string target) =>
            Next.TryGetValue(current, out var expected) && expected == target;
    }

    public class WorkOrderCreateRequest
    {
        public string? Notes { get; set; }
    }

    public class WorkOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}

namespace BoxService_BackEnd.Models
{
    // Cliente del portal preguntando "¿cómo va mi auto?" — queda pendiente
    // hasta que alguien del taller la marca como atendida.
    public class VehicleStatusInquiry
    {
        public int InquiryId { get; set; }
        public int TenantId { get; set; }
        public int VehicleId { get; set; }
        public int ClientId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    // Fila ya resuelta con los datos de cliente/vehículo para el panel del
    // taller — mismo enfoque que ServiceReminder (join resuelto en el repo,
    // no en el frontend).
    public sealed record VehicleStatusInquiryRow(
        int InquiryId, int VehicleId, int ClientId, string ClientName,
        string Vehicle, string Plate, DateTime CreatedAt);
}

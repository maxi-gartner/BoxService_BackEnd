using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services;

public static class InvoiceRules
{
    private const decimal MaximumAmount = 99999999.99m;

    public static DateTime PeriodStartUtc(DateTime localDate) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified),
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"));

    public static List<InvoiceDetail> BuildDetails(IReadOnlyList<InvoiceDetailRequest>? requests)
    {
        if (requests is null || requests.Count is < 1 or > 100)
            throw new ArgumentException("La factura debe tener entre uno y cien conceptos.");
        var result = new List<InvoiceDetail>();
        foreach (var item in requests)
        {
            if (item is null) throw new ArgumentException("Hay un concepto inválido en la factura.");
            var type = item.Type?.Trim().ToLowerInvariant();
            var description = item.Description?.Trim() ?? "";
            if (type is not ("labor" or "part"))
                throw new ArgumentException("El tipo debe ser mano de obra o repuesto.");
            if (description.Length is < 1 or > 200)
                throw new ArgumentException("Cada concepto debe tener una descripción de hasta 200 caracteres.");
            if (item.Quantity <= 0 || item.Quantity > MaximumAmount || decimal.Round(item.Quantity, 2) != item.Quantity)
                throw new ArgumentException("La cantidad debe ser positiva y tener como máximo dos decimales.");
            if (item.UnitPrice < 0 || item.UnitPrice > MaximumAmount || decimal.Round(item.UnitPrice, 2) != item.UnitPrice)
                throw new ArgumentException("El precio no puede ser negativo y debe tener como máximo dos decimales.");
            var subtotal = decimal.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
            if (subtotal > MaximumAmount) throw new ArgumentException("El importe del concepto supera el máximo permitido.");
            result.Add(new InvoiceDetail { Type = type, Description = description,
                Quantity = item.Quantity, UnitPrice = item.UnitPrice, Subtotal = subtotal });
        }
        if (result.Sum(d => d.Subtotal) > MaximumAmount)
            throw new ArgumentException("El total supera el máximo permitido.");
        return result;
    }

    public static string? Validate(Service service, Budget? budget, int tenantId, decimal total)
    {
        if (service.TenantId != tenantId) return "Service no encontrado.";
        if (budget is not null)
        {
            if (budget.TenantId != tenantId || budget.VehicleId != service.VehicleId
                || budget.ServiceId != service.ServiceId)
                return "El presupuesto debe corresponder al mismo service, vehículo y taller.";
            if (budget.Status != "completed") return "El presupuesto debe estar finalizado antes de facturar.";
        }
        if (total <= 0 || total > MaximumAmount) return "El total de la factura debe ser mayor a cero y estar dentro del máximo permitido.";
        return null;
    }
}

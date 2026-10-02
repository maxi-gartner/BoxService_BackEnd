using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record InvoiceDto(int InvoiceId, string TenantId, string Number, string Date,
    decimal Total, string Status, int ServiceId, int? BudgetId)
{
    public static InvoiceDto FromModel(Invoice value) => new(value.InvoiceId,
        ClientDto.PlaceholderTenantId, value.Number, value.Date, value.Total,
        value.Status, value.ServiceId, value.BudgetId);
}

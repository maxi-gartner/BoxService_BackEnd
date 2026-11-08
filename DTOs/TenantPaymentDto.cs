using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record TenantPaymentDto(
    int PaymentId,
    int TenantId,
    decimal Amount,
    string Period,
    string PaymentDate,
    string? Notes,
    string CreatedAt)
{
    public static TenantPaymentDto FromModel(TenantPayment p) => new(
        PaymentId: p.PaymentId,
        TenantId: p.TenantId,
        Amount: p.Amount,
        Period: p.Period,
        PaymentDate: p.PaymentDate.ToString("yyyy-MM-dd"),
        Notes: p.Notes,
        CreatedAt: p.CreatedAt.ToString("O"));
}

public sealed record TenantPaymentStatusDto(
    int TenantId,
    string TenantName,
    bool PaidCurrentPeriod,
    decimal? LastAmount,
    string? LastPeriod,
    string? LastPaymentDate)
{
    public static TenantPaymentStatusDto FromModel(TenantPaymentStatus s) => new(
        TenantId: s.TenantId,
        TenantName: s.TenantName,
        PaidCurrentPeriod: s.PaidCurrentPeriod,
        LastAmount: s.LastAmount,
        LastPeriod: s.LastPeriod,
        LastPaymentDate: s.LastPaymentDate?.ToString("yyyy-MM-dd"));
}

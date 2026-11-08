using System;

namespace BoxService_BackEnd.Models
{
    // Control de pago mensual por taller — por ahora solo "pagó este mes
    // sí/no" y cuánto, no una contabilidad completa (ver migración 013).
    public class TenantPayment
    {
        public int PaymentId { get; set; }
        public int TenantId { get; set; }
        public decimal Amount { get; set; }
        public string Period { get; set; } = string.Empty; // "YYYY-MM"
        public DateTime PaymentDate { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TenantPaymentCreateRequest
    {
        public decimal Amount { get; set; }
        public string Period { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public string? Notes { get; set; }
    }

    // GET /admin/payments — pantalla de superadmin: de un vistazo, quién
    // pagó el mes actual y quién no, con el último pago como referencia.
    public class TenantPaymentStatus
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool PaidCurrentPeriod { get; set; }
        public decimal? LastAmount { get; set; }
        public string? LastPeriod { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
}

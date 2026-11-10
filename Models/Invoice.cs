namespace BoxService_BackEnd.Models
{
    // ROL A — Maxi
    public class Invoice
    {
        public int     InvoiceId { get; set; }
        public int     TenantId  { get; set; }
        public string  Number    { get; set; } = string.Empty; // F-0001
        public string  Date      { get; set; } = string.Empty;
        public decimal Total     { get; set; }
        public string  Status    { get; set; } = "issued";     // issued | paid | cancelled
        public int     ServiceId { get; set; }
        public int?    BudgetId  { get; set; }
        public List<InvoiceDetail> Details { get; set; } = [];
    }

    // DTOs de request — camelCase, igual que el resto de la API.
    public class InvoiceCreateRequest
    {
        public int  ServiceId { get; set; }
        public int? BudgetId  { get; set; }
        public IReadOnlyList<InvoiceDetailRequest>? Details { get; set; }
    }

    public class InvoiceDetailRequest
    {
        public string Type { get; set; } = "labor";
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; } = 1;
        [System.Text.Json.Serialization.JsonRequired]
        public decimal UnitPrice { get; set; }
    }

    public class InvoiceDetail
    {
        public int DetailId { get; set; }
        public string Type { get; set; } = "labor";
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class InvoiceStatusRequest
    {
        public string Status { get; set; } = "";
    }
}

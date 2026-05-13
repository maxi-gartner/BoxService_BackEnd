namespace BoxService_BackEnd.Models
{
    // ROL A — Maxi
    public class Invoice
    {
        public int     InvoiceId { get; set; }
        public string  Number    { get; set; } = string.Empty; // F-0001
        public string  Date      { get; set; } = string.Empty;
        public decimal Total     { get; set; }
        public string  Status    { get; set; } = "issued";     // issued | paid | cancelled
        public int     ServiceId { get; set; }
        public int?    BudgetId  { get; set; }
    }
}

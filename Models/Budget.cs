namespace BoxService_BackEnd.Models
{
    // ROL A — Maxi
    public class Budget
    {
        public int     BudgetId  { get; set; }
        public string  Number    { get; set; } = string.Empty; // P-0001
        public string  Date      { get; set; } = string.Empty;
        public string  Status    { get; set; } = "draft";      // draft | sent | approved | rejected
        public string? Notes     { get; set; }
        public int     VehicleId { get; set; }
        public int?    ServiceId { get; set; }
    }

    public class BudgetDetail
    {
        public int     DetailId    { get; set; }
        public int     BudgetId    { get; set; }
        public string  Type        { get; set; } = string.Empty; // labor | part
        public string  Description { get; set; } = string.Empty;
        public decimal Quantity    { get; set; }
        public decimal UnitPrice   { get; set; }
        public decimal Subtotal    { get; set; }
    }

    public class BudgetWithDetails
    {
        public Budget             Budget  { get; set; } = new();
        public List<BudgetDetail> Details { get; set; } = new();
        public decimal            Total   => Details.Sum(d => d.Subtotal);
    }
}

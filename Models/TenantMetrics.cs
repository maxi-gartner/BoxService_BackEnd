namespace BoxService_BackEnd.Models
{
    // GET /metrics — resumen de cómo viene el mes y el año para el
    // tenant del que llama. Pensado para que el dueño vea de un vistazo
    // cómo va el negocio, no para contabilidad exacta.
    public class TenantMetrics
    {
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueThisYear { get; set; }
        public int ServicesThisMonth { get; set; }
        public int ServicesThisYear { get; set; }
        public int NewClientsThisMonth { get; set; }
        public int PendingBudgetsCount { get; set; }
        public decimal PendingBudgetsValue { get; set; }
        public decimal AverageTicketThisMonth { get; set; }
        public List<MonthlyRevenue> RevenueByMonth { get; set; } = new();
        public List<ServiceRanking> TopServicesByRevenue { get; set; } = new();
        public List<ServiceRanking> TopServicesByFrequency { get; set; } = new();
    }

    public class MonthlyRevenue
    {
        public string Month { get; set; } = string.Empty; // "YYYY-MM"
        public decimal Total { get; set; }
    }

    public class ServiceRanking
    {
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // labor | part
        public int Count { get; set; }
        public decimal Total { get; set; }
    }
}

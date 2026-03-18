namespace BoxService_BackEnd.Models
{
    public class Presupuesto
    {
        public int     IdPresupuesto { get; set; }
        public string  Numero        { get; set; } = "";
        public string  Fecha         { get; set; } = "";
        public string  Estado        { get; set; } = "borrador";
        public string? Observaciones { get; set; }
        public int     IdVehiculo    { get; set; }
        public int?    IdService     { get; set; }
    }

    public class DetallePresupuesto
    {
        public int     IdDetalle      { get; set; }
        public int     IdPresupuesto  { get; set; }
        public string  Tipo           { get; set; } = "";
        public string  Descripcion    { get; set; } = "";
        public decimal Cantidad       { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal       { get; set; }
    }

    public class PresupuestoConDetalle
    {
        public Presupuesto              Presupuesto { get; set; } = new();
        public List<DetallePresupuesto> Detalles    { get; set; } = new();
        public decimal                  Total        => Detalles.Sum(d => d.Subtotal);
    }
}

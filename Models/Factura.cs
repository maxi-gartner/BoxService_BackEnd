namespace BoxService_BackEnd.Models
{
    public class Factura
    {
        public int     IdFactura      { get; set; }
        public string  Numero         { get; set; } = "";
        public string  Fecha          { get; set; } = "";
        public decimal Total          { get; set; }
        public string  Estado         { get; set; } = "emitida";
        public int     IdService      { get; set; }
        public int?    IdPresupuesto  { get; set; }
    }
}

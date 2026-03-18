namespace BoxService_BackEnd.Models
{
    public class DetalleService
    {
        public int    IdDetalle   { get; set; }
        public int    IdService   { get; set; }
        public string Descripcion { get; set; } = "";
        public bool   Realizado   { get; set; } = true;
    }
}

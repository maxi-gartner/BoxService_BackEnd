namespace BoxService_BackEnd.Models
{
    // ROL D — Oscar
    // Representa una tarea realizada dentro de un service
    public class DetalleService
    {
        public int IdDetalle { get; set; }

        public int IdService { get; set; }

        public string Descripcion { get; set; } = string.Empty;

        public bool Realizado { get; set; } = true;
    }
}

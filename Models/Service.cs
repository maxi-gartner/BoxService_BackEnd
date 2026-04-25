namespace BoxService_BackEnd.Models
{
    // ROL D — Oscar
    // Representa un service realizado a un vehículo
    public class Service
    {
        public int IdService { get; set; }

        public DateTime Fecha { get; set; }

        public int Kilometraje { get; set; }

        public string TipoService { get; set; } = string.Empty;

        public string Observaciones { get; set; } = string.Empty;

        // Calculados en la lógica de negocio
        public int ProximoKm { get; set; }

        public DateTime ProximaFecha { get; set; }

        // Relaciones
        public int IdVehiculo { get; set; }

        public int? IdPresupuesto { get; set; }
    }
}
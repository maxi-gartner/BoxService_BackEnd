using System;

namespace BoxService_BackEnd.Models
{
    public class Vehicle
    {
        public int VehicleId { get; set; }
        public int ClientId { get; set; }
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public int? Year { get; set; }
        public string Plate { get; set; } = null!;

        // NUEVO:
        // Representa la columna "kilometraje_actual" de la tabla vehiculos.
        // Esto permite que el frontend de Services pueda mostrar el KM actual.
        public int CurrentMileage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class VehicleCreateRequest
    {
        public int ClientId { get; set; }
        public string Brand { get; set; } = null!;
        public string Model { get; set; } = null!;
        public int? Year { get; set; }
        public string Plate { get; set; } = null!;

        // NUEVO:
        // Permite recibir el kilometraje actual cuando se crea un vehículo.
        public int CurrentMileage { get; set; }
    }
}
using System;

namespace BoxService_BackEnd.Models
{
    public class Client
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int ClienteId { get; set; }
        public string Nombre { get; set; } = "";
        public string Name { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ClientCreateRequest
    {
        public string? Nombre { get; set; }
        public string? Name { get; set; }
        public string? Telefono { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class ClientVehicle
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public int VehiculoId { get; set; }
        public string Patente { get; set; } = "";
        public string Plate { get; set; } = "";
        public string Placa { get; set; } = "";
        public string Marca { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Modelo { get; set; } = "";
        public string Model { get; set; } = "";
        public int? Anio { get; set; }
        public int? Year { get; set; }
        public int? Ano { get; set; }
        public int KilometrajeActual { get; set; }
        public int Kilometraje { get; set; }
        public int Mileage { get; set; }
    }
}

using System;

namespace BoxService_BackEnd.Models
{
    public class Vehicle
    {
        public int      VehiculoId { get; set; }
        public int      ClienteId  { get; set; }
        public string   Marca      { get; set; } = null!;
        public string   Modelo     { get; set; } = null!;
        public int?     Anio       { get; set; }
        public string   Patente    { get; set; } = null!;
        public DateTime CreadoEn   { get; set; }
    }

    public class VehicleCreateRequest
    {
        public int    ClienteId { get; set; }
        public string Marca     { get; set; } = null!;
        public string Modelo    { get; set; } = null!;
        public int?   Anio      { get; set; }
        public string Patente   { get; set; } = null!;
    }
}
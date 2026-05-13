using System;

namespace BoxService_BackEnd.Models
{
    public class Vehicle
    {
        public int      VehicleId  { get; set; }
        public int      ClientId   { get; set; }
        public string   Brand      { get; set; } = null!;
        public string   Model      { get; set; } = null!;
        public int?     Year       { get; set; }
        public string   Plate      { get; set; } = null!;
        public DateTime CreatedAt  { get; set; }
    }

    public class VehicleCreateRequest
    {
        public int    ClientId { get; set; }
        public string Brand    { get; set; } = null!;
        public string Model    { get; set; } = null!;
        public int?   Year     { get; set; }
        public string Plate    { get; set; } = null!;
    }
}
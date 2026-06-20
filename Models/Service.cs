using System;

namespace BoxService_BackEnd.Models
{
    // ROL D — Oscar
    public class Service
    {
        public int ServiceId { get; set; }
        public DateTime Date { get; set; }
        public int Mileage { get; set; }
        public string ServiceType { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public int? NextMileage { get; set; }
        public DateTime? NextDate { get; set; }

        public int VehicleId { get; set; }
    }

    public class ServiceDetail
    {
        public int DetailId { get; set; }
        public int ServiceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool Done { get; set; } = true;
    }

    public class ServiceCreateRequest
    {
        public DateTime Date { get; set; }
        public int Mileage { get; set; }
        public string ServiceType { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int VehicleId { get; set; }
    }
}
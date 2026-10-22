using System;

namespace BoxService_BackEnd.Models
{
    public class Sucursal
    {
        public int SucursalId { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class SucursalCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
    }

    public class SucursalUpdateRequest
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public bool? Active { get; set; }
    }
}

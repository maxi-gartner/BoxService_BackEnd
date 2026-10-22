using System;

namespace BoxService_BackEnd.Models
{
    public class Tenant
    {
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class TenantCreateRequest
    {
        public string BusinessName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TenantUpdateRequest
    {
        public bool? Active { get; set; }
    }
}

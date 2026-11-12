using System;

namespace BoxService_BackEnd.Services
{
    // Patente que ya existe en ESTE MISMO tenant, a nombre de otro cliente —
    // a diferencia del InvalidOperationException genérico (patente de otro
    // taller), acá sí exponemos quién es el dueño actual, porque el caller
    // tiene permiso para ver y potencialmente transferirse ese vehículo
    // (ver VehicleService.Transfer).
    public sealed class VehiclePlateConflictException : Exception
    {
        public int ExistingVehicleId { get; }
        public int ExistingClientId { get; }
        public string ExistingClientName { get; }

        public VehiclePlateConflictException(int existingVehicleId, int existingClientId, string existingClientName)
            : base($"Ya existe un vehículo con esa patente, registrado a nombre de {existingClientName}.")
        {
            ExistingVehicleId = existingVehicleId;
            ExistingClientId = existingClientId;
            ExistingClientName = existingClientName;
        }
    }
}

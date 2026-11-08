using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class ServicesRepository
    {
        public List<Service> GetAll(int tenantId)
        {
            var services = new List<Service>();

            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo, tenant_id, sucursal_id
                FROM services
                WHERE tenant_id = @tenant_id
                ORDER BY fecha DESC;";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                services.Add(MapService(reader));
            }

            return services;
        }

        public Service? GetById(int id)
        {
            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo, tenant_id, sucursal_id
                FROM services
                WHERE id_service = @id;";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            return reader.Read() ? MapService(reader) : null;
        }

        // Scoped: la usan las rutas públicas (Api/ServiceEndpoints.cs) para
        // no dejar ver/tocar un service de otro tenant adivinando el ID.
        // GetById (sin tenant) queda para los usos internos que ya validan
        // pertenencia por otro lado (ver BudgetService.AssignService).
        public Service? GetByIdScoped(int id, int tenantId)
        {
            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo, tenant_id, sucursal_id
                FROM services
                WHERE id_service = @id AND tenant_id = @tenant_id;";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = command.ExecuteReader();

            return reader.Read() ? MapService(reader) : null;
        }

        public List<Service> GetByVehicleId(int vehicleId)
        {
            var services = new List<Service>();

            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo, tenant_id, sucursal_id
                FROM services
                WHERE id_vehiculo = @id_vehiculo
                ORDER BY fecha DESC;";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id_vehiculo", vehicleId);

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                services.Add(MapService(reader));
            }

            return services;
        }

        public Service Create(Service service)
        {
            using var connection = DatabaseConnection.GetConnection();
            using var transaction = connection.BeginTransaction();

            const string sql = @"
                INSERT INTO services (
                    fecha,
                    kilometraje,
                    tipo_service,
                    observaciones,
                    proximo_km,
                    proxima_fecha,
                    id_vehiculo,
                    tenant_id,
                    sucursal_id
                )
                VALUES (
                    @fecha,
                    @kilometraje,
                    @tipo_service,
                    @observaciones,
                    @proximo_km,
                    @proxima_fecha,
                    @id_vehiculo,
                    @tenant_id,
                    @sucursal_id
                )
                RETURNING id_service;";

            using var command = new NpgsqlCommand(sql, connection, transaction);

            command.Parameters.AddWithValue("fecha", DateOnly.FromDateTime(service.Date));
            command.Parameters.AddWithValue("kilometraje", service.Mileage);
            command.Parameters.AddWithValue("tipo_service", service.ServiceType);
            command.Parameters.AddWithValue("observaciones", (object?)service.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("proximo_km", (object?)service.NextMileage ?? DBNull.Value);
            command.Parameters.AddWithValue("proxima_fecha", NpgsqlTypes.NpgsqlDbType.Date,
                service.NextDate.HasValue ? DateOnly.FromDateTime(service.NextDate.Value) : DBNull.Value);
            command.Parameters.AddWithValue("id_vehiculo", service.VehicleId);
            command.Parameters.AddWithValue("tenant_id", service.TenantId);
            command.Parameters.AddWithValue("sucursal_id", (object?)service.SucursalId ?? DBNull.Value);

            service.ServiceId = Convert.ToInt32(command.ExecuteScalar());

            using var mileage = new NpgsqlCommand(@"
                UPDATE vehiculos SET kilometraje_actual = GREATEST(COALESCE(kilometraje_actual, 0), @km)
                WHERE id_vehiculo = @id", connection, transaction);
            mileage.Parameters.AddWithValue("km", service.Mileage);
            mileage.Parameters.AddWithValue("id", service.VehicleId);
            mileage.ExecuteNonQuery();
            transaction.Commit();

            return service;
        }

        public List<ServiceDetail> GetDetailsByServiceId(int serviceId)
        {
            var detalles = new List<ServiceDetail>();

            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_detalle, id_service, descripcion, realizado
                FROM detalle_service
                WHERE id_service = @id_service
                ORDER BY id_detalle;";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id_service", serviceId);

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                detalles.Add(new ServiceDetail
                {
                    DetailId = reader.GetInt32(reader.GetOrdinal("id_detalle")),
                    ServiceId = reader.GetInt32(reader.GetOrdinal("id_service")),
                    Description = reader.GetString(reader.GetOrdinal("descripcion")),
                    Done = reader.GetBoolean(reader.GetOrdinal("realizado"))
                });
            }

            return detalles;
        }

        public ServiceDetail CreateDetail(ServiceDetail detail)
        {
            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                INSERT INTO detalle_service (
                    id_service,
                    descripcion,
                    realizado
                )
                VALUES (
                    @id_service,
                    @descripcion,
                    @realizado
                )
                RETURNING id_detalle;";

            using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("id_service", detail.ServiceId);
            command.Parameters.AddWithValue("descripcion", detail.Description);
            command.Parameters.AddWithValue("realizado", detail.Done);

            detail.DetailId = Convert.ToInt32(command.ExecuteScalar());

            return detail;
        }

        private static Service MapService(NpgsqlDataReader r) => new()
        {
            ServiceId = r.GetInt32(r.GetOrdinal("id_service")),
            Date = r.GetDateTime(r.GetOrdinal("fecha")),
            Mileage = r.GetInt32(r.GetOrdinal("kilometraje")),
            ServiceType = r.GetString(r.GetOrdinal("tipo_service")),

            Notes = r.IsDBNull(r.GetOrdinal("observaciones"))
                ? string.Empty
                : r.GetString(r.GetOrdinal("observaciones")),

            NextMileage = r.IsDBNull(r.GetOrdinal("proximo_km"))
                ? null
                : r.GetInt32(r.GetOrdinal("proximo_km")),

            NextDate = r.IsDBNull(r.GetOrdinal("proxima_fecha"))
                ? null
                : r.GetDateTime(r.GetOrdinal("proxima_fecha")),

            VehicleId = r.GetInt32(r.GetOrdinal("id_vehiculo")),
            TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),

            SucursalId = r.IsDBNull(r.GetOrdinal("sucursal_id"))
                ? null
                : r.GetInt32(r.GetOrdinal("sucursal_id"))
        };
    }
}

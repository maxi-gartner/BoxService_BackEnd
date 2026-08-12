using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class ServicesRepository
    {
        public List<Service> GetAll()
        {
            var services = new List<Service>();

            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo
                FROM services
                ORDER BY fecha DESC;";

            using var command = new NpgsqlCommand(sql, connection);
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
                       proximo_km, proxima_fecha, id_vehiculo
                FROM services
                WHERE id_service = @id;";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            return reader.Read() ? MapService(reader) : null;
        }

        public List<Service> GetByVehicleId(int vehicleId)
        {
            var services = new List<Service>();

            using var connection = DatabaseConnection.GetConnection();

            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo
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

            const string sql = @"
                INSERT INTO services (
                    fecha,
                    kilometraje,
                    tipo_service,
                    observaciones,
                    proximo_km,
                    proxima_fecha,
                    id_vehiculo
                )
                VALUES (
                    @fecha,
                    @kilometraje,
                    @tipo_service,
                    @observaciones,
                    @proximo_km,
                    @proxima_fecha,
                    @id_vehiculo
                )
                RETURNING id_service;";

            using var command = new NpgsqlCommand(sql, connection);

            command.Parameters.AddWithValue("fecha", service.Date);
            command.Parameters.AddWithValue("kilometraje", service.Mileage);
            command.Parameters.AddWithValue("tipo_service", service.ServiceType);
            command.Parameters.AddWithValue("observaciones", (object?)service.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("proximo_km", (object?)service.NextMileage ?? DBNull.Value);
            command.Parameters.AddWithValue("proxima_fecha", (object?)service.NextDate ?? DBNull.Value);
            command.Parameters.AddWithValue("id_vehiculo", service.VehicleId);

            service.ServiceId = Convert.ToInt32(command.ExecuteScalar());

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
                    DetailId  = reader.GetInt32(reader.GetOrdinal("id_detalle")),
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

            VehicleId = r.GetInt32(r.GetOrdinal("id_vehiculo"))
        };
    }
}
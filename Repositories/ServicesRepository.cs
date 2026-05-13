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
                       proximo_km, proxima_fecha, id_vehiculo, id_presupuesto
                FROM services
                ORDER BY fecha DESC;";
            using var command = new NpgsqlCommand(sql, connection);
            using var reader  = command.ExecuteReader();
            while (reader.Read()) services.Add(MapService(reader));
            return services;
        }

        public Service? GetById(int id)
        {
            using var connection = DatabaseConnection.GetConnection();
            const string sql = @"
                SELECT id_service, fecha, kilometraje, tipo_service, observaciones,
                       proximo_km, proxima_fecha, id_vehiculo, id_presupuesto
                FROM services
                WHERE id_service = @id;";
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapService(reader) : null;
        }

        public Service Create(Service service)
        {
            using var connection = DatabaseConnection.GetConnection();
            const string sql = @"
                INSERT INTO services (fecha, kilometraje, tipo_service, observaciones,
                                      proximo_km, proxima_fecha, id_vehiculo, id_presupuesto)
                VALUES (@fecha, @kilometraje, @tipo_service, @observaciones,
                        @proximo_km, @proxima_fecha, @id_vehiculo, @id_presupuesto)
                RETURNING id_service;";
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("fecha",          service.Date);
            command.Parameters.AddWithValue("kilometraje",    service.Mileage);
            command.Parameters.AddWithValue("tipo_service",   service.ServiceType);
            command.Parameters.AddWithValue("observaciones",  service.Notes);
            command.Parameters.AddWithValue("proximo_km",     service.NextMileage);
            command.Parameters.AddWithValue("proxima_fecha",  service.NextDate);
            command.Parameters.AddWithValue("id_vehiculo",    service.VehicleId);
            command.Parameters.AddWithValue("id_presupuesto",
                service.BudgetId.HasValue ? (object)service.BudgetId.Value : DBNull.Value);
            service.ServiceId = Convert.ToInt32(command.ExecuteScalar());
            return service;
        }

        private static Service MapService(NpgsqlDataReader r) => new()
        {
            ServiceId   = r.GetInt32(r.GetOrdinal("id_service")),
            Date        = r.GetDateTime(r.GetOrdinal("fecha")),
            Mileage     = r.GetInt32(r.GetOrdinal("kilometraje")),
            ServiceType = r.GetString(r.GetOrdinal("tipo_service")),
            Notes       = r.IsDBNull(r.GetOrdinal("observaciones")) ? string.Empty : r.GetString(r.GetOrdinal("observaciones")),
            NextMileage = r.GetInt32(r.GetOrdinal("proximo_km")),
            NextDate    = r.GetDateTime(r.GetOrdinal("proxima_fecha")),
            VehicleId   = r.GetInt32(r.GetOrdinal("id_vehiculo")),
            BudgetId    = r.IsDBNull(r.GetOrdinal("id_presupuesto")) ? null : r.GetInt32(r.GetOrdinal("id_presupuesto"))
        };
    }
}

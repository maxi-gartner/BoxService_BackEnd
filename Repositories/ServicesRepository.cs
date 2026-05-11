using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    /// <summary>
    /// Repository de Services
    /// Solo acceso a datos — sin lógica de negocio.
    /// </summary>
    public class ServicesRepository
    {
        public List<Service> GetAll()
        {
            var services = new List<Service>();

            using var connection = DatabaseConnection.GetConnection();

            var sql = @"
                SELECT
                    id_service,
                    fecha,
                    kilometraje,
                    tipo_service,
                    observaciones,
                    proximo_km,
                    proxima_fecha,
                    id_vehiculo,
                    id_presupuesto
                FROM services
                ORDER BY fecha DESC;
            ";

            using var command = new NpgsqlCommand(sql, connection);
            using var reader  = command.ExecuteReader();

            while (reader.Read())
                services.Add(MapService(reader));

            return services;
        }

        public Service? GetById(int id)
        {
            using var connection = DatabaseConnection.GetConnection();

            var sql = @"
                SELECT
                    id_service,
                    fecha,
                    kilometraje,
                    tipo_service,
                    observaciones,
                    proximo_km,
                    proxima_fecha,
                    id_vehiculo,
                    id_presupuesto
                FROM services
                WHERE id_service = @id_service;
            ";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id_service", id);

            using var reader = command.ExecuteReader();

            if (reader.Read())
                return MapService(reader);

            return null;
        }

        public Service Create(Service service)
        {
            using var connection = DatabaseConnection.GetConnection();

            var sql = @"
                INSERT INTO services
                (
                    fecha,
                    kilometraje,
                    tipo_service,
                    observaciones,
                    proximo_km,
                    proxima_fecha,
                    id_vehiculo,
                    id_presupuesto
                )
                VALUES
                (
                    @fecha,
                    @kilometraje,
                    @tipo_service,
                    @observaciones,
                    @proximo_km,
                    @proxima_fecha,
                    @id_vehiculo,
                    @id_presupuesto
                )
                RETURNING id_service;
            ";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@fecha",          service.Fecha);
            command.Parameters.AddWithValue("@kilometraje",    service.Kilometraje);
            command.Parameters.AddWithValue("@tipo_service",   service.TipoService);
            command.Parameters.AddWithValue("@observaciones",  service.Observaciones);
            command.Parameters.AddWithValue("@proximo_km",     service.ProximoKm);
            command.Parameters.AddWithValue("@proxima_fecha",  service.ProximaFecha);
            command.Parameters.AddWithValue("@id_vehiculo",    service.IdVehiculo);
            command.Parameters.AddWithValue("@id_presupuesto",
                service.IdPresupuesto.HasValue ? (object)service.IdPresupuesto.Value : DBNull.Value);

            service.IdService = Convert.ToInt32(command.ExecuteScalar());

            return service;
        }

        private static Service MapService(NpgsqlDataReader reader) => new()
        {
            IdService   = reader.GetInt32(reader.GetOrdinal("id_service")),
            Fecha       = reader.GetDateTime(reader.GetOrdinal("fecha")),
            Kilometraje = reader.GetInt32(reader.GetOrdinal("kilometraje")),
            TipoService = reader.GetString(reader.GetOrdinal("tipo_service")),

            Observaciones = reader.IsDBNull(reader.GetOrdinal("observaciones"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("observaciones")),

            ProximoKm   = reader.GetInt32(reader.GetOrdinal("proximo_km")),
            ProximaFecha = reader.GetDateTime(reader.GetOrdinal("proxima_fecha")),
            IdVehiculo  = reader.GetInt32(reader.GetOrdinal("id_vehiculo")),

            IdPresupuesto = reader.IsDBNull(reader.GetOrdinal("id_presupuesto"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("id_presupuesto"))
        };
    }
}
using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    /// <summary>
    /// Repository de Services
    /// 
    /// Se encarga de:
    /// - Conectarse a la base de datos PostgreSQL
    /// - Ejecutar consultas SQL (SELECT, INSERT)
    /// - Convertir los datos de la DB en objetos Service
    /// 
    /// IMPORTANTE:
    /// - NO contiene lógica de negocio
    /// - SOLO acceso a datos
    /// </summary>
    public class ServicesRepository
    {
        /// <summary>
        /// Obtiene todos los services de la base de datos
        /// </summary>
        public List<Service> GetAll()
        {
            // Lista donde vamos a guardar los resultados
            var services = new List<Service>();

            // Abrimos conexión a la base
            using var connection = DatabaseConnection.GetConnection();
            //connection.Open();

            // Consulta SQL
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
                FROM service
                ORDER BY fecha DESC;
            ";

            // Ejecutamos la consulta
            using var command = new NpgsqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            // Recorremos cada fila del resultado
            while (reader.Read())
            {
                // Convertimos la fila en objeto Service
                services.Add(MapService(reader));
            }

            return services;
        }

        /// <summary>
        /// Obtiene un service por su ID
        /// </summary>
        public Service? GetById(int id)
        {
            using var connection = DatabaseConnection.GetConnection();
            //connection.Open();

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
                FROM service
                WHERE id_service = @id_service;
            ";

            using var command = new NpgsqlCommand(sql, connection);

            // Parámetro para evitar SQL Injection
            command.Parameters.AddWithValue("@id_service", id);

            using var reader = command.ExecuteReader();

            // Si encuentra el registro → lo devuelve
            if (reader.Read())
            {
                return MapService(reader);
            }

            // Si no existe → devuelve null
            return null;
        }

        /// <summary>
        /// Inserta un nuevo service en la base de datos
        /// </summary>
        public Service Create(Service service)
        {
            using var connection = DatabaseConnection.GetConnection();
            //connection.Open();

            var sql = @"
                INSERT INTO service
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

            // Asignamos valores a los parámetros
            command.Parameters.AddWithValue("@fecha", service.Fecha);
            command.Parameters.AddWithValue("@kilometraje", service.Kilometraje);
            command.Parameters.AddWithValue("@tipo_service", service.TipoService);
            command.Parameters.AddWithValue("@observaciones", service.Observaciones);
            command.Parameters.AddWithValue("@proximo_km", service.ProximoKm);
            command.Parameters.AddWithValue("@proxima_fecha", service.ProximaFecha);
            command.Parameters.AddWithValue("@id_vehiculo", service.IdVehiculo);

            // Si idPresupuesto es null → se manda NULL a la DB
            command.Parameters.AddWithValue(
                "@id_presupuesto",
                service.IdPresupuesto.HasValue ? service.IdPresupuesto.Value : DBNull.Value
            );

            // Ejecuta el INSERT y devuelve el ID generado
            service.IdService = Convert.ToInt32(command.ExecuteScalar());

            return service;
        }

        /// <summary>
        /// Convierte una fila de la base de datos en un objeto Service
        /// </summary>
        private Service MapService(NpgsqlDataReader reader)
        {
            return new Service
            {
                IdService = reader.GetInt32(reader.GetOrdinal("id_service")),
                Fecha = reader.GetDateTime(reader.GetOrdinal("fecha")),
                Kilometraje = reader.GetInt32(reader.GetOrdinal("kilometraje")),
                TipoService = reader.GetString(reader.GetOrdinal("tipo_service")),

                // Si observaciones es NULL en DB → string vacío
                Observaciones = reader.IsDBNull(reader.GetOrdinal("observaciones"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("observaciones")),

                ProximoKm = reader.GetInt32(reader.GetOrdinal("proximo_km")),
                ProximaFecha = reader.GetDateTime(reader.GetOrdinal("proxima_fecha")),
                IdVehiculo = reader.GetInt32(reader.GetOrdinal("id_vehiculo")),

                // Manejo de NULL para id_presupuesto
                IdPresupuesto = reader.IsDBNull(reader.GetOrdinal("id_presupuesto"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("id_presupuesto"))
            };
        }
    }
}
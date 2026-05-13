using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Database;

namespace BoxService_BackEnd.Repositories
{
    public class VehicleRepository
    {
        public List<Vehicle> GetAll()
        {
            var result = new List<Vehicle>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand(@"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos ORDER BY id_vehiculo;", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) result.Add(MapRow(reader));
            return result;
        }

        public Vehicle? GetById(int id)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand(@"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos WHERE id_vehiculo = @id;", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public Vehicle? GetByPlate(string plate)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand(@"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos WHERE patente = @patente;", conn);
            cmd.Parameters.AddWithValue("patente", plate);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public Vehicle Create(Vehicle vehicle)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand(@"
                INSERT INTO vehiculos (id_cliente, marca, modelo, anio, patente)
                VALUES (@id_cliente, @marca, @modelo, @anio, @patente)
                RETURNING id_vehiculo, created_at;", conn);
            cmd.Parameters.AddWithValue("id_cliente", vehicle.ClientId);
            cmd.Parameters.AddWithValue("marca",      vehicle.Brand);
            cmd.Parameters.AddWithValue("modelo",     vehicle.Model);
            cmd.Parameters.AddWithValue("anio",       vehicle.Year.HasValue ? (object)vehicle.Year.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("patente",    vehicle.Plate);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            vehicle.VehicleId = reader.GetInt32(0);
            vehicle.CreatedAt = reader.GetDateTime(1);
            return vehicle;
        }

        private static Vehicle MapRow(NpgsqlDataReader r) => new()
        {
            VehicleId = r.GetInt32(r.GetOrdinal("id_vehiculo")),
            ClientId  = r.GetInt32(r.GetOrdinal("id_cliente")),
            Brand     = r.GetString(r.GetOrdinal("marca")),
            Model     = r.GetString(r.GetOrdinal("modelo")),
            Year      = r.IsDBNull(r.GetOrdinal("anio")) ? null : r.GetInt32(r.GetOrdinal("anio")),
            Plate     = r.GetString(r.GetOrdinal("patente")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at"))
        };
    }
}
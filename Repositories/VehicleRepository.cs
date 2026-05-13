using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class VehicleRepository
    {
        private readonly string _connectionString;

        public VehicleRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<Vehicle>> GetAllAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos
                ORDER BY id_vehiculo;
            ";

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader  = await command.ExecuteReaderAsync();

            var result = new List<Vehicle>();
            while (await reader.ReadAsync())
                result.Add(ReadVehicle(reader));

            return result;
        }

        public async Task<Vehicle?> GetByIdAsync(int id)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos
                WHERE id_vehiculo = @id;
            ";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return ReadVehicle(reader);
        }

        public async Task<Vehicle?> GetByPlateAsync(string plate)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos
                WHERE patente = @patente;
            ";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("patente", plate);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return ReadVehicle(reader);
        }

        public async Task<Vehicle> CreateAsync(Vehicle vehicle)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO vehiculos (id_cliente, marca, modelo, anio, patente)
                VALUES (@id_cliente, @marca, @modelo, @anio, @patente)
                RETURNING id_vehiculo, created_at;
            ";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id_cliente", vehicle.ClientId);
            command.Parameters.AddWithValue("marca",      vehicle.Brand);
            command.Parameters.AddWithValue("modelo",     vehicle.Model);
            command.Parameters.AddWithValue("anio",       vehicle.Year.HasValue ? (object)vehicle.Year.Value : DBNull.Value);
            command.Parameters.AddWithValue("patente",    vehicle.Plate);

            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            vehicle.VehicleId = reader.GetInt32(0);
            vehicle.CreatedAt = reader.GetDateTime(1);

            return vehicle;
        }

        private static Vehicle ReadVehicle(NpgsqlDataReader reader) => new()
        {
            VehicleId = reader.GetInt32(reader.GetOrdinal("id_vehiculo")),
            ClientId  = reader.GetInt32(reader.GetOrdinal("id_cliente")),
            Brand     = reader.GetString(reader.GetOrdinal("marca")),
            Model     = reader.GetString(reader.GetOrdinal("modelo")),
            Year      = reader.IsDBNull(reader.GetOrdinal("anio")) ? null : reader.GetInt32(reader.GetOrdinal("anio")),
            Plate     = reader.GetString(reader.GetOrdinal("patente")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }
}
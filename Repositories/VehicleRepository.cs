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

        public async Task<Vehicle?> GetByPlateAsync(string patente)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT id_vehiculo, id_cliente, marca, modelo, anio, patente, created_at
                FROM vehiculos
                WHERE patente = @patente;
            ";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("patente", patente);

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
            command.Parameters.AddWithValue("id_cliente", vehicle.ClienteId);
            command.Parameters.AddWithValue("marca",      vehicle.Marca);
            command.Parameters.AddWithValue("modelo",     vehicle.Modelo);
            command.Parameters.AddWithValue("anio",       vehicle.Anio.HasValue ? (object)vehicle.Anio.Value : DBNull.Value);
            command.Parameters.AddWithValue("patente",    vehicle.Patente);

            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            vehicle.VehiculoId = reader.GetInt32(0);
            vehicle.CreadoEn   = reader.GetDateTime(1);

            return vehicle;
        }

        private static Vehicle ReadVehicle(NpgsqlDataReader reader) => new()
        {
            VehiculoId = reader.GetInt32(reader.GetOrdinal("id_vehiculo")),
            ClienteId  = reader.GetInt32(reader.GetOrdinal("id_cliente")),
            Marca      = reader.GetString(reader.GetOrdinal("marca")),
            Modelo     = reader.GetString(reader.GetOrdinal("modelo")),
            Anio       = reader.IsDBNull(reader.GetOrdinal("anio")) ? null : reader.GetInt32(reader.GetOrdinal("anio")),
            Patente    = reader.GetString(reader.GetOrdinal("patente")),
            CreadoEn   = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }
}
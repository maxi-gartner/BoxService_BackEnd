using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class ClientRepository
    {
        public List<Client> GetAll()
        {
            var result = new List<Client>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_cliente, nombre, telefono, email, activo, created_at
                FROM clientes
                WHERE activo = TRUE
                ORDER BY id_cliente;", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapClient(reader));
            }

            return result;
        }

        public Client? GetById(int id)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_cliente, nombre, telefono, email, activo, created_at
                FROM clientes
                WHERE id_cliente = @id AND activo = TRUE;", conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapClient(reader) : null;
        }

        public Client Create(ClientCreateRequest request)
        {
            var name = (request.Nombre ?? request.Name)!.Trim();
            var phone = (request.Telefono ?? request.Phone ?? "").Trim();
            var email = (request.Email ?? "").Trim();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO clientes (nombre, telefono, email)
                VALUES (@nombre, @telefono, @email)
                RETURNING id_cliente, nombre, telefono, email, activo, created_at;", conn);

            cmd.Parameters.AddWithValue("nombre", name);
            cmd.Parameters.AddWithValue("telefono", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone);
            cmd.Parameters.AddWithValue("email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email);

            using var reader = cmd.ExecuteReader();
            reader.Read();

            return MapClient(reader);
        }

        public List<ClientVehicle> GetVehiclesByClientId(int clientId)
        {
            var result = new List<ClientVehicle>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_vehiculo, patente, marca, modelo, anio, kilometraje_actual
                FROM vehiculos
                WHERE id_cliente = @client_id AND activo = TRUE
                ORDER BY id_vehiculo;", conn);

            cmd.Parameters.AddWithValue("client_id", clientId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapVehicle(reader));
            }

            return result;
        }

        private static Client MapClient(NpgsqlDataReader r)
        {
            var id = r.GetInt32(r.GetOrdinal("id_cliente"));
            var name = r.GetString(r.GetOrdinal("nombre"));
            var phone = r.IsDBNull(r.GetOrdinal("telefono")) ? "" : r.GetString(r.GetOrdinal("telefono"));
            var email = r.IsDBNull(r.GetOrdinal("email")) ? "" : r.GetString(r.GetOrdinal("email"));

            return new Client
            {
                Id = id,
                ClientId = id,
                ClienteId = id,
                Nombre = name,
                Name = name,
                Telefono = phone,
                Phone = phone,
                Email = email,
                Active = r.GetBoolean(r.GetOrdinal("activo")),
                CreatedAt = r.GetDateTime(r.GetOrdinal("created_at"))
            };
        }

        private static ClientVehicle MapVehicle(NpgsqlDataReader r)
        {
            var id = r.GetInt32(r.GetOrdinal("id_vehiculo"));
            var plate = r.GetString(r.GetOrdinal("patente"));
            var brand = r.GetString(r.GetOrdinal("marca"));
            var model = r.GetString(r.GetOrdinal("modelo"));
            var year = r.IsDBNull(r.GetOrdinal("anio")) ? (int?)null : r.GetInt32(r.GetOrdinal("anio"));
            var mileage = r.IsDBNull(r.GetOrdinal("kilometraje_actual"))
                ? 0
                : r.GetInt32(r.GetOrdinal("kilometraje_actual"));

            return new ClientVehicle
            {
                Id = id,
                VehicleId = id,
                VehiculoId = id,
                Patente = plate,
                Plate = plate,
                Placa = plate,
                Marca = brand,
                Brand = brand,
                Modelo = model,
                Model = model,
                Anio = year,
                Year = year,
                Ano = year,
                KilometrajeActual = mileage,
                Kilometraje = mileage,
                Mileage = mileage
            };
        }
    }
}

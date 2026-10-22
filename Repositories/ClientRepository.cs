using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class ClientRepository
    {
        public List<Client> GetAll(int tenantId)
        {
            var result = new List<Client>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_cliente, nombre, telefono, email, activo, created_at, tenant_id
                FROM clientes
                WHERE activo = TRUE AND tenant_id = @tenant_id
                ORDER BY id_cliente;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapClient(reader));
            }

            return result;
        }

        public Client? GetById(int id, int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_cliente, nombre, telefono, email, activo, created_at, tenant_id
                FROM clientes
                WHERE id_cliente = @id AND activo = TRUE AND tenant_id = @tenant_id;", conn);

            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapClient(reader) : null;
        }

        // Sin tenant: la usa el portal del cliente (rol "customer", JWT
        // sin tenantId) — ahí la autorización pasa por la cadena
        // google_sub → client_portal_access, no por pertenencia a tenant.
        public Client? GetById(int id)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_cliente, nombre, telefono, email, activo, created_at, tenant_id
                FROM clientes
                WHERE id_cliente = @id AND activo = TRUE;", conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapClient(reader) : null;
        }

        // Para el panel cross-tenant de superadmin — sin filtro, con el
        // nombre del tenant embebido.
        public List<(Client Client, string TenantName)> GetAllAdmin()
        {
            var result = new List<(Client, string)>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT c.id_cliente, c.nombre, c.telefono, c.email, c.activo, c.created_at, c.tenant_id,
                       t.nombre AS tenant_nombre
                FROM clientes c
                JOIN tenants t ON t.id_tenant = c.tenant_id
                WHERE c.activo = TRUE
                ORDER BY t.nombre, c.id_cliente;", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add((MapClient(reader), reader.GetString(reader.GetOrdinal("tenant_nombre"))));
            }

            return result;
        }

        public Client Create(int tenantId, ClientCreateRequest request)
        {
            var name = (request.Nombre ?? request.Name)!.Trim();
            var phone = (request.Telefono ?? request.Phone ?? "").Trim();
            var email = (request.Email ?? "").Trim();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO clientes (nombre, telefono, email, tenant_id)
                VALUES (@nombre, @telefono, @email, @tenant_id)
                RETURNING id_cliente, nombre, telefono, email, activo, created_at, tenant_id;", conn);

            cmd.Parameters.AddWithValue("nombre", name);
            cmd.Parameters.AddWithValue("telefono", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone);
            cmd.Parameters.AddWithValue("email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            reader.Read();

            return MapClient(reader);
        }

        public List<ClientVehicle> GetVehiclesByClientId(int clientId, int tenantId)
        {
            var result = new List<ClientVehicle>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_vehiculo, patente, marca, modelo, anio, kilometraje_actual
                FROM vehiculos
                WHERE id_cliente = @client_id AND activo = TRUE AND tenant_id = @tenant_id
                ORDER BY id_vehiculo;", conn);

            cmd.Parameters.AddWithValue("client_id", clientId);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

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
                TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
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

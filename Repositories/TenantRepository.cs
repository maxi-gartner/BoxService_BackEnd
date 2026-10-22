using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class TenantRepository
    {
        public List<Tenant> GetAll()
        {
            var result = new List<Tenant>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_tenant, nombre, activo, created_at
                FROM tenants
                ORDER BY id_tenant;", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        public Tenant? GetById(int id)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_tenant, nombre, activo, created_at
                FROM tenants
                WHERE id_tenant = @id;", conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public Tenant Create(string name)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO tenants (nombre)
                VALUES (@nombre)
                RETURNING id_tenant, nombre, activo, created_at;", conn);

            cmd.Parameters.AddWithValue("nombre", name);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return MapRow(reader);
        }

        // Crea el tenant y su primer usuario "dueno" en una sola
        // transacción — si falla cualquiera de los dos inserts, no queda
        // un tenant huérfano sin dueño ni un usuario suelto sin tenant.
        public (Tenant Tenant, UserAccount Owner) CreateWithOwner(
            string businessName, string username, string passwordHash, string ownerName)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var transaction = conn.BeginTransaction();

            Tenant tenant;
            using (var cmd = new NpgsqlCommand(@"
                INSERT INTO tenants (nombre)
                VALUES (@nombre)
                RETURNING id_tenant, nombre, activo, created_at;", conn, transaction))
            {
                cmd.Parameters.AddWithValue("nombre", businessName);
                using var reader = cmd.ExecuteReader();
                reader.Read();
                tenant = MapRow(reader);
            }

            UserAccount owner;
            using (var cmd = new NpgsqlCommand(@"
                INSERT INTO usuarios (id_tenant, username, password_hash, nombre, role)
                VALUES (@tenant_id, @username, @password_hash, @nombre, 'dueno')
                RETURNING id_usuario, id_tenant, username, password_hash, nombre, email, role, activo, created_at;", conn, transaction))
            {
                cmd.Parameters.AddWithValue("tenant_id", tenant.TenantId);
                cmd.Parameters.AddWithValue("username", username);
                cmd.Parameters.AddWithValue("password_hash", passwordHash);
                cmd.Parameters.AddWithValue("nombre", ownerName);
                using var reader = cmd.ExecuteReader();
                reader.Read();
                owner = new UserAccount
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("id_usuario")),
                    TenantId = reader.GetInt32(reader.GetOrdinal("id_tenant")),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                    Name = reader.GetString(reader.GetOrdinal("nombre")),
                    Role = reader.GetString(reader.GetOrdinal("role")),
                    Active = reader.GetBoolean(reader.GetOrdinal("activo")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                };
            }

            transaction.Commit();
            return (tenant, owner);
        }

        public bool SetActive(int id, bool active)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "UPDATE tenants SET activo = @activo WHERE id_tenant = @id;", conn);

            cmd.Parameters.AddWithValue("activo", active);
            cmd.Parameters.AddWithValue("id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        private static Tenant MapRow(NpgsqlDataReader r) => new()
        {
            TenantId = r.GetInt32(r.GetOrdinal("id_tenant")),
            Name = r.GetString(r.GetOrdinal("nombre")),
            Active = r.GetBoolean(r.GetOrdinal("activo")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}

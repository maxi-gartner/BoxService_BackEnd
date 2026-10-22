using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class UserRepository
    {
        public UserAccount? GetByUsername(string username)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_usuario, id_tenant, username, password_hash, nombre, email, role, activo, created_at
                FROM usuarios
                WHERE LOWER(username) = LOWER(@username) AND activo = TRUE;", conn);

            cmd.Parameters.AddWithValue("username", username);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public UserAccount? GetById(int id)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_usuario, id_tenant, username, password_hash, nombre, email, role, activo, created_at
                FROM usuarios
                WHERE id_usuario = @id;", conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public List<UserAccount> GetByTenant(int tenantId)
        {
            var result = new List<UserAccount>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_usuario, id_tenant, username, password_hash, nombre, email, role, activo, created_at
                FROM usuarios
                WHERE id_tenant = @tenant_id
                ORDER BY id_usuario;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        // Todos los usuarios de todos los tenants, con el nombre del
        // tenant embebido — el panel cross-tenant de superadmin.
        public List<(UserAccount User, string? TenantName)> GetAllWithTenant()
        {
            var result = new List<(UserAccount, string?)>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT u.id_usuario, u.id_tenant, u.username, u.password_hash, u.nombre, u.email, u.role, u.activo, u.created_at,
                       t.nombre AS tenant_nombre
                FROM usuarios u
                LEFT JOIN tenants t ON t.id_tenant = u.id_tenant
                ORDER BY u.id_usuario;", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var user = MapRow(reader);
                var tenantName = reader.IsDBNull(reader.GetOrdinal("tenant_nombre"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("tenant_nombre"));
                result.Add((user, tenantName));
            }

            return result;
        }

        public UserAccount Create(int? tenantId, string username, string passwordHash, string name, string? email, string role)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO usuarios (id_tenant, username, password_hash, nombre, email, role)
                VALUES (@tenant_id, @username, @password_hash, @nombre, @email, @role)
                RETURNING id_usuario, id_tenant, username, password_hash, nombre, email, role, activo, created_at;", conn);

            cmd.Parameters.AddWithValue("tenant_id", (object?)tenantId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("username", username);
            cmd.Parameters.AddWithValue("password_hash", passwordHash);
            cmd.Parameters.AddWithValue("nombre", name);
            cmd.Parameters.AddWithValue("email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email);
            cmd.Parameters.AddWithValue("role", role);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return MapRow(reader);
        }

        public bool Update(int id, int? tenantId, UserUpdateRequest request)
        {
            // tenantId null = superadmin pidiendo (puede tocar cualquier
            // usuario); si no es null, solo puede tocar usuarios de su
            // propio tenant — el WHERE lo garantiza sin un IF aparte.
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE usuarios
                SET nombre = COALESCE(@nombre, nombre),
                    email = COALESCE(@email, email),
                    activo = COALESCE(@activo, activo)
                WHERE id_usuario = @id
                  AND (@tenant_id::int IS NULL OR id_tenant = @tenant_id);", conn);

            cmd.Parameters.AddWithValue("nombre", (object?)request.Name?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)request.Email?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("activo", (object?)request.Active ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", (object?)tenantId ?? DBNull.Value);

            return cmd.ExecuteNonQuery() > 0;
        }

        // Mismo alcance que Update: dueno solo dentro de su propio tenant
        // (el WHERE lo garantiza), superadmin con tenantId null = cualquiera.
        public bool UpdatePassword(int id, int? tenantId, string passwordHash)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE usuarios
                SET password_hash = @password_hash
                WHERE id_usuario = @id
                  AND (@tenant_id::int IS NULL OR id_tenant = @tenant_id);", conn);

            cmd.Parameters.AddWithValue("password_hash", passwordHash);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", (object?)tenantId ?? DBNull.Value);

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UsernameExists(string username)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM usuarios WHERE LOWER(username) = LOWER(@username));", conn);
            cmd.Parameters.AddWithValue("username", username);
            return (bool)cmd.ExecuteScalar()!;
        }

        private static UserAccount MapRow(NpgsqlDataReader r) => new()
        {
            UserId = r.GetInt32(r.GetOrdinal("id_usuario")),
            TenantId = r.IsDBNull(r.GetOrdinal("id_tenant")) ? null : r.GetInt32(r.GetOrdinal("id_tenant")),
            Username = r.GetString(r.GetOrdinal("username")),
            PasswordHash = r.GetString(r.GetOrdinal("password_hash")),
            Name = r.GetString(r.GetOrdinal("nombre")),
            Email = r.IsDBNull(r.GetOrdinal("email")) ? null : r.GetString(r.GetOrdinal("email")),
            Role = r.GetString(r.GetOrdinal("role")),
            Active = r.GetBoolean(r.GetOrdinal("activo")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}

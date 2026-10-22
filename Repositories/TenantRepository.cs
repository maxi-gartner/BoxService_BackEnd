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
                SELECT id_tenant, nombre, activo, created_at, telefono, email
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
                SELECT id_tenant, nombre, activo, created_at, telefono, email
                FROM tenants
                WHERE id_tenant = @id;", conn);

            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        // Lista de superadmin: cada taller con el nombre/usuario de su
        // dueño ya resuelto (subconsulta al más antiguo "dueno" del
        // tenant — en el caso normal de uno solo, es exactamente ese).
        public List<TenantWithOwner> GetAllWithOwner()
        {
            var result = new List<TenantWithOwner>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT t.id_tenant, t.nombre, t.activo, t.created_at, t.telefono, t.email,
                       (SELECT u.nombre FROM usuarios u WHERE u.id_tenant = t.id_tenant AND u.role = 'dueno' ORDER BY u.id_usuario LIMIT 1) AS owner_nombre,
                       (SELECT u.username FROM usuarios u WHERE u.id_tenant = t.id_tenant AND u.role = 'dueno' ORDER BY u.id_usuario LIMIT 1) AS owner_username
                FROM tenants t
                ORDER BY t.id_tenant;", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tenant = MapRow(reader);
                var ownerNameOrdinal = reader.GetOrdinal("owner_nombre");
                var ownerUsernameOrdinal = reader.GetOrdinal("owner_username");
                result.Add(new TenantWithOwner(
                    tenant,
                    reader.IsDBNull(ownerNameOrdinal) ? null : reader.GetString(ownerNameOrdinal),
                    reader.IsDBNull(ownerUsernameOrdinal) ? null : reader.GetString(ownerUsernameOrdinal)));
            }

            return result;
        }

        public Tenant Create(string name)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO tenants (nombre)
                VALUES (@nombre)
                RETURNING id_tenant, nombre, activo, created_at, telefono, email;", conn);

            cmd.Parameters.AddWithValue("nombre", name);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return MapRow(reader);
        }

        // Crea el tenant y su primer usuario "dueno" en una sola
        // transacción — si falla cualquiera de los dos inserts, no queda
        // un tenant huérfano sin dueño ni un usuario suelto sin tenant.
        public (Tenant Tenant, UserAccount Owner) CreateWithOwner(
            string businessName, string username, string passwordHash, string ownerName,
            string? telefono, string? email)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var transaction = conn.BeginTransaction();

            Tenant tenant;
            using (var cmd = new NpgsqlCommand(@"
                INSERT INTO tenants (nombre, telefono, email)
                VALUES (@nombre, @telefono, @email)
                RETURNING id_tenant, nombre, activo, created_at, telefono, email;", conn, transaction))
            {
                cmd.Parameters.AddWithValue("nombre", businessName);
                cmd.Parameters.AddWithValue("telefono", (object?)telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
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

        public bool Update(int id, TenantUpdateRequest request)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE tenants
                SET activo = COALESCE(@activo, activo),
                    telefono = COALESCE(@telefono, telefono),
                    email = COALESCE(@email, email)
                WHERE id_tenant = @id;", conn);

            cmd.Parameters.AddWithValue("activo", (object?)request.Active ?? DBNull.Value);
            cmd.Parameters.AddWithValue("telefono", (object?)request.Telefono?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email", (object?)request.Email?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        private static Tenant MapRow(NpgsqlDataReader r) => new()
        {
            TenantId = r.GetInt32(r.GetOrdinal("id_tenant")),
            Name = r.GetString(r.GetOrdinal("nombre")),
            Active = r.GetBoolean(r.GetOrdinal("activo")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
            Telefono = r.IsDBNull(r.GetOrdinal("telefono")) ? null : r.GetString(r.GetOrdinal("telefono")),
            Email = r.IsDBNull(r.GetOrdinal("email")) ? null : r.GetString(r.GetOrdinal("email")),
        };
    }
}

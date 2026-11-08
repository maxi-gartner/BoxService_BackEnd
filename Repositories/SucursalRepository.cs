using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class SucursalRepository
    {
        public List<Sucursal> GetByTenant(int tenantId)
        {
            var result = new List<Sucursal>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_sucursal, id_tenant, nombre, direccion, activo, created_at
                FROM sucursales
                WHERE id_tenant = @tenant_id
                ORDER BY id_sucursal;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        public Sucursal? GetById(int id, int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_sucursal, id_tenant, nombre, direccion, activo, created_at
                FROM sucursales
                WHERE id_sucursal = @id AND id_tenant = @tenant_id;", conn);

            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public Sucursal Create(int tenantId, SucursalCreateRequest request)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO sucursales (id_tenant, nombre, direccion)
                VALUES (@tenant_id, @nombre, @direccion)
                RETURNING id_sucursal, id_tenant, nombre, direccion, activo, created_at;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("nombre", request.Name.Trim());
            cmd.Parameters.AddWithValue("direccion", string.IsNullOrWhiteSpace(request.Address) ? DBNull.Value : request.Address.Trim());

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return MapRow(reader);
        }

        public bool Update(int id, int tenantId, SucursalUpdateRequest request)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE sucursales
                SET nombre = COALESCE(@nombre, nombre),
                    direccion = COALESCE(@direccion, direccion),
                    activo = COALESCE(@activo, activo)
                WHERE id_sucursal = @id AND id_tenant = @tenant_id;", conn);

            cmd.Parameters.AddWithValue("nombre", (object?)request.Name?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("direccion", (object?)request.Address?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("activo", (object?)request.Active ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            return cmd.ExecuteNonQuery() > 0;
        }

        private static Sucursal MapRow(NpgsqlDataReader r) => new()
        {
            SucursalId = r.GetInt32(r.GetOrdinal("id_sucursal")),
            TenantId = r.GetInt32(r.GetOrdinal("id_tenant")),
            Name = r.GetString(r.GetOrdinal("nombre")),
            Address = r.IsDBNull(r.GetOrdinal("direccion")) ? null : r.GetString(r.GetOrdinal("direccion")),
            Active = r.GetBoolean(r.GetOrdinal("activo")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}

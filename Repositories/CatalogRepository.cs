using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class CatalogRepository
    {
        public List<CatalogItem> GetAll(int tenantId)
        {
            var lista = new List<CatalogItem>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT id_catalogo, nombre, tipo, precio, tenant_id FROM catalogo_servicios WHERE tenant_id = @tenant_id ORDER BY nombre",
                conn
            );
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(MapRow(reader));
            }

            return lista;
        }

        public CatalogItem? GetById(int id, int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT id_catalogo, nombre, tipo, precio, tenant_id FROM catalogo_servicios WHERE id_catalogo = @id AND tenant_id = @tenant_id",
                conn
            );
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public CatalogItem Create(int tenantId, CatalogItem item)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO catalogo_servicios (nombre, tipo, precio, tenant_id)
                VALUES (@nombre, @tipo, @precio, @tenant_id)
                RETURNING id_catalogo", conn);

            cmd.Parameters.AddWithValue("nombre", item.Name);
            cmd.Parameters.AddWithValue("tipo", item.Type);
            cmd.Parameters.AddWithValue("precio", item.Price);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            item.CatalogId = (int)cmd.ExecuteScalar()!;
            return item;
        }

        public bool Update(int id, int tenantId, CatalogItemUpdateRequest req)
        {
            var current = GetById(id, tenantId);
            if (current == null) return false;

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE catalogo_servicios
                SET nombre = @nombre, tipo = @tipo, precio = @precio
                WHERE id_catalogo = @id AND tenant_id = @tenant_id", conn);

            cmd.Parameters.AddWithValue("nombre", req.Name ?? current.Name);
            cmd.Parameters.AddWithValue("tipo", req.Type ?? current.Type);
            cmd.Parameters.AddWithValue("precio", req.Price ?? current.Price);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id, int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "DELETE FROM catalogo_servicios WHERE id_catalogo = @id AND tenant_id = @tenant_id",
                conn
            );
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            return cmd.ExecuteNonQuery() > 0;
        }

        private static CatalogItem MapRow(NpgsqlDataReader r) => new()
        {
            CatalogId = (int)r["id_catalogo"],
            TenantId  = (int)r["tenant_id"],
            Name      = r["nombre"].ToString()!,
            Type      = r["tipo"].ToString()!,
            Price     = (decimal)r["precio"]
        };
    }
}

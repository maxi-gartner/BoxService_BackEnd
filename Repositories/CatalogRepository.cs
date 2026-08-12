using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class CatalogRepository
    {
        public List<CatalogItem> GetAll()
        {
            var lista = new List<CatalogItem>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT id_catalogo, nombre, tipo, precio FROM catalogo_servicios ORDER BY nombre",
                conn
            );

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(MapRow(reader));
            }

            return lista;
        }

        public CatalogItem? GetById(int id)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT id_catalogo, nombre, tipo, precio FROM catalogo_servicios WHERE id_catalogo = @id",
                conn
            );
            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public CatalogItem Create(CatalogItem item)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO catalogo_servicios (nombre, tipo, precio)
                VALUES (@nombre, @tipo, @precio)
                RETURNING id_catalogo", conn);

            cmd.Parameters.AddWithValue("nombre", item.Name);
            cmd.Parameters.AddWithValue("tipo", item.Type);
            cmd.Parameters.AddWithValue("precio", item.Price);

            item.CatalogId = (int)cmd.ExecuteScalar()!;
            return item;
        }

        public bool Update(int id, CatalogItemUpdateRequest req)
        {
            var current = GetById(id);
            if (current == null) return false;

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE catalogo_servicios
                SET nombre = @nombre, tipo = @tipo, precio = @precio
                WHERE id_catalogo = @id", conn);

            cmd.Parameters.AddWithValue("nombre", req.Name ?? current.Name);
            cmd.Parameters.AddWithValue("tipo", req.Type ?? current.Type);
            cmd.Parameters.AddWithValue("precio", req.Price ?? current.Price);
            cmd.Parameters.AddWithValue("id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "DELETE FROM catalogo_servicios WHERE id_catalogo = @id",
                conn
            );
            cmd.Parameters.AddWithValue("id", id);

            return cmd.ExecuteNonQuery() > 0;
        }

        private static CatalogItem MapRow(NpgsqlDataReader r) => new()
        {
            CatalogId = (int)r["id_catalogo"],
            Name      = r["nombre"].ToString()!,
            Type      = r["tipo"].ToString()!,
            Price     = (decimal)r["precio"]
        };
    }
}

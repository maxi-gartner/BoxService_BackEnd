using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class FacturasRepository
    {
        public List<Factura> GetAll()
        {
            var lista = new List<Factura>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM facturas ORDER BY created_at DESC", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapRow(reader));
            return lista;
        }

        public Factura? GetById(int id)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM facturas WHERE id_factura = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public bool ExisteFacturaParaService(int idService)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT COUNT(*) FROM facturas WHERE id_service = @id", conn);
            cmd.Parameters.AddWithValue("id", idService);
            return (long)cmd.ExecuteScalar()! > 0;
        }

        public string GetUltimoNumero()
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT numero FROM facturas ORDER BY id_factura DESC LIMIT 1", conn);
            return cmd.ExecuteScalar()?.ToString() ?? "F-0000";
        }

        public int CrearConTransaccion(Factura f)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                // PASO 1 — insertar factura
                using var cmdFactura = new NpgsqlCommand(@"
                    INSERT INTO facturas (numero, fecha, total, estado, id_service, id_presupuesto)
                    VALUES (@numero, @fecha, @total, 'emitida', @id_service, @id_presupuesto)
                    RETURNING id_factura", conn, tx);
                cmdFactura.Parameters.AddWithValue("numero",         f.Numero);
                cmdFactura.Parameters.AddWithValue("fecha",          DateTime.Today);
                cmdFactura.Parameters.AddWithValue("total",          f.Total);
                cmdFactura.Parameters.AddWithValue("id_service",     f.IdService);
                cmdFactura.Parameters.AddWithValue("id_presupuesto", (object?)f.IdPresupuesto ?? DBNull.Value);
                var idFactura = (int)cmdFactura.ExecuteScalar()!;

                // PASO 2 — marcar service como facturado
                using var cmdService = new NpgsqlCommand(
                    "UPDATE services SET tipo_service = tipo_service WHERE id_service = @id", conn, tx);
                cmdService.Parameters.AddWithValue("id", f.IdService);
                cmdService.ExecuteNonQuery();

                tx.Commit();
                return idFactura;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void CambiarEstado(int id, string estado)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("UPDATE facturas SET estado = @estado WHERE id_factura = @id", conn);
            cmd.Parameters.AddWithValue("estado", estado);
            cmd.Parameters.AddWithValue("id",     id);
            cmd.ExecuteNonQuery();
        }

        private static Factura MapRow(NpgsqlDataReader r) => new()
        {
            IdFactura     = (int)r["id_factura"],
            Numero        = r["numero"].ToString()!,
            Fecha         = r["fecha"].ToString()!,
            Total         = (decimal)r["total"],
            Estado        = r["estado"].ToString()!,
            IdService     = (int)r["id_service"],
            IdPresupuesto = r["id_presupuesto"] as int?
        };
    }
}

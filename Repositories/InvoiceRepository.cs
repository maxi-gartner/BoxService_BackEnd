using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class InvoiceRepository
    {
        public List<Invoice> GetAll()
        {
            var lista = new List<Invoice>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM facturas ORDER BY created_at DESC", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapRow(reader));
            return lista;
        }

        public Invoice? GetById(int id)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM facturas WHERE id_factura = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public bool InvoiceExistsForService(int serviceId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT COUNT(*) FROM facturas WHERE id_service = @id", conn);
            cmd.Parameters.AddWithValue("id", serviceId);
            return (long)cmd.ExecuteScalar()! > 0;
        }

        public string GetLastNumber()
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT numero FROM facturas ORDER BY id_factura DESC LIMIT 1", conn);
            return cmd.ExecuteScalar()?.ToString() ?? "F-0000";
        }

        /// <summary>
        /// Transacción ACID:
        /// 1. Inserta la factura
        /// 2. Actualiza el service
        /// </summary>
        public int CreateWithTransaction(Invoice inv)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                using var cmdInvoice = new NpgsqlCommand(@"
                    INSERT INTO facturas (numero, fecha, total, estado, id_service, id_presupuesto)
                    VALUES (@numero, @fecha, @total, 'issued', @id_service, @id_presupuesto)
                    RETURNING id_factura", conn, tx);
                cmdInvoice.Parameters.AddWithValue("numero",         inv.Number);
                cmdInvoice.Parameters.AddWithValue("fecha",          DateTime.Today);
                cmdInvoice.Parameters.AddWithValue("total",          inv.Total);
                cmdInvoice.Parameters.AddWithValue("id_service",     inv.ServiceId);
                cmdInvoice.Parameters.AddWithValue("id_presupuesto", (object?)inv.BudgetId ?? DBNull.Value);
                var invoiceId = (int)cmdInvoice.ExecuteScalar()!;

                using var cmdService = new NpgsqlCommand(
                    "UPDATE services SET tipo_service = tipo_service WHERE id_service = @id", conn, tx);
                cmdService.Parameters.AddWithValue("id", inv.ServiceId);
                cmdService.ExecuteNonQuery();

                tx.Commit();
                return invoiceId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void UpdateStatus(int id, string status)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("UPDATE facturas SET estado = @estado WHERE id_factura = @id", conn);
            cmd.Parameters.AddWithValue("estado", status);
            cmd.Parameters.AddWithValue("id",     id);
            cmd.ExecuteNonQuery();
        }

        private static Invoice MapRow(NpgsqlDataReader r) => new()
        {
            InvoiceId = (int)r["id_factura"],
            Number    = r["numero"].ToString()!,
            Date      = r["fecha"].ToString()!,
            Total     = (decimal)r["total"],
            Status    = r["estado"].ToString()!,
            ServiceId = (int)r["id_service"],
            BudgetId  = r["id_presupuesto"] as int?
        };
    }
}

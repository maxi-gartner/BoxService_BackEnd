using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class InvoiceRepository
    {
        public List<Invoice> GetAll(int tenantId)
        {
            var lista = new List<Invoice>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM facturas WHERE tenant_id = @tenant_id ORDER BY created_at DESC", conn);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapRow(reader));
            return lista;
        }

        // status: "por-cobrar" (issued) o null/"todas" (sin filtro). Trae
        // marca/modelo/patente del vehículo embebidos (join a través de
        // services) para que Facturas no tenga que pedir /services y
        // /vehicles aparte solo para armar la etiqueta de cada fila.
        public (List<(Invoice Invoice, int VehicleId, string Plate, string Brand, string Model)> Items, int TotalCount) GetPaged(
            int tenantId, int page, int pageSize, string? status)
        {
            var result = new List<(Invoice, int, string, string, string)>();
            var totalCount = 0;

            var statusFilter = status == "por-cobrar" ? "AND f.estado = 'issued'" : "";

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand($@"
                SELECT f.*, v.id_vehiculo AS vehicle_id, v.patente AS vehicle_patente,
                       v.marca AS vehicle_marca, v.modelo AS vehicle_modelo,
                       COUNT(*) OVER() AS total_count
                FROM facturas f
                JOIN services s ON s.id_service = f.id_service
                JOIN vehiculos v ON v.id_vehiculo = s.id_vehiculo
                WHERE f.tenant_id = @tenant_id {statusFilter}
                ORDER BY f.created_at DESC
                LIMIT @page_size OFFSET @offset;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add((
                    MapRow(reader),
                    reader.GetInt32(reader.GetOrdinal("vehicle_id")),
                    reader.GetString(reader.GetOrdinal("vehicle_patente")),
                    reader.GetString(reader.GetOrdinal("vehicle_marca")),
                    reader.GetString(reader.GetOrdinal("vehicle_modelo"))));
                totalCount = reader.GetInt32(reader.GetOrdinal("total_count"));
            }

            return (result, totalCount);
        }

        // Sin paginar a propósito: un vehículo puntual nunca acumula miles
        // de facturas propias — la usa InvoicingList (Taller) en vez de
        // pedir la tabla global y filtrar del lado del cliente.
        public List<Invoice> GetByVehicleId(int vehicleId)
        {
            var lista = new List<Invoice>();
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT f.* FROM facturas f
                JOIN services s ON s.id_service = f.id_service
                WHERE s.id_vehiculo = @vehicle_id
                ORDER BY f.created_at DESC;", conn);
            cmd.Parameters.AddWithValue("vehicle_id", vehicleId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapRow(reader));
            return lista;
        }

        // Agregados para las tarjetas de resumen de FacturasPage — un solo
        // COUNT/SUM en la base en vez de traer todas las facturas al
        // frontend solo para sumarlas ahí.
        public (int TotalCount, int PendingCount, decimal PendingTotal) GetStats(int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) AS total_count,
                       COUNT(*) FILTER (WHERE estado = 'issued') AS pending_count,
                       COALESCE(SUM(total) FILTER (WHERE estado = 'issued'), 0) AS pending_total
                FROM facturas
                WHERE tenant_id = @tenant_id;", conn);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            return (
                reader.GetInt32(reader.GetOrdinal("total_count")),
                reader.GetInt32(reader.GetOrdinal("pending_count")),
                reader.GetDecimal(reader.GetOrdinal("pending_total")));
        }

        public Invoice? GetById(int id, int tenantId)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM facturas WHERE id_factura = @id AND tenant_id = @tenant_id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
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

        // Ver comentario en BudgetRepository.GetNextNumber: usar una SEQUENCE evita
        // que dos facturas concurrentes terminen con el mismo número.
        public long GetNextNumber()
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT nextval('facturas_numero_seq')", conn);
            return (long)cmd.ExecuteScalar()!;
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
                    INSERT INTO facturas (numero, fecha, total, estado, id_service, id_presupuesto, tenant_id)
                    VALUES (@numero, @fecha, @total, 'issued', @id_service, @id_presupuesto, @tenant_id)
                    RETURNING id_factura", conn, tx);
                cmdInvoice.Parameters.AddWithValue("numero",         inv.Number);
                cmdInvoice.Parameters.AddWithValue("fecha",          DateTime.Today);
                cmdInvoice.Parameters.AddWithValue("total",          inv.Total);
                cmdInvoice.Parameters.AddWithValue("id_service",     inv.ServiceId);
                cmdInvoice.Parameters.AddWithValue("id_presupuesto", (object?)inv.BudgetId ?? DBNull.Value);
                cmdInvoice.Parameters.AddWithValue("tenant_id",      inv.TenantId);
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

        public bool UpdateStatus(int id, int tenantId, string status)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("UPDATE facturas SET estado = @estado WHERE id_factura = @id AND tenant_id = @tenant_id", conn);
            cmd.Parameters.AddWithValue("estado", status);
            cmd.Parameters.AddWithValue("id",     id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static Invoice MapRow(NpgsqlDataReader r) => new()
        {
            InvoiceId = (int)r["id_factura"],
            TenantId  = (int)r["tenant_id"],
            Number    = r["numero"].ToString()!,
            // Npgsql mapea las columnas DATE de Postgres a DateOnly, no a DateTime.
            Date      = ((DateOnly)r["fecha"]).ToString("yyyy-MM-dd"),
            Total     = (decimal)r["total"],
            Status    = r["estado"].ToString()!,
            ServiceId = (int)r["id_service"],
            BudgetId  = r["id_presupuesto"] as int?
        };
    }
}

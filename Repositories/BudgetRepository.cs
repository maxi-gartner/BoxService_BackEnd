using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class BudgetRepository
    {
        public List<Budget> GetAll()
        {
            var lista = new List<Budget>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM presupuestos ORDER BY created_at DESC", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapRow(reader));
            return lista;
        }

        public Budget? GetById(int id)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM presupuestos WHERE id_presupuesto = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public List<BudgetDetail> GetDetails(int budgetId)
        {
            var lista = new List<BudgetDetail>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM detalle_presupuesto WHERE id_presupuesto = @id", conn);
            cmd.Parameters.AddWithValue("id", budgetId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(new BudgetDetail
                {
                    DetailId    = (int)reader["id_detalle"],
                    BudgetId    = (int)reader["id_presupuesto"],
                    Type        = reader["tipo"].ToString()!,
                    Description = reader["descripcion"].ToString()!,
                    Quantity    = (decimal)reader["cantidad"],
                    UnitPrice   = (decimal)reader["precio_unitario"],
                    Subtotal    = (decimal)reader["subtotal"]
                });
            return lista;
        }

        public string GetLastNumber()
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT numero FROM presupuestos ORDER BY id_presupuesto DESC LIMIT 1", conn);
            return cmd.ExecuteScalar()?.ToString() ?? "P-0000";
        }

        public int Create(Budget b)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO presupuestos (numero, fecha, estado, observaciones, id_vehiculo)
                VALUES (@numero, @fecha, @estado, @observaciones, @id_vehiculo)
                RETURNING id_presupuesto", conn);
            cmd.Parameters.AddWithValue("numero",        b.Number);
            cmd.Parameters.AddWithValue("fecha",         DateTime.Today);
            cmd.Parameters.AddWithValue("estado",        "draft");
            cmd.Parameters.AddWithValue("observaciones", (object?)b.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id_vehiculo",   b.VehicleId);
            return (int)cmd.ExecuteScalar()!;
        }

        public void CreateDetail(BudgetDetail d, NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO detalle_presupuesto (id_presupuesto, tipo, descripcion, cantidad, precio_unitario, subtotal)
                VALUES (@id_presupuesto, @tipo, @descripcion, @cantidad, @precio_unitario, @subtotal)", conn, tx);
            cmd.Parameters.AddWithValue("id_presupuesto",  d.BudgetId);
            cmd.Parameters.AddWithValue("tipo",            d.Type);
            cmd.Parameters.AddWithValue("descripcion",     d.Description);
            cmd.Parameters.AddWithValue("cantidad",        d.Quantity);
            cmd.Parameters.AddWithValue("precio_unitario", d.UnitPrice);
            cmd.Parameters.AddWithValue("subtotal",        d.Subtotal);
            cmd.ExecuteNonQuery();
        }

        public void UpdateStatus(int id, string status)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand(
                "UPDATE presupuestos SET estado = @estado WHERE id_presupuesto = @id", conn);
            cmd.Parameters.AddWithValue("estado", status);
            cmd.Parameters.AddWithValue("id",     id);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Transacción ACID:
        /// 1. Crea el service
        /// 2. Copia detalles como detalle_service
        /// 3. Actualiza presupuesto a approved y vincula el service
        /// </summary>
        public int ApproveWithTransaction(int budgetId, List<BudgetDetail> details, int vehicleId)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                using var cmdService = new NpgsqlCommand(@"
                    INSERT INTO services (fecha, kilometraje, tipo_service, id_vehiculo, id_presupuesto)
                    VALUES (@fecha, 0, 'From budget', @id_vehiculo, @id_presupuesto)
                    RETURNING id_service", conn, tx);
                cmdService.Parameters.AddWithValue("fecha",          DateTime.Today);
                cmdService.Parameters.AddWithValue("id_vehiculo",    vehicleId);
                cmdService.Parameters.AddWithValue("id_presupuesto", budgetId);
                var serviceId = (int)cmdService.ExecuteScalar()!;

                foreach (var d in details)
                {
                    using var cmdDet = new NpgsqlCommand(@"
                        INSERT INTO detalle_service (id_service, descripcion, realizado)
                        VALUES (@id_service, @descripcion, FALSE)", conn, tx);
                    cmdDet.Parameters.AddWithValue("id_service",  serviceId);
                    cmdDet.Parameters.AddWithValue("descripcion", d.Description);
                    cmdDet.ExecuteNonQuery();
                }

                using var cmdUpdate = new NpgsqlCommand(@"
                    UPDATE presupuestos SET estado = 'approved', id_service = @id_service
                    WHERE id_presupuesto = @id", conn, tx);
                cmdUpdate.Parameters.AddWithValue("id_service", serviceId);
                cmdUpdate.Parameters.AddWithValue("id",         budgetId);
                cmdUpdate.ExecuteNonQuery();

                tx.Commit();
                return serviceId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static Budget MapRow(NpgsqlDataReader r) => new()
        {
            BudgetId  = (int)r["id_presupuesto"],
            Number    = r["numero"].ToString()!,
            Date      = r["fecha"].ToString()!,
            Status    = r["estado"].ToString()!,
            Notes     = r["observaciones"] as string,
            VehicleId = (int)r["id_vehiculo"],
            ServiceId = r["id_service"] as int?
        };
    }
}

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

            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(
                "SELECT * FROM presupuestos ORDER BY created_at DESC",
                conn
            );

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(MapRow(reader));
            }

            return lista;
        }

        public Budget? GetById(int id)
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(
                "SELECT * FROM presupuestos WHERE id_presupuesto = @id",
                conn
            );

            cmd.Parameters.AddWithValue("id", id);

            using var reader = cmd.ExecuteReader();

            return reader.Read() ? MapRow(reader) : null;
        }

        public List<BudgetDetail> GetDetails(int budgetId)
        {
            var lista = new List<BudgetDetail>();

            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(
                "SELECT * FROM detalle_presupuesto WHERE id_presupuesto = @id",
                conn
            );

            cmd.Parameters.AddWithValue("id", budgetId);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new BudgetDetail
                {
                    DetailId = (int)reader["id_detalle"],
                    BudgetId = (int)reader["id_presupuesto"],
                    Type = reader["tipo"].ToString()!,
                    Description = reader["descripcion"].ToString()!,
                    Quantity = (decimal)reader["cantidad"],
                    UnitPrice = (decimal)reader["precio_unitario"],
                    Subtotal = (decimal)reader["subtotal"]
                });
            }

            return lista;
        }

        // Genera el próximo número de presupuesto de forma atómica usando una
        // SEQUENCE de Postgres. nextval() nunca devuelve el mismo valor dos veces,
        // incluso si dos requests concurrentes lo llaman al mismo tiempo — a
        // diferencia de leer el último número y sumarle 1 en memoria.
        public long GetNextNumber()
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(
                "SELECT nextval('presupuestos_numero_seq')",
                conn
            );

            return (long)cmd.ExecuteScalar()!;
        }

        public int Create(Budget b)
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO presupuestos (
                    numero,
                    fecha,
                    estado,
                    observaciones,
                    id_vehiculo
                )
                VALUES (
                    @numero,
                    @fecha,
                    @estado,
                    @observaciones,
                    @id_vehiculo
                )
                RETURNING id_presupuesto", conn);

            cmd.Parameters.AddWithValue("numero", b.Number);
            cmd.Parameters.AddWithValue("fecha", DateTime.Today);
            cmd.Parameters.AddWithValue("estado", "draft");
            cmd.Parameters.AddWithValue("observaciones", (object?)b.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id_vehiculo", b.VehicleId);

            return (int)cmd.ExecuteScalar()!;
        }

        public void CreateDetail(BudgetDetail d, NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO detalle_presupuesto (
                    id_presupuesto,
                    tipo,
                    descripcion,
                    cantidad,
                    precio_unitario,
                    subtotal
                )
                VALUES (
                    @id_presupuesto,
                    @tipo,
                    @descripcion,
                    @cantidad,
                    @precio_unitario,
                    @subtotal
                )", conn, tx);

            cmd.Parameters.AddWithValue("id_presupuesto", d.BudgetId);
            cmd.Parameters.AddWithValue("tipo", d.Type);
            cmd.Parameters.AddWithValue("descripcion", d.Description);
            cmd.Parameters.AddWithValue("cantidad", d.Quantity);
            cmd.Parameters.AddWithValue("precio_unitario", d.UnitPrice);
            cmd.Parameters.AddWithValue("subtotal", d.Subtotal);

            cmd.ExecuteNonQuery();
        }

        public void UpdateStatus(int id, string status)
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                UPDATE presupuestos
                SET estado = @estado
                WHERE id_presupuesto = @id", conn);

            cmd.Parameters.AddWithValue("estado", status);
            cmd.Parameters.AddWithValue("id", id);

            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// CAMBIO:
        /// Antes este método creaba automáticamente un registro en services
        /// cuando se aprobaba un presupuesto.
        ///
        /// Ahora solo aprueba el presupuesto.
        /// El service lo crea después el módulo de Services.
        /// </summary>
        public int ApproveWithTransaction(int budgetId, List<BudgetDetail> details, int vehicleId)
        {
            // CAMBIO:
            // Se mantiene la firma del método para no romper BudgetService,
            // pero details y vehicleId ya no se usan acá porque no se crea service.
            _ = details;
            _ = vehicleId;

            using var conn = DatabaseConnection.GetConnection();
            using var tx = conn.BeginTransaction();

            try
            {
                // CAMBIO:
                // Ya NO se hace INSERT INTO services.
                // Solo se cambia el estado del presupuesto a approved.
                // id_service queda como estaba.
                // Si el presupuesto todavía no generó service, seguirá en NULL.
                using var cmdUpdate = new NpgsqlCommand(@"
                    UPDATE presupuestos
                    SET estado = 'approved'
                    WHERE id_presupuesto = @id", conn, tx);

                cmdUpdate.Parameters.AddWithValue("id", budgetId);

                var rowsAffected = cmdUpdate.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    throw new Exception("Budget not found");
                }

                tx.Commit();

                // CAMBIO:
                // Antes devolvía el id_service creado automáticamente.
                // Ahora devuelve el id_presupuesto aprobado.
                return budgetId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// NUEVO:
        /// Vincula un presupuesto aprobado con el service creado desde el módulo de Services.
        ///
        /// Esta es la relación correcta según el modelo nuevo:
        /// presupuestos.id_service -> services.id_service
        /// </summary>
        public void AssignService(int budgetId, int serviceId)
        {
            using var conn = DatabaseConnection.GetConnection();

            // El presupuesto pasa a "completed" acá: es el momento en el que el
            // trabajo efectivamente se hizo (se generó el service), no cuando
            // se aprobó. "approved" pasa a significar "esperando que se haga".
            using var cmd = new NpgsqlCommand(@"
                UPDATE presupuestos
                SET id_service = @id_service, estado = 'completed'
                WHERE id_presupuesto = @id_presupuesto", conn);

            cmd.Parameters.AddWithValue("id_service", serviceId);
            cmd.Parameters.AddWithValue("id_presupuesto", budgetId);

            var rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected == 0)
            {
                throw new Exception("Budget not found");
            }
        }

        private static Budget MapRow(NpgsqlDataReader r) => new()
        {
            BudgetId = (int)r["id_presupuesto"],
            Number = r["numero"].ToString()!,
            // Npgsql mapea las columnas DATE de Postgres a DateOnly, no a DateTime.
            Date = ((DateOnly)r["fecha"]).ToString("yyyy-MM-dd"),
            Status = r["estado"].ToString()!,
            Notes = r["observaciones"] as string,
            VehicleId = (int)r["id_vehiculo"],

            // CAMBIO:
            // Se lee id_service de forma segura porque puede venir NULL.
            ServiceId = r.IsDBNull(r.GetOrdinal("id_service"))
                ? null
                : r.GetInt32(r.GetOrdinal("id_service"))
        };
    }
}
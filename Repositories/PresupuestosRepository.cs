using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories
{
    public class PresupuestosRepository
    {
        public List<Presupuesto> GetAll()
        {
            var lista = new List<Presupuesto>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM presupuestos ORDER BY created_at DESC", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapRow(reader));
            return lista;
        }

        public Presupuesto? GetById(int id)
        {
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM presupuestos WHERE id_presupuesto = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapRow(reader) : null;
        }

        public List<DetallePresupuesto> GetDetalles(int idPresupuesto)
        {
            var lista = new List<DetallePresupuesto>();
            using var conn   = DatabaseConnection.GetConnection();
            using var cmd    = new NpgsqlCommand("SELECT * FROM detalle_presupuesto WHERE id_presupuesto = @id", conn);
            cmd.Parameters.AddWithValue("id", idPresupuesto);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(new DetallePresupuesto
                {
                    IdDetalle      = (int)reader["id_detalle"],
                    IdPresupuesto  = (int)reader["id_presupuesto"],
                    Tipo           = reader["tipo"].ToString()!,
                    Descripcion    = reader["descripcion"].ToString()!,
                    Cantidad       = (decimal)reader["cantidad"],
                    PrecioUnitario = (decimal)reader["precio_unitario"],
                    Subtotal       = (decimal)reader["subtotal"]
                });
            return lista;
        }

        public string GetUltimoNumero()
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT numero FROM presupuestos ORDER BY id_presupuesto DESC LIMIT 1", conn);
            return cmd.ExecuteScalar()?.ToString() ?? "P-0000";
        }

        public int Create(Presupuesto p)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO presupuestos (numero, fecha, estado, observaciones, id_vehiculo)
                VALUES (@numero, @fecha, @estado, @observaciones, @id_vehiculo)
                RETURNING id_presupuesto", conn);
            cmd.Parameters.AddWithValue("numero",        p.Numero);
            cmd.Parameters.AddWithValue("fecha",         DateTime.Today);
            cmd.Parameters.AddWithValue("estado",        "borrador");
            cmd.Parameters.AddWithValue("observaciones", (object?)p.Observaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id_vehiculo",   p.IdVehiculo);
            return (int)cmd.ExecuteScalar()!;
        }

        public void CreateDetalle(DetallePresupuesto d, NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO detalle_presupuesto (id_presupuesto, tipo, descripcion, cantidad, precio_unitario, subtotal)
                VALUES (@id_presupuesto, @tipo, @descripcion, @cantidad, @precio_unitario, @subtotal)", conn, tx);
            cmd.Parameters.AddWithValue("id_presupuesto",  d.IdPresupuesto);
            cmd.Parameters.AddWithValue("tipo",            d.Tipo);
            cmd.Parameters.AddWithValue("descripcion",     d.Descripcion);
            cmd.Parameters.AddWithValue("cantidad",        d.Cantidad);
            cmd.Parameters.AddWithValue("precio_unitario", d.PrecioUnitario);
            cmd.Parameters.AddWithValue("subtotal",        d.Subtotal);
            cmd.ExecuteNonQuery();
        }

        public void CambiarEstado(int id, string estado)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd  = new NpgsqlCommand(
                "UPDATE presupuestos SET estado = @estado WHERE id_presupuesto = @id", conn);
            cmd.Parameters.AddWithValue("estado", estado);
            cmd.Parameters.AddWithValue("id",     id);
            cmd.ExecuteNonQuery();
        }

        public int AprobarConTransaccion(int idPresupuesto, List<DetallePresupuesto> detalles, int idVehiculo)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var tx   = conn.BeginTransaction();
            try
            {
                // PASO 1 — crear service
                using var cmdService = new NpgsqlCommand(@"
                    INSERT INTO services (fecha, kilometraje, tipo_service, id_vehiculo, id_presupuesto)
                    VALUES (@fecha, 0, 'Desde presupuesto', @id_vehiculo, @id_presupuesto)
                    RETURNING id_service", conn, tx);
                cmdService.Parameters.AddWithValue("fecha",          DateTime.Today);
                cmdService.Parameters.AddWithValue("id_vehiculo",    idVehiculo);
                cmdService.Parameters.AddWithValue("id_presupuesto", idPresupuesto);
                var idService = (int)cmdService.ExecuteScalar()!;

                // PASO 2 — copiar detalles como detalle_service
                foreach (var d in detalles)
                {
                    using var cmdDet = new NpgsqlCommand(@"
                        INSERT INTO detalle_service (id_service, descripcion, realizado)
                        VALUES (@id_service, @descripcion, FALSE)", conn, tx);
                    cmdDet.Parameters.AddWithValue("id_service",  idService);
                    cmdDet.Parameters.AddWithValue("descripcion", d.Descripcion);
                    cmdDet.ExecuteNonQuery();
                }

                // PASO 3 — actualizar presupuesto
                using var cmdUpdate = new NpgsqlCommand(@"
                    UPDATE presupuestos SET estado = 'aprobado', id_service = @id_service
                    WHERE id_presupuesto = @id", conn, tx);
                cmdUpdate.Parameters.AddWithValue("id_service", idService);
                cmdUpdate.Parameters.AddWithValue("id",         idPresupuesto);
                cmdUpdate.ExecuteNonQuery();

                tx.Commit();
                return idService;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static Presupuesto MapRow(NpgsqlDataReader r) => new()
        {
            IdPresupuesto = (int)r["id_presupuesto"],
            Numero        = r["numero"].ToString()!,
            Fecha         = r["fecha"].ToString()!,
            Estado        = r["estado"].ToString()!,
            Observaciones = r["observaciones"] as string,
            IdVehiculo    = (int)r["id_vehiculo"],
            IdService     = r["id_service"] as int?
        };
    }
}

using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class TenantPaymentRepository
    {
        public List<TenantPayment> GetByTenant(int tenantId)
        {
            var result = new List<TenantPayment>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id_pago, id_tenant, monto, periodo, fecha_pago, notas, created_at
                FROM tenant_payments
                WHERE id_tenant = @tenant_id
                ORDER BY periodo DESC;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        // Upsert: un pago por (tenant, periodo) — si superadmin corrige
        // un monto ya cargado del mismo mes, actualiza en vez de duplicar.
        public TenantPayment Record(int tenantId, TenantPaymentCreateRequest request)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO tenant_payments (id_tenant, monto, periodo, fecha_pago, notas)
                VALUES (@tenant_id, @monto, @periodo, @fecha_pago, @notas)
                ON CONFLICT (id_tenant, periodo) DO UPDATE SET
                    monto = EXCLUDED.monto,
                    fecha_pago = EXCLUDED.fecha_pago,
                    notas = EXCLUDED.notas
                RETURNING id_pago, id_tenant, monto, periodo, fecha_pago, notas, created_at;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("monto", request.Amount);
            cmd.Parameters.AddWithValue("periodo", request.Period);
            cmd.Parameters.AddWithValue("fecha_pago", NpgsqlTypes.NpgsqlDbType.Date,
                DateOnly.FromDateTime(request.PaymentDate ?? DateTime.Today));
            cmd.Parameters.AddWithValue("notas", string.IsNullOrWhiteSpace(request.Notes) ? DBNull.Value : request.Notes.Trim());

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return MapRow(reader);
        }

        // Pantalla de superadmin: por cada taller, si pagó el período
        // actual y cuál fue su último pago (de cualquier período) como
        // referencia — un LEFT JOIN LATERAL trae "el más reciente" sin
        // necesitar una query aparte por tenant.
        public List<TenantPaymentStatus> GetOverview(string currentPeriod)
        {
            var result = new List<TenantPaymentStatus>();

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT
                    t.id_tenant,
                    t.nombre,
                    (p_cur.id_pago IS NOT NULL) AS paid_current,
                    p_last.monto AS last_amount,
                    p_last.periodo AS last_period,
                    p_last.fecha_pago AS last_payment_date
                FROM tenants t
                LEFT JOIN tenant_payments p_cur
                    ON p_cur.id_tenant = t.id_tenant AND p_cur.periodo = @periodo
                LEFT JOIN LATERAL (
                    SELECT monto, periodo, fecha_pago
                    FROM tenant_payments
                    WHERE id_tenant = t.id_tenant
                    ORDER BY periodo DESC
                    LIMIT 1
                ) p_last ON true
                ORDER BY t.id_tenant;", conn);

            cmd.Parameters.AddWithValue("periodo", currentPeriod);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var lastAmountOrdinal = reader.GetOrdinal("last_amount");
                var lastPeriodOrdinal = reader.GetOrdinal("last_period");
                var lastDateOrdinal = reader.GetOrdinal("last_payment_date");

                result.Add(new TenantPaymentStatus
                {
                    TenantId = reader.GetInt32(reader.GetOrdinal("id_tenant")),
                    TenantName = reader.GetString(reader.GetOrdinal("nombre")),
                    PaidCurrentPeriod = reader.GetBoolean(reader.GetOrdinal("paid_current")),
                    LastAmount = reader.IsDBNull(lastAmountOrdinal) ? null : reader.GetDecimal(lastAmountOrdinal),
                    LastPeriod = reader.IsDBNull(lastPeriodOrdinal) ? null : reader.GetString(lastPeriodOrdinal),
                    LastPaymentDate = reader.IsDBNull(lastDateOrdinal) ? null : reader.GetDateTime(lastDateOrdinal),
                });
            }

            return result;
        }

        private static TenantPayment MapRow(NpgsqlDataReader r) => new()
        {
            PaymentId = r.GetInt32(r.GetOrdinal("id_pago")),
            TenantId = r.GetInt32(r.GetOrdinal("id_tenant")),
            Amount = r.GetDecimal(r.GetOrdinal("monto")),
            Period = r.GetString(r.GetOrdinal("periodo")),
            PaymentDate = r.GetDateTime(r.GetOrdinal("fecha_pago")),
            Notes = r.IsDBNull(r.GetOrdinal("notas")) ? null : r.GetString(r.GetOrdinal("notas")),
            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at")),
        };
    }
}

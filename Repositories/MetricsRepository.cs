using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Repositories
{
    public class MetricsRepository
    {
        public TenantMetrics GetMetrics(int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var yearStart = new DateTime(today.Year, 1, 1);
            var yearEnd = yearStart.AddYears(1);

            decimal Scalar(string sql, Action<NpgsqlCommand> bind)
            {
                using var cmd = new NpgsqlCommand(sql, conn);
                bind(cmd);
                var result = cmd.ExecuteScalar();
                return result is null || result is DBNull ? 0 : Convert.ToDecimal(result);
            }

            int ScalarInt(string sql, Action<NpgsqlCommand> bind)
            {
                using var cmd = new NpgsqlCommand(sql, conn);
                bind(cmd);
                var result = cmd.ExecuteScalar();
                return result is null || result is DBNull ? 0 : Convert.ToInt32(result);
            }

            var metrics = new TenantMetrics();

            metrics.RevenueThisMonth = Scalar(
                "SELECT COALESCE(SUM(total), 0) FROM facturas WHERE tenant_id = @t AND estado <> 'cancelled' AND fecha >= @start AND fecha < @end",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("t", tenantId);
                    cmd.Parameters.AddWithValue("start", DateOnly.FromDateTime(monthStart));
                    cmd.Parameters.AddWithValue("end", DateOnly.FromDateTime(monthEnd));
                });

            metrics.RevenueThisYear = Scalar(
                "SELECT COALESCE(SUM(total), 0) FROM facturas WHERE tenant_id = @t AND estado <> 'cancelled' AND fecha >= @start AND fecha < @end",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("t", tenantId);
                    cmd.Parameters.AddWithValue("start", DateOnly.FromDateTime(yearStart));
                    cmd.Parameters.AddWithValue("end", DateOnly.FromDateTime(yearEnd));
                });

            metrics.ServicesThisMonth = ScalarInt(
                "SELECT COUNT(*) FROM services WHERE tenant_id = @t AND fecha >= @start AND fecha < @end",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("t", tenantId);
                    cmd.Parameters.AddWithValue("start", DateOnly.FromDateTime(monthStart));
                    cmd.Parameters.AddWithValue("end", DateOnly.FromDateTime(monthEnd));
                });

            metrics.ServicesThisYear = ScalarInt(
                "SELECT COUNT(*) FROM services WHERE tenant_id = @t AND fecha >= @start AND fecha < @end",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("t", tenantId);
                    cmd.Parameters.AddWithValue("start", DateOnly.FromDateTime(yearStart));
                    cmd.Parameters.AddWithValue("end", DateOnly.FromDateTime(yearEnd));
                });

            metrics.NewClientsThisMonth = ScalarInt(
                "SELECT COUNT(*) FROM clientes WHERE tenant_id = @t AND created_at >= @start AND created_at < @end",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("t", tenantId);
                    cmd.Parameters.AddWithValue("start", monthStart);
                    cmd.Parameters.AddWithValue("end", monthEnd);
                });

            metrics.PendingBudgetsCount = ScalarInt(
                "SELECT COUNT(*) FROM presupuestos WHERE tenant_id = @t AND estado IN ('draft','sent')",
                cmd => cmd.Parameters.AddWithValue("t", tenantId));

            metrics.PendingBudgetsValue = Scalar(@"
                SELECT COALESCE(SUM(dp.subtotal), 0)
                FROM detalle_presupuesto dp
                JOIN presupuestos p ON p.id_presupuesto = dp.id_presupuesto
                WHERE p.tenant_id = @t AND p.estado IN ('draft','sent')",
                cmd => cmd.Parameters.AddWithValue("t", tenantId));

            var invoiceCountThisMonth = ScalarInt(
                "SELECT COUNT(*) FROM facturas WHERE tenant_id = @t AND estado <> 'cancelled' AND fecha >= @start AND fecha < @end",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("t", tenantId);
                    cmd.Parameters.AddWithValue("start", DateOnly.FromDateTime(monthStart));
                    cmd.Parameters.AddWithValue("end", DateOnly.FromDateTime(monthEnd));
                });
            metrics.AverageTicketThisMonth = invoiceCountThisMonth > 0 ? metrics.RevenueThisMonth / invoiceCountThisMonth : 0;

            // Últimos 6 meses (incluido el actual) — alcanza para ver una
            // tendencia simple sin tener que armar un gráfico de verdad.
            using (var cmd = new NpgsqlCommand(@"
                SELECT TO_CHAR(fecha, 'YYYY-MM') AS mes, COALESCE(SUM(total), 0) AS total
                FROM facturas
                WHERE tenant_id = @t AND estado <> 'cancelled' AND fecha >= @start
                GROUP BY mes
                ORDER BY mes;", conn))
            {
                cmd.Parameters.AddWithValue("t", tenantId);
                cmd.Parameters.AddWithValue("start", DateOnly.FromDateTime(monthStart.AddMonths(-5)));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    metrics.RevenueByMonth.Add(new MonthlyRevenue
                    {
                        Month = reader.GetString(0),
                        Total = reader.GetDecimal(1),
                    });
                }
            }

            // Ranking de ítems — solo de presupuestos completados (trabajo
            // realmente hecho), no cotizaciones que quedaron en borrador.
            var ranked = new List<ServiceRanking>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT dp.descripcion, dp.tipo, COUNT(*) AS veces, SUM(dp.subtotal) AS total
                FROM detalle_presupuesto dp
                JOIN presupuestos p ON p.id_presupuesto = dp.id_presupuesto
                WHERE p.tenant_id = @t AND p.estado = 'completed'
                GROUP BY dp.descripcion, dp.tipo
                ORDER BY total DESC;", conn))
            {
                cmd.Parameters.AddWithValue("t", tenantId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ranked.Add(new ServiceRanking
                    {
                        Description = reader.GetString(0),
                        Type = reader.GetString(1),
                        Count = (int)reader.GetInt64(2),
                        Total = reader.GetDecimal(3),
                    });
                }
            }
            metrics.TopServicesByRevenue = ranked.OrderByDescending(r => r.Total).Take(8).ToList();
            metrics.TopServicesByFrequency = ranked.OrderByDescending(r => r.Count).Take(8).ToList();

            return metrics;
        }
    }
}

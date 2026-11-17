using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories;

public class WorkOrderRepository
{
    // Insert condicionado a que no haya otra orden abierta (estado <>
    // 'entregado') para el mismo vehículo — una sola query, sin ventana
    // entre el chequeo y el insert (mismo patrón que
    // VehicleStatusInquiryRepository.Create).
    public virtual WorkOrder? Create(int tenantId, int vehicleId, string? notes, string receivedBy)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO ordenes_trabajo (tenant_id, id_vehiculo, notas, recibido_por)
            SELECT @tenant, @vehicle, @notas, @receivedBy
            WHERE NOT EXISTS (
                SELECT 1 FROM ordenes_trabajo
                WHERE id_vehiculo = @vehicle AND tenant_id = @tenant AND estado <> 'entregado'
            )
            RETURNING id_orden, tenant_id, id_vehiculo, estado, notas, recibido_por,
                      entregado_por, created_at, started_at, finished_at, delivered_at;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("vehicle", vehicleId);
        cmd.Parameters.AddWithValue("notas", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("receivedBy", receivedBy);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapRow(reader) : null;
    }

    public virtual WorkOrder? GetById(int id, int tenantId)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            SELECT id_orden, tenant_id, id_vehiculo, estado, notas, recibido_por,
                   entregado_por, created_at, started_at, finished_at, delivered_at
            FROM ordenes_trabajo
            WHERE id_orden = @id AND tenant_id = @tenant;", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenant", tenantId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapRow(reader) : null;
    }

    // La más reciente de ese vehículo, abierta o no — así el header del
    // Taller sigue mostrando "entregado" hasta que se reciba de nuevo.
    public virtual WorkOrder? GetLatestForVehicle(int vehicleId, int tenantId)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            SELECT id_orden, tenant_id, id_vehiculo, estado, notas, recibido_por,
                   entregado_por, created_at, started_at, finished_at, delivered_at
            FROM ordenes_trabajo
            WHERE id_vehiculo = @vehicle AND tenant_id = @tenant
            ORDER BY created_at DESC
            LIMIT 1;", conn);
        cmd.Parameters.AddWithValue("vehicle", vehicleId);
        cmd.Parameters.AddWithValue("tenant", tenantId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapRow(reader) : null;
    }

    public virtual List<WorkOrderRow> ListOpen(int tenantId)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            SELECT o.id_orden, v.id_vehiculo, o.estado, o.notas, c.nombre,
                   v.marca || ' ' || v.modelo, v.patente, o.created_at
            FROM ordenes_trabajo o
            JOIN vehiculos v ON v.id_vehiculo = o.id_vehiculo AND v.tenant_id = @tenant
            JOIN clientes c ON c.id_cliente = v.id_cliente AND c.tenant_id = @tenant
            WHERE o.tenant_id = @tenant AND o.estado <> 'entregado'
            ORDER BY o.created_at;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);

        using var reader = cmd.ExecuteReader();
        var result = new List<WorkOrderRow>();
        while (reader.Read())
        {
            result.Add(new WorkOrderRow(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.GetString(6), reader.GetDateTime(7)));
        }
        return result;
    }

    // Compare-and-swap: solo pisa si "estado" sigue siendo el esperado —
    // si otro click ya lo movió, 0 filas, sin necesidad de un lock aparte.
    public virtual bool UpdateStatus(int id, int tenantId, string currentStatus, string newStatus, string? notes, string? deliveredBy)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            UPDATE ordenes_trabajo
            SET estado = @newStatus,
                notas = COALESCE(@notas, notas),
                entregado_por = COALESCE(@deliveredBy, entregado_por),
                started_at = CASE WHEN @newStatus = 'en_proceso' THEN NOW() ELSE started_at END,
                finished_at = CASE WHEN @newStatus = 'terminado' THEN NOW() ELSE finished_at END,
                delivered_at = CASE WHEN @newStatus = 'entregado' THEN NOW() ELSE delivered_at END
            WHERE id_orden = @id AND tenant_id = @tenant AND estado = @currentStatus;", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("currentStatus", currentStatus);
        cmd.Parameters.AddWithValue("newStatus", newStatus);
        cmd.Parameters.AddWithValue("notas", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("deliveredBy", (object?)deliveredBy ?? DBNull.Value);
        return cmd.ExecuteNonQuery() > 0;
    }

    public virtual bool Cancel(int id, int tenantId)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            DELETE FROM ordenes_trabajo
            WHERE id_orden = @id AND tenant_id = @tenant AND estado = 'en_espera';", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static WorkOrder MapRow(NpgsqlDataReader r) => new()
    {
        OrderId = r.GetInt32(0),
        TenantId = r.GetInt32(1),
        VehicleId = r.GetInt32(2),
        Status = r.GetString(3),
        Notes = r.IsDBNull(4) ? null : r.GetString(4),
        ReceivedBy = r.IsDBNull(5) ? null : r.GetString(5),
        DeliveredBy = r.IsDBNull(6) ? null : r.GetString(6),
        CreatedAt = r.GetDateTime(7),
        StartedAt = r.IsDBNull(8) ? null : r.GetDateTime(8),
        FinishedAt = r.IsDBNull(9) ? null : r.GetDateTime(9),
        DeliveredAt = r.IsDBNull(10) ? null : r.GetDateTime(10),
    };
}

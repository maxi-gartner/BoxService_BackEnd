using BoxService_BackEnd.Database;
using BoxService_BackEnd.Models;
using Npgsql;

namespace BoxService_BackEnd.Repositories;

public class VehicleStatusInquiryRepository
{
    // Insert condicionado a que no haya otra consulta del mismo vehículo
    // en la última hora — una sola query, sin ventana entre el chequeo y
    // el insert (dos clicks a la vez no pueden colarse los dos).
    public virtual VehicleStatusInquiry? Create(int tenantId, int vehicleId, int clientId)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO vehicle_status_inquiries (tenant_id, id_vehiculo, id_cliente)
            SELECT @tenant, @vehicle, @client
            WHERE NOT EXISTS (
                SELECT 1 FROM vehicle_status_inquiries
                WHERE id_vehiculo = @vehicle AND tenant_id = @tenant
                  AND created_at > NOW() - INTERVAL '1 hour'
            )
            RETURNING id_inquiry, tenant_id, id_vehiculo, id_cliente, created_at, resolved_at;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("vehicle", vehicleId);
        cmd.Parameters.AddWithValue("client", clientId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new VehicleStatusInquiry
        {
            InquiryId = reader.GetInt32(0),
            TenantId = reader.GetInt32(1),
            VehicleId = reader.GetInt32(2),
            ClientId = reader.GetInt32(3),
            CreatedAt = reader.GetDateTime(4),
            ResolvedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        };
    }

    public virtual List<VehicleStatusInquiryRow> ListPending(int tenantId)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            SELECT i.id_inquiry, v.id_vehiculo, c.id_cliente, c.nombre,
                   v.marca || ' ' || v.modelo, v.patente, i.created_at
            FROM vehicle_status_inquiries i
            JOIN vehiculos v ON v.id_vehiculo = i.id_vehiculo AND v.tenant_id = @tenant
            JOIN clientes c ON c.id_cliente = i.id_cliente AND c.tenant_id = @tenant
            WHERE i.tenant_id = @tenant AND i.resolved_at IS NULL
            ORDER BY i.created_at;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);

        using var reader = cmd.ExecuteReader();
        var result = new List<VehicleStatusInquiryRow>();
        while (reader.Read())
        {
            result.Add(new VehicleStatusInquiryRow(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetDateTime(6)));
        }
        return result;
    }

    public virtual bool Resolve(int tenantId, int id)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            UPDATE vehicle_status_inquiries
            SET resolved_at = NOW()
            WHERE id_inquiry = @id AND tenant_id = @tenant AND resolved_at IS NULL;", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        return cmd.ExecuteNonQuery() > 0;
    }
}

using BoxService_BackEnd.Database;
using BoxService_BackEnd.Services;
using Npgsql;

namespace BoxService_BackEnd.Repositories;

public sealed record ServiceReminder(
    int ServiceId, int VehicleId, int ClientId, string ClientName, string Phone,
    string Vehicle, string Plate, DateOnly NextDate, int DaysRemaining, string Status,
    DateTime? ContactedAt, string? ContactedBy, string? WhatsappUrl);

public sealed class ServiceReminderRepository
{
    // Orden estable: un solo service por vehículo; los nuevos reemplazan el aviso anterior.
    private const string Latest = @"
        WITH latest AS (
            SELECT DISTINCT ON (id_vehiculo) id_service, id_vehiculo, proxima_fecha
            FROM services WHERE tenant_id = @tenant
            ORDER BY id_vehiculo, fecha DESC, id_service DESC
        ) ";

    public List<ServiceReminder> List(int tenantId, DateOnly today)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(Latest + @"
            SELECT s.id_service, v.id_vehiculo, c.id_cliente, c.nombre,
                   COALESCE(c.telefono, ''), v.marca || ' ' || v.modelo, v.patente,
                   s.proxima_fecha, r.contacted_at, r.contacted_by, t.nombre
            FROM latest s
            JOIN vehiculos v ON v.id_vehiculo = s.id_vehiculo AND v.tenant_id = @tenant
            JOIN clientes c ON c.id_cliente = v.id_cliente AND c.tenant_id = @tenant
            JOIN tenants t ON t.id_tenant = @tenant
            LEFT JOIN service_reminder_contacts r
                ON r.id_service = s.id_service AND r.tenant_id = @tenant
            WHERE s.proxima_fecha <= @limit AND v.activo AND c.activo AND t.activo
            ORDER BY s.proxima_fecha, s.id_service;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("limit", today.AddDays(ServiceReminderRules.AdvanceDays));
        using var reader = cmd.ExecuteReader();
        var result = new List<ServiceReminder>();
        while (reader.Read())
        {
            var due = DateOnly.FromDateTime(reader.GetDateTime(7));
            result.Add(new(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
                due, due.DayNumber - today.DayNumber, ServiceReminderRules.Status(due, today),
                reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                ServiceReminderRules.WhatsAppUrl(reader.GetString(4), reader.GetString(3),
                    reader.GetString(10), reader.GetString(5), due)));
        }
        return result;
    }

    public bool Contact(int tenantId, int serviceId, string username, DateOnly today)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(Latest + @"
            INSERT INTO service_reminder_contacts (id_service, tenant_id, contacted_by)
            SELECT s.id_service, @tenant, @username FROM latest s
            JOIN vehiculos v ON v.id_vehiculo = s.id_vehiculo AND v.tenant_id = @tenant
            JOIN clientes c ON c.id_cliente = v.id_cliente AND c.tenant_id = @tenant
            JOIN tenants t ON t.id_tenant = @tenant
            WHERE s.id_service = @id AND s.proxima_fecha <= @limit
                AND v.activo AND c.activo AND t.activo
            ON CONFLICT (id_service) DO UPDATE
                SET contacted_at = service_reminder_contacts.contacted_at
                WHERE service_reminder_contacts.tenant_id = @tenant
            RETURNING id_service;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("id", serviceId);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("limit", today.AddDays(ServiceReminderRules.AdvanceDays));
        return cmd.ExecuteScalar() is not null;
    }

    public bool UpdatePhone(int tenantId, int clientId, string phone)
    {
        using var conn = DatabaseConnection.GetConnection();
        using var cmd = new NpgsqlCommand(@"
            UPDATE clientes SET telefono = @phone
            WHERE id_cliente = @id AND tenant_id = @tenant AND activo;", conn);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("id", clientId);
        cmd.Parameters.AddWithValue("phone", phone);
        return cmd.ExecuteNonQuery() == 1;
    }
}

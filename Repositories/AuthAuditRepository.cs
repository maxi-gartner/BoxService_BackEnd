using BoxService_BackEnd.Database;
using Npgsql;

namespace BoxService_BackEnd.Repositories;

public sealed class AuthAuditRepository
{
    public void Record(
        string eventType,
        string outcome,
        int? actorUserId,
        int? targetUserId,
        int? tenantId,
        string? sourceIp,
        string? httpMethod = null,
        string? resourcePath = null)
    {
        using var connection = DatabaseConnection.GetConnection();
        using var command = new NpgsqlCommand(@"
            INSERT INTO auth_audit_events
                (event_type, outcome, actor_user_id, target_user_id, tenant_id, source_ip, http_method, resource_path)
            VALUES
                (@event_type, @outcome, @actor_user_id, @target_user_id, @tenant_id, @source_ip, @http_method, @resource_path);", connection);

        command.Parameters.AddWithValue("event_type", eventType);
        command.Parameters.AddWithValue("outcome", outcome);
        command.Parameters.AddWithValue("actor_user_id", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("target_user_id", (object?)targetUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("tenant_id", (object?)tenantId ?? DBNull.Value);
        command.Parameters.AddWithValue("source_ip", (object?)sourceIp ?? DBNull.Value);
        command.Parameters.AddWithValue("http_method", (object?)httpMethod ?? DBNull.Value);
        command.Parameters.AddWithValue("resource_path", (object?)resourcePath ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public List<AuthAuditEvent> GetRecent(int limit, int? targetUserId)
    {
        var result = new List<AuthAuditEvent>();
        using var connection = DatabaseConnection.GetConnection();
        using var command = new NpgsqlCommand(@"
            SELECT event_type, outcome, actor_user_id, target_user_id, tenant_id, source_ip, http_method, resource_path, created_at
            FROM auth_audit_events
            WHERE @target_user_id::int IS NULL OR target_user_id = @target_user_id
            ORDER BY created_at DESC, id_evento DESC
            LIMIT @limit;", connection);
        command.Parameters.AddWithValue("target_user_id", (object?)targetUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var createdAt = DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("created_at")),
                DateTimeKind.Utc);
            result.Add(new AuthAuditEvent(
                reader.GetString(reader.GetOrdinal("event_type")),
                reader.GetString(reader.GetOrdinal("outcome")),
                reader.IsDBNull(reader.GetOrdinal("actor_user_id"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("actor_user_id")),
                reader.IsDBNull(reader.GetOrdinal("target_user_id"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("target_user_id")),
                reader.IsDBNull(reader.GetOrdinal("tenant_id"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("tenant_id")),
                reader.IsDBNull(reader.GetOrdinal("source_ip"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("source_ip")),
                reader.IsDBNull(reader.GetOrdinal("http_method"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("http_method")),
                reader.IsDBNull(reader.GetOrdinal("resource_path"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("resource_path")),
                new DateTimeOffset(createdAt)));
        }

        return result;
    }
}

public sealed record AuthAuditEvent(
    string EventType,
    string Outcome,
    int? ActorUserId,
    int? TargetUserId,
    int? TenantId,
    string? SourceIp,
    string? HttpMethod,
    string? ResourcePath,
    DateTimeOffset CreatedAt);

using BoxService_BackEnd.Database;
using Npgsql;

namespace BoxService_BackEnd.Repositories;

public sealed class PasswordResetRepository
{
    public void CreateToken(int userId, string tokenHash, DateTimeOffset expiresAt)
    {
        using var connection = DatabaseConnection.GetConnection();
        using var transaction = connection.BeginTransaction();

        using (var cleanup = new NpgsqlCommand(@"
            DELETE FROM password_reset_tokens
            WHERE expires_at < NOW() - INTERVAL '1 day'
               OR consumed_at < NOW() - INTERVAL '1 day';", connection, transaction))
        {
            cleanup.ExecuteNonQuery();
        }

        using (var revoke = new NpgsqlCommand(@"
            UPDATE password_reset_tokens
            SET consumed_at = NOW()
            WHERE user_id = @user_id AND consumed_at IS NULL;", connection, transaction))
        {
            revoke.Parameters.AddWithValue("user_id", userId);
            revoke.ExecuteNonQuery();
        }

        using (var insert = new NpgsqlCommand(@"
            INSERT INTO password_reset_tokens (user_id, token_hash, expires_at)
            VALUES (@user_id, @token_hash, @expires_at);", connection, transaction))
        {
            insert.Parameters.AddWithValue("user_id", userId);
            insert.Parameters.AddWithValue("token_hash", tokenHash);
            insert.Parameters.AddWithValue("expires_at", expiresAt);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void RevokeToken(string tokenHash)
    {
        using var connection = DatabaseConnection.GetConnection();
        using var command = new NpgsqlCommand(@"
            UPDATE password_reset_tokens
            SET consumed_at = NOW()
            WHERE token_hash = @token_hash AND consumed_at IS NULL;", connection);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.ExecuteNonQuery();
    }

    public PasswordResetCompletion? CompleteReset(string tokenHash, string passwordHash)
    {
        using var connection = DatabaseConnection.GetConnection();
        using var command = new NpgsqlCommand(@"
            WITH consumed AS (
                UPDATE password_reset_tokens
                SET consumed_at = NOW()
                WHERE token_hash = @token_hash
                  AND consumed_at IS NULL
                  AND expires_at > NOW()
                RETURNING user_id
            )
            UPDATE usuarios AS u
            SET password_hash = @password_hash
            FROM consumed
            WHERE u.id_usuario = consumed.user_id
              AND u.activo = TRUE
            RETURNING u.id_usuario, u.id_tenant;", connection);

        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("password_hash", passwordHash);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new PasswordResetCompletion(
            reader.GetInt32(reader.GetOrdinal("id_usuario")),
            reader.IsDBNull(reader.GetOrdinal("id_tenant"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("id_tenant")));
    }
}

public sealed record PasswordResetCompletion(int UserId, int? TenantId);

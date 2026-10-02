using Npgsql;
using BoxService_BackEnd.Database;

namespace BoxService_BackEnd.Repositories
{
    public class PortalAccessRepository
    {
        // Genera (o renueva) una invitación. Si el cliente ya estaba
        // vinculado a una cuenta de Google, reinvitar la resetea a
        // propósito — es una acción explícita del taller, sirve para
        // el caso "el cliente perdió acceso a su Google viejo".
        public (string InviteToken, DateTime ExpiresAt) UpsertInvite(int clientId)
        {
            var token = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddDays(7);

            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO client_portal_access (id_cliente, invite_token, invite_expires_at, google_sub, linked_at)
                VALUES (@id, @token, @expires, NULL, NULL)
                ON CONFLICT (id_cliente) DO UPDATE
                    SET invite_token = EXCLUDED.invite_token,
                        invite_expires_at = EXCLUDED.invite_expires_at,
                        google_sub = NULL,
                        linked_at = NULL;", conn);

            cmd.Parameters.AddWithValue("id", clientId);
            cmd.Parameters.AddWithValue("token", token);
            cmd.Parameters.AddWithValue("expires", expiresAt);
            cmd.ExecuteNonQuery();

            return (token, expiresAt);
        }

        // Login de vuelta: ya sabemos a qué cliente corresponde esta
        // cuenta de Google, sin necesitar el link de invitación de nuevo.
        public int? GetClientIdByGoogleSub(string googleSub)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT id_cliente FROM client_portal_access WHERE google_sub = @sub;", conn);
            cmd.Parameters.AddWithValue("sub", googleSub);

            var result = cmd.ExecuteScalar();
            return result is null ? null : (int)result;
        }

        // Primera activación: consume el token de invitación y vincula la
        // cuenta de Google, en una sola operación atómica (evita que dos
        // intentos casi simultáneos con el mismo link lo usen dos veces).
        public int? TryConsumeInvite(string inviteToken, string googleSub)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(@"
                UPDATE client_portal_access
                SET google_sub = @sub, linked_at = NOW(), invite_token = NULL
                WHERE invite_token = @token
                  AND invite_expires_at > NOW()
                  AND linked_at IS NULL
                RETURNING id_cliente;", conn);

            cmd.Parameters.AddWithValue("token", inviteToken);
            cmd.Parameters.AddWithValue("sub", googleSub);

            var result = cmd.ExecuteScalar();
            return result is null ? null : (int)result;
        }
    }
}

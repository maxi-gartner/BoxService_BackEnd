-- Registra eventos de autenticación sin guardar contraseñas, tokens ni
-- direcciones de correo ingresadas en solicitudes no asociadas a usuarios.

ALTER TABLE usuarios
    ADD COLUMN IF NOT EXISTS auth_version INT NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS idx_usuarios_email_normalized
    ON usuarios(LOWER(BTRIM(email))) WHERE email IS NOT NULL;

CREATE TABLE IF NOT EXISTS auth_audit_events (
    id_evento       BIGSERIAL PRIMARY KEY,
    event_type      VARCHAR(50) NOT NULL,
    outcome         VARCHAR(50) NOT NULL,
    actor_user_id   INT REFERENCES usuarios(id_usuario) ON DELETE SET NULL,
    target_user_id  INT REFERENCES usuarios(id_usuario) ON DELETE SET NULL,
    tenant_id       INT REFERENCES tenants(id_tenant) ON DELETE SET NULL,
    source_ip       VARCHAR(45),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_auth_audit_events_created_at
    ON auth_audit_events(created_at);
CREATE INDEX IF NOT EXISTS idx_auth_audit_events_target_user
    ON auth_audit_events(target_user_id, created_at);

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id_token    BIGSERIAL PRIMARY KEY,
    user_id     INT NOT NULL REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
    token_hash  CHAR(64) NOT NULL UNIQUE,
    expires_at  TIMESTAMPTZ NOT NULL,
    consumed_at TIMESTAMPTZ,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_user
    ON password_reset_tokens(user_id);
CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_expires
    ON password_reset_tokens(expires_at);

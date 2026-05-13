-- 001_crear_clientes.sql
CREATE TABLE IF NOT EXISTS clientes (
    id_cliente  SERIAL PRIMARY KEY,
    nombre      VARCHAR(100) NOT NULL,
    telefono    VARCHAR(20),
    email       VARCHAR(100),
    activo      BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMP    NOT NULL DEFAULT NOW()
);
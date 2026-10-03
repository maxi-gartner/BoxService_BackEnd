-- 011_crear_client_portal_access.sql
-- Portal del cliente: el dueño del auto entra con Google para ver el
-- estado de su vehículo. No se vincula automático por email — el taller
-- manda una invitación puntual (por WhatsApp, usando el teléfono que ya
-- está en clientes.telefono) con un link de un solo uso; recién ahí la
-- cuenta de Google del cliente queda atada a su registro. Una fila por
-- cliente que alguna vez fue invitado.

CREATE TABLE IF NOT EXISTS client_portal_access (
    id_cliente          INT                      PRIMARY KEY REFERENCES clientes(id_cliente),
    google_sub          VARCHAR(255)             UNIQUE,
    invite_token        VARCHAR(100)             UNIQUE,
    invite_expires_at   TIMESTAMPTZ,
    linked_at           TIMESTAMPTZ,
    created_at          TIMESTAMPTZ              NOT NULL DEFAULT NOW()
);

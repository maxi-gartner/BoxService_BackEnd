-- 008_crear_catalogo_servicios.sql
-- Catálogo de ítems reutilizables con precio (mano de obra, revisiones frecuentes,
-- checkeos, etc.) para no tipear tipo+descripción+precio a mano en cada presupuesto.
-- El precio acá es solo un valor de partida: se puede editar al presupuestar.

CREATE TABLE IF NOT EXISTS catalogo_servicios (
    id_catalogo SERIAL PRIMARY KEY,
    nombre      VARCHAR(150)  NOT NULL,
    tipo        VARCHAR(20)   NOT NULL DEFAULT 'labor', -- labor | part
    precio      DECIMAL(10,2) NOT NULL,
    created_at  TIMESTAMP     NOT NULL DEFAULT NOW()
);

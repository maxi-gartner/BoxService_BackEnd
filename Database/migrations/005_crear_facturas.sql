-- 005_crear_facturas.sql
CREATE TABLE IF NOT EXISTS facturas (
    id_factura      SERIAL PRIMARY KEY,
    numero          VARCHAR(10)   NOT NULL UNIQUE,
    fecha           DATE          NOT NULL DEFAULT CURRENT_DATE,
    total           DECIMAL(10,2) NOT NULL,
    estado          VARCHAR(20)   NOT NULL DEFAULT 'issued',
    id_service      INT           NOT NULL REFERENCES services(id_service),
    id_presupuesto  INT           REFERENCES presupuestos(id_presupuesto),
    created_at      TIMESTAMP     NOT NULL DEFAULT NOW()
);
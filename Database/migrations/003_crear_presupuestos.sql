-- 003_crear_presupuestos.sql
CREATE TABLE IF NOT EXISTS presupuestos (
    id_presupuesto  SERIAL PRIMARY KEY,
    numero          VARCHAR(10)   NOT NULL UNIQUE,
    fecha           DATE          NOT NULL DEFAULT CURRENT_DATE,
    estado          VARCHAR(20)   NOT NULL DEFAULT 'draft',
    observaciones   TEXT,
    id_vehiculo     INT           NOT NULL REFERENCES vehiculos(id_vehiculo),
    id_service      INT,
    created_at      TIMESTAMP     NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS detalle_presupuesto (
    id_detalle      SERIAL PRIMARY KEY,
    id_presupuesto  INT           NOT NULL REFERENCES presupuestos(id_presupuesto),
    tipo            VARCHAR(20)   NOT NULL,
    descripcion     VARCHAR(200)  NOT NULL,
    cantidad        DECIMAL(10,2) NOT NULL DEFAULT 1,
    precio_unitario DECIMAL(10,2) NOT NULL,
    subtotal        DECIMAL(10,2) NOT NULL
);
-- 004_crear_services.sql
CREATE TABLE IF NOT EXISTS services (
    id_service      SERIAL PRIMARY KEY,
    fecha           DATE          NOT NULL DEFAULT CURRENT_DATE,
    kilometraje     INT           NOT NULL,
    tipo_service    VARCHAR(100)  NOT NULL,
    observaciones   TEXT,
    proximo_km      INT,
    proxima_fecha   DATE,
    id_vehiculo     INT           NOT NULL REFERENCES vehiculos(id_vehiculo),
    id_presupuesto  INT           REFERENCES presupuestos(id_presupuesto),
    created_at      TIMESTAMP     NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS detalle_service (
    id_detalle   SERIAL PRIMARY KEY,
    id_service   INT           NOT NULL REFERENCES services(id_service),
    descripcion  VARCHAR(200)  NOT NULL,
    realizado    BOOLEAN       NOT NULL DEFAULT TRUE
);

-- FK que cierra el ciclo presupuesto → service
-- Postgres no soporta "ADD CONSTRAINT IF NOT EXISTS" (sí existe para ADD
-- COLUMN, pero no para constraints) — sin este chequeo manual, re-correr las
-- migraciones sobre una base que ya las tiene aplicadas (ej. "dotnet run --
-- migrate" ejecutado más de una vez) fallaba acá, aunque el resultado final
-- fuera el mismo.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_presupuesto_service'
    ) THEN
        ALTER TABLE presupuestos
            ADD CONSTRAINT fk_presupuesto_service
            FOREIGN KEY (id_service) REFERENCES services(id_service);
    END IF;
END $$;
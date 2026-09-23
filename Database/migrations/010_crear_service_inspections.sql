-- 010_crear_service_inspections.sql
-- Documenta 3 tablas que ya existían en la base real (Supabase) sin
-- migración: alguien las creó a mano en el editor SQL para una feature de
-- "inspección de vehículo con fotos" (checklist + fotos por service) que
-- todavía no se conectó a ningún código (no hay Model/DTO/Repository/
-- endpoint que las use, ni nada en el frontend). Esta migración solo
-- versiona el schema tal como está hoy — no agrega lógica de negocio.
-- Las 3 tablas están vacías en producción, así que no hay riesgo de datos.

CREATE TABLE IF NOT EXISTS service_inspections (
    id_inspection         SERIAL                   PRIMARY KEY,
    id_service            INT                      NOT NULL UNIQUE REFERENCES services(id_service),
    fuel_level             SMALLINT,
    general_observations   TEXT,
    inspected_by            VARCHAR(100)             NOT NULL,
    inspected_at            TIMESTAMPTZ              NOT NULL DEFAULT NOW(),
    CONSTRAINT service_inspections_fuel_level_check CHECK (fuel_level >= 0 AND fuel_level <= 100)
);

CREATE TABLE IF NOT EXISTS service_inspection_items (
    id_item      SERIAL                   PRIMARY KEY,
    id_inspection INT                      NOT NULL REFERENCES service_inspections(id_inspection),
    item_code     VARCHAR(50)              NOT NULL,
    item_name     VARCHAR(100)             NOT NULL,
    status        VARCHAR(20)              NOT NULL,
    observation   VARCHAR(500),
    CONSTRAINT service_inspection_items_status_check CHECK (status IN ('ok', 'damaged', 'not_checked')),
    CONSTRAINT service_inspection_items_id_inspection_item_code_key UNIQUE (id_inspection, item_code)
);

CREATE INDEX IF NOT EXISTS idx_service_inspection_items_inspection
    ON service_inspection_items (id_inspection);

CREATE TABLE IF NOT EXISTS service_inspection_photos (
    id_photo             SERIAL                   PRIMARY KEY,
    id_inspection         INT                      NOT NULL REFERENCES service_inspections(id_inspection),
    storage_path           TEXT                     NOT NULL UNIQUE,
    photo_type             VARCHAR(30)              NOT NULL,
    description             VARCHAR(500),
    original_file_name      VARCHAR(255)             NOT NULL,
    content_type            VARCHAR(100)             NOT NULL,
    created_at              TIMESTAMPTZ              NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_service_inspection_photos_inspection
    ON service_inspection_photos (id_inspection);

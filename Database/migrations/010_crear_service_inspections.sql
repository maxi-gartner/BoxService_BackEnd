-- Registra el estado del vehículo al ingresar para un service determinado.

CREATE TABLE IF NOT EXISTS service_inspections (
    id_inspection        SERIAL PRIMARY KEY,
    id_service           INT NOT NULL UNIQUE,
    fuel_level           SMALLINT,
    general_observations TEXT,
    inspected_by         VARCHAR(100) NOT NULL,
    inspected_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_service_inspections_service
        FOREIGN KEY (id_service)
        REFERENCES services(id_service)
        ON DELETE CASCADE,

    CONSTRAINT chk_service_inspections_fuel_level
        CHECK (fuel_level BETWEEN 0 AND 100)
);

CREATE TABLE IF NOT EXISTS service_inspection_items (
    id_item       SERIAL PRIMARY KEY,
    id_inspection INT NOT NULL,
    item_code     VARCHAR(50) NOT NULL,
    item_name     VARCHAR(100) NOT NULL,
    status        VARCHAR(20) NOT NULL,
    observation   VARCHAR(500),

    CONSTRAINT fk_service_inspection_items_inspection
        FOREIGN KEY (id_inspection)
        REFERENCES service_inspections(id_inspection)
        ON DELETE CASCADE,

    CONSTRAINT chk_service_inspection_items_status
        CHECK (status IN ('ok', 'damaged', 'not_checked')),

    CONSTRAINT uq_service_inspection_items_code
        UNIQUE (id_inspection, item_code)
);

CREATE TABLE IF NOT EXISTS service_inspection_photos (
    id_photo           SERIAL PRIMARY KEY,
    id_inspection      INT NOT NULL,
    storage_path       TEXT NOT NULL UNIQUE,
    photo_type         VARCHAR(30) NOT NULL,
    description        VARCHAR(500),
    original_file_name VARCHAR(255) NOT NULL,
    content_type       VARCHAR(100) NOT NULL,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_service_inspection_photos_inspection
        FOREIGN KEY (id_inspection)
        REFERENCES service_inspections(id_inspection)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_service_inspection_items_inspection
    ON service_inspection_items(id_inspection);

CREATE INDEX IF NOT EXISTS idx_service_inspection_photos_inspection
    ON service_inspection_photos(id_inspection);

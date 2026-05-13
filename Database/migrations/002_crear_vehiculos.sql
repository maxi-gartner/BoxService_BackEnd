-- 002_crear_vehiculos.sql
CREATE TABLE IF NOT EXISTS vehiculos (
    id_vehiculo        SERIAL PRIMARY KEY,
    patente            VARCHAR(10)  NOT NULL UNIQUE,
    marca              VARCHAR(50)  NOT NULL,
    modelo             VARCHAR(50)  NOT NULL,
    anio               INT          NOT NULL,
    kilometraje_actual INT          NOT NULL DEFAULT 0,
    id_cliente         INT          NOT NULL REFERENCES clientes(id_cliente),
    activo             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at         TIMESTAMP    NOT NULL DEFAULT NOW()
);
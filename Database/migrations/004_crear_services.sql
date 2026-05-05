-- Migración 004: Crear tabla services
CREATE TABLE services (
    service_id SERIAL PRIMARY KEY,
    presupuesto_id INT NOT NULL REFERENCES presupuestos(presupuesto_id),
    descripcion TEXT NOT NULL,
    precio NUMERIC(12,2) NOT NULL DEFAULT 0,
    realizado BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_servicio DATE,
    creado_en TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

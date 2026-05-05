-- Migración 005: Crear tabla facturas
CREATE TABLE facturas (
    factura_id SERIAL PRIMARY KEY,
    presupuesto_id INT NOT NULL REFERENCES presupuestos(presupuesto_id),
    fecha_factura DATE NOT NULL DEFAULT CURRENT_DATE,
    total NUMERIC(12,2) NOT NULL,
    pagada BOOLEAN NOT NULL DEFAULT FALSE,
    creado_en TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

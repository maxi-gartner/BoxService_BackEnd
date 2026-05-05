-- Migración 003: Crear tabla presupuestos
CREATE TABLE presupuestos (
    presupuesto_id SERIAL PRIMARY KEY,
    cliente_id INT NOT NULL REFERENCES clientes(cliente_id),
    vehiculo_id INT NOT NULL REFERENCES vehiculos(vehiculo_id),
    fecha_presupuesto DATE NOT NULL DEFAULT CURRENT_DATE,
    total NUMERIC(12,2) NOT NULL DEFAULT 0,
    estado VARCHAR(50) NOT NULL DEFAULT 'pendiente',
    creado_en TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

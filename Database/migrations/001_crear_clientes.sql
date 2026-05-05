-- Migración 001: Crear tabla clientes
CREATE TABLE clientes (
    cliente_id SERIAL PRIMARY KEY,
    nombre VARCHAR(200) NOT NULL,
    email VARCHAR(200) UNIQUE NOT NULL,
    telefono VARCHAR(50),
    direccion TEXT,
    creado_en TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

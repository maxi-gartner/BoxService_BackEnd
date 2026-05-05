-- Migración 002: Crear tabla vehiculos
CREATE TABLE vehiculos (
    vehiculo_id SERIAL PRIMARY KEY,
    cliente_id INT NOT NULL REFERENCES clientes(cliente_id),
    marca VARCHAR(100) NOT NULL,
    modelo VARCHAR(100) NOT NULL,
    ano INT,
    placa VARCHAR(50) UNIQUE NOT NULL,
    creado_en TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

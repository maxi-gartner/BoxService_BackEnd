-- The frontend contract allows an unknown vehicle year (null).
ALTER TABLE vehiculos ALTER COLUMN anio DROP NOT NULL;

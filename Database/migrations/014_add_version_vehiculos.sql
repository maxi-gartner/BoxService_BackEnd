-- 014_add_version_vehiculos.sql
-- Versión/línea del vehículo (ej. "XEI 1.8", "GLS") — opcional, texto
-- libre; cuando hay credenciales de Mercado Libre configuradas el
-- formulario sugiere valores reales según marca/modelo (ver
-- MercadoLibreCatalogService), pero el campo nunca lo exige.

ALTER TABLE vehiculos ADD COLUMN IF NOT EXISTS version VARCHAR(100);

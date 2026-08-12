-- 007_add_numero_sequences.sql
-- Numeración atómica para presupuestos y facturas.
-- Antes el número se calculaba leyendo el último registro y sumando 1 en memoria,
-- lo que generaba números repetidos si dos requests llegaban al mismo tiempo
-- (falla el UNIQUE de "numero"). Con una SEQUENCE, Postgres garantiza que cada
-- llamada a nextval() devuelve un valor distinto aunque lleguen en simultáneo.

CREATE SEQUENCE IF NOT EXISTS presupuestos_numero_seq;
CREATE SEQUENCE IF NOT EXISTS facturas_numero_seq;

-- Si ya había presupuestos/facturas cargados antes de esta migración,
-- arrancamos la secuencia después del número más alto existente
-- para no generar un "numero" que choque con uno ya guardado.
SELECT setval(
    'presupuestos_numero_seq',
    COALESCE((SELECT MAX(CAST(SPLIT_PART(numero, '-', 2) AS INT)) FROM presupuestos), 0) + 1,
    false
);

SELECT setval(
    'facturas_numero_seq',
    COALESCE((SELECT MAX(CAST(SPLIT_PART(numero, '-', 2) AS INT)) FROM facturas), 0) + 1,
    false
);

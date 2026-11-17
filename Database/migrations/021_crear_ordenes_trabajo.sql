-- 021_crear_ordenes_trabajo.sql
CREATE TABLE IF NOT EXISTS public.ordenes_trabajo (
    id_orden SERIAL PRIMARY KEY,
    tenant_id INT NOT NULL
        REFERENCES public.tenants(id_tenant),
    id_vehiculo INT NOT NULL
        REFERENCES public.vehiculos(id_vehiculo) ON DELETE CASCADE,
    estado TEXT NOT NULL DEFAULT 'en_espera',
    notas TEXT,
    recibido_por TEXT,
    entregado_por TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    finished_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ
);

-- Soporta el chequeo atómico de "una sola orden abierta por vehículo"
-- (ver WorkOrderRepository.Create) y el listado del tablero por tenant.
CREATE INDEX IF NOT EXISTS idx_ordenes_trabajo_vehiculo_abierta
    ON public.ordenes_trabajo(id_vehiculo) WHERE estado <> 'entregado';

CREATE INDEX IF NOT EXISTS idx_ordenes_trabajo_tenant_abiertas
    ON public.ordenes_trabajo(tenant_id) WHERE estado <> 'entregado';

ALTER TABLE public.ordenes_trabajo ENABLE ROW LEVEL SECURITY;

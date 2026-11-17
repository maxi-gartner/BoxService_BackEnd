-- 020_crear_vehicle_status_inquiries.sql
CREATE TABLE IF NOT EXISTS public.vehicle_status_inquiries (
    id_inquiry SERIAL PRIMARY KEY,
    tenant_id INT NOT NULL
        REFERENCES public.tenants(id_tenant),
    id_vehiculo INT NOT NULL
        REFERENCES public.vehiculos(id_vehiculo) ON DELETE CASCADE,
    id_cliente INT NOT NULL
        REFERENCES public.clientes(id_cliente) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMPTZ
);

-- Soporta tanto el chequeo de rate-limit (1 por vehículo por hora) como
-- el listado de pendientes del taller.
CREATE INDEX IF NOT EXISTS idx_vehicle_status_inquiries_vehicle_created
    ON public.vehicle_status_inquiries(id_vehiculo, created_at);

CREATE INDEX IF NOT EXISTS idx_vehicle_status_inquiries_tenant_pending
    ON public.vehicle_status_inquiries(tenant_id) WHERE resolved_at IS NULL;

ALTER TABLE public.vehicle_status_inquiries ENABLE ROW LEVEL SECURITY;

-- 016_fecha_cobro_facturas.sql
-- No se completan fechas antiguas porque se desconoce cuándo se cobraron.

ALTER TABLE public.facturas
    ADD COLUMN IF NOT EXISTS paid_at TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS idx_facturas_tenant_paid_at
    ON public.facturas(tenant_id, paid_at)
    WHERE estado = 'paid';

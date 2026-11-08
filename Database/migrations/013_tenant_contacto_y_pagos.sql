-- 013_tenant_contacto_y_pagos.sql
-- Datos de contacto del taller (para que superadmin pueda comunicarse
-- con el dueño ante un problema) y control mensual de pagos — por ahora
-- solo pagado/pendiente por mes, no una contabilidad completa.

ALTER TABLE tenants ADD COLUMN IF NOT EXISTS telefono VARCHAR(30);
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS email VARCHAR(150);

CREATE TABLE IF NOT EXISTS tenant_payments (
    id_pago     SERIAL PRIMARY KEY,
    id_tenant   INT NOT NULL REFERENCES tenants(id_tenant),
    monto       DECIMAL(10,2) NOT NULL,
    periodo     VARCHAR(7) NOT NULL, -- 'YYYY-MM', el mes que cubre el pago
    fecha_pago  DATE NOT NULL DEFAULT CURRENT_DATE,
    notas       VARCHAR(255),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tenant_payments_tenant ON tenant_payments(id_tenant);

-- Un registro por taller y por mes — si hay que corregir un pago ya
-- cargado del mismo mes, se actualiza (ver TenantPaymentRepository.Record,
-- hace upsert), no se duplica.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'uq_tenant_payments_tenant_periodo'
    ) THEN
        ALTER TABLE tenant_payments
            ADD CONSTRAINT uq_tenant_payments_tenant_periodo UNIQUE (id_tenant, periodo);
    END IF;
END $$;

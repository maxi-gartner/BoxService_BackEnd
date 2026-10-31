-- Ejecutar manualmente en Supabase antes de usar Recordatorios.
BEGIN;
CREATE TABLE public.service_reminder_contacts (
    id_service INT PRIMARY KEY REFERENCES public.services(id_service) ON DELETE CASCADE,
    tenant_id INT NOT NULL REFERENCES public.tenants(id_tenant),
    contacted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    contacted_by VARCHAR(150) NOT NULL
);
CREATE INDEX idx_service_reminder_contacts_tenant
    ON public.service_reminder_contacts(tenant_id);
ALTER TABLE public.service_reminder_contacts ENABLE ROW LEVEL SECURITY;
COMMIT;

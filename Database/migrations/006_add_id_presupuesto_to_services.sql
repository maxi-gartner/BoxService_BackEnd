-- 006_add_id_presupuesto_to_services.sql
ALTER TABLE services
    ADD COLUMN IF NOT EXISTS id_presupuesto INT REFERENCES presupuestos(id_presupuesto);

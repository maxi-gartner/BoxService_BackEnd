-- 012_crear_tenants_usuarios_sucursales.sql
-- Multi-tenant real: tenants (talleres), usuarios reales (reemplaza la
-- lista fija de Jwt:Users en appsettings.json), sucursales, y tenant_id
-- en cada tabla de negocio. tenant_id queda NULLABLE acá a propósito —
-- hay que vaciar y resembrar los datos existentes (no tienen dueño)
-- antes de poder exigirlo NOT NULL, eso es un paso aparte, manual,
-- fuera de esta migración (ver docs/MULTI_TENANT.md).

CREATE TABLE IF NOT EXISTS tenants (
    id_tenant   SERIAL PRIMARY KEY,
    nombre      VARCHAR(150) NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sucursales (
    id_sucursal  SERIAL PRIMARY KEY,
    id_tenant    INT          NOT NULL REFERENCES tenants(id_tenant),
    nombre       VARCHAR(150) NOT NULL,
    direccion    VARCHAR(255),
    activo       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- id_tenant NULL = superadmin (no pertenece a ningún taller).
CREATE TABLE IF NOT EXISTS usuarios (
    id_usuario     SERIAL PRIMARY KEY,
    id_tenant      INT REFERENCES tenants(id_tenant),
    username       VARCHAR(50)  NOT NULL UNIQUE,
    password_hash  VARCHAR(255) NOT NULL,
    nombre         VARCHAR(150) NOT NULL,
    email          VARCHAR(150),
    role           VARCHAR(20)  NOT NULL, -- superadmin | dueno | empleado
    activo         BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

ALTER TABLE clientes             ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);
ALTER TABLE vehiculos            ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);
ALTER TABLE presupuestos         ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);
ALTER TABLE services             ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);
ALTER TABLE facturas             ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);
ALTER TABLE catalogo_servicios   ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);
ALTER TABLE client_portal_access ADD COLUMN IF NOT EXISTS tenant_id INT REFERENCES tenants(id_tenant);

-- "dónde se atendió el vehículo" — opcional, services es la única tabla
-- que lo necesita hoy (ver plan: sucursales no particiona el resto de
-- los datos todavía).
ALTER TABLE services ADD COLUMN IF NOT EXISTS sucursal_id INT REFERENCES sucursales(id_sucursal);

CREATE INDEX IF NOT EXISTS idx_clientes_tenant   ON clientes(tenant_id);
CREATE INDEX IF NOT EXISTS idx_vehiculos_tenant   ON vehiculos(tenant_id);
CREATE INDEX IF NOT EXISTS idx_presupuestos_tenant ON presupuestos(tenant_id);
CREATE INDEX IF NOT EXISTS idx_services_tenant    ON services(tenant_id);
CREATE INDEX IF NOT EXISTS idx_facturas_tenant    ON facturas(tenant_id);
CREATE INDEX IF NOT EXISTS idx_catalogo_tenant    ON catalogo_servicios(tenant_id);
CREATE INDEX IF NOT EXISTS idx_usuarios_tenant    ON usuarios(id_tenant);
CREATE INDEX IF NOT EXISTS idx_sucursales_tenant  ON sucursales(id_tenant);

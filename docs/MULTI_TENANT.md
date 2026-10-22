# Multi-tenant real

Hasta acá "multi-tenant" era una ficción de interfaz: no existía tabla
`tenants`, no existía tabla de usuarios (3 cuentas fijas en
`appsettings.json`), y ninguna tabla de negocio tenía `tenant_id` — el
`tenantId` que mandaba la API era un placeholder fijo (`"default"`).
Esto describe el modelo real, implementado en la migración
`012_crear_tenants_usuarios_sucursales.sql`.

## Modelo

- **`tenants`**: un taller (negocio). `nombre`, `activo`.
- **`sucursales`**: ubicaciones de un tenant (`id_tenant`, `nombre`,
  `direccion`). No particionan datos — un cliente/vehículo/presupuesto
  sigue siendo del tenant entero, no de una sucursal puntual. Lo único
  que registra sucursal es `services.sucursal_id` (opcional): en qué
  sucursal se atendió ese vehículo puntual.
- **`usuarios`**: reemplaza la lista fija de `Jwt:Users`. Cada fila es
  `id_tenant` (NULL solo para `superadmin`), `username`,
  `password_hash`, `nombre`, `email`, `role`.
- `tenant_id` en `clientes`, `vehiculos`, `presupuestos`, `services`,
  `facturas`, `catalogo_servicios`, `client_portal_access`.

## Roles

| Rol | Qué puede hacer |
|---|---|
| `superadmin` | Administra tenants: `GET/POST /tenants`, `PATCH /tenants/{id}` (activar/desactivar), `POST /tenants/{id}/users` (alta en un tenant puntual), `GET /users` (todos, con el nombre del tenant), `GET /admin/clients` (todos los clientes, con el nombre del tenant). **No** puede tocar `/clients`, `/vehicles`, `/budgets`, `/services`, `/invoices`, `/catalog`, `/sucursales` — no tiene tenant propio. |
| `dueno` | Todo lo operativo de su tenant (clientes, vehículos, presupuestos, services, facturas), más `/catalog` en escritura, `/sucursales` (CRUD) y `/users` (alta de empleados) — todo scoped a su propio tenant. |
| `empleado` | Todo lo operativo de su tenant, salvo escribir en `/catalog`, `/sucursales` y `/users` (eso es solo del dueño). |
| `customer` | Portal del cliente (`/portal/*`) — sin cambios, ver `docs/PORTAL.md`. No tiene tenant en el JWT; la autorización pasa por `client_portal_access` (google_sub → cliente), no por tenant. |

El JWT lleva `sub` (id de usuario), `name` (username), `role`, y
`tenantId` (ausente para `superadmin`). El middleware de `Program.cs`
aplica las reglas de arriba antes de que el endpoint se ejecute —
`HttpContext.TenantId()` (en `Api/HttpContextExtensions.cs`) asume que
el claim existe porque el middleware ya garantizó que solo `dueno`/
`empleado` llegan a las rutas que lo usan.

## Alta de un tenant nuevo

`superadmin` carga el negocio y los datos del dueño directo desde su
panel — no hay invitación por link para esto (si en algún momento hace
falta, es un cambio aparte). `POST /tenants` crea el tenant y su primer
usuario `dueno` en una sola transacción
(`TenantRepository.CreateWithOwner`).

## Fuera de esta pasada

- Partición de clientes/vehículos/presupuestos por sucursal — hoy solo
  `services.sucursal_id` existe.
- Stock/inventario de repuestos por sucursal — no existe el sistema de
  stock todavía, independientemente de multi-tenant.
- Alta de tenant por invitación (link + contraseña que define el
  dueño).

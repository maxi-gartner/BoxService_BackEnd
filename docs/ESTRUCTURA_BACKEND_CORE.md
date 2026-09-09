# Estructura propuesta para BoxService Backend ASP.NET Core

Este documento define la estructura base para migrar el backend viejo de `HttpListener` a ASP.NET Core + PostgreSQL.

## Objetivo

Tener una API REST mantenible, separada por capas, con respuestas JSON consistentes y acceso a datos centralizado.

## Carpetas

```txt
BoxService_BackEnd/
├── Api/                  # Minimal API endpoints, envelope y errores HTTP
├── Controllers/          # Controllers HttpListener legados, no registrados
├── Data/                 # Conexión PostgreSQL y configuración de persistencia
├── Database/             # Migraciones SQL existentes
├── DTOs/                 # Requests y responses expuestos por la API
├── Models/               # Entidades del dominio
├── Repositories/         # SQL y acceso a PostgreSQL
├── Services/             # Reglas de negocio y validaciones
├── docs/                 # Documentación técnica del backend
├── Program.cs            # Configuración de ASP.NET Core
└── appsettings.json      # Configuración local, no se sube al repo
```

## Flujo de request

```txt
Frontend
  -> Endpoint ASP.NET Core (Api/*Endpoints.cs)
  -> Service
  -> Repository
  -> PostgreSQL
```

El `Controller` no debe acceder directo a la base. El `Service` no debe ejecutar SQL.

## Estado actual de la migración

El proyecto arranca con ASP.NET Core y expone:

- `GET /`
- `GET /health` (público, usa `PostgresConnectionFactory` para verificar conexión real a PostgreSQL)
- `GET /clients` · `GET /clients/{id}` · `GET /clients/{id}/vehicles` · `POST /clients`
  (requieren `X-Api-Key` — primer módulo migrado de punta a punta)

Todo lo que no sea `/` o `/health` exige el header `X-Api-Key`, igual que en
el backend viejo (`Server.cs`) — se había perdido al migrar y se restauró
como middleware en `Program.cs`.

- `GET /vehicles` (filtro opcional `?plate=`), `GET /vehicles/{id}`,
  `GET /vehicles/{id}/history`, `POST /vehicles`: entrada ASP.NET Core en
  `Api/VehicleEndpoints.cs`, registrada desde `Program.cs`.
  El historial usa `ServiceDto` y conserva las consultas del servicio existente.

Todos los módulos todavía usan el tenant provisional de `ClientDto` y API key.
Esto no implementa aislamiento multi-tenant: queda pendiente integrar JWT y
filtros de tenant con el trabajo de autenticación.

Para activar los módulos en el frontend, agregar `vehicles`, `budgets`,
`services`, `invoices` y `catalog` a
`REAL_BACKEND_RESOURCES` en el proxy de Next.js después de integrar este cambio.

### Verificación de Vehículos

Con el backend iniciado, ejecutar `./scripts/Test-Vehicles.ps1` desde PowerShell.
Se puede indicar `-BaseUrl` y configurar `BOXSERVICE_API_KEY` para una API key
distinta de la de desarrollo. El script verifica envelope, autenticación,
validaciones, métodos no permitidos y consultas. No inserta ni elimina datos.
Si `/health` falla, omite las consultas y termina con error para no confundir
la validación HTTP con una prueba completa de integración.

Para probar escrituras, `./scripts/Test-VehicleCreation.ps1` crea un cliente
temporal, realiza el alta HTTP y elimina los registros propios al terminar.
Requiere PowerShell 7, el build Debug net8.0, `appsettings.json` local y acceso
a PostgreSQL. Ejecutarlo solo contra una base donde se permitan datos de prueba.
Verifica alta (`201`), persistencia, historial vacío, asociación al cliente,
patente duplicada e identificador de cliente inexistente (`400`).

Ambos scripts pasaron con la conexión por hotspot: 13 controles de lectura y
validación, más la prueba de alta y limpieza. La red de la facultad agotaba el
tiempo de conexión TCP a PostgreSQL; no fue necesario cambiar las credenciales.

### Módulos funcionales migrados

| Módulo | Rutas nuevas (sin prefijo /api) |
|---|---|
| Presupuestos | GET/POST `/budgets`, GET/PATCH `/budgets/{id}`, PUT `/budgets/{id}/service` |
| Services | GET/POST `/services`, GET `/services/{id}`, GET/POST `/services/{id}/details` |
| Facturas | GET/POST `/invoices`, GET/PATCH `/invoices/{id}` |
| Catálogo | GET/POST `/catalog`, PATCH/DELETE `/catalog/{id}` |

Cada grupo está en `Api/*Endpoints.cs` y usa los Services y Repositories
existentes. `Program.cs` registra los servicios y los endpoints. Los routers y
controllers HttpListener quedan como referencia; no reciben tráfico.

Presupuestos guarda encabezado y detalles en una transacción del Repository.
Services guarda el service y actualiza el kilometraje en una sola transacción,
sin disminuirlo cuando se carga un service histórico. Vincular un service pasa
el presupuesto a `completed`. Facturas calcula el total desde sus detalles de
presupuesto; sin presupuesto conserva el comportamiento actual de total cero.

### Migración SQL y prueba integral

Aplicar `Database/migrations/009_vehicle_year_nullable.sql` después de las
migraciones anteriores: permite omitir el año del vehículo, como requiere el
contrato. Es idempotente y no cambia los valores existentes. El script opcional
`./scripts/Apply-VehicleYearMigration.ps1` la ejecuta usando la configuración local.

Con el backend iniciado y una base habilitada para pruebas, ejecutar
`./scripts/Test-Modules.ps1`. Crea registros temporales y verifica catálogo,
presupuesto, rollback ante detalle inválido, aprobación, service, kilometraje,
historial, vinculación, factura, estados y errores JSON. Al finalizar elimina
los registros del cliente temporal y el ítem de catálogo de esa ejecución.
Las secuencias pueden tener saltos después de las pruebas; no se reinician.

Validación realizada: 62 comprobaciones HTTP del recorrido completo y sus
aserciones de persistencia pasaron contra PostgreSQL. La limpieza final terminó
correctamente. Build y verificación de whitespace de los archivos modificados
también pasaron.

Queda fuera de esta migración funcional: Auth/JWT, roles, aislamiento real por
tenant, activación de recursos en el proxy del frontend y despliegue.

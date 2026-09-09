# Estructura propuesta para BoxService Backend ASP.NET Core

Este documento define la estructura base para migrar el backend viejo de `HttpListener` a ASP.NET Core + PostgreSQL.

## Objetivo

Tener una API REST mantenible, separada por capas, con respuestas JSON consistentes y acceso a datos centralizado.

## Carpetas

```txt
BoxService_BackEnd/
├── Api/                  # Envelope, errores y helpers HTTP compartidos
├── Controllers/          # Endpoints ASP.NET Core por módulo
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
  -> Controller
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

Clientes y Vehículos todavía usan el tenant provisional de `ClientDto` y API key.
Esto no implementa aislamiento multi-tenant: queda pendiente integrar JWT y
filtros de tenant con el trabajo de autenticación.

Para activar Vehículos en el frontend, agregar `vehicles` a
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

Los módulos viejos (Presupuestos, Services, Facturas, Catálogo)
quedan temporalmente como referencia hasta migrarlos de a uno. Reciben
`GET`/`POST`/etc. reales cuando se los migra, siguiendo la receta que dejó
Clientes:

1. El `Service`/`Repository` viejo ya sirve tal cual — no reescribir lógica
   de negocio, solo agregarle una entrada HTTP nueva.
2. Si el `Repository` usa `Database.DatabaseConnection` (estático), no hace
   falta tocarlo: ya se configura una sola vez al arrancar (`Program.cs`).
3. Crear un DTO en `DTOs/` con la forma exacta que espera el frontend nuevo
   (`web/types/entities.ts` / `web/docs/API_CONTRACT.md`), incluyendo
   `TenantId = DTOs.ClientDto.PlaceholderTenantId` si la entidad no tiene
   tenant real todavía.
4. Registrar el `Repository`/`Service` en el DI container y mapear las
   rutas en `Program.cs` (`app.MapGet(...)`, `app.MapPost(...)`) devolviendo
   siempre `ApiEnvelope<object>.Ok(...)` / `.Fail(...)`.
5. En `BoxService_FrontEnd/web/app/api/proxy/[...path]/route.ts`, agregar
   el nombre del recurso a `REAL_BACKEND_RESOURCES` — es el único cambio
   que necesita el frontend para dejar de usar el mock en ese módulo.

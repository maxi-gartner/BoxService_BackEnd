# Infraestructura: ambientes, CI/CD y secretos

Backend en [Render](https://render.com) (Docker — Render no tiene runtime
nativo para .NET), frontend en [Vercel](https://vercel.com) — ver el
`DEPLOYMENT.md` de `BoxService_FrontEnd/web/`. Postgres en
[Supabase](https://supabase.com).

## Ambientes

Dos por ahora: **development** (rama `develop`) y **production** (rama
`main`). `staging` se agrega el día que haya usuarios reales y haga falta
un ensayo antes de cada release.

| Ambiente | Rama | Servicio Render | Proyecto Supabase |
|---|---|---|---|
| development | `develop` | `boxservice-backend-dev` | el mismo proyecto de Supabase que production |
| production | `main` | `boxservice-backend-prod` | el proyecto de Supabase actual |

**Un solo Supabase para los dos ambientes, a propósito** (decisión
explícita, no un descuido): simplifica la gestión mientras no hay
usuarios reales todavía — no hay que mantener 2 bases sincronizadas ni
duplicar datos de catálogo/config. `ConnectionStrings__DefaultConnection`
es **el mismo valor** en `boxservice-backend-dev` y en
`boxservice-backend-prod`.

Costo de esta simplificación, para tenerlo presente: probar algo en
`develop` escribe en la misma base que ve "production". Mientras
production no tenga clientes reales no es un problema — el día que los
haya, separar en 2 proyectos de Supabase es un cambio de una sola env var
por servicio (`ConnectionStrings__DefaultConnection`), nada de código.

## Dónde vive cada secreto

**Nunca en el repo.** Tres lugares, cada uno con su rol:

- **GitHub Actions**: ninguno. El workflow de CI (`.github/workflows/ci.yml`)
  solo compila y buildea la imagen de Docker como smoke test — no despliega
  y no toca ninguna base real, así que no necesita credenciales.
- **Render**: el runtime del backend. Cada servicio (dev/prod) tiene sus
  propias env vars, cargadas la primera vez que se crea el Blueprint
  (`render.yaml`, con `sync: false` en cada secreto — Render las pide una
  sola vez y no vuelven a aparecer en ningún archivo).
- No hace falta ningún cambio de código para esto: `WebApplication.CreateBuilder`
  ya incluye `AddEnvironmentVariables()`, y ASP.NET Core mapea `Seccion__Clave`
  a `Seccion:Clave` automáticamente — son los mismos nombres que ya usa
  `appsettings.json`, solo con `__` en vez de anidamiento JSON.

### Env vars que pide cada servicio de Render

| Env var | Equivale a (`appsettings.json`) | Ejemplo |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | connection string de Supabase — **la misma en dev y en prod**, ver más arriba |
| `Jwt__Key` | `Jwt:Key` | 32+ bytes al azar, **distinta en dev y en prod** |
| `Cors__AllowedOrigin` | (nuevo, no existe en local) | la URL del frontend de ese ambiente en Vercel |
| `Google__ClientId` | `Google:ClientId` | OAuth Client ID de Google Cloud Console — ver `docs/PORTAL.md` |
| `Portal__AppUrl` | `Portal:AppUrl` | la misma URL del frontend que `Cors__AllowedOrigin` |
| `Email__Host`, `Email__Port` | `Email:Host`, `Email:Port` | servidor SMTP y puerto del proveedor de correo |
| `Email__Username`, `Email__Password` | `Email:Username`, `Email:Password` | credenciales SMTP; nunca guardarlas en el repo |
| `Email__FromAddress`, `Email__FromName` | `Email:FromAddress`, `Email:FromName` | remitente verificado del email |
| `Email__EnableSsl` | `Email:EnableSsl` | `true` para SMTP con TLS |
| `PasswordReset__FrontendUrl` | `PasswordReset:FrontendUrl` | página del frontend que consume el token de recuperación |

`Jwt__Issuer`, `Jwt__Audience` y `Jwt__ExpiresMinutes` ya vienen con un
valor por defecto en `render.yaml` — no hace falta tocarlos salvo que
cambien a propósito.

Las cuentas de staff (superadmin/dueño/empleado) ya no viven en
secretos de Render — son filas reales en la tabla `usuarios` (ver
`docs/MULTI_TENANT.md`). No hace falta cargar nada nuevo acá cuando se
crea un tenant o un empleado, eso se hace desde el panel de
administración una vez desplegado.

**Supabase**: el connection string sale del dashboard de cada proyecto
(Settings → Database → Connection string, modo *Transaction pooler* como
ya usa el `appsettings.json` local).

La recuperación de contraseña envía un enlace de un solo uso, válido por
30 minutos. El backend guarda únicamente el hash del token y audita
solicitudes, resultados de login, cambios de contraseña y mutaciones de la
API (método y ruta, sin guardar cuerpos); no guarda contraseñas, tokens ni
los emails ingresados. El endpoint de auditoría
`GET /admin/auth-audit?limit=100&userId=<id>` requiere el JWT de
`superadmin` y se muestra en `/admin/auditoria`. Para habilitar el email
hay que cargar las variables SMTP y `PasswordReset__FrontendUrl` con la
URL del frontend más `/reset-password`. Las páginas públicas del frontend
solicitan el enlace y envían `token` y `newPassword` a los endpoints de
recuperación del backend.
Las solicitudes están limitadas a 5 por IP cada 15 minutos. Si el email
no se configura o falla el proveedor, la respuesta pública sigue siendo
genérica para no revelar si una cuenta existe; el error queda en logs y
auditoría.
El login tiene un límite adicional de 10 intentos por IP cada 5 minutos.
Al cambiar una contraseña se invalidan los JWT de staff previamente
emitidos; los usuarios deberán iniciar sesión de nuevo. Los JWT del portal
de clientes no se ven afectados.

## Desplegar

Push a `develop` o a `main` dispara el deploy automático en el servicio de
Render correspondiente (Render hace build del `Dockerfile` y lo levanta).
No hay ningún paso manual de deploy — es el mismo flujo de PR → merge que
ya se usa hoy.

## Aplicar migraciones en un ambiente desplegado

`dotnet run -- migrate` (ver `docs/ESTRUCTURA_BACKEND_CORE.md`) corre local,
apuntando a la base que digas con `ConnectionStrings__DefaultConnection`:

```bash
ConnectionStrings__DefaultConnection="<connection string de Supabase>" dotnet run -- migrate
```

En PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "<connection string de Supabase>"
dotnet run -- migrate
```

Es seguro correrlo más de una vez contra la misma base — todas las
migraciones son idempotentes.
Aplicá las migraciones antes de desplegar esta versión: el login y la
validación de JWT necesitan la columna `usuarios.auth_version` creada por
la migración `015_auth_auditoria_y_recuperacion.sql`; la auditoría de
mutaciones también requiere aplicar `016_ampliar_auditoria_api.sql`.

## CORS

`Cors:AllowedOrigin` reemplaza el `AllowAnyOrigin()` que había antes. Sin
setear (por ejemplo, en dev local sin esa env var) sigue permitiendo
cualquier origen — no rompe nada corriendo en `localhost`.

# Infraestructura: ambientes, CI/CD y secretos

Backend en [Render](https://render.com) (Docker — Render no tiene runtime
nativo para .NET), frontend en [Vercel](https://vercel.com) — ver el
`DEPLOYMENT.md` de `BoxService_FrontEnd/web/`. Postgres en
[Supabase](https://supabase.com), un proyecto por ambiente.

## Ambientes

Dos por ahora: **development** (rama `develop`) y **production** (rama
`main`). `staging` se agrega el día que haya usuarios reales y haga falta
un ensayo antes de cada release.

| Ambiente | Rama | Servicio Render | Proyecto Supabase |
|---|---|---|---|
| development | `develop` | `boxservice-backend-dev` | el proyecto actual (ya tiene datos de prueba del equipo) |
| production | `main` | `boxservice-backend-prod` | proyecto nuevo, vacío |

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
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | connection string de Supabase de ese ambiente |
| `Jwt__Key` | `Jwt:Key` | 32+ bytes al azar, **distinta en dev y en prod** |
| `Jwt__Users__0__Username` | `Jwt:Users[0].Username` | `superadmin` |
| `Jwt__Users__0__PasswordHash` | `Jwt:Users[0].PasswordHash` | generado con `dotnet run -- hash-password "<pwd>"` (ver README) |
| `Jwt__Users__0__Role` | `Jwt:Users[0].Role` | `superadmin` |
| ...`__1__`, `__2__` | ídem para `dueno` y `empleado` | |
| `Cors__AllowedOrigin` | (nuevo, no existe en local) | la URL del frontend de ese ambiente en Vercel |
| `Google__ClientId` | `Google:ClientId` | OAuth Client ID de Google Cloud Console — ver `docs/PORTAL.md` |
| `Portal__AppUrl` | `Portal:AppUrl` | la misma URL del frontend que `Cors__AllowedOrigin` |

`Jwt__Issuer`, `Jwt__Audience` y `Jwt__ExpiresMinutes` ya vienen con un
valor por defecto en `render.yaml` — no hace falta tocarlos salvo que
cambien a propósito.

**Supabase**: el connection string sale del dashboard de cada proyecto
(Settings → Database → Connection string, modo *Transaction pooler* como
ya usa el `appsettings.json` local).

## Desplegar

Push a `develop` o a `main` dispara el deploy automático en el servicio de
Render correspondiente (Render hace build del `Dockerfile` y lo levanta).
No hay ningún paso manual de deploy — es el mismo flujo de PR → merge que
ya se usa hoy.

## Aplicar migraciones en un ambiente desplegado

`dotnet run -- migrate` (ver `docs/ESTRUCTURA_BACKEND_CORE.md`) corre local,
apuntando a la base que digas con `ConnectionStrings__DefaultConnection`:

```
ConnectionStrings__DefaultConnection="<connection string de Supabase>" dotnet run -- migrate
```

Es seguro correrlo más de una vez contra la misma base — todas las
migraciones son idempotentes.

## CORS

`Cors:AllowedOrigin` reemplaza el `AllowAnyOrigin()` que había antes. Sin
setear (por ejemplo, en dev local sin esa env var) sigue permitiendo
cualquier origen — no rompe nada corriendo en `localhost`.

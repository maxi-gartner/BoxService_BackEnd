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
| `SUPABASE_URL` | (no tiene equivalente en `appsettings.json`, se lee directo de la env var) | Project URL de Supabase — Settings → API. **La misma en dev y en prod** (un solo proyecto) |
| `SUPABASE_KEY` | ídem | **service_role key**, no la `anon` — el backend sube fotos de inspección y crea URLs firmadas del lado del servidor |
| `SUPABASE_BUCKET` | ídem | opcional, default `vehicle-inspections` si no se define |

Sin `SUPABASE_URL`/`SUPABASE_KEY`, cualquier operación con fotos de
inspección (subir, ver, borrar) falla con 503 — el backend lo loguea
como `InspectionPhotoStorageUnavailable`, pero el frontend muestra el
503 como "no pudimos conectar con el servidor" (mismo texto que un
fallo de red real), así que si pasa esto revisá primero estas tres env
vars antes de sospechar de Vercel/Render.

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
Aplicá las migraciones antes de desplegar esta versión: el registro de
eventos de auth y recuperación de contraseña necesitan la migración
`018_auth_auditoria_y_recuperacion.sql`; la auditoría de mutaciones
también requiere aplicar `019_ampliar_auditoria_api.sql`.

## CORS

En `Production`, `Cors:AllowedOrigin` es obligatorio. En Render se configura como
`Cors__AllowedOrigin`, usando **el origen real del frontend de ese ambiente**:
esquema, host y puerto si corresponde. No copiar un dominio de ejemplo ni usar
la URL del backend. La variable ya está declarada como `sync: false` en
`render.yaml`; su valor debe verificarse en el dashboard de cada servicio.

Se rechazan valores vacíos, comodines (`*`, subdominios wildcard), listas de
orígenes, credenciales, rutas, query strings, fragmentos y esquemas distintos de
HTTP/HTTPS. Se acepta una barra final de raíz y se normaliza al origen sin barra.
Una configuración inválida impide iniciar el servidor HTTP con un error que
identifica la clave, sin imprimir su valor. Para el frontend público de Render/
Vercel se debe utilizar su origen HTTPS real. No se habilitan cookies cross-origin.

En `Development` se conserva el comportamiento anterior: sin origen configurado
se permite cualquier origen; si se configura uno, se utiliza ese origen. Para
trabajar localmente con el ejemplo de configuración (cuyo origen queda vacío):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

Comprobar también `DOTNET_ENVIRONMENT` si está definido. El nombre del servicio
Render (por ejemplo, que termine en `-dev`) no determina el ambiente de ASP.NET
Core. Sin configuración explícita, el ambiente predeterminado es `Production`.
No cambiar un despliegue público a `Development` para evitar esta validación.
Los comandos de mantenimiento `migrate`, `hash-password` y `--setup-db` no
necesitan CORS porque no sirven solicitudes HTTP.

Conservar la correspondencia exacta con el `Origin` enviado por el navegador.
Una solicitud server-to-server del proxy de Next.js puede no llevar `Origin`:
CORS no reemplaza la autenticación y no bloquea por sí mismo esos clientes.

## HTTPS y proxies en Render

Render documenta que termina TLS en su balanceador, redirige HTTP público a HTTPS
y reenvía HTTP al proceso. Por eso Kestrel mantiene su listener HTTP y no se agrega
una redirección HTTPS que pueda interferir con ese esquema o el health check.
Ver [Web Services de Render](https://render.com/docs/web-services#connect-to-your-web-service).

Pendiente de comprobar en el despliegue real:

- Certificado y redirección del dominio público utilizado por el frontend.
- Ambiente efectivo y valor real de `Cors__AllowedOrigin` en cada servicio.
- IP que recibe Kestrel y cadena navegador → posible proxy Next.js/Vercel → Render.
- Proxies/redes confiables, cantidad de saltos y tratamiento de X-Forwarded-For/
  X-Forwarded-Proto por cada intermediario.

No se habilita `UseForwardedHeaders` sin esa información. No limpiar las listas
de confianza ni activar variables de forwarding indiscriminado para resolverlo.
Una futura configuración deberá aceptar sólo intermediarios confiables y operar
antes del rate limiter. Ver [configuración oficial de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

## Límites de solicitudes y cabeceras

| Política | Endpoints | Cupo por IP de conexión |
|---|---|---|
| login | POST /auth/login | 10 cada 5 minutos |
| portal-login | POST /portal/auth/google | 10 cada 5 minutos, independiente del personal |
| password-recovery | POST /auth/forgot-password y /auth/reset-password | 5 cada 15 minutos, compartido entre ambos |

Las políticas usan ventanas fijas, sin cola, y responden 429 con el envelope JSON
existente. Los contadores viven en memoria por instancia y se reinician con el
proceso; no constituyen una cuota distribuida ni una protección completa contra
abuso. Usuarios con la misma IP/NAT comparten cupo. Sin forwarding confiable,
pueden compartir la IP del proxy; con IP ausente se comparte la partición `unknown`.
El encabezado X-Forwarded-For enviado por el cliente no se usa directamente.

Todas las respuestas reciben `X-Content-Type-Options: nosniff`. Las rutas sensibles
reciben `Cache-Control: no-store`, incluidos errores de autenticación, autorización,
validación, límite de solicitudes y servidor. Abarca autenticación, tokens,
invitaciones, datos de talleres/clientes y URLs firmadas. Sólo las lecturas de `/`
y `/health` quedan fuera de esa directiva; no se les agrega un permiso de caché.
Se cubren también rutas desconocidas de forma conservadora. No se cambian códigos
HTTP, cuerpos JSON, ni cabeceras de challenge JWT.

`no-store` controla cachés HTTP compatibles; no borra datos previamente almacenados,
no gobierna cachés programadas por el frontend y no modifica la respuesta del
objeto alojado en Supabase cuando se accede directamente a una URL firmada.

## Cierre de seguridad — etapa 6

El middleware existente de auditoría se ejecuta después de autenticación y antes
 de autorización y controles manuales. Registra una sola vez los rechazos 401/403
 de endpoints reconocidos, incluidas consultas GET; conserva los eventos propios
 de login, recuperación y logout. Las mutaciones y los rechazos son excluyentes.
Se almacena la plantilla de ruta, nunca valores de parámetros, query strings,
cuerpos ni credenciales. `access_denied` usa outcome `401` o `403` sin cambios de
esquema. Una falla de persistencia no reemplaza la respuesta; el diagnóstico
50302 incluye tipo de evento, resultado, plantilla y TraceId, sin la excepción
ni el mensaje del proveedor. La auditoría es best effort: si no hay persistencia,
el evento puede perderse; el log permite detectar esa situación.

CI compila la solución (incluido xUnit) en Release y ejecuta `dotnet test` después
de Build y antes de Docker, sin servicios PostgreSQL, Supabase ni Google reales.
Un test fallido hace fallar el job. Verificar que el check sea obligatorio para
integrar en develop/main: el workflow por sí solo no configura esa protección.
Confirmar la primera ejecución en Ubuntu/GitHub Actions antes de cerrar el PR.

Antes del despliegue siguen pendientes las verificaciones de las secciones CORS
 y HTTPS/proxies: origen HTTPS real, ambiente Production, certificado y
redirección pública, preflight, IP observada y cadena de proxies confiables.
No se habilita forwarded headers automáticamente. Confirmar migraciones 018/019.
Los límites por IP siguen siendo locales a cada instancia y pueden compartir
cupo entre usuarios detrás de NAT o del proxy.

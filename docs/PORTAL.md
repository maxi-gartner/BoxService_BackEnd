# Portal del cliente

Portal chico, de solo lectura, para que el dueño de un auto vea en qué
está su vehículo en el taller — Sprint 4 / Nivel PRO de
`ESTADO_DEL_PROYECTO.md`, adelantado.

## El flujo

1. El cliente ya existe en la base (`clientes`, se crea al cargar su
   vehículo). No hace falta que tenga email cargado — alcanza con el
   teléfono.
2. Un miembro del taller, desde Clientes en la web, toca **"Invitar al
   portal"**. El backend genera un link único (`POST
   /clients/{id}/portal-invite`) y el frontend abre WhatsApp con el
   mensaje ya armado — un solo click.
3. El cliente toca el link (`/portal/activar?token=...`), se loguea con
   Google **esa primera vez** — ahí su cuenta de Google queda vinculada
   a ese cliente puntual.
4. Las próximas veces entra por `/portal` directo, mismo botón de
   Google, sin necesitar el link de nuevo.

**Por qué invitación y no matchear por email**: el email que carga el
taller no siempre coincide con el Gmail personal del cliente; el
teléfono (que siempre se carga) alcanza, y la invitación explícita le da
al taller control real de quién entra — no es una puerta abierta a
cualquiera que adivine un dato.

## Modelo de datos

`client_portal_access` (migración `011`): una fila por cliente alguna
vez invitado. `google_sub` (el ID estable que manda Google, no el
email) es lo que identifica al cliente en logins futuros.
`invite_token` se limpia al activarse — un link ya usado no sirve una
segunda vez. Reinvitar resetea el vínculo (sirve si el cliente perdió
acceso a su cuenta de Google vieja).

## Auth — un rol más, no un sistema paralelo

El portal emite JWT con el mismo `Jwt:Key`/`AuthService` que el login de
staff, pero `role: "customer"` y `sub` = `clientId` (no username). El
middleware en `Program.cs` separa los dos mundos: `/portal/*` exige
`role=customer`, y —al revés— ninguna ruta de staff acepta un JWT con
`role=customer`, aunque esté autenticado.

## Endpoints

| Ruta | Quién | Qué hace |
|---|---|---|
| `POST /clients/{id}/portal-invite` | staff | Genera/renueva la invitación, devuelve `inviteUrl` y `whatsappUrl` |
| `POST /portal/auth/google` | pública | `{ idToken, inviteToken? }` → verifica con Google, vincula o reconoce la cuenta, devuelve JWT de portal |
| `GET /portal/me` | `role=customer` | Cliente + sus vehículos + historial completo de presupuestos y services de cada uno |

## Configuración

- `Google:ClientId` — OAuth 2.0 Client ID de Google Cloud Console (ver
  más abajo). Tiene que ser el mismo que `NEXT_PUBLIC_GOOGLE_CLIENT_ID`
  del frontend — es el mismo token el que se valida de los dos lados.
- `Portal:AppUrl` — URL pública del frontend, para armar el link de
  invitación (`{AppUrl}/portal/activar?token=...`).

### Crear el OAuth 2.0 Client ID

1. [Google Cloud Console](https://console.cloud.google.com/) → crear (o
   elegir) un proyecto.
2. **APIs & Services → OAuth consent screen**: tipo "External", nombre
   de la app "BoxService", nada más hace falta para esta etapa (no pide
   verificación de Google mientras sea de uso interno con pocos
   usuarios de prueba).
3. **APIs & Services → Credentials → Create Credentials → OAuth client
   ID** → tipo **"Web application"**.
4. **Authorized JavaScript origins**: agregar `http://localhost:3000`
   (dev) y, cuando estén desplegados, los dominios de Vercel de
   development y production (ver `BoxService_FrontEnd/web/docs/DEPLOYMENT.md`).
   No hace falta "Authorized redirect URIs" — Google Identity Services
   (lo que usa `@react-oauth/google`) no redirige, usa un popup/One Tap.
5. Copiar el **Client ID** (termina en `.apps.googleusercontent.com`) a
   `Google:ClientId` acá y a `NEXT_PUBLIC_GOOGLE_CLIENT_ID` en el
   frontend.

## Fuera de alcance (por ahora)

Aprobar presupuestos desde el portal, notificaciones automáticas (hoy
el mensaje de WhatsApp lo manda el taller a mano, con un click).

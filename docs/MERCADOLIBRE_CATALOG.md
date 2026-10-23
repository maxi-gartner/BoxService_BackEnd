# Catálogo de vehículos (Mercado Libre Argentina)

Sugerencia de **marca** al cargar un vehículo (web y mobile) — mejora la
precisión y evita typos, pero **nunca bloquea**: si la API de ML no
responde, el campo sigue aceptando texto libre como antes.

## Qué endpoint expone el backend

- `GET /vehicle-catalog/brands` — las ~200 marcas reales del mercado
  argentino, sacadas de `GET /categories/MLA1744/attributes` (API
  pública de ML, sin autenticación). Se cachea 24hs en memoria del
  proceso (`MercadoLibreCatalogService`).

Requiere sesión de staff (`RequireAuthorization()`) — no es dato de un
tenant puntual, cualquier dueño/empleado autenticado puede pedirlo.

`GET /vehicle-catalog/models` y `GET /vehicle-catalog/versions` siguen
existiendo (para no romper los frontends) pero devuelven siempre lista
vacía — ver el porqué abajo. Modelo y versión quedan como campos de
texto libre en el formulario.

## Por qué modelo y versión quedaron sin sugerencias (investigado y descartado)

ML sí documenta un endpoint para esto —
`POST /catalog_domains/MLA-CARS_AND_VANS/attributes/{MODEL|VEHICLE_YEAR|TRIM}/top_values`
con `known_attributes` (marca, y marca+modelo para año/versión) — pero
requiere un token OAuth de usuario, no uno de aplicación.

Se probó de punta a punta, con una app real creada en
developers.mercadolibre.com.ar:

- `client_credentials` (token de app, sin login de nadie): **no
  soportado** por ML para este endpoint — devuelve
  `unsupported_grant_type`.
- `authorization_code` + PKCE (requerido por esta app): funciona, emite
  access token válido. `refresh_token` nunca llegó a emitirse (ML
  ignoró `scope=offline_access` en todos los intentos).
- Con el access token válido, `catalog_domains/.../top_values` devuelve
  **403** `PA_UNAUTHORIZED_RESULT_FROM_POLICIES` sin importar la
  configuración de permisos probada en el panel: permisos default,
  "Publicación y sincronización" en "Lectura" + unidad de negocio "VIS"
  habilitada, "Tópicos" de items/catalog con todo seleccionado. Ninguna
  combinación disponible desde el panel público de developers lo
  destrabó.

Conclusión: este endpoint necesita un nivel de acceso partner/
certificado que ML no otorga por auto-registro estándar. Si en el
futuro ML lo habilita para apps normales, el patrón a implementar está
documentado arriba (`known_attributes` cascada marca→modelo→versión).
No quedó nada de la app de ML atado al código — la app sigue existiendo
en la cuenta de ML del usuario, simplemente sin usarse.

## Por qué no hay cascada estricta marca→modelo→versión en la UI

El listado de marcas es el universo completo y real — por eso es un
`<select>` cerrado (con un "Otra (escribir)" por si falta alguna
rarísima). Modelo y versión son campos de texto libre simples, sin
sugerencias — cualquier mejora futura ahí depende de que ML habilite
el endpoint de catálogo para apps estándar.

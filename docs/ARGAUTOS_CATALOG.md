# Catálogo de vehículos (ArgAutos)

Sugerencias de marca/modelo/versión al cargar un vehículo (web y
mobile) — mejoran la precisión y evitan typos, pero **nunca bloquean**:
si ArgAutos no responde (o se agota el rate limit, ver abajo), los tres
campos siguen aceptando texto libre como antes.

Reemplaza al intento anterior con la API de Mercado Libre: esa requería
un token OAuth de usuario para modelo/versión y el endpoint
(`catalog_domains`) devolvía 403 sin importar los permisos configurados
en una app estándar — quedó abandonado (detalle en el historial de
commits de este archivo). ArgAutos da el mismo catálogo marca→modelo→
versión sin OAuth.

## Qué endpoints expone el backend

- `GET /vehicle-catalog/brands` — las marcas del catálogo de ArgAutos.
- `GET /vehicle-catalog/models?brandId=X` — los modelos de esa marca.
- `GET /vehicle-catalog/versions?brandId=X&modelId=Y` — las versiones
  de ese modelo (`brandId` se ignora del lado de ArgAutos, pero se pide
  igual para mantener la misma firma de ruta).

Las tres requieren sesión de staff (`RequireAuthorization()`) — no son
datos de un tenant puntual, cualquier dueño/empleado autenticado puede
pedirlas.

## Cómo funciona por dentro (`ArgAutosCatalogService`)

- API pública de ArgAutos (`https://argautos.com/api/v1/argautos/...`),
  **sin API key** por ahora (decisión explícita — ver "Rate limit" más
  abajo).
- Cada nivel (marcas, modelos de una marca, versiones de un modelo) se
  cachea 24hs en memoria del proceso: marcas en un solo campo, modelos
  y versiones en un diccionario por `brandId`/`modelId`. Cambian casi
  nunca, así que el cache cubre la inmensa mayoría de los pedidos.
- Paginación (`per_page=100`, el máximo que acepta la API): como la
  gran mayoría de marcas/modelos/versiones entra en una sola página,
  en la práctica es 1 request por nivel cacheado. Si hiciera falta más
  de una página, el servicio las va pidiendo solo hasta que
  `meta.current_page` llega a `meta.last_page` (con un tope de 10
  páginas, por si algo raro pasa del lado de la API).
- Si ArgAutos devuelve error (incluido 429 por rate limit) o no
  responde, se devuelve el último cache que haya (aunque esté vencido)
  o lista vacía — nunca una excepción al frontend.

## Rate limit (por qué no hay API key todavía)

| Acceso | Límite por minuto | Cuota diaria |
|---|---|---|
| Sin API key (el que usamos) | 3 requests/min | **30/día** (confirmado con los headers `X-DailyQuota-*` — la docu de ArgAutos dice "sin cuota garantizada" pero en la práctica sí hay un tope) |
| Starter | 15 requests/min | 1.000/día |
| Pro | 40 requests/min | 5.000/día |

Se arrancó sin key a propósito: con el cacheo de 24hs de arriba, el
tráfico real hacia ArgAutos es bajísimo (básicamente 1 request por
marca/modelo nuevo que alguien consulta por primera vez en el día). Si
en uso real el límite anónimo queda corto (muchos altas simultáneas de
marcas/modelos nunca antes cacheados), conseguir una key en
developers de ArgAutos y agregar el header
`Authorization: Bearer <key>` en `ArgAutosCatalogService` alcanza —
no hace falta cambiar la arquitectura de caché ni los endpoints.

## Por qué no hay cascada estricta marca→modelo→versión en la UI

A diferencia del intento con Mercado Libre (donde modelo/versión
quedaron sin datos), ArgAutos sí da un catálogo completo en los tres
niveles — por eso los tres campos son `<select>` cerrados (con un
"Otra (escribir)" en cada uno, por si el catálogo no cubre algún caso
particular). Ningún nivel obliga a elegir de una lista si lo que
necesitás no está.

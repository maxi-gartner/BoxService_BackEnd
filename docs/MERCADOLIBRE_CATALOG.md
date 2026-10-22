# Catálogo de vehículos (Mercado Libre Argentina)

Sugerencias de marca/modelo/versión al cargar un vehículo (web y
mobile) — mejoran la precisión y evitan typos, pero **nunca bloquean**:
si falta configurar las credenciales o la API de ML no responde, los
tres campos siguen aceptando texto libre como antes.

## Cómo conseguir las credenciales

1. Entrar a https://developers.mercadolibre.com.ar/ con una cuenta de
   Mercado Libre (o crear una, es gratis).
2. "Mis aplicaciones" → "Crear nueva aplicación". No hace falta
   configurar Redirect URI para este uso.
3. Copiar el **App ID** (`MercadoLibre:ClientId`) y la **Secret Key**
   (`MercadoLibre:ClientSecret`) a `appsettings.json` (local) o a las
   env vars `MercadoLibre__ClientId` / `MercadoLibre__ClientSecret`
   (Render — ver `docs/DEPLOYMENT.md`).

## Qué endpoints expone el backend

- `GET /vehicle-catalog/brands` — las ~200 marcas reales del mercado
  argentino. **Público del lado de ML**, no necesita las credenciales
  configuradas para funcionar.
- `GET /vehicle-catalog/models?brandId=X` — los modelos más comunes de
  esa marca. Necesita las credenciales (token de app).
- `GET /vehicle-catalog/versions?brandId=X&modelId=Y` — las versiones
  más comunes de esa marca+modelo. Mismo requisito.

Las tres rutas requieren sesión de staff (`RequireAuthorization()`) —
no son datos de un tenant puntual, cualquier dueño/empleado autenticado
puede pedirlas. El middleware de roles ya bloquea a superadmin (no
tiene tenant, no carga vehículos).

## Cómo funciona por dentro (`MercadoLibreCatalogService`)

- Token de app vía `grant_type=client_credentials` contra
  `https://api.mercadolibre.com/oauth/token` — **no requiere que nadie
  inicie sesión en ML**, es un token de aplicación. Se cachea en
  memoria del proceso y se renueva solo cuando falta menos de 5 minutos
  para que venza (confirmado: dura ~6hs).
- El listado de marcas se cachea 24hs en memoria (cambia casi nunca).
- Modelo y versión usan el patrón `known_attributes` documentado por
  ML: `POST /catalog_domains/MLA-CARS_AND_VANS/attributes/{ATRIBUTO}/
  top_values` con body `{"known_attributes":[{"id":"BRAND","value_id":
  "..."}]}` (y sumando `{"id":"MODEL","value_id":"..."}` para pedir
  versión). Devuelve los valores más frecuentes en avisos de ML, no un
  catálogo exhaustivo — por eso el frontend siempre deja escribir lo
  que no aparezca en la lista.

## Por qué no hay cascada estricta marca→modelo→versión en la UI

El listado de marcas es la lista completa y real — por eso ahí sí es
un `<select>` cerrado (con un "Otra (escribir)" por si falta alguna
rarísima). Modelo y versión son "los más vistos en ML", no el universo
completo, así que quedan como texto libre con sugerencias (datalist en
web, chips en mobile) — nunca como un select que te obligue a elegir
de una lista que puede no tener tu caso.

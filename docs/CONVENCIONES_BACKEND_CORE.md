# Convenciones Backend ASP.NET Core

## Ramas

- No trabajar directo en `main`.
- No trabajar directo en `develop` para cambios grandes.
- Usar una rama propia para la migración, por ejemplo `dev-cris-back-core`.

## Capas

### Controllers

- Reciben HTTP.
- Validan datos básicos de entrada.
- Llaman a Services.
- Devuelven siempre envelope JSON.
- No contienen reglas de negocio.
- No ejecutan SQL.

### Services

- Contienen reglas de negocio.
- Validan casos propios del dominio.
- Coordinan llamadas a Repositories.
- No abren conexiones a PostgreSQL.
- No construyen respuestas HTTP.

### Repositories

- Son el único lugar donde se ejecuta SQL.
- Usan `PostgresConnectionFactory`.
- No contienen reglas de negocio.
- Devuelven modelos o DTOs internos.

### DTOs

- Separan lo que entra y sale por la API de las entidades internas.
- Cada endpoint que recibe body debería tener un request DTO.
- Cada endpoint complejo debería tener un response DTO.

## Respuestas JSON

Todo endpoint debe responder con envelope:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": 400,
    "message": "Mensaje de error"
  }
}
```

No devolver HTML en errores.

## Base de datos

- PostgreSQL es la base oficial.
- Usar Npgsql.
- No editar migraciones ya aplicadas.
- Crear una migración nueva para cada cambio de estructura.
- `appsettings.json` es local y no se sube al repo.
- `appsettings.example.json` mantiene un ejemplo sin credenciales reales.

## Formatter y linter

El proyecto usa:

- `.editorconfig` para estilo y formato.
- `Directory.Build.props` para activar análisis de código y reglas de estilo en build.
- `dotnet format` para aplicar formato automático.

`.editorconfig` y `Directory.Build.props` aplican a **todo** el proyecto, no
solo a los archivos nuevos de la migración — el código viejo (`Router/`,
`Services/`, `Controllers/`, `Database/`, `Models/`, `Repositories/`,
`ResponseHelper.cs`, `Server.cs`) nunca se formateó con estas reglas, así
que `dotnet format` sin argumentos va a marcar ~40 errores ahí que no
tienen nada que ver con lo que estés tocando. Mientras no se normalice ese
código en una pasada aparte, apuntá el comando solo a lo que cambiaste:

```powershell
dotnet format --include Ruta/Al/Archivo.cs
dotnet build
```

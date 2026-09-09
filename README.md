<h1 align="center">🚗 BoxService</h1>
<p align="center">
Sistema de gestión para lubricentros y talleres mecánicos
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8-blue" />
  <img src="https://img.shields.io/badge/PostgreSQL-Database-blue" />
  <img src="https://img.shields.io/badge/Status-En desarrollo-yellow" />
</p>
# BoxService — Backend

> API REST para el sistema de gestión de lubricentro.
> Módulos funcionales en ASP.NET Core + PostgreSQL. Auth/JWT y multi-tenant pendientes.

---

## Descripción

Este repositorio contiene el servidor backend de **BoxService**, un sistema de gestión para lubricentros y centros de service vehicular.

Es un backend sin interfaz gráfica: expone una API HTTP que recibe requests del frontend, ejecuta la lógica de negocio y persiste los datos en PostgreSQL. Toda la comunicación se hace mediante JSON siguiendo el patrón envelope:

```json
{ "success": true,  "data": {},   "error": null }
{ "success": false, "data": null, "error": { "code": 400, "message": "..." } }
```

El sistema cubre el flujo completo de trabajo de un taller:

```
Cliente → Vehículo → Presupuesto → Service → Factura
```

---

## Integrantes

| Nombre | Legajo | Módulo |
|---|---|---|
| Gartner, Maximiliano | 18396 | Arquitectura + Presupuestos y Facturas |
| Carrasco, Cristhian | 18403 | Clientes |
| Busch, Leonardo | 18404 | Vehículos |
| Quesada, Oscar | 18382 | Services + Dashboard |

---

## Tecnologías

| Capa | Tecnología |
|---|---|
| Lenguaje | C# .NET 8 |
| Servidor HTTP | ASP.NET Core |
| Base de datos | PostgreSQL |
| Driver BD | Npgsql |
| IDE | Visual Studio Community 2022 |

---

## Estructura del proyecto

```
BoxService-BackEnd/
├── Api/                ← endpoints ASP.NET Core, envelope y errores HTTP
├── Controllers/        ← controllers HttpListener legados, no registrados
├── Data/               ← conexión PostgreSQL y configuración de persistencia
├── DTOs/               ← requests y responses expuestos por la API
├── Services/           ← lógica de negocio y validaciones
├── Repositories/       ← único punto de acceso a la base de datos
├── Models/             ← clases que representan las entidades
├── Database/
│   └── migrations/     ← scripts SQL para crear y modificar tablas
├── docs/               ← documentación técnica y convenciones
├── Program.cs          ← configura y arranca ASP.NET Core en localhost:5001
└── BoxService-BackEnd.csproj
```

### Responsabilidad de cada capa

| Capa | Regla |
|---|---|
| Controller | No piensa. Solo recibe el request, llama al Service y devuelve la respuesta |
| Service | Contiene toda la lógica de negocio. No accede a la BD directamente |
| Repository | El único que ejecuta SQL. No contiene lógica de negocio |
| Model | Solo propiedades. Sin métodos ni lógica |

---

## Requisitos previos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) corriendo en `localhost:5432`
- Visual Studio Community 2022

---

## Cómo correr el proyecto

### 1. Clonar el repositorio

```bash
git clone https://github.com/tu-usuario/BoxService-BackEnd.git
```

### 2. Restaurar dependencias

Ejecutar `dotnet restore` para instalar las versiones definidas en el proyecto.

### 3. Crear la base de datos

Desde pgAdmin o psql:

```sql
CREATE DATABASE boxservice;
```

### 4. Ejecutar las migraciones en orden

Abrir cada archivo en `Database/migrations/` y ejecutarlos en este orden:

```
001_crear_clientes.sql
002_crear_vehiculos.sql
003_crear_presupuestos.sql
004_crear_services.sql
005_crear_facturas.sql
006_add_id_presupuesto_to_services.sql
007_add_numero_sequences.sql
008_crear_catalogo_servicios.sql
009_vehicle_year_nullable.sql
```

### 5. Correr el proyecto

Presionar **F5** en Visual Studio. El servidor levanta en:

```
http://localhost:5001
```

### 6. Verificar que funciona

Abrir el navegador o Postman y acceder a:

```
GET http://localhost:5001/health
```

Respuesta esperada:

```json
{
  "success": true,
  "data": {
    "status": "healthy",
    "database": "connected",
    "timestamp": "2026-03-18T10:00:00Z",
    "version": "1.0.0"
  },
  "error": null
}
```

---

## Endpoints disponibles

Todas las rutas excepto `/` y `/health` requieren el header `X-Api-Key`
(ver [Autenticación](#autenticación) más abajo). La API nueva usa nombres
en inglés, sin prefijo `/api`. Los alias del servidor HttpListener anterior
no están registrados en el pipeline nuevo. Consultar la lista actual en
[Estructura ASP.NET Core](docs/ESTRUCTURA_BACKEND_CORE.md).

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/health` | Estado del servidor y la base de datos (no requiere API key) |
| GET | `/clients` | Lista todos los clientes |
| GET | `/clients/{id}` | Obtiene un cliente por ID |
| GET | `/clients/{id}/vehicles` | Lista los vehículos de un cliente |
| POST | `/clients` | Crea un nuevo cliente |
| GET | `/vehicles` | Lista todos los vehículos |
| GET | `/vehicles?plate=` | Filtra por patente |
| GET | `/vehicles/{id}` | Obtiene un vehículo por ID |
| GET | `/vehicles/{id}/history` | Historial de services del vehículo |
| POST | `/vehicles` | Crea un nuevo vehículo |
| GET | `/budgets` | Lista todos los presupuestos |
| GET | `/budgets/{id}` | Obtiene un presupuesto con su detalle y total |
| POST | `/budgets` | Crea un presupuesto con sus ítems |
| PATCH | `/budgets/{id}` | Cambia estado: `sent` \| `rejected` \| `approved` |
| PUT | `/budgets/{id}/service` | Vincula el presupuesto aprobado con un service ya creado |
| GET | `/services` | Lista todos los services |
| GET | `/services/{id}` | Obtiene un service |
| GET | `/services/{id}/details` | Obtiene los detalles de un service |
| POST | `/services` | Crea un service manual |
| POST | `/services/{id}/details` | Agrega un detalle a un service existente |
| GET | `/invoices` | Lista todas las facturas |
| GET | `/invoices/{id}` | Obtiene una factura por ID |
| POST | `/invoices` | Emite una factura desde un service ⚡ |
| PATCH | `/invoices/{id}` | Cambia estado: `paid` \| `cancelled` |
| GET | `/catalog` | Lista el catálogo |
| POST | `/catalog` | Crea un ítem de catálogo |
| PATCH | `/catalog/{id}` | Edita un ítem de catálogo |
| DELETE | `/catalog/{id}` | Elimina un ítem de catálogo |

> ⚡ Estos endpoints usan transacción SQL. Si cualquier paso falla se hace ROLLBACK completo.
>
> Aprobar un presupuesto (`PATCH .../{id}` con `{"status":"approved"}`) ya
> **no** crea un service automáticamente — eso pasa desde el módulo de
> Services, que después vincula el service creado con `PUT .../{id}/service`.

### Autenticación

Un candado simple, no un sistema de auth completo: todas las rutas de
negocio requieren el header `X-Api-Key` con el valor configurado en
`appsettings.json` (campo `ApiKey`, por defecto `boxservice-dev-key` en
desarrollo). El frontend ya lo manda automáticamente en cada request
(proxy de Next.js) — si cambiás la key acá, actualizala también ahí.

---

## Flujo de un request

```
Frontend (fetch)
      ↓
  Endpoint ASP.NET Core → recibe el request, llama al Service
      ↓
  Service           → valida, aplica lógica de negocio
      ↓
  Repository        → ejecuta el SQL contra PostgreSQL
      ↓
  ApiEnvelope       → serializa la respuesta en JSON envelope
      ↓
Frontend (muestra el resultado)
```

---

## Modelo de datos

```
clientes
    └── vehiculos (1:N)
            └── presupuestos (1:N)
            │       └── detalle_presupuesto (1:N)
            │       └── services (al vincular el trabajo realizado)
            └── services (1:N)
                    └── detalle_service (1:N)
                    └── facturas (1:1) ⚡
```

---

## Convenciones del equipo

- Nunca trabajar directo en `main` — cada uno trabaja en su rama `feature/[modulo]`
- Nunca poner lógica de negocio en los Controllers
- Nunca acceder a la BD desde Services — solo desde Repositories
- Nunca editar una migración ya aplicada — crear una nueva

Más detalle:

- [Estructura ASP.NET Core](docs/ESTRUCTURA_BACKEND_CORE.md)
- [Convenciones Backend ASP.NET Core](docs/CONVENCIONES_BACKEND_CORE.md)

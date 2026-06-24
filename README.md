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
> Construida en C# puro con `HttpListener`, sin ASP.NET ni frameworks MVC.

---

## Descripción

Este repositorio contiene el servidor backend de **BoxService**, un sistema de gestión para lubricentros y centros de service vehicular.

Es un backend puro, sin interfaz gráfica ni ASP.NET MVC: expone una API HTTP que recibe requests del frontend, ejecuta la lógica de negocio y persiste los datos en PostgreSQL. Toda la comunicación se hace mediante JSON siguiendo el patrón envelope:

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
| Servidor HTTP | `HttpListener` (sin ASP.NET) |
| Base de datos | PostgreSQL |
| Driver BD | Npgsql 8.x |
| IDE | Visual Studio Community 2022 |

---

## Estructura del proyecto

```
BoxService-BackEnd/
├── Controllers/        ← reciben el request y devuelven la respuesta JSON
├── Services/           ← lógica de negocio y validaciones
├── Repositories/       ← único punto de acceso a la base de datos
├── Models/             ← clases que representan las entidades
├── Router/             ← decide qué controlador atiende cada ruta
├── Database/
│   └── migrations/     ← scripts SQL para crear y modificar tablas
├── Server.cs           ← inicia el HttpListener en localhost:5001
├── ResponseHelper.cs   ← helpers para respuestas JSON estandarizadas
├── Program.cs          ← punto de entrada
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

### 2. Instalar Npgsql

Click derecho en el proyecto → **Manage NuGet Packages** → buscar `Npgsql` → instalar versión **8.x**

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
```

### 5. Correr el proyecto

Presionar **F5** en Visual Studio. El servidor levanta en:

```
http://localhost:5001
```

### 6. Verificar que funciona

Abrir el navegador o Postman y acceder a:

```
GET http://localhost:5000/health
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

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/health` | Estado del servidor y la base de datos |
| GET | `/api/clientes` | Lista todos los clientes |
| GET | `/api/clientes/{id}` | Obtiene un cliente por ID |
| GET | `/api/clientes/{id}/vehiculos` | Lista los vehículos de un cliente |
| POST | `/api/clientes` | Crea un nuevo cliente |
| GET | `/api/vehiculos` | Lista todos los vehículos |
| GET | `/api/vehiculos/{id}` | Obtiene un vehículo por ID |
| GET | `/api/vehiculos/buscar?patente=` | Busca un vehículo por patente |
| GET | `/api/vehiculos/{id}/historial` | Historial de services del vehículo |
| POST | `/api/vehiculos` | Crea un nuevo vehículo |
| GET | `/api/presupuestos` | Lista todos los presupuestos |
| GET | `/api/presupuestos/{id}` | Obtiene un presupuesto con su detalle |
| POST | `/api/presupuestos` | Crea un presupuesto con sus ítems |
| PUT | `/api/presupuestos/{id}/estado` | Cambia estado (enviado / rechazado) |
| POST | `/api/presupuestos/{id}/aprobar` | Aprueba y genera el service automáticamente ⚡ |
| GET | `/api/services` | Lista todos los services |
| GET | `/api/services/{id}` | Obtiene un service con su detalle |
| POST | `/api/services` | Crea un service manual |
| GET | `/api/facturas` | Lista todas las facturas |
| GET | `/api/facturas/{id}` | Obtiene una factura por ID |
| POST | `/api/facturas` | Emite una factura desde un service ⚡ |
| PUT | `/api/facturas/{id}/estado` | Cambia estado (cobrada / anulada) |

> ⚡ Estos endpoints usan transacción SQL. Si cualquier paso falla se hace ROLLBACK completo.

---

## Flujo de un request

```
Frontend (fetch)
      ↓
  Router.cs         → decide qué controller maneja la ruta
      ↓
  Controller        → recibe el request, llama al Service
      ↓
  Service           → valida, aplica lógica de negocio
      ↓
  Repository        → ejecuta el SQL contra PostgreSQL
      ↓
  ResponseHelper    → serializa la respuesta en JSON envelope
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
            │       └── services (1:1 al aprobar) ⚡
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
- `Router.cs` solo lo modifica Maxi
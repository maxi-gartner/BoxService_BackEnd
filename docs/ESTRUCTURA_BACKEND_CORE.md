# Estructura propuesta para BoxService Backend ASP.NET Core

Este documento define la estructura base para migrar el backend viejo de `HttpListener` a ASP.NET Core + PostgreSQL.

## Objetivo

Tener una API REST mantenible, separada por capas, con respuestas JSON consistentes y acceso a datos centralizado.

## Carpetas

```txt
BoxService_BackEnd/
├── Api/                  # Envelope, errores y helpers HTTP compartidos
├── Controllers/          # Endpoints ASP.NET Core por módulo
├── Data/                 # Conexión PostgreSQL y configuración de persistencia
├── Database/             # Migraciones SQL existentes
├── DTOs/                 # Requests y responses expuestos por la API
├── Models/               # Entidades del dominio
├── Repositories/         # SQL y acceso a PostgreSQL
├── Services/             # Reglas de negocio y validaciones
├── docs/                 # Documentación técnica del backend
├── Program.cs            # Configuración de ASP.NET Core
└── appsettings.json      # Configuración local, no se sube al repo
```

## Flujo de request

```txt
Frontend
  -> Controller
  -> Service
  -> Repository
  -> PostgreSQL
```

El `Controller` no debe acceder directo a la base. El `Service` no debe ejecutar SQL.

## Estado actual de la migración

El proyecto ya arranca con ASP.NET Core y expone:

- `GET /`
- `GET /health`

`/health` usa `PostgresConnectionFactory` para verificar conexión real a PostgreSQL.

Los módulos viejos quedan temporalmente como referencia hasta migrarlos de a uno.

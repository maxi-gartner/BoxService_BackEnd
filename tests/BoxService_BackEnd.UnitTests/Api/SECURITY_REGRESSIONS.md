# Regresiones de seguridad — etapas 1 y 2

Estas pruebas exigen el comportamiento seguro esperado. En la etapa 1 quedaron
en rojo para reproducir los defectos. En la etapa 2 se corrige producción sin
omitir ni debilitar ninguna de esas comprobaciones.

## Ejecución

Desde la raíz del repositorio:

```powershell
dotnet test tests/BoxService_BackEnd.UnitTests/BoxService_BackEnd.UnitTests.csproj --logger "trx;LogFileName=security-regression.trx" --results-directory TestResults/SecurityRegression
```

Para ejecutar sólo los casos nuevos, agregar
`--filter FullyQualifiedName~SecurityRegressionTests`.

## Aislamiento y alcance

- `WebApplicationFactory<AuthService>` descubre el entry point del assembly real:
  ejecuta Program.cs, sus middlewares, políticas y handlers sin copiar sus reglas.
- HTTP se ejecuta en TestServer, sin publicar un servidor de la aplicación.
- Se usan una clave, emisor, audiencia e identidades exclusivos de pruebas. La
  validación criptográfica JWT sigue activa. Sólo se sustituye, en el host de
  pruebas, OnTokenValidated para no consultar sesiones en una base de datos.
  Estas pruebas no cubren login, OIDC ni SessionValidation.
- La conexión se sobrescribe con un puerto loopback reservado exclusivamente por
  un socket que no escucha. No hay PostgreSQL ni posibilidad de usar la conexión
  de producción en este host. El socket permanece reservado hasta disponer el host.
- Los POST tienen username vacío deliberadamente: si llegan al handler del empleado,
  la validación real devuelve 400 antes de cualquier consulta/creación de usuarios.
  Esto prueba el bypass de autorización, no una inserción efectiva en una base.
- La auditoría de producción intenta escribir y registra un NpgsqlException
  esperado, que ya captura su middleware. No se reemplaza ese middleware ni se
  considera ese log un fallo de regresión.
- El caso del superadmin comprueba además que el 500 proviene de
  `HttpContextExtensions.TenantId` y de "El token no tiene tenantId.", para no
  confundirlo con una falla de infraestructura.
- La colección no corre en paralelo con otras colecciones porque
  DatabaseConnection mantiene configuración estática. No agregar pruebas con una
  base real a esta colección.

## Casos y resultado inicial (etapa 1)

| Casos | Contrato esperado | Comportamiento inicial |
|---|---|---|
| Empleado POST /users y /users/ | 403 | 403; pasan |
| Empleado POST /USERS, /Users, /USERS/, /Users/ | 403 | 400 del handler; fallan 4 |
| Anónimo GET /users | 401 con envelope | Cumple; pasa |
| Anónimo GET /clients | 401 con envelope | 401 sin Content-Type ni cuerpo; falla |
| Empleado GET /tenants | 403 con envelope | Cumple; pasa |
| Empleado GET /metrics | 403 con envelope | 403 sin Content-Type ni cuerpo; falla |
| Superadmin POST /users sin tenant | 403 | 500 por tenantId ausente; falla |

El 403 esperado para superadmin conserva la separación actual: la creación global
se realiza mediante `/tenants/{id}/users`; `/users` crea dentro del taller propio.

Resultado de etapa 1: 11 casos nuevos, 4 aprobados y 7 fallidos; sin cambios de
producción en esa etapa.

## Correcciones y verificación de etapa 2

- POST /users exige explícitamente el rol dueno. Empleado y superadmin reciben
  403 antes del handler, independientemente de mayúsculas o barra final.
- La comparación manual de /users usa OrdinalIgnoreCase, consistente con las
  rutas hijas y el router. Las restricciones de los demás endpoints se conservan.
- StatusCodePages envuelve autenticación y autorización; completa respuestas sin
  cuerpo con el envelope existente sin reemplazar el status ni WWW-Authenticate.
- Las pruebas mantienen todas sus aserciones anteriores y agregan controles:
  el dueño alcanza la validación en las seis variantes, superadmin alcanza la
  validación en /tenants/7/users y un JWT inválido produce 401 JSON con challenge
  Bearer. Los datos inválidos detienen esos controles antes del acceso a usuarios.
- Resultados: 11/11 regresiones y 84/84 pruebas de la suite completa aprobadas.
  Reportes separados en TestResults/SecurityRegression/stage2-regressions.trx y
  TestResults/SecurityRegression/stage2-full.trx.

# Auditoría de rechazos — etapa 6

## Alcance

AuditRegressionTests agrega 24 casos sin bases ni servicios externos:

- Ocho casos HTTP de 401/403, GET y escritura, autorización explícita y controles
  manuales; cada rechazo produce exactamente un evento con plantilla de ruta.
- Logout y tres casos de login/recuperación ejecutan handlers reales con entradas
  que no acceden a repositorios de negocio; conservan su único evento propio.
- Cuatro casos del middleware comprueban exclusiones de auditoría de autenticación.
- Tres casos comprueban mutaciones exitosas, fallidas y denegadas sin duplicados.
- Tres casos excluyen rechazos públicos o rutas desconocidas.
- Dos casos simulan fallas de persistencia con secretos en el mensaje: conservan
  status, cuerpo y challenge, y no registran la excepción ni valores sensibles.

Los eventos `access_denied` usan outcome `401`/`403` y la tabla existente. Los
identificadores proceden sólo del principal autenticado. Se excluyen los datos de
la solicitud y los valores de parámetros de ruta. El diagnóstico de persistencia
incluye un TraceId para correlación, sin mensajes internos del proveedor.

No se cambian auditorías propias de autenticación ni su manejo de persistencia.
La protección best effort de esta etapa corresponde al middleware de auditoría.
Los logs de excepciones de otros componentes quedan fuera de este cambio.

## Verificación

Compilar la solución con `dotnet build --no-restore --configuration Release`.
Ejecutar la suite con el comando de `.github/workflows/ci.yml`; para aislar estos
casos agregar `--filter FullyQualifiedName~AuditRegressionTests`.

La ejecución local aislada puede impedir la comunicación vstest/testhost: un
aborto de infraestructura con cero casos ejecutados no es un fallo de regresión.
Confirmar la ejecución en Ubuntu de CI antes de integrar.

Resultado local de esta etapa: 24/24 nuevas y 332/332 en la suite completa,
sin fallos ni omisiones; se conservan las 308 anteriores. Build Release: cero
advertencias y errores. Reportes locales ignorados por Git:
`TestResults/SecurityRegression/stage6-audit-verified.trx` y `stage6-full.trx`.

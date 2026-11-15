# Aislamiento entre talleres y clientes del portal — etapa 4

## Alcance y datos

Se ejecutan Program.cs, el routing, los middlewares, los endpoints, los servicios
y TenantServiceFilter reales. No se copia la autorización en un servidor ficticio.
La fábrica emite tokens de pruebas para dueños/empleados distintos de los talleres
1 y 2. Conserva la validación criptográfica JWT, omitiendo la consulta de sesión
a base de datos igual que en las etapas anteriores.

TenantIsolationStore contiene clientes/vehículos/presupuestos/services/facturas/
inspecciones/fotos 101 y 102 del taller 1, y 201 del taller 2. Los clientes 101 y
102 permiten comprobar también aislamiento del portal dentro del mismo taller.
Cada caso comienza con un estado nuevo.

## Matriz automatizada (172 casos)

| Grupo | Casos | Verificación |
|---|---:|---|
| Accesos cruzados | 136 | 34 solicitudes × 2 talleres × 2 roles: dueño y empleado |
| Recursos propios | 14 | Lectura permitida de los 7 módulos, en ambos talleres |
| Listados | 10 | Los 5 listados usan el taller del JWT, ignorando tenantId ajeno en query |
| Alta de clientes | 2 | tenantId inyectado en body no cambia el taller del nuevo cliente |
| Portal por sujeto | 3 | sub determina cliente y vehículos; no clientId/sub/tenantId/vehicleId de query |
| Portal contra staff | 6 | Customer recibe 403 antes de acceder a repositorios o almacenamiento |
| Transferencia válida | 1 | Transferencia dentro del taller conserva historial y cambia visibilidad en portal |

Las 34 solicitudes cruzadas por usuario cubren:

- Clientes: detalle, vehículos asociados e invitación al portal.
- Vehículos: detalle, historial, búsqueda por patente, creación con cliente ajeno,
  transferencia de un vehículo ajeno y transferencia a un cliente ajeno.
- Presupuestos: detalle, filtro por vehículo ajeno, creación, cambio de estado,
  sustitución de detalles y vinculación de service (recurso principal o secundario ajeno).
- Services: detalle, detalles, filtro por vehículo, alta con vehículo ajeno y alta de detalle ajeno.
- Facturas: detalle, filtro por vehículo, creación con service ajeno o presupuesto
  ajeno y cambio de estado. Para probar el presupuesto ajeno se prepara un service
  propio sin factura, evitando un rechazo anterior por factura duplicada.
- Inspecciones: GET, POST y PATCH sobre un service ajeno.
- Fotos: carga, URL y eliminación sobre service ajeno; además, service propio con
  photoId del otro taller, para comprobar que se consulta por ambos identificadores.

Se conservan los códigos existentes: 404 para recursos no disponibles y 400 para
relaciones inválidas donde la implementación ya usa ese contrato. No se normalizan
reglas de negocio. La tabla de facturas no se expone directamente en el portal.

## Prueba de ausencia de efectos

Cada rechazo compara una serialización completa del estado antes/después y exige
una lista vacía de escrituras de negocio, cero llamadas a almacenamiento y cero
lecturas de detalles ajenos. Los repositorios simulados de escritura no agregan
autorización: si el handler alcanza una escritura indebida, ésta queda registrada
y modifica el estado, haciendo fallar la prueba. La auditoría tiene un doble
separado: registrar un intento no cuenta como modificación de datos de negocio.

Las lecturas registran los parámetros recibidos y exigen el tenant del JWT. Las
fotos registran el par serviceId/photoId. Los clientes del portal se seleccionan
por sub y sólo reciben sus vehículos, presupuestos no draft y services.

## Revisión de los controles existentes

- ClientRepository.GetById/GetPaged y VehicleRepository.GetById/GetByClientId
  incluyen tenant en SQL. ClientService propaga el tenant recibido del contexto.
- VehicleService verifica cliente de origen/destino antes de crear o transferir.
  La unicidad global de patente y la transferencia con historial se conservan.
- BudgetService valida primero presupuesto/vehículo y el service vinculado;
  BudgetRepository filtra cabeceras por tenant. Los detalles dependen de esa validación previa.
- ServicesService valida vehículo/sucursal al crear; ServiceEndpoints valida el
  service antes de leer o crear detalles.
- InvoiceService consulta service/presupuesto por tenant e InvoiceRules comprueba
  taller, vehículo y vínculo. InvoiceRepository incluye tenant en cabeceras y detalles.
- TenantServiceFilter consulta ServicesService.GetByIdForTenant antes de los
  handlers de inspección/fotos. ServiceInspectionRepository.GetPhotoAsync relaciona
  photoId con la inspección del service solicitado.
- PortalEndpoints usa sub del principal, luego el tenant del cliente encontrado,
  y limita la consulta de vehículos por clientId y tenantId.

## Cambios de testabilidad, sin correcciones de seguridad

Para ejecutar los servicios reales con datos en memoria fue necesario:

- Inyectar repositorios en BudgetService, InvoiceService, ServicesService y
  VehicleService, conservando constructores anteriores y las mismas condiciones.
- Registrar ServicesRepository e InvoiceRepository en DI.
- Hacer virtuales los métodos sustituidos de ClientRepository, VehicleRepository,
  BudgetRepository, ServicesRepository, InvoiceRepository, ServiceInspectionRepository,
  SucursalRepository, AuthAuditRepository y SupabaseStorageService. Las últimas
  clases selladas necesarias se abren a herencia para esos dobles.
- Permitir que SecurityRegressionFactory genere identidades de prueba con tenant
  y sub seleccionados; los valores predeterminados usados por las 101 pruebas
  anteriores se conservan.

No se cambiaron SQL, cuerpos de métodos de repositorios/almacenamiento, roles,
políticas, JWT de producción, Google ni lógica de transferencia. Se compararon
los nueve archivos de acceso externo con HEAD normalizando únicamente las
declaraciones virtual/sealed: los cuerpos coinciden.

## Límites y riesgos pendientes

Los dobles simulan el contrato de las consultas SQL; estas pruebas verifican los
controles de aplicación y los parámetros enviados, NO la ejecución real del SQL,
las constraints, RLS ni la concurrencia de PostgreSQL. El SQL se revisó estáticamente.
Un cambio futuro que quite un filtro SQL podría no ser detectado por estos dobles.

Los métodos de detalles e inspecciones sin tenant explícito dependen del control
del recurso padre. Las rutas actuales revisadas lo realizan; una futura reutilización
sin ese control es un riesgo potencial, no una vulnerabilidad demostrada aquí.
Queda fuera la validación contra cuentas reales/Google y el comportamiento temporal
de cambios concurrentes de propietario o tenant. No se usa ninguna base real.

No se encontró una vulnerabilidad reproducida en los escenarios de esta etapa.
Los primeros 10 fallos de pruebas nuevas eran una lectura errónea de tenantId
numérico: los DTO existentes lo exponen como string. Sólo se corrigió la prueba.

## Ejecución

```powershell
dotnet test tests/BoxService_BackEnd.UnitTests/BoxService_BackEnd.UnitTests.csproj --no-restore --filter FullyQualifiedName~TenantIsolationTests --logger "trx;LogFileName=stage4-isolation.trx" --results-directory TestResults/SecurityRegression
dotnet test tests/BoxService_BackEnd.UnitTests/BoxService_BackEnd.UnitTests.csproj --no-build --no-restore --logger "trx;LogFileName=stage4-full.trx" --results-directory TestResults/SecurityRegression
```

Resultado de la matriz: 172/172 aprobadas. Suite completa: 273/273 aprobadas,
sin fallos ni omisiones; conserva las 101 anteriores.

## Deuda técnica diferida

Los 56 métodos virtuales y los dobles por herencia se conservan para esta entrega.
Una migración futura a interfaces de repositorio permitiría reducir la superficie
 de extensión, pero no forma parte del cierre de seguridad. Los constructores
compatibles y registros DI actuales se mantienen. No agregar estado mutable a
estos repositorios sin revisar su ciclo de vida y las pruebas.

Estos dobles verifican controles de aplicación y parámetros; no ejecutan SQL real
ni prueban constraints, RLS o concurrencia. Una futura suite de integración con
base aislada podría cubrir esos límites, sin sustituir las pruebas actuales.

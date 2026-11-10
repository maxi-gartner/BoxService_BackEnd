# Permisos por taller, sesiones, facturación y métricas

## Preparación

Las migraciones 016_fecha_cobro_facturas.sql y 017_crear_detalle_factura.sql se
aplican solas como todas las demás (`dotnet run -- migrate` corre cada `.sql` de
`Database/migrations/` en orden, son idempotentes). No completa fechas históricas ni modifica importes existentes.
Las facturas ya cobradas sin fecha se informan como "sin fecha registrada" y no
se asignan artificialmente a ningún mes/año. Las facturas de cero existentes
deben revisarse manualmente; esta corrección no las altera.

Reiniciar backend y web, e iniciar sesión nuevamente. Los tokens viejos sin
versión de credencial se rechazan intencionalmente. No cambia el contrato de
login: el JWT sigue siendo opaco para web/mobile. No se cambia mobile.

## Reglas

- El backend comprueba en cada JWT validado que el usuario siga activo, con el
  mismo rol y taller; un taller desactivado pierde acceso. Restablecer la contraseña
  invalida las sesiones anteriores mediante una huella del hash, sin exponerlo.
- Portal: se comprueban cliente, taller y vínculo Google vigentes. Renovar una
  invitación invalida la sesión de ese cliente. No se agregan cuentas globales
  ni relaciones entre varios talleres al portal anterior.
- Inspecciones y fotografías comparten un filtro de pertenencia del service.
  Un identificador ajeno devuelve 404 antes de consultar o modificar la inspección
  o acceder a Storage. Las seis rutas quedan sujetas al mismo filtro.
- Crear vehículo exige cliente activo del mismo taller. Crear presupuesto/service
  exige vehículo del mismo taller. Una sucursal de otro taller o inactiva no se admite.
- Historiales y listados usan tenantId explícito, incluyendo el portal. Los joins
  no muestran nombres o datos vinculados de otros talleres por relaciones antiguas.
- Facturación con presupuesto: exige que esté finalizado y vinculado al mismo
  service, vehículo y taller. Copia sus conceptos y calcula el total en el servidor.
  Si el caller omite el presupuesto pero el service ya tiene uno, lo obtiene del
  taller; no permite sustituir sus precios por importes manuales.
- Facturación directa: permite service sin presupuesto. La web pide conceptos,
  cantidades y precios (manuales o del catálogo). El servidor valida cada ítem,
  calcula subtotales y exige total mayor a cero. No crea un presupuesto artificial.
- Las nuevas facturas conservan sus conceptos en detalle_factura. Factura e ítems
  se guardan juntos o se revierte toda la emisión. La impresión usa esa copia.
  Las facturas históricas sin detalle usan el presupuesto como respaldo si existe;
  no se inventa ni se reconstruye su detalle mediante una migración.
- La emisión bloquea la fila del service dentro de una transacción para impedir
  duplicación de facturas ante dos solicitudes concurrentes de esta aplicación.
- Cambiar a Cobrada registra paid_at al pasar de Emitida a Cobrada; repetir la
  solicitud no modifica esa fecha. Anular conserva el dato para consulta histórica,
  pero la factura anulada queda fuera de las sumas de facturación y cobros.
- Métricas se restringe a Dueño, tanto en la web como en el endpoint. Facturado
  usa fecha de emisión, Cobrado usa paid_at y límites de calendario de Argentina.
  Pendiente de cobro es el saldo total actual, sin limitarlo al mes de emisión.
  Los campos revenue anteriores conservan su significado de facturado por
  compatibilidad. Mobile deberá adaptar sus etiquetas en una modificación propia.
- El empleado consulta el catálogo sin formularios ni botones de escritura;
  el backend mantiene la misma restricción, también en el modo mock del catálogo.

## Verificación local

scripts/Test-TenantBillingSecurity.ps1 comprueba reglas de sesión, presupuesto
facturable, límites temporales y filtro de recursos propios/ajenos sin ejecutar
la aplicación ni conectar a Supabase. No sustituye las pruebas de integración.

## Pruebas manuales con los usuarios del equipo

1. tenant1 y empleado1/empleado2 deben ver los datos del primer taller; tenant2,
   los del segundo. Con el JWT de tenant2, usar un service de tenant1 en las seis
   rutas de inspección/fotos: siempre 404 y sin cambios en base ni Storage.
2. Desde tenant2 intentar crear vehículo con cliente de tenant1, presupuesto
   con vehículo de tenant1 y service con vehículo/sucursal ajenos: rechazo.
3. Desactivar empleado1 mientras tiene sesión: la siguiente petición devuelve 401.
   Restablecer su contraseña: el token anterior falla y puede ingresar con la nueva.
   Desactivar el primer taller: tampoco acceden su dueño, empleados ni clientes del portal.
4. empleado1 consulta catálogo sin botones de modificación. Una petición de
   escritura manual devuelve 403. Métricas devuelve 403 para empleado y 200 para dueño.
5. Service sin presupuesto: cargar lámpara $5000 y mano de obra $3000; emitir
   factura por $8000, recargar e imprimir ambos conceptos. Sin conceptos, precios
   inválidos o total cero: rechazo sin insertar factura. Un presupuesto ajeno, no
   finalizado o de otro vehículo/service también debe rechazarse.
6. Presupuesto válido: emite por el total esperado. Dos emisiones simultáneas
   del mismo service no deben insertar dos facturas.
7. Cobrar factura emitida el mes pasado: suma en facturado del mes pasado y
   cobrado del mes actual. Repetir el POST/PATCH no cambia la fecha de cobro.
8. Facturas pagadas históricas sin paid_at no se incluyen en cobros mensuales;
   la web avisa el importe sin fecha. No completar esas fechas a partir de emisión.
9. No se deben registrar contraseñas, JWT, invitaciones ni claves de Storage.

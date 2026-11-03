# Recordatorios de service

## Preparación

Ejecutar manualmente Database/migrations/015_crear_service_reminder_contacts.sql
en Supabase una sola vez, antes de usar la sección. El desarrollo no ejecuta
migraciones contra la base. Esta tabla no depende de las cuentas del portal.
RLS queda activado sin políticas de acceso directo desde el navegador. La
conexión PostgreSQL del backend necesita permisos apropiados para esta tabla.

## Funcionamiento

- Próximo service: sigue siendo seis meses y 10.000 km después del registrado.
- Los recordatorios usan FECHA, no kilometraje estimado. La fecha de hoy se
  calcula en America/Argentina/Buenos_Aires, no en la zona horaria del servidor.
- Se toma el último service del vehículo por fecha y, ante empate, por ID.
  Una próxima fecha nula no reactiva recordatorios de services más viejos.
- Desde siete días antes aparece pendiente; el día de vencimiento dice Vence hoy.
  Los vencidos permanecen hasta marcar contacto o registrar otro service.
- Enviar WhatsApp solo abre https://wa.me con un mensaje preparado. No llama
  APIs de mensajería, no envía mensajes y no marca contacto automáticamente.
- Marcar contactado requiere confirmación. Guarda fecha UTC y usuario del taller.
  Repetir la operación conserva el contacto original, no genera duplicados.
- Contactados muestra los contactos del ciclo vigente. El registro anterior se
  conserva en la base cuando un nuevo service reemplaza el aviso.
- Cargar teléfono actualiza la ficha del cliente, validando su taller. Incluir
  código de país, por ejemplo +5493431234567. No se adivina el código de país.
- Web: sección Recordatorios y contador de pendientes en el menú. Mobile:
  acceso desde Más y contador. Actualización al entrar, cada minuto mientras
  esté abierta la app y después de registrar un service o confirmar un contacto.
- Solo dueño y empleado del propio taller; el backend obtiene tenantId del JWT.
  No permite acceso del portal de clientes ni de superadmin.
- El modo mock muestra un error explícito: esta función usa el backend real.

## API

GET /service-reminders -> { items, pendingCount, today }
POST /service-reminders/{serviceId}/contact -> { contacted: true }
PATCH /service-reminders/clients/{clientId}/phone { phone } -> { updated: true }

## Prueba manual

1. Service con próxima fecha en ocho días: no aparece. En siete: aparece.
2. Probar hoy y ayer: Vence hoy y Vencido, respectivamente.
3. Abrir WhatsApp y cancelar: sigue pendiente. Marcar contactado: pasa a Contactados.
4. Actualizar o abrir en otro dispositivo: conserva el contacto.
5. Registrar nuevo service del mismo vehículo: desaparece el aviso del ciclo anterior.
6. Cliente sin teléfono o con número local incompleto: cargar teléfono y abrir WhatsApp.
7. Probar otro taller: no puede listar, contactar ni editar teléfonos del primero.
8. Repetir POST de contacto: no altera la fecha ni el usuario original.

No se inicia ninguna aplicación ni se envían mensajes durante las verificaciones.

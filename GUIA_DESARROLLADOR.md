# Guía simple del proyecto BoxService Backend

Este proyecto es el backend de un sistema de gestión para lubricentros y talleres. No contiene interfaz de usuario en este repositorio; lo que hace es recibir peticiones desde un frontend o desde Postman y responder con datos en formato JSON.

En otras palabras: este proyecto sirve como el "cerebro" del sistema. Recibe pedidos, valida información, consulta la base de datos y devuelve respuestas.

---

## 1. ¿Qué hace este proyecto?

El sistema permite manejar:

- Clientes
- Vehículos
- Presupuestos
- Services
- Facturas
- Estado del servidor y conexión con la base de datos

El flujo general es:

1. Un cliente o frontend hace una petición HTTP.
2. El router decide qué parte del sistema debe atenderla.
3. El controller recibe la petición.
4. El service aplica la lógica de negocio.
5. El repository consulta o modifica la base de datos.
6. Se devuelve una respuesta JSON estandarizada.

---

## 2. Estructura general del proyecto

### Capa de entrada / interfaz HTTP
Responsable de recibir peticiones web.

- Program.cs
- Server.cs
- Router/
- Controllers/
- ResponseHelper.cs

### Capa de lógica de negocio
Responsable de reglas, validaciones y procesos del sistema.

- Services/

### Capa de acceso a datos
Responsable de hablar con PostgreSQL mediante SQL.

- Repositories/
- Database/

### Modelos
Representan las entidades del sistema.

- Models/

---

## 3. Explicación de cada archivo importante

### Archivos principales

#### Program.cs
Archivo inicial del proyecto.

Qué hace:
- Lee la configuración de conexión desde appsettings.json.
- Configura la conexión a la base de datos.
- Puede ejecutar comandos especiales como:
  - --setup: crear las tablas
  - --reset: borrar y recrear las tablas
- Inicia el servidor.

#### Server.cs
Es el corazón del servidor HTTP.

Qué hace:
- Crea el listener de HTTP.
- Escucha peticiones en localhost:5001.
- Revisa la ruta pedida.
- Envía la petición al router correcto.
- Maneja errores generales.

#### ResponseHelper.cs
Archivo auxiliar para responder siempre de forma uniforme.

Qué hace:
- Genera respuestas JSON con formato estándar.
- Evita repetir estructura de respuesta en todos los controladores.
- Permite responder con éxito o error.

---

## 4. Router y controladores

### Router/
Los archivos de esta carpeta deciden a qué controlador debe ir cada petición.

#### IndexRouter.cs
Es el router principal.

Qué hace:
- Recibe la ruta completa.
- Si la ruta empieza con /health, delega a HealthRouter.
- Si es /api/clients o /api/clientes, delega a ClientRouter.
- Si es /api/vehiculos, delega a VehicleRouter.
- Si es /api/budgets, delega a BudgetRouter.
- Si es /api/services, delega a ServiceRouter.
- Si es /api/invoices, delega a InvoiceRouter.

#### ClientRouter.cs
Dirige las rutas relacionadas con clientes.

#### VehicleRouter.cs
Dirige las rutas relacionadas con vehículos.

#### BudgetRouter.cs
Dirige las rutas relacionadas con presupuestos.

#### ServiceRouter.cs
Dirige las rutas relacionadas con services.

#### InvoiceRouter.cs
Dirige las rutas relacionadas con facturas.

#### HealthRouter.cs
Dirige la ruta de salud del servidor.

### Controllers/
Los controllers reciben la petición HTTP y la traducen a acciones del sistema.

Importante: aquí no debe haber lógica compleja. Solo recibir, validar datos básicos y llamar al service.

#### ClientController.cs
Qué hace:
- Lista clientes.
- Busca un cliente por ID.
- Devuelve los vehículos de un cliente.
- Crea un cliente nuevo.

#### VehicleController.cs
Qué hace:
- Lista vehículos.
- Busca un vehículo por ID.
- Busca un vehículo por patente.
- Crea un vehículo.
- Tiene un endpoint de historial que todavía no está implementado.

#### BudgetController.cs
Qué hace:
- Lista presupuestos.
- Busca un presupuesto por ID.
- Crea un presupuesto.
- Cambia su estado.
- Aprueba un presupuesto y genera un service asociado.

#### ServicesController.cs
Qué hace:
- Lista services.
- Busca un service por ID.
- Crea un service.
- Agrega detalles a un service.

#### InvoiceController.cs
Qué hace:
- Lista facturas.
- Busca una factura por ID.
- Crea una factura.
- Cambia el estado de una factura.

#### HealthController.cs
Qué hace:
- Devuelve un estado simple del backend.
- Sirve para verificar que el servidor está vivo y que la base de datos responde.

---

## 5. Servicios: la lógica de negocio

### Services/
Aquí va la lógica del negocio. Es la capa donde se aplican reglas importantes.

#### ClientService.cs
Qué hace:
- Valida que el nombre del cliente sea obligatorio.
- Valida que el email tenga formato correcto si se envía.
- Llama al repository para guardar o consultar datos.

#### VehicleService.cs
Qué hace:
- Valida que el vehículo tenga datos obligatorios.
- Verifica que no exista otro vehículo con la misma patente.
- Crea el vehículo si todo está bien.

#### BudgetService.cs
Qué hace:
- Recibe un presupuesto en formato JSON.
- Crea los detalles del presupuesto.
- Genera el número del presupuesto.
- Puede cambiar el estado de un presupuesto.
- Puede aprobar un presupuesto y crear automáticamente un service.

#### ServicesService.cs
Qué hace:
- Valida datos del service.
- Calcula el próximo kilometraje y la próxima fecha de mantenimiento.
- Crea el service y sus detalles.

#### InvoiceService.cs
Qué hace:
- Valida que una factura no se cree dos veces para el mismo service.
- Calcula el total de la factura.
- Genera el número de factura.
- Cambia su estado.

---

## 6. Repositories: acceso a datos

### Repositories/
Aquí está la parte que habla con la base de datos usando SQL.

#### ClientRepository.cs
Qué hace:
- Trae todos los clientes activos.
- Busca un cliente por ID.
- Inserta un nuevo cliente.
- Trae los vehículos de un cliente.

#### VehicleRepository.cs
Qué hace:
- Trae todos los vehículos.
- Busca vehículo por ID.
- Busca vehículo por patente.
- Inserta un nuevo vehículo.

#### BudgetRepository.cs
Qué hace:
- Guarda presupuestos.
- Guarda detalles del presupuesto.
- Busca presupuestos por ID.
- Actualiza estados.
- Aprueba un presupuesto con transacción.

#### ServicesRepository.cs
Qué hace:
- Guarda services.
- Guarda detalles de cada service.
- Busca services por ID.

#### InvoiceRepository.cs
Qué hace:
- Guarda facturas.
- Busca facturas.
- Verifica si ya existe una factura para un service.
- Actualiza estados.

---

## 7. Modelos: las estructuras de datos

### Models/
Estas clases representan los objetos del sistema.

#### Client.cs
Representa a un cliente.

#### Vehicle.cs
Representa a un vehículo, con datos como patente, marca, modelo, año y kilometraje actual.

#### Budget.cs
Representa un presupuesto y sus detalles.

#### Service.cs
Representa un service de mantenimiento y sus detalles.

#### Invoice.cs
Representa una factura.

Importante: los modelos no contienen lógica de negocio. Solo guardan datos.

---

## 8. Base de datos

### Database/
Aquí está todo lo relacionado con PostgreSQL.

#### DatabaseConnection.cs
Qué hace:
- Guarda la cadena de conexión.
- Abre conexiones con la base de datos.
- Permite reutilizar la misma conexión en todo el sistema.

#### DatabaseSetup.cs
Qué hace:
- Ejecuta las migraciones SQL para crear las tablas.
- Puede borrar y recrear la base de datos si se pide.

#### migrations/
Archivos SQL que crean las tablas del sistema.

Ejemplo:
- 001_crear_clientes.sql
- 002_crear_vehiculos.sql
- 003_crear_presupuestos.sql
- 004_crear_services.sql
- 005_crear_facturas.sql
- 006_add_id_presupuesto_to_services.sql

---

## 9. Flujo simple de una petición

Por ejemplo, cuando se crea un cliente:

1. El frontend hace un POST a una ruta como /api/clientes.
2. El router dirige la petición al ClientController.
3. El controller lee el JSON recibido.
4. Llama al ClientService.
5. El service valida los datos.
6. El ClientRepository inserta al cliente en PostgreSQL.
7. El controller devuelve una respuesta JSON con el cliente creado.

---

## 10. Regla simple para entender el proyecto

Una forma fácil de pensar el proyecto es esta:

- Router = decide dónde ir
- Controller = recibe la solicitud
- Service = piensa y valida
- Repository = consulta la base de datos
- Model = representa los datos

---

## 11. Nota importante para futuros programadores

Este proyecto es un backend puro. No hay frontend aquí.

Eso significa que:
- el sistema expone APIs
- un frontend externo consume esas APIs
- esta carpeta no tiene vistas, componentes ni interfaz visual

Si se quiere agregar una nueva funcionalidad, normalmente se sigue este orden:

1. Crear o modificar el modelo
2. Crear o modificar el repository
3. Crear o modificar el service
4. Crear o modificar el controller
5. Añadir la ruta en el router

---

## 12. Resumen ultra simple

Si lo quieres resumir en una frase:

Este backend recibe pedidos web, aplica reglas de negocio y guarda o consulta información en la base de datos para hacer funcionar el sistema de BoxService.

# Pruebas unitarias — Etapa 1

## Requisitos y alcance

- SDK .NET 10.
- Restauración inicial de paquetes NuGet (requiere acceso al feed).
- xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1 y adaptador
  xunit.runner.visualstudio 3.1.1, ejecutados con VSTest.

El proyecto `tests/BoxService_BackEnd.UnitTests` referencia al backend, pero
no inicia el servidor ni ejecuta `Program.cs`. Las pruebas no leen credenciales,
no requieren PostgreSQL, no aplican migraciones y no llaman servicios externos.
No se incorporan interfaces ni mocks en esta etapa.

## Casos incluidos

| Clase | Casos | Comportamiento |
| --- | ---: | --- |
| BudgetWithDetailsTests | 3 | Total vacío, suma de subtotales almacenados y precisión decimal. |
| PagedResultTests | 6 | Cero resultados, páginas completas/incompletas, una página parcial y tamaño cero/negativo. |
| ClientServiceTests | 10 | Nombre efectivo inválido y email inválido, incluyendo espacios exteriores. |
| Total | 19 | Cada fila de InlineData cuenta como un caso ejecutado. |

`BudgetWithDetails.Total` suma `Subtotal`: no recalcula cantidad por precio
ni impone un redondeo a dos decimales.

`ClientService.Create` usa `Nombre ?? Name`. Un `Nombre` vacío no utiliza
`Name` como alternativa. Las pruebas documentan el comportamiento actual.
La validación del email es básica: no pretende validar todo el estándar de email.

Los casos de clientes construyen un `ClientRepository` real cuyo constructor
no abre conexiones. Todos terminan con `ArgumentException` antes de acceder
al repositorio. No agregar casos de creación exitosa aquí sin aislar primero
la persistencia en una etapa posterior. No configurar `DatabaseConnection`
ni cargar `appsettings.json` para ejecutar esta suite.

## Ejecutar desde la raíz del repositorio

Solo el proyecto unitario:

```powershell
dotnet test .\tests\BoxService_BackEnd.UnitTests\BoxService_BackEnd.UnitTests.csproj
```

Listar los casos descubiertos:

```powershell
dotnet test .\tests\BoxService_BackEnd.UnitTests\BoxService_BackEnd.UnitTests.csproj --list-tests
```

Ejecutar la solución y guardar resultados TRX:

```powershell
dotnet test .\BoxService_BackEnd.sln --configuration Release --logger "trx;LogFileName=unit-tests.trx" --results-directory .\TestResults
```

Ejecutar una clase:

```powershell
dotnet test .\tests\BoxService_BackEnd.UnitTests\BoxService_BackEnd.UnitTests.csproj --filter "FullyQualifiedName~ClientServiceTests"
```

Compilar toda la solución:

```powershell
dotnet build .\BoxService_BackEnd.sln --configuration Release
```

Después de restaurar y compilar Release, se pueden ejecutar las pruebas sin
restauración ni compilación con `--no-restore --no-build --configuration Release`.
En Visual Studio también se pueden ejecutar desde el Explorador de pruebas.

## Interpretar los resultados

La suite inicial debe descubrir y ejecutar 19 casos: 19 aprobados, 0 fallidos
y 0 omitidos. Una compilación correcta o una ejecución con cero pruebas no
demuestra que la suite haya pasado. `dotnet test` devuelve código 0 cuando
las pruebas pasan y un código distinto de cero ante fallos.

El reporte queda en `TestResults/unit-tests.trx`. Las reglas existentes de
`.gitignore` excluyen `TestResults/`, `coverage/`, `bin/`, `obj/` y temporales;
no versionar estos resultados. Para comprobar detección de fallos durante el
desarrollo, se puede cambiar temporalmente una expectativa en una prueba,
confirmar que falla y restaurarla antes de ejecutar de nuevo toda la suite.

## Separación de responsabilidades

Los scripts `scripts/Test-*.ps1` son pruebas de integración/flujo: requieren
HTTP y/o base de datos. No forman parte de esta suite ni se ejecutan con ella.
No ejecutar esos scripts sobre una base compartida como sustituto de estas
pruebas unitarias.

La Etapa 1 no prueba creación exitosa de clientes, SQL, transacciones, login,
permisos, middleware ni servicios externos. Tampoco modifica el workflow de CI.
La incorporación de mocks, cobertura y pruebas de integración queda para
entregas posteriores.

El proyecto web excluye `tests/**` de Compile, Content, None y EmbeddedResource
para evitar compilar o publicar pruebas y sus artefactos dentro de la aplicación.

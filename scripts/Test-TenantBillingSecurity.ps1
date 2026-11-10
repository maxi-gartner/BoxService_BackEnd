param([string]$ProjectPath = (Join-Path $PSScriptRoot '../BoxService_BackEnd.csproj'))
$ErrorActionPreference = 'Stop'
# Pruebas locales; no inicia BoxService ni usa una base de datos.
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('boxservice-security-tests-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null
$escapedProject = [System.Security.SecurityElement]::Escape([System.IO.Path]::GetFullPath($ProjectPath))
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit></PropertyGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><ProjectReference Include="$escapedProject" AdditionalProperties="UseAppHost=false" /></ItemGroup>
</Project>
"@
[System.IO.File]::WriteAllText((Join-Path $testRoot 'SecurityTests.csproj'), $project)
@'
using System.Security.Claims;
using BoxService_BackEnd.Api;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using Microsoft.AspNetCore.Http;

var passed = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; }
UserAccount User(string role = "dueno", int? tenant = 1) => new() {
    UserId = 10, Username = "tenant1", Role = role, TenantId = tenant,
    Active = true, PasswordHash = "hash-de-prueba-no-es-una-credencial" };
ClaimsPrincipal Principal(UserAccount user, string? version = "current", string? tenantOverride = null) {
    var claims = new List<Claim> { new("sub", user.UserId.ToString()), new("name", user.Username), new("role", user.Role) };
    if (user.TenantId.HasValue || tenantOverride is not null) claims.Add(new("tenantId", tenantOverride ?? user.TenantId!.Value.ToString()));
    if (version is not null) claims.Add(new(SessionSecurity.VersionClaim, version == "current" ? SessionSecurity.Version(user.PasswordHash) : version));
    return new(new ClaimsIdentity(claims, "test", "name", "role"));
}
var owner = User(); var tenant = new Tenant { TenantId = 1, Active = true };
Check(SessionSecurity.IsValidStaff(Principal(owner), owner, tenant), "Dueño con acceso vigente");
var employee = User("empleado");
Check(SessionSecurity.IsValidStaff(Principal(employee), employee, tenant), "Empleado del mismo taller");
Check(!SessionSecurity.IsValidStaff(Principal(owner), null, tenant), "Usuario eliminado");
owner.Active = false;
Check(!SessionSecurity.IsValidStaff(Principal(owner), owner, tenant), "Usuario desactivado"); owner.Active = true;
tenant.Active = false;
Check(!SessionSecurity.IsValidStaff(Principal(owner), owner, tenant), "Taller desactivado"); tenant.Active = true;
Check(!SessionSecurity.IsValidStaff(Principal(owner), owner, new Tenant {TenantId=2, Active=true}), "No acepta otro taller");
Check(!SessionSecurity.IsValidStaff(Principal(owner, tenantOverride:"2"), owner, tenant), "JWT de otro taller");
Check(!SessionSecurity.IsValidStaff(Principal(owner, version:null), owner, tenant), "JWT anterior sin versión");
Check(!SessionSecurity.IsValidStaff(Principal(owner, version:"vieja"), owner, tenant), "Versión inválida");
var old = Principal(owner); owner.PasswordHash = "otra-contraseña-hasheada-de-prueba";
Check(!SessionSecurity.IsValidStaff(old, owner, tenant), "Reset de contraseña revoca sesión");
owner = User(); old = Principal(owner); owner.Role = "empleado";
Check(!SessionSecurity.IsValidStaff(old, owner, tenant), "Cambio de rol revoca sesión");
owner = User(); old = Principal(owner); owner.Username = "otro";
Check(!SessionSecurity.IsValidStaff(old, owner, tenant), "Cambio de usuario revoca sesión");
var admin = User("superadmin", null);
Check(SessionSecurity.IsValidStaff(Principal(admin), admin, null), "Administrador global sin tenant");
Check(!SessionSecurity.IsValidStaff(Principal(admin, tenantOverride:"1"), admin, tenant), "Administrador con tenant no válido");
var unknown = User("desconocido");
Check(!SessionSecurity.IsValidStaff(Principal(unknown), unknown, tenant), "Rol no reconocido");
Check(!SessionSecurity.IsValidStaff(new ClaimsPrincipal(new ClaimsIdentity()), User(), tenant), "Identidad no autenticada");

var service = new Service { ServiceId = 11, VehicleId = 5, TenantId = 1 };
Budget Budget() => new() { BudgetId = 7, ServiceId = 11, VehicleId = 5, TenantId = 1, Status = "completed" };
Check(InvoiceRules.Validate(service, Budget(), 1, 25000) is null, "Factura de presupuesto finalizado");
Check(InvoiceRules.Validate(service, null, 1, 25000) is null, "Service directo admite importe definido");
Check(InvoiceRules.Validate(service, null, 1, 0) is not null, "Service directo no emite total cero");
Check(InvoiceRules.Validate(service, Budget(), 2, 25000) is not null, "Service de otro taller");
var b = Budget(); b.TenantId = 2;
Check(InvoiceRules.Validate(service, b, 1, 25000) is not null, "Presupuesto de otro taller");
b = Budget(); b.VehicleId = 999;
Check(InvoiceRules.Validate(service, b, 1, 25000) is not null, "Presupuesto de otro vehículo");
b = Budget(); b.ServiceId = 999;
Check(InvoiceRules.Validate(service, b, 1, 25000) is not null, "Presupuesto de otro service");
b = Budget(); b.ServiceId = null;
Check(InvoiceRules.Validate(service, b, 1, 25000) is not null, "Presupuesto sin vínculo");
foreach (var status in new[] {"draft", "sent", "approved", "rejected"}) {
    b = Budget(); b.Status = status;
    Check(InvoiceRules.Validate(service, b, 1, 25000) is not null, "No factura estado " + status);
}
Check(InvoiceRules.Validate(service, Budget(), 1, 0) is not null, "No emite total cero");
Check(InvoiceRules.Validate(service, Budget(), 1, -100) is not null, "No emite total negativo");
Check(InvoiceRules.PeriodStartUtc(new DateTime(2026,11,1)) == new DateTime(2026,11,1,3,0,0,DateTimeKind.Utc), "Mes de cobro: inicio Argentina");
Check(InvoiceRules.PeriodStartUtc(new DateTime(2027,1,1)) == new DateTime(2027,1,1,3,0,0,DateTimeKind.Utc), "Año de cobro: inicio Argentina");

void Reject(Action action, string label) {
    var rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; }
    Check(rejected, label);
}
InvoiceDetailRequest Item(string type = "labor", string description = "Mano de obra", decimal quantity = 1, decimal price = 3000) =>
    new() { Type = type, Description = description, Quantity = quantity, UnitPrice = price };
var lamp = InvoiceRules.BuildDetails(new[] { Item("part", "Lámpara", 1, 5000), Item() });
Check(lamp.Count == 2 && lamp.Sum(d => d.Subtotal) == 8000, "Lámpara y mano de obra suman 8000");
var rounded = InvoiceRules.BuildDetails(new[] { Item(quantity: 1.5m, price: 10.01m) });
Check(rounded[0].Subtotal == 15.02m, "Redondeo monetario del servidor");
Check(InvoiceRules.BuildDetails(new[] { Item(description: "  Trabajo  ") })[0].Description == "Trabajo", "Limpia descripción");
Reject(() => InvoiceRules.BuildDetails(null), "Rechaza conceptos ausentes");
Reject(() => InvoiceRules.BuildDetails(Array.Empty<InvoiceDetailRequest>()), "Rechaza lista vacía");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(type: "otro") }), "Rechaza tipo desconocido");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(description: " ") }), "Rechaza descripción vacía");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(description: new string('x', 201)) }), "Límite de descripción");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(quantity: 0) }), "Rechaza cantidad cero");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(quantity: -1) }), "Rechaza cantidad negativa");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(quantity: 1.001m) }), "Límite de decimales de cantidad");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(price: -1) }), "Rechaza precio negativo");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(price: 1.001m) }), "Límite de decimales del precio");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(price: 100000000) }), "Límite de precio");
Reject(() => InvoiceRules.BuildDetails(new[] { Item(quantity: 100000, price: 100000) }), "Límite de subtotal");
Reject(() => InvoiceRules.BuildDetails(Enumerable.Repeat(Item(price: 60000000), 2).ToArray()), "Límite de total");
Reject(() => InvoiceRules.BuildDetails(Enumerable.Repeat(Item(), 101).ToArray()), "Límite de conceptos");
Reject(() => InvoiceRules.BuildDetails(new InvoiceDetailRequest[] { null! }), "Rechaza concepto nulo");

var access = new FakeAccess(); var filter = new TenantServiceFilter(access);
foreach (var operation in new[] {"Filtro común de inspección y fotos"}) {
    foreach (var allowed in new[] {true,false}) {
        access.Allowed = allowed; var called = false;
        var http = new DefaultHttpContext(); http.User = Principal(User()); http.Request.RouteValues["serviceId"] = "11";
        var result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(http), _ => { called=true; return ValueTask.FromResult<object?>("ok"); });
        Check(called == allowed, operation + ": permiso antes del handler");
        Check(access.ServiceId == 11 && access.TenantId == 1, operation + ": usa el taller del JWT");
        if(!allowed) Check(result is IStatusCodeHttpResult {StatusCode:404}, operation + ": no revela información ajena");
    }
}
var invalidContext = new DefaultHttpContext(); invalidContext.User = Principal(User()); invalidContext.Request.RouteValues["serviceId"] = "no-es-id";
var invalidCalled = false;
await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(invalidContext), _ => {invalidCalled=true; return ValueTask.FromResult<object?>(null);});
Check(!invalidCalled, "ID inválido nunca llega al handler");
Console.WriteLine($"Seguridad y facturación: {passed} comprobaciones correctas. Sin conexión a base de datos.");

sealed class FakeAccess : ITenantServiceAccess {
    public bool Allowed; public int ServiceId; public int TenantId;
    public bool Exists(int serviceId,int tenantId) { ServiceId=serviceId;TenantId=tenantId;return Allowed; }
}
'@ | Set-Content -LiteralPath (Join-Path $testRoot 'Program.cs') -Encoding utf8
& dotnet run --project (Join-Path $testRoot 'SecurityTests.csproj') --configuration Debug -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de seguridad y facturación.' }

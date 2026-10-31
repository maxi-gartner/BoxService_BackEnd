$ErrorActionPreference = 'Stop'
# Pruebas sin dependencias externas, aplicación ni base de datos.
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('boxservice-reminder-tests-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($testRoot) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../Services/ServiceReminderRules.cs') -Destination (Join-Path $testRoot 'ServiceReminderRules.cs')
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $testRoot 'RulesTests.csproj') -Encoding utf8
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /></packageSources></configuration>
'@ | Set-Content -LiteralPath (Join-Path $testRoot 'NuGet.Config') -Encoding utf8
@'
using BoxService_BackEnd.Services;
var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
var today = new DateOnly(2026, 10, 31);
Check(ServiceReminderRules.IsDue(today.AddDays(7), today), "Incluye séptimo día");
Check(!ServiceReminderRules.IsDue(today.AddDays(8), today), "Excluye octavo día");
Check(ServiceReminderRules.IsDue(today.AddDays(-200), today), "Vencidos permanecen");
Check(ServiceReminderRules.Status(today.AddDays(1), today) == "upcoming", "Próximo");
Check(ServiceReminderRules.Status(today, today) == "today", "Vence hoy");
Check(ServiceReminderRules.Status(today.AddDays(-1), today) == "overdue", "Vencido");
Check(ServiceReminderRules.IsDue(new DateOnly(2028, 3, 1), new DateOnly(2028, 2, 23)), "Año bisiesto");
Check(ServiceReminderRules.Today(DateTimeOffset.Parse("2026-11-01T01:00:00Z")) == today, "Fecha Argentina antes de medianoche");
Check(ServiceReminderRules.Today(DateTimeOffset.Parse("2026-11-01T03:00:00Z")) == today.AddDays(1), "Fecha Argentina después de medianoche");
Check(ServiceReminderRules.PhoneDigits(null) is null, "Sin teléfono");
Check(ServiceReminderRules.PhoneDigits("3431234567") is null, "No adivina código de país");
Check(ServiceReminderRules.PhoneDigits("+54 9 343 123-4567") == "5493431234567", "Normaliza teléfono");
Check(ServiceReminderRules.PhoneDigits("0054 9 343 1234567") == "5493431234567", "Prefijo internacional 00");
Check(ServiceReminderRules.PhoneDigits("+01234567890") is null, "No admite cero inicial");
Check(ServiceReminderRules.PhoneDigits("+1234567890123456") is null, "Límite internacional");
Check(ServiceReminderRules.WhatsAppUrl(null, "Ana", "Taller", "Auto", today) is null, "Sin enlace para teléfono inválido");
var url = ServiceReminderRules.WhatsAppUrl("+5493431234567", "Ana & José", "Taller Norte", "Auto A", today)!;
Check(url.StartsWith("https://wa.me/5493431234567?text="), "Destino WhatsApp");
Check(!url.Contains("Ana & José"), "Codifica contenido del enlace");
var message = Uri.UnescapeDataString(url.Split("?text=")[1]);
Check(message.Contains("Ana & José") && message.Contains("Taller Norte") && message.Contains("31/10/2026"), "Mensaje y fecha");
Console.WriteLine($"Recordatorios: {checks} comprobaciones correctas.");
'@ | Set-Content -LiteralPath (Join-Path $testRoot 'Program.cs') -Encoding utf8
& dotnet run --project (Join-Path $testRoot 'RulesTests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de recordatorios.' }

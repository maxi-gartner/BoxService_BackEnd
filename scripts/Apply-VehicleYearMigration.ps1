$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$config = Get-Content (Join-Path $root 'appsettings.json') -Raw | ConvertFrom-Json
$dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
$logging = Get-ChildItem (Join-Path $dotnetRoot 'shared/Microsoft.AspNetCore.App/*/Microsoft.Extensions.Logging.Abstractions.dll') |
    Sort-Object FullName -Descending | Select-Object -First 1
[void][Reflection.Assembly]::LoadFrom($logging.FullName)
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin/Debug/net8.0/Npgsql.dll'))
$connection = [Npgsql.NpgsqlConnection]::new($config.ConnectionStrings.DefaultConnection)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = Get-Content (Join-Path $root 'Database/migrations/009_vehicle_year_nullable.sql') -Raw
    [void]$command.ExecuteNonQuery()
    $command.Dispose()
    Write-Host 'Migration 009 applied: vehicle year accepts null'
} finally { $connection.Dispose() }

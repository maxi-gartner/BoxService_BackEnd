param([string]$BaseUrl = 'http://localhost:5001')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$config = Get-Content (Join-Path $root 'appsettings.json') -Raw | ConvertFrom-Json
$dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
$logging = Get-ChildItem (Join-Path $dotnetRoot 'shared/Microsoft.AspNetCore.App/*/Microsoft.Extensions.Logging.Abstractions.dll') |
    Sort-Object FullName -Descending | Select-Object -First 1
[void][Reflection.Assembly]::LoadFrom($logging.FullName)
[void][Reflection.Assembly]::LoadFrom((Join-Path $root 'bin/Debug/net8.0/Npgsql.dll'))
$connection = [Npgsql.NpgsqlConnection]::new($config.ConnectionStrings.DefaultConnection)
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(40)
$key = if ($config.ApiKey) { $config.ApiKey } else { 'boxservice-dev-key' }
$http.DefaultRequestHeaders.Add('X-Api-Key', $key)
$marker = 'CodexTest-' + [Guid]::NewGuid().ToString('N')
$plate = 'T' + [Guid]::NewGuid().ToString('N').Substring(0, 9).ToUpperInvariant()
$clientId = $null

function Request([string]$method, [string]$path, [int]$expected, $body = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), $BaseUrl + $path)
    if ($null -ne $body) {
        $request.Content = [System.Net.Http.StringContent]::new(($body | ConvertTo-Json), [Text.Encoding]::UTF8, 'application/json')
    }
    $response = $null
    try {
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if ([int]$response.StatusCode -ne $expected) { throw "Expected $expected, got $([int]$response.StatusCode): $($json.error.message)" }
        if ($expected -ge 400) {
            if ($json.success -ne $false -or $null -ne $json.data -or $json.error.code -ne $expected) { throw 'Invalid error envelope' }
        } elseif ($json.success -ne $true -or $null -ne $json.error) { throw 'Invalid success envelope' }
        Write-Host "PASS $method $path ($expected)"
        return $json.data
    } finally {
        if ($response) { $response.Dispose() }
        $request.Dispose()
    }
}

try {
    $connection.Open()
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'INSERT INTO clientes (nombre) VALUES (@name) RETURNING id_cliente'
    [void]$cmd.Parameters.AddWithValue('name', $marker)
    $clientId = [int]$cmd.ExecuteScalar()
    $cmd.Dispose()
    $body = @{ clientId=$clientId; brand=' Test '; model=' Integration '; plate=$plate.ToLowerInvariant(); currentMileage=123; year=2020 }
    $created = Request POST /vehicles 201 $body
    $saved = Request GET "/vehicles/$($created.vehicleId)" 200
    if ($saved.plate -ne $plate -or $saved.brand -ne 'Test' -or $saved.model -ne 'Integration' -or $saved.currentMileage -ne 123 -or $saved.year -ne 2020 -or $saved.clientId -ne $clientId) { throw 'Persistence mismatch' }
    $history = @(Request GET "/vehicles/$($created.vehicleId)/history" 200)
    if ($history.Count -ne 0) { throw 'New vehicle should have empty history' }
    $associated = @(Request GET "/clients/$clientId/vehicles" 200)
    if ($created.vehicleId -notin $associated.vehicleId) { throw 'Vehicle missing from client' }
    $body.plate = ' ' + $plate.ToLowerInvariant() + ' '
    Request POST /vehicles 400 $body | Out-Null
    $body.clientId = -1
    Request POST /vehicles 400 $body | Out-Null
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'SELECT COALESCE(MAX(id_cliente), 0) + 1 FROM clientes'
    $body.clientId = [int]$cmd.ExecuteScalar()
    $cmd.Dispose()
    $body.plate = 'T' + [Guid]::NewGuid().ToString('N').Substring(0,9).ToUpperInvariant()
    Request POST /vehicles 400 $body | Out-Null
    Write-Host 'PASS creation, persistence, associations, duplicate and missing client checks'
} finally {
    try {
        if ($null -ne $clientId) {
            $cmd = $connection.CreateCommand()
            $cmd.CommandText = 'DELETE FROM vehiculos WHERE id_cliente = @id AND patente = @plate AND marca = @brand AND modelo = @model; DELETE FROM clientes WHERE id_cliente = @id AND nombre = @name;'
            [void]$cmd.Parameters.AddWithValue('id', $clientId)
            [void]$cmd.Parameters.AddWithValue('plate', $plate)
            [void]$cmd.Parameters.AddWithValue('brand', 'Test')
            [void]$cmd.Parameters.AddWithValue('model', 'Integration')
            [void]$cmd.Parameters.AddWithValue('name', $marker)
            [void]$cmd.ExecuteNonQuery()
            $cmd.Dispose()
            Write-Host 'CLEANUP temporary client and vehicle removed'
        }
    } finally { $connection.Dispose(); $http.Dispose() }
}

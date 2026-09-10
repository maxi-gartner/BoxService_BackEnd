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
$http.Timeout = [TimeSpan]::FromSeconds(90)
$key = if ($config.ApiKey) { $config.ApiKey } else { 'boxservice-dev-key' }
$marker = 'CodexModules-' + [Guid]::NewGuid().ToString('N')
$clientId = $null
$script:passed = 0

function Assert($condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Request([string]$method, [string]$path, [int]$expected, $body = $null, [switch]$NoKey, [switch]$Raw) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method), $BaseUrl + $path)
    if (-not $NoKey) { $request.Headers.Add('X-Api-Key', $key) }
    if ($null -ne $body) {
        $text = if ($Raw) { [string]$body } else { $body | ConvertTo-Json -Depth 8 }
        $request.Content = [System.Net.Http.StringContent]::new($text, [Text.Encoding]::UTF8, 'application/json')
    }
    $response = $null
    try {
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        Assert ($response.Content.Headers.ContentType.MediaType -eq 'application/json') 'Expected JSON'
        Assert ([int]$response.StatusCode -eq $expected) "$method $path expected $expected, got $([int]$response.StatusCode): $($json.error.message)"
        if ($expected -ge 400) {
            Assert ($json.success -eq $false -and $null -eq $json.data -and $json.error.code -eq $expected) 'Invalid error envelope'
        } else { Assert ($json.success -eq $true -and $null -eq $json.error) 'Invalid success envelope' }
        $script:passed++
        Write-Host "PASS $method $path ($expected)"
        return $json.data
    } finally {
        if ($response) { $response.Dispose() }
        $request.Dispose()
    }
}

try {
    Request GET /health 200 | Out-Null
    foreach ($resource in @('budgets','services','invoices','catalog')) {
        Request GET "/$resource" 401 -NoKey | Out-Null
        Request GET "/$resource" 200 | Out-Null
        Request POST "/$resource" 400 '{' -Raw | Out-Null
        Request POST "/$resource" 400 @{} | Out-Null
    }
    $connection.Open()
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'INSERT INTO clientes (nombre) VALUES (@name) RETURNING id_cliente'
    [void]$cmd.Parameters.AddWithValue('name', $marker)
    $clientId = [int]$cmd.ExecuteScalar()
    $cmd.Dispose()
    $vehicle = Request POST /vehicles 201 @{ clientId=$clientId; brand='Test'; model=$marker; plate=('T'+[Guid]::NewGuid().ToString('N').Substring(0,9)); currentMileage=100 }
    $vid = $vehicle.vehicleId

    $catalog = Request POST /catalog 201 @{ name=$marker; type='labor'; price=25.50 }
    Assert ($catalog.tenantId -and $catalog.price -eq 25.50) 'Catalog DTO mismatch'
    Request PATCH "/catalog/$($catalog.catalogId)" 200 @{price=30} | Out-Null
    $items = @(Request GET /catalog 200)
    Assert (($items | Where-Object catalogId -eq $catalog.catalogId).price -eq 30) 'Catalog price was not saved'
    Request PATCH "/catalog/$($catalog.catalogId)" 400 @{type='invalid'} | Out-Null
    Request PATCH "/catalog/$($catalog.catalogId)" 400 @{price=-1} | Out-Null
    Request POST /catalog 400 @{name=$marker} | Out-Null

    $detail = @{type='labor'; description=$marker; quantity=2; unitPrice=30}
    $budget = Request POST /budgets 201 @{vehicleId=$vid; notes=$marker; details=@($detail)}
    $bid = $budget.budgetId
    Assert ($budget.tenantId -and $budget.number -match '^P-\d+$' -and $budget.status -eq 'draft') 'Budget DTO mismatch'
    $readBudget = Request GET "/budgets/$bid" 200
    Assert ($readBudget.total -eq 60 -and $readBudget.details.Count -eq 1 -and $readBudget.budget.tenantId) 'Budget details/total mismatch'
    Request POST /budgets 400 @{vehicleId=$vid; details=@($null)} | Out-Null
    Request PATCH "/budgets/$bid" 400 @{status='invalid'} | Out-Null
    Request PATCH "/budgets/$bid" 200 @{status='sent'} | Out-Null
    Request PATCH "/budgets/$bid" 200 @{status='approved'} | Out-Null
    Request PATCH "/budgets/$bid" 400 @{status='approved'} | Out-Null
    Request PUT "/budgets/$bid/service" 400 @{serviceId=0} | Out-Null

    # A detail too long fails in PostgreSQL after the header INSERT: verify rollback.
    Request POST /budgets 400 @{vehicleId=$vid; details=@(@{type='labor'; description=('x'*201); quantity=1; unitPrice=1})} | Out-Null
    $allBudgets = @(Request GET /budgets 200)
    Assert (@($allBudgets | Where-Object vehicleId -eq $vid).Count -eq 1) 'Failed budget left an orphan header'

    $service = Request POST /services 201 @{vehicleId=$vid; date='2026-09-09'; mileage=500; serviceType='Test'; notes=$marker}
    $sid = $service.serviceId
    Assert ($service.tenantId -and $service.nextMileage -eq 10500 -and $service.nextDate -eq '2027-03-09') 'Service next maintenance mismatch'
    $savedService = Request GET "/services/$sid" 200
    Assert ($savedService.date -eq '2026-09-09') 'Service date was not saved'
    $savedVehicle = Request GET "/vehicles/$vid" 200
    Assert ($savedVehicle.currentMileage -eq 500) 'Mileage was not updated'
    Request POST /services 201 @{vehicleId=$vid; date='2026-09-08'; mileage=200; serviceType='Test'; notes=$marker} | Out-Null
    $savedVehicle = Request GET "/vehicles/$vid" 200
    Assert ($savedVehicle.currentMileage -eq 500) 'Mileage must never decrease'
    $serviceDetail = Request POST "/services/$sid/details" 201 @{description=$marker; done=$false; serviceId=2147483647}
    Assert ($serviceDetail.serviceId -eq $sid -and $serviceDetail.done -eq $false) 'Detail must use route ID and preserve done=false'
    $details = @(Request GET "/services/$sid/details" 200)
    Assert ($details.Count -eq 1 -and $details[0].done -eq $false) 'Service detail was not saved'
    Request POST "/services/$sid/details" 400 @{description=' '} | Out-Null
    $history = @(Request GET "/vehicles/$vid/history" 200)
    Assert ($history.Count -eq 2) 'History should include both services'

    Request PUT "/budgets/$bid/service" 200 @{serviceId=$sid} | Out-Null
    $completed = Request GET "/budgets/$bid" 200
    Assert ($completed.budget.status -eq 'completed' -and $completed.budget.serviceId -eq $sid) 'Budget link was not saved'
    Request PATCH "/budgets/$bid" 400 @{status='approved'} | Out-Null
    Request PUT "/budgets/$bid/service" 400 @{serviceId=$sid} | Out-Null

    $invoice = Request POST /invoices 201 @{serviceId=$sid; budgetId=$bid}
    $iid = $invoice.invoiceId
    Assert ($invoice.total -eq 60 -and $invoice.tenantId -and $invoice.number -match '^F-\d+$') 'Invoice total/DTO mismatch'
    $savedInvoice = Request GET "/invoices/$iid" 200
    Assert ($savedInvoice.total -eq 60 -and $savedInvoice.budgetId -eq $bid) 'Invoice was not saved'
    Request POST /invoices 400 @{serviceId=$sid; budgetId=$bid} | Out-Null
    Request PATCH "/invoices/$iid" 400 @{status='invalid'} | Out-Null
    Request PATCH "/invoices/$iid" 200 @{status='paid'} | Out-Null
    Request PATCH "/invoices/$iid" 200 @{status='cancelled'} | Out-Null
    Request PATCH "/invoices/$iid" 400 @{status='paid'} | Out-Null

    foreach ($resource in @('budgets','services','invoices')) {
        Request GET "/$resource/2147483647" 404 | Out-Null
    }
    Request GET /services/2147483647/details 404 | Out-Null
    Request POST /services/2147483647/details 404 @{description='Test'} | Out-Null
    Request PATCH /catalog/2147483647 404 @{price=1} | Out-Null
    Request DELETE "/catalog/$($catalog.catalogId)" 200 | Out-Null
    Request DELETE "/catalog/$($catalog.catalogId)" 404 | Out-Null
    Write-Host "PASS $script:passed HTTP checks and workflow assertions"
} finally {
    try {
        if ($null -ne $clientId) {
            $cmd = $connection.CreateCommand()
            # Restrict every deletion to the fixture client or exact random catalog name.
            $cmd.CommandText = @'
DELETE FROM facturas WHERE id_service IN (SELECT s.id_service FROM services s JOIN vehiculos v USING(id_vehiculo) WHERE v.id_cliente=@id);
DELETE FROM detalle_presupuesto WHERE id_presupuesto IN (SELECT p.id_presupuesto FROM presupuestos p JOIN vehiculos v USING(id_vehiculo) WHERE v.id_cliente=@id);
UPDATE services SET id_presupuesto=NULL WHERE id_vehiculo IN (SELECT id_vehiculo FROM vehiculos WHERE id_cliente=@id);
DELETE FROM presupuestos WHERE id_vehiculo IN (SELECT id_vehiculo FROM vehiculos WHERE id_cliente=@id);
DELETE FROM detalle_service WHERE id_service IN (SELECT s.id_service FROM services s JOIN vehiculos v USING(id_vehiculo) WHERE v.id_cliente=@id);
DELETE FROM services WHERE id_vehiculo IN (SELECT id_vehiculo FROM vehiculos WHERE id_cliente=@id);
DELETE FROM vehiculos WHERE id_cliente=@id;
DELETE FROM clientes WHERE id_cliente=@id AND nombre=@name;
DELETE FROM catalogo_servicios WHERE nombre=@name;
'@
            [void]$cmd.Parameters.AddWithValue('id', $clientId)
            [void]$cmd.Parameters.AddWithValue('name', $marker)
            [void]$cmd.ExecuteNonQuery()
            $cmd.Dispose()
            Write-Host 'CLEANUP all temporary workflow records removed'
        }
    } finally { $connection.Dispose(); $http.Dispose() }
}

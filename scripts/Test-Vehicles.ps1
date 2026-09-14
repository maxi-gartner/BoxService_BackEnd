param(
    [string]$BaseUrl = 'http://localhost:5001',
    [string]$ApiKey = $env:BOXSERVICE_API_KEY
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ApiKey)) { $ApiKey = 'boxservice-dev-key' }
$http = [System.Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(25)
$script:failures = 0

function Test-Response {
    param([string]$Method, [string]$Path, [int]$ExpectedStatus, [string]$Body, [switch]$NoKey)
    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method), $BaseUrl.TrimEnd('/') + $Path)
    if (-not $NoKey) { $request.Headers.Add('X-Api-Key', $ApiKey) }
    if ($PSBoundParameters.ContainsKey('Body')) {
        $request.Content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, 'application/json')
    }
    $response = $null
    try {
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        $status = [int]$response.StatusCode
        if ($response.Content.Headers.ContentType.MediaType -ne 'application/json') { throw 'Expected JSON content type' }
        foreach ($name in @('success', 'data', 'error')) {
            if ($name -notin $content.PSObject.Properties.Name) { throw "Missing envelope field: $name" }
        }
        if ($status -ne $ExpectedStatus) { throw "Expected $ExpectedStatus, received $status" }
        if ($status -ge 400) {
            if ($content.success -ne $false -or $null -ne $content.data -or $content.error.code -ne $status) {
                throw 'Invalid error envelope'
            }
        } elseif ($content.success -ne $true -or $null -ne $content.error) { throw 'Invalid success envelope' }
        Write-Host "PASS $Method $Path ($status)"
        return $content
    } catch {
        $script:failures++
        Write-Host "FAIL $Method $Path : $($_.Exception.Message)"
    } finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
    }
}

try {
    Test-Response GET /vehicles 401 -NoKey | Out-Null
    Test-Response GET /vehicles/invalid 404 | Out-Null
    Test-Response DELETE /vehicles 405 | Out-Null
    Test-Response POST /vehicles 400 -Body '{' | Out-Null
    Test-Response POST /vehicles 400 -Body '{}' | Out-Null
    Test-Response POST /vehicles 400 -Body '{"clientId":1,"brand":"Test","model":"Test","plate":"TEST","currentMileage":-1}' | Out-Null

    $health = Test-Response GET /health 200
    if ($null -ne $health) {
        $list = Test-Response GET /vehicles 200
        Test-Response GET /vehicles/2147483647 404 | Out-Null
        Test-Response GET /vehicles/2147483647/history 404 | Out-Null
        if ($null -ne $list -and @($list.data).Count -gt 0) {
            $vehicle = @($list.data)[0]
            foreach ($field in @('vehicleId','tenantId','clientId','brand','model','year','plate','currentMileage','createdAt')) {
                if ($field -notin $vehicle.PSObject.Properties.Name) { throw "Missing vehicle field: $field" }
            }
            Test-Response GET "/vehicles/$($vehicle.vehicleId)" 200 | Out-Null
            Test-Response GET "/vehicles/$($vehicle.vehicleId)/history" 200 | Out-Null
            $plate = [Uri]::EscapeDataString($vehicle.plate.ToLowerInvariant())
            $found = Test-Response GET "/vehicles?plate=$plate" 200
            if ($null -ne $found -and $found.data.vehicleId -ne $vehicle.vehicleId) { throw 'Plate lookup returned a different vehicle' }
        } else { Write-Host 'SKIP existing vehicle checks: no vehicles returned' }
    } else { Write-Host 'SKIP database checks: health unavailable' }
} finally {
    $http.Dispose()
}

if ($script:failures -gt 0) { throw "$script:failures checks failed" }

param([string]$BaseUrl = 'http://localhost:5243')
$ErrorActionPreference = 'Stop'
# Read-only smoke test: requests without a token MUST stop at authorization.
# A stale API returns 404 here, even when /api/test succeeds.
$health = Invoke-WebRequest "$BaseUrl/api/test" -SkipHttpErrorCheck
if ($health.StatusCode -ne 200) { throw "API health failed: HTTP $($health.StatusCode)" }
$attemptId = '00112233-4455-4677-8899-aabbccddeeff'
foreach ($route in @(
    @{ Method = 'GET'; Path = "/api/pairing-transactions/$attemptId" },
    @{ Method = 'PUT'; Path = "/api/pairing-transactions/$attemptId/aquariums/1" },
    @{ Method = 'POST'; Path = "/api/pairing-transactions/$attemptId/complete" },
    @{ Method = 'DELETE'; Path = "/api/pairing-transactions/$attemptId" }
)) {
    $response = Invoke-WebRequest -Uri "$BaseUrl$($route.Path)" -Method $route.Method -SkipHttpErrorCheck
    if ($response.StatusCode -ne 401) {
        throw "$($route.Method) $($route.Path): expected protected route (401), got $($response.StatusCode). Rebuild/restart ORBIT.API if 404."
    }
    Write-Output "$($route.Method) $($route.Path): registered, Bearer authorization required."
}

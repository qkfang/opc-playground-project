<#
  smoke.ps1 — local smoke test for the proj43 FinOps Foundry chatbot.

  Verifies the app builds (Release), starts, serves a healthy /api/health (offline engine),
  answers a FinOps question over /api/chat with grounded output, and streams via SSE on
  /api/chat/stream. No Azure/Foundry/Fabric required — exercises the deterministic engine.

  Usage:   pwsh ./scripts/smoke.ps1
  Exit 0 on success; non-zero on first failure.
#>

[CmdletBinding()]
param(
  [int]    $Port = 0,                       # 0 => auto-pick a free port
  [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root   = Split-Path -Parent $PSScriptRoot
$webDir = Join-Path $root "apps\web"
$proc   = $null
$failed = $false

function Section($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }
function Ok($t)      { Write-Host "  [PASS] $t" -ForegroundColor Green }
function Fail($t)    { Write-Host "  [FAIL] $t" -ForegroundColor Red; $script:failed = $true }

if ($Port -eq 0) {
  $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
  $listener.Start(); $Port = $listener.LocalEndpoint.Port; $listener.Stop()
}
$baseUrl = "http://127.0.0.1:$Port"

try {
  Section "Build ($Configuration)"
  Push-Location $webDir
  dotnet build -c $Configuration -v quiet
  if ($LASTEXITCODE -ne 0) { Fail "dotnet build"; throw "build failed" }
  Ok "dotnet build"
  Pop-Location

  Section "Start app on $baseUrl"
  $env:ASPNETCORE_URLS = $baseUrl
  $env:ASPNETCORE_ENVIRONMENT = "Production"
  $proc = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "-c", $Configuration, "--no-build", "--project", (Join-Path $webDir "Proj43.FinOps.Web.csproj")) `
    -PassThru -WindowStyle Hidden

  # Wait for health.
  $healthy = $false
  for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 800
    try {
      $h = Invoke-RestMethod "$baseUrl/api/health" -TimeoutSec 3
      if ($h.status -eq "ok") { $healthy = $true; break }
    } catch { }
  }
  if (-not $healthy) { Fail "health endpoint did not become ready"; throw "no health" }
  Ok "health ready (engine=$($h.engine), data through $($h.dataThrough))"

  Section "Suggestions"
  $sugg = Invoke-RestMethod "$baseUrl/api/suggestions" -TimeoutSec 5
  if ($sugg.Count -ge 1) { Ok "suggestions returned ($($sugg.Count))" } else { Fail "no suggestions" }

  Section "Chat round-trip (/api/chat)"
  $cid = $null
  $checks = @(
    @{ q = "What did we spend last month?"; needs = "USD" },
    @{ q = "Top 5 services by cost";        needs = "Top 5 services" },
    @{ q = "Any cost anomalies?";           needs = "Azure SQL Database" },
    @{ q = "How is our commitment coverage?"; needs = "coverage" },
    @{ q = "Where can we save money?";      needs = "savings" }
  )
  foreach ($c in $checks) {
    $body = @{ conversationId = $cid; message = $c.q } | ConvertTo-Json
    $r = Invoke-RestMethod -Method Post "$baseUrl/api/chat" -ContentType "application/json" -Body $body -TimeoutSec 15
    $cid = $r.conversationId
    if ($r.reply -match [Regex]::Escape($c.needs)) {
      Ok ("'{0}' -> intent={1}" -f $c.q, $r.intent)
    } else {
      Fail ("'{0}' missing expected text '{1}'" -f $c.q, $c.needs)
    }
  }
  if ($cid) { Ok "conversation id preserved across turns" } else { Fail "no conversation id" }

  Section "Streaming (/api/chat/stream, SSE)"
  $body = @{ message = "break down cost by team" } | ConvertTo-Json
  $resp = Invoke-WebRequest -Method Post "$baseUrl/api/chat/stream" -ContentType "application/json" -Body $body -TimeoutSec 20
  $raw  = $resp.Content
  $tok  = ([regex]::Matches($raw, "event: token")).Count
  if ($raw -match "event: meta" -and $tok -gt 0 -and $raw -match "event: done") {
    Ok "SSE stream meta + $tok token events + done"
  } else {
    Fail "SSE stream incomplete"
  }
}
catch {
  Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
  $script:failed = $true
}
finally {
  if ($proc -and -not $proc.HasExited) {
    try { $proc.Kill($true) } catch { try { $proc.Kill() } catch { } }
  }
  if (Get-Location | Where-Object { $_.Path -ne $PWD.Path }) { }
}

Section "Result"
if ($failed) { Write-Host "SMOKE FAILED" -ForegroundColor Red; exit 1 }
Write-Host "SMOKE PASSED" -ForegroundColor Green
exit 0

#requires -Version 7.0
<#
.SYNOPSIS
  Local smoke test for the proj45 Relay Desk POC (Inbound Email Orchestration).

.DESCRIPTION
  Builds the web app, launches it on a free localhost port with the offline engine, then exercises the
  full pipeline through the HTTP API:
    - /api/health                 -> healthy, offline engine
    - /api/agents                 -> 5 explicit Foundry agent instruction sets
    - /api/mcp/tools              -> D365 MCP tool catalog (lookup + operation tools)
    - /api/cases/run-demo         -> processes the whole seeded mailbox
    - /api/queue                  -> exactly one uncertain case routed to a human
    - /api/queue/{id}/resolve     -> human confirms intent; case closes with an audit step

  Exits non-zero on the first failed assertion. Always stops the app it started.

.EXAMPLE
  pwsh ./scripts/smoke.ps1
#>
[CmdletBinding()]
param(
  [int] $Port = 0,                      # 0 = pick a free port
  [int] $StartupTimeoutSec = 40
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$web  = Join-Path $root 'apps/web'
$pass = 0; $fail = 0
$proc = $null

function Ok($msg)   { Write-Host "  [PASS] $msg" -ForegroundColor Green; $script:pass++ }
function Bad($msg)  { Write-Host "  [FAIL] $msg" -ForegroundColor Red;   $script:fail++ }
function Assert($cond, $msg) { if ($cond) { Ok $msg } else { Bad $msg } }

function Get-FreePort {
  $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
  $l.Start(); $p = ([System.Net.IPEndPoint]$l.LocalEndpoint).Port; $l.Stop(); return $p
}

try {
  Write-Host "== proj45 Relay Desk smoke ==" -ForegroundColor Cyan

  Write-Host "Building (Release)..." -ForegroundColor Cyan
  & dotnet build (Join-Path $web 'Proj45.RelayDesk.Web.csproj') -c Release -v q | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Build failed." }
  Ok "dotnet build -c Release"

  if ($Port -eq 0) { $Port = Get-FreePort }
  $baseUrl = "http://127.0.0.1:$Port"
  Write-Host "Starting app at $baseUrl ..." -ForegroundColor Cyan

  $env:ASPNETCORE_ENVIRONMENT = 'Development'
  $env:ASPNETCORE_URLS = $baseUrl
  $proc = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '-c', 'Release', '--no-build', '--project', (Join-Path $web 'Proj45.RelayDesk.Web.csproj')) `
    -PassThru -WindowStyle Hidden

  # Wait for health.
  $ready = $false
  for ($i = 0; $i -lt ($StartupTimeoutSec * 2); $i++) {
    try {
      $h = Invoke-RestMethod "$baseUrl/api/health" -TimeoutSec 3
      if ($h.status -eq 'healthy') { $ready = $true; break }
    } catch { Start-Sleep -Milliseconds 500 }
  }
  if (-not $ready) { throw "App did not become healthy within $StartupTimeoutSec s." }

  $health = Invoke-RestMethod "$baseUrl/api/health"
  Assert ($health.status -eq 'healthy') "health = healthy"
  Assert ($health.engine -eq 'offline') "engine = offline (offline-by-default)"

  $agents = Invoke-RestMethod "$baseUrl/api/agents"
  Assert ($agents.Count -eq 5) "5 Foundry agent instruction sets surfaced"
  Assert (($agents | Where-Object { $_.instructions.Length -gt 80 }).Count -eq 5) "every agent has explicit instructions"

  $tools = Invoke-RestMethod "$baseUrl/api/mcp/tools"
  $tnames = $tools.name
  Assert ($tools.Count -ge 8) "D365 MCP catalog has >= 8 tools"
  Assert ($tnames -contains 'customer.search' -and $tnames -contains 'case.create' -and $tnames -contains 'creditmemo.raise') "MCP catalog has lookup + operation tools"

  # Clean slate, then process the whole mailbox.
  Invoke-RestMethod -Method Delete "$baseUrl/api/cases" | Out-Null
  $demo = Invoke-RestMethod -Method Post "$baseUrl/api/cases/run-demo"
  Assert ($demo.processed -ge 7) "run-demo processed the whole mailbox ($($demo.processed) emails)"
  Assert ($demo.engine -eq 'offline') "run-demo used the offline engine"

  $billing = $demo.cases | Where-Object { $_.category -eq 'Billing' } | Select-Object -First 1
  Assert ($null -ne $billing -and $billing.finalStatus -match 'approval') "billing dispute is held for approval"
  $spam = $demo.cases | Where-Object { $_.category -eq 'Spam' } | Select-Object -First 1
  Assert ($null -ne $spam -and $spam.finalStatus -match 'spam') "spam is closed with no action"

  $queue = Invoke-RestMethod "$baseUrl/api/queue"
  Assert ($queue.Count -ge 1) "at least one uncertain case routed to the human queue"

  # Resolve the first human case and confirm it closes with an audit step.
  $hid = ($queue | Select-Object -First 1).caseId
  Invoke-RestMethod -Method Post "$baseUrl/api/queue/$hid/resolve" -ContentType 'application/json' `
    -Body (@{ intent = 'Complaint Escalation'; resolvedBy = 'smoke.ps1' } | ConvertTo-Json) | Out-Null
  $resolved = Invoke-RestMethod "$baseUrl/api/cases/$hid"
  Assert (-not $resolved.intent.requiresHuman) "human resolve clears the review flag"
  Assert ($resolved.status -eq 'completed') "resolved case is completed"
  Assert (($resolved.trace | Where-Object { $_.stage -eq 'Human Review' }).Count -ge 1) "audit trail records the Human Review step"

  $foundry = Invoke-RestMethod "$baseUrl/api/health/foundry"
  Assert ($foundry.foundryMode -eq 'offline') "Foundry probe reports offline mode (by design)"
}
finally {
  if ($proc -and -not $proc.HasExited) {
    Write-Host "Stopping app (pid $($proc.Id))..." -ForegroundColor Cyan
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
  }
  Get-Process -Name 'Proj45.RelayDesk.Web' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "== Smoke result: $pass passed, $fail failed ==" -ForegroundColor ($(if ($fail -eq 0) { 'Green' } else { 'Red' }))
if ($fail -gt 0) { exit 1 }
exit 0

<#
  smoke.ps1 — proj39 Foundry Intake & Origination POC local smoke test.

  Builds the app, starts it on a free port, exercises the API end-to-end (health, mock inbox,
  process a known email, ad-hoc process, spam quarantine, report download, ad-hoc compose), then
  shuts down. Exit code 0 = all checks passed.

  Usage:  pwsh proj39_foundry_intake_origination_poc/scripts/smoke.ps1
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$webDir = Join-Path $here '..\apps\web'
$csproj = Join-Path $webDir 'Proj39.IntakeOrigination.Web.csproj'
$port = 5239
$baseUrl = "http://localhost:$port"
$failures = @()

function Check($name, [scriptblock]$test) {
  try { & $test; Write-Host "  [PASS] $name" -ForegroundColor Green }
  catch { Write-Host "  [FAIL] $name -> $($_.Exception.Message)" -ForegroundColor Red; $script:failures += $name }
}

Write-Host "==> Building..." -ForegroundColor Cyan
dotnet build $csproj -c Release | Out-Null

Write-Host "==> Starting app on $baseUrl ..." -ForegroundColor Cyan
$proc = Start-Process -FilePath 'dotnet' `
  -ArgumentList @('run', '-c', 'Release', '--no-build', '--project', (Resolve-Path $csproj).Path) `
  -PassThru -WindowStyle Hidden -Environment @{ ASPNETCORE_URLS = $baseUrl; ASPNETCORE_ENVIRONMENT = 'Development' }

try {
  $ready = $false
  for ($i = 0; $i -lt 40; $i++) {
    try { if ((Invoke-WebRequest "$baseUrl/api/health" -TimeoutSec 3).StatusCode -eq 200) { $ready = $true; break } } catch { }
    Start-Sleep -Milliseconds 700
  }
  if (-not $ready) { throw "App did not become ready at $baseUrl" }

  Write-Host "==> Running checks..." -ForegroundColor Cyan

  Check "GET /api/health returns healthy" {
    $h = Invoke-RestMethod "$baseUrl/api/health"
    if ($h.status -ne 'healthy') { throw "status=$($h.status)" }
  }

  Check "GET /api/emails seeds >= 5 mock emails" {
    $e = Invoke-RestMethod "$baseUrl/api/emails"
    if ($e.Count -lt 5) { throw "only $($e.Count) emails" }
  }

  $script:hot = $null
  Check "POST /api/cases/process/eml-001 classifies Hot with full pipeline" {
    $script:hot = Invoke-RestMethod -Method Post "$baseUrl/api/cases/process/eml-001"
    if ($script:hot.status -ne 'completed') { throw "status=$($script:hot.status)" }
    if ($script:hot.triage.classification -ne 'Hot') { throw "classification=$($script:hot.triage.classification)" }
    if (-not $script:hot.extraction.account.name) { throw "no account name" }
    if ($script:hot.research.demandSignals.Count -lt 1) { throw "no demand signals" }
    if (-not $script:hot.report.generatedMarkdown) { throw "no report markdown" }
    if (($script:hot.agentSteps | Where-Object { $_.agent -eq 'Report' }).Count -lt 1) { throw "no report step" }
  }

  Check "Deal value prefers budget over company revenue" {
    if ($script:hot.extraction.opportunity.estimatedValue -ne 1400000) {
      throw "expected 1,400,000 got $($script:hot.extraction.opportunity.estimatedValue)"
    }
  }

  Check "POST /api/cases/process/eml-004 is quarantined as Spam" {
    $spam = Invoke-RestMethod -Method Post "$baseUrl/api/cases/process/eml-004"
    if ($spam.triage.classification -ne 'Spam') { throw "classification=$($spam.triage.classification)" }
    if ($spam.report.disposition -ne 'Disqualify') { throw "disposition=$($spam.report.disposition)" }
  }

  Check "Report downloads as markdown" {
    $tmp = Join-Path $env:TEMP "proj39-smoke-report.md"
    Invoke-WebRequest "$baseUrl/api/cases/$($script:hot.caseId)/report" -OutFile $tmp
    $md = Get-Content $tmp -Raw
    if ($md -notmatch 'Origination Study') { throw "report missing title" }
  }

  Check "Ad-hoc compose + process runs pipeline" {
    $payload = @{ from='cto@newco.com'; fromName='Sam Lee'; subject='Data platform RFP'; body='We are a 2,000 staff manufacturer. AUD $400k budget approved. Our CTO is sponsoring and we want to go live this quarter.' } | ConvertTo-Json
    $r = Invoke-RestMethod -Method Post "$baseUrl/api/cases/process" -ContentType 'application/json' -Body $payload
    if ($r.status -ne 'completed') { throw "status=$($r.status)" }
    if ($r.triage.score -lt 45) { throw "expected warm/hot, score=$($r.triage.score)" }
  }

  Check "GET /api/cases lists processed cases" {
    $list = Invoke-RestMethod "$baseUrl/api/cases"
    if ($list.Count -lt 3) { throw "only $($list.Count) cases" }
  }
}
finally {
  Write-Host "==> Stopping app..." -ForegroundColor Cyan
  if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
  Get-CimInstance Win32_Process -Filter "Name='Proj39.IntakeOrigination.Web.exe'" -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

Write-Host ""
if ($failures.Count -eq 0) {
  Write-Host "SMOKE TEST PASSED — all checks green." -ForegroundColor Green
  exit 0
} else {
  Write-Host "SMOKE TEST FAILED — $($failures.Count) check(s): $($failures -join ', ')" -ForegroundColor Red
  exit 1
}

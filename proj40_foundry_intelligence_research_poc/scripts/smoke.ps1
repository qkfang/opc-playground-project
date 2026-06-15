<#
  smoke.ps1 — proj40 Foundry Intelligence & Research POC local smoke test.

  Builds the app, starts it on a fixed port, exercises the API end-to-end (health, inbox + documents,
  the full intelligence pipeline for a known email, entity-keyed source pulls, spam quarantine, report
  email download, ad-hoc compose, case listing), then shuts down.
  Exit code 0 = all checks passed.

  Usage:  pwsh proj40_foundry_intelligence_research_poc/scripts/smoke.ps1
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$webDir = Join-Path $here '..\apps\web'
$port = 5240
$baseUrl = "http://localhost:$port"
$failures = @()

function Check($name, [scriptblock]$test) {
  try {
    & $test
    Write-Host "  [PASS] $name" -ForegroundColor Green
  } catch {
    Write-Host "  [FAIL] $name -> $($_.Exception.Message)" -ForegroundColor Red
    $script:failures += $name
  }
}

Write-Host "==> Building..." -ForegroundColor Cyan
dotnet build (Join-Path $webDir 'Proj40.IntelligenceResearch.Web.csproj') -c Release | Out-Null

Write-Host "==> Starting app on $baseUrl ..." -ForegroundColor Cyan
$proc = Start-Process -FilePath 'dotnet' `
  -ArgumentList @('run', '-c', 'Release', '--no-build', '--project', (Resolve-Path (Join-Path $webDir 'Proj40.IntelligenceResearch.Web.csproj')).Path) `
  -PassThru -WindowStyle Hidden -Environment @{ ASPNETCORE_URLS = $baseUrl; ASPNETCORE_ENVIRONMENT = 'Development' }

try {
  $ready = $false
  for ($i = 0; $i -lt 30; $i++) {
    try { if ((Invoke-WebRequest "$baseUrl/api/health" -TimeoutSec 3).StatusCode -eq 200) { $ready = $true; break } } catch { }
    Start-Sleep -Milliseconds 700
  }
  if (-not $ready) { throw "App did not become ready at $baseUrl" }

  Write-Host "==> Running checks..." -ForegroundColor Cyan

  Check "GET /api/health returns healthy" {
    $h = Invoke-RestMethod "$baseUrl/api/health"
    if ($h.status -ne 'healthy') { throw "status=$($h.status)" }
  }

  Check "GET /api/inbox lists >=4 emails incl. attached documents" {
    $inbox = Invoke-RestMethod "$baseUrl/api/inbox"
    if ($inbox.Count -lt 4) { throw "too few emails: $($inbox.Count)" }
    if (($inbox | Where-Object { $_.hasDocument }).Count -lt 1) { throw "no emails carry a document" }
  }

  $script:case = $null
  Check "POST /api/process/eml-001 runs the full pipeline (entities->insights->sources->brief->report)" {
    $script:case = Invoke-RestMethod -Method Post "$baseUrl/api/process/eml-001"
    if ($script:case.entities.primaryOrganisation -ne 'Nordwind Energy AG') { throw "org=$($script:case.entities.primaryOrganisation)" }
    if ($script:case.insights.Count -lt 3) { throw "too few insights: $($script:case.insights.Count)" }
    if ($script:case.brief.keyFindings.Count -lt 1) { throw "no findings" }
    if ([string]::IsNullOrWhiteSpace($script:case.reportEmail.renderedMarkdown)) { throw "no report email" }
  }

  Check "Source pull returns internal AND external hits keyed by entity" {
    $internal = ($script:case.sourceHits | Where-Object { $_.sourceType -eq 'Internal' }).Count
    $external = ($script:case.sourceHits | Where-Object { $_.sourceType -eq 'External' }).Count
    if ($internal -lt 1) { throw "no internal source hits" }
    if ($external -lt 1) { throw "no external source hits" }
    if ($script:case.brief.citations.Count -lt 1) { throw "no citations" }
  }

  Check "Report email routed by industry + budget uses deal value not revenue" {
    if ($script:case.reportEmail.to -ne 'energy-vertical@contoso.com') { throw "routed to $($script:case.reportEmail.to)" }
    if ($script:case.brief.executiveSummary -match '1\.3 billion') { throw "exec summary used revenue, not deal budget" }
  }

  Check "POST /api/process/eml-004 (spam) is quarantined with no research" {
    $spam = Invoke-RestMethod -Method Post "$baseUrl/api/process/eml-004"
    if ($spam.sourceHits.Count -ne 0) { throw "spam pulled sources: $($spam.sourceHits.Count)" }
    if ($spam.reportEmail.to -notmatch 'intake-triage') { throw "spam not quarantined: $($spam.reportEmail.to)" }
  }

  Check "Report email downloads as text/markdown" {
    $tmp = Join-Path $env:TEMP "proj40-report.md"
    $resp = Invoke-WebRequest "$baseUrl/api/cases/$($script:case.caseId)/report" -OutFile $tmp -PassThru
    if ((Get-Item $tmp).Length -lt 200) { throw "report too small" }
    if ((Get-Content $tmp -Raw) -notmatch 'Subject:') { throw "report missing email envelope" }
  }

  Check "POST /api/process (ad-hoc compose + pasted document) works" {
    $body = @{ fromName='Smoke Tester'; from='smoke@test.com'; subject='RFP — payments resilience';
               body='See attached.'; documentType='RFP';
               documentContent='AuroraPay needs a multi-region PCI-DSS payments platform after a SEV-1 outage. Indicative budget AUD 400k-600k.' } | ConvertTo-Json
    $adhoc = Invoke-RestMethod -Method Post "$baseUrl/api/process" -ContentType 'application/json' -Body $body
    if ($adhoc.insights.Count -lt 1) { throw "no insights from ad-hoc" }
  }

  Check "GET /api/cases lists processed cases" {
    $list = Invoke-RestMethod "$baseUrl/api/cases"
    if ($list.Count -lt 1) { throw "no cases listed" }
  }
}
finally {
  Write-Host "==> Stopping app..." -ForegroundColor Cyan
  if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
  Get-CimInstance Win32_Process -Filter "Name='Proj40.IntelligenceResearch.Web.exe'" -ErrorAction SilentlyContinue |
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

<#
  smoke.ps1 — proj44 APRA CPS 230 Compliance-Mapping POC local smoke test.

  Builds the app, starts it on a free port, exercises the API end-to-end (health, the six-agent
  pipeline run, framework scale, gap analysis findings, traceability chain) and confirms a UI tab
  renders with the persistent nav, then shuts down.
  Exit code 0 = all checks passed.

  Usage:  pwsh proj44_apra_cps_compliance_poc/scripts/smoke.ps1
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$webDir = Join-Path $here '..\apps\web'
$port = 5244
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
dotnet build (Join-Path $webDir 'Proj44.Compliance.Web.csproj') -c Release | Out-Null

Write-Host "==> Starting app on $baseUrl ..." -ForegroundColor Cyan
$proc = Start-Process -FilePath 'dotnet' `
  -ArgumentList @('run', '-c', 'Release', '--no-build', '--project', (Resolve-Path (Join-Path $webDir 'Proj44.Compliance.Web.csproj')).Path) `
  -PassThru -WindowStyle Hidden -Environment @{ ASPNETCORE_URLS = $baseUrl; ASPNETCORE_ENVIRONMENT = 'Development' }

try {
  # Wait for readiness
  $ready = $false
  for ($i = 0; $i -lt 30; $i++) {
    try { if ((Invoke-WebRequest "$baseUrl/api/health" -TimeoutSec 3).StatusCode -eq 200) { $ready = $true; break } } catch { }
    Start-Sleep -Milliseconds 700
  }
  if (-not $ready) { throw "App did not become ready at $baseUrl" }

  Write-Host "==> Running checks..." -ForegroundColor Cyan

  Check "GET /api/health returns healthy + offline engine" {
    $h = Invoke-RestMethod "$baseUrl/api/health"
    if ($h.status -ne 'healthy') { throw "status=$($h.status)" }
    if ($h.engine -ne 'offline') { throw "engine=$($h.engine) (expected offline with no Foundry config)" }
  }

  Check "GET /api/agent-instructions exposes the six pipeline agents" {
    $ai = Invoke-RestMethod "$baseUrl/api/agent-instructions"
    if ($ai.stages.Count -ne 6) { throw "expected 6 stages, got $($ai.stages.Count)" }
    $keys = ($ai.stages | ForEach-Object { $_.key }) -join ','
    if ($keys -ne 'ingestion,requirements,policies,standards,controls,gap') { throw "stage order/keys wrong: $keys" }
  }

  $script:run = $null
  Check "POST /api/run completes and logs all six agent steps in order" {
    $script:run = Invoke-RestMethod -Method Post "$baseUrl/api/run"
    if ($script:run.status -ne 'completed') { throw "status=$($script:run.status)" }
    if ($script:run.agentSteps.Count -ne 6) { throw "expected 6 agent steps, got $($script:run.agentSteps.Count)" }
    $order = ($script:run.agentSteps | ForEach-Object { $_.step }) -join '->'
    if ($order -ne 'ingestion->requirements->policies->standards->controls->gap') { throw "step order wrong: $order" }
  }

  Check "GET /api/framework returns >=130 policies and >=30 controls (mapped)" {
    $fw = Invoke-RestMethod "$baseUrl/api/framework"
    if ($fw.policies.Count -lt 130) { throw "policies=$($fw.policies.Count) (expected >=130)" }
    if ($fw.controls.Count -lt 30) { throw "controls=$($fw.controls.Count) (expected >=30)" }
    if ($fw.requirements.Count -lt 30) { throw "requirements=$($fw.requirements.Count)" }
    if ($fw.standards.Count -lt 30) { throw "standards=$($fw.standards.Count)" }
    # Mappings must be present at every layer.
    $reqLinks = ($fw.requirements | ForEach-Object { $_.policyIds.Count } | Measure-Object -Sum).Sum
    $polLinks = ($fw.policies | ForEach-Object { $_.standardIds.Count } | Measure-Object -Sum).Sum
    $stdLinks = ($fw.standards | ForEach-Object { $_.controlIds.Count } | Measure-Object -Sum).Sum
    if ($reqLinks -lt 50) { throw "too few requirement->policy links: $reqLinks" }
    if ($polLinks -lt 100) { throw "too few policy->standard links: $polLinks" }
    if ($stdLinks -lt 50) { throw "too few standard->control links: $stdLinks" }
    if ($fw.source.code -notmatch '230') { throw "source is not CPS 230: $($fw.source.code)" }
  }

  Check "GET /api/gaps returns real findings + coverage at every layer" {
    $g = Invoke-RestMethod "$baseUrl/api/gaps"
    if ($g.totalGaps -lt 1) { throw "expected deliberate gaps, got totalGaps=$($g.totalGaps)" }
    if ($g.findings.Count -lt 1) { throw "no findings" }
    if ($g.unmappedRequirements.Count -lt 1) { throw "no unmapped requirements" }
    if ($g.unmappedPolicies.Count -lt 1) { throw "no unmapped policies" }
    if ($g.unmappedStandards.Count -lt 1) { throw "no unmapped standards" }
    if ($g.coverage.requirementCoverage -le 0 -or $g.coverage.requirementCoverage -ge 100) { throw "requirementCoverage=$($g.coverage.requirementCoverage)" }
    if ($g.coverage.endToEndCoverage -le 0) { throw "endToEndCoverage not computed" }
  }

  Check "GET /api/traceability/{id} returns a full requirement->policy->standard->control chain" {
    $t = Invoke-RestMethod "$baseUrl/api/traceability/REQ-001"
    if (-not $t.isComplete) { throw "REQ-001 chain not complete" }
    if ($t.policies.Count -lt 1) { throw "no policies in chain" }
    if ($t.policies[0].standards.Count -lt 1) { throw "no standards in chain" }
    if ($t.policies[0].standards[0].controls.Count -lt 1) { throw "no controls in chain" }
  }

  Check "Traceability surfaces a broken chain for a deliberate gap requirement" {
    $t = Invoke-RestMethod "$baseUrl/api/traceability/REQ-034"
    if ($t.isComplete) { throw "REQ-034 should be a broken chain" }
    if ($t.brokenLinks.Count -lt 1) { throw "no broken links reported" }
  }

  Check "GET /api/policies and /api/controls list collections" {
    $p = Invoke-RestMethod "$baseUrl/api/policies"
    $c = Invoke-RestMethod "$baseUrl/api/controls"
    if ($p.Count -lt 130) { throw "policies list=$($p.Count)" }
    if ($c.Count -lt 30) { throw "controls list=$($c.Count)" }
  }

  Check "UI tab renders 200 with the persistent top nav" {
    foreach ($path in @('/', '/Requirements', '/Policies', '/Standards', '/Controls', '/Mappings', '/Gaps', '/Traceability', '/Pipeline')) {
      $r = Invoke-WebRequest "$baseUrl$path" -TimeoutSec 5
      if ($r.StatusCode -ne 200) { throw "$path -> $($r.StatusCode)" }
      if ($r.Content -notmatch 'mainnav') { throw "$path missing top nav" }
      if ($r.Content -notmatch 'CPS 230') { throw "$path missing CPS 230 branding" }
    }
  }
}
finally {
  Write-Host "==> Stopping app..." -ForegroundColor Cyan
  if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
  # also clean child dotnet/app processes spawned by 'dotnet run'
  Get-CimInstance Win32_Process -Filter "Name='Proj44.Compliance.Web.exe'" -ErrorAction SilentlyContinue |
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

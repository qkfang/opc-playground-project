# proj41 — Live Foundry fix verification (toad)

**Task:** proj41-fix-foundry-live-20260616-2211
**App (live):** https://proj41-web-rxkk72.azurewebsites.net
**Date:** 2026-06-16 (Australia/Sydney)
**Outcome:** Deployed app now runs the **live Microsoft Foundry agent path** (offline retained as fallback only).

## Root cause
The deployed App Service had **`Foundry__Enabled=false`** in its app settings (the live agent path is gated by
`FoundryOptions.IsConfigured = Enabled && ProjectEndpoint`). Everything else was already correct:
- `Foundry__ProjectEndpoint = https://proj41-ais-rxkk72.services.ai.azure.com/api/projects/proj41-proj` ✓
- `Foundry__ModelDeployment = gpt-4o` ✓
- Foundry AI Services account `proj41-ais-rxkk72` Succeeded, keyless (`disableLocalAuth=true`), project mgmt on ✓
- `gpt-4o` GlobalStandard deployment Succeeded (capacity 50) ✓
- Web App managed identity RBAC on the Foundry account: **Cognitive Services User** + **Cognitive Services OpenAI User** ✓ (plus KV Secrets User, Storage Blob Data Contributor)

So the only blocker was the disabled flag (the infra deploy had been run with `foundryEnabled=false`).

A secondary defect surfaced once enabled: several pipeline stages threw `JsonException` and silently fell
back to the offline engine, because the live model emits JSON shapes the strict binder rejects
(numbers as strings / with `$`, commas, units; booleans as strings; trailing commas). This both reduced
"liveness" and degraded data quality (offline fallback grabbed e.g. the deductible as the limit).

## Fixes applied (code)
1. **Enabled live Foundry**: `Foundry__Enabled=true` on the App Service (and the deploy workflow now asserts it).
2. **Tolerant JSON parsing** for live agent responses (`Services/Foundry/LenientConverters.cs`): converters that
   coerce string/currency/unit numbers ("$10M", "10,000,000", "1.2M", "750k"), string booleans
   ("yes"/"true"/"appointed"), string ints/doubles, ISO dates, trailing commas, and comments — so a correct
   live answer is no longer discarded. Offline pipeline untouched.
3. **Stronger agent instructions/prompts**: numbers must be raw (no `$`/commas/units), dates ISO-8601, nulls for
   unknowns; risk/fit/intent pinned to an explicit 0..100 integer scale (the model had used 0..10).
4. **Clear health surface (no secrets)** — the captain's improvement ask:
   - `GET /api/health` now returns `engine`, `foundryConfigured`, `foundryEnabled`, `foundryMode`
     (configured | misconfigured | offline), `modelDeployment`.
   - **`GET /api/health/foundry`** performs a **real minimal agent round-trip** and reports
     `foundryMode` = **live | fallback | error | offline**, `foundryLive`, `endpointHost`, `probeMs`, `detail`.
     Returns 200 when live/offline-by-design, 503 when configured-but-not-live (alertable).
5. **Self-checking deploy**: `proj41_intake_origination_deploy.yml` now (a) asserts `Foundry__Enabled=true`
   post-deploy and (b) fails the run unless `/api/health/foundry` reports `foundryMode=live`
   (input `assertFoundryLive`, default true). Mirrored at repo-root `.github/workflows/`.

## Local verification
- `dotnet build -c Release` → **0 warn / 0 err** (.NET 10.0.300).
- `dotnet test -c Release` → **26/26 PASS** (6 original + 20 hardening: money/bool/int/double/date coercion,
  full ExtractedRecords with LLM-style values, and the active `/api/health/foundry` offline-mode probe).

## Live verification (deployed app)
- `GET /api/health` → `engine=foundry, foundryConfigured=true, foundryEnabled=true, foundryMode=configured, modelDeployment=gpt-4o`.
- `GET /api/health/foundry` → **`foundryMode=live, foundryLive=true`**, `endpointHost=proj41-ais-rxkk72.services.ai.azure.com`, `probeMs≈1.1–9.6s`, `detail="Live Foundry agent round-trip succeeded."` (real agent call via managed identity).
- **Live agent-backed submission run** (`POST /api/cases`, Vertex Payments cyber submission) → **all 4 stages `engine=foundry`**:
  - Submission Intake (foundry, ~21s): insured "Vertex Payments Pty Ltd", LOB Cyber, **limit $10,000,000**, **incumbent Beazley** (offline fallback had mis-set limit=$50k and dropped Beazley).
  - Appetite & Triage (foundry, ~9s): Refer to Underwriter, **risk 82 / fit 58** (correct 0..100 scale), P2.
  - Risk Research (foundry, ~10s): 4 exposure signals, intent 74.
  - Underwriting Study (foundry, ~40s): "Executive Underwriting Risk Study: Vertex Payments Pty Ltd — Cyber Liability", recommendation Refer, **indicated premium $210,000**.
  - Case `engine=foundry`, status=completed.

Progression observed while hardening: 2/4 → 3/4 (after lenient numbers + scale fix) → **4/4** (after lenient booleans).

## Deployment note (handoff to yoshi)
Live verification was done by directly publishing the fixed build to the existing App Service to prove the fix.
For the canonical, reproducible record, re-run the CI pipeline:
`proj41_intake_origination_infra` (keep `foundryEnabled=true`) then `proj41_intake_origination_deploy`
(`assertFoundryLive=true`) — the deploy now self-verifies the live Foundry path.

**Result: PASS — live Foundry confirmed (not fallback).**

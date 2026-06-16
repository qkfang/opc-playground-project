# proj41 — Build verification evidence (toad)

**Task:** proj41-build-intake-origination-20260616-2100
**App:** Sentinel Underwriting — Submission Desk (`Proj41.Underwriting.Web`)
**Date:** 2026-06-16 (Australia/Sydney)
**Engine:** offline (Foundry disabled by default; live path wired for post-deploy)

## Build
- `dotnet build apps/web/Proj41.Underwriting.Web.csproj -c Release` → **Build succeeded. 0 Warning(s), 0 Error(s)**.
- SDK: .NET 10.0.300. TargetFramework `net10.0`. DLL: `Proj41.Underwriting.Web.dll`.

## Tests
- `dotnet test -c Release` → **Passed! Failed: 0, Passed: 6, Skipped: 0** (~0.9s).
- Coverage (WebApplicationFactory over the real DI graph + minimal API):
  1. `Health_reports_offline_engine` — `/api/health` → healthy, engine=offline, foundryConfigured=false.
  2. `Inbox_is_seeded_with_submissions` — broker mailbox seeded (>=5).
  3. `Property_submission_extracts_records_and_routes` — Atlas Steel: company/LOB/limit($50M)/TIV($85M) extracted; high-limit referral trigger; 4 trace stages; status completed; premium > 0.
  4. `Cyber_submission_is_classified_and_priced` — Meridian Health cyber: LOB=Cyber, regulatory/industry signal present, not declined.
  5. `Prohibited_class_is_out_of_appetite` — Big Bang Fireworks: Out of Appetite, declined, status=declined, study recommendation=Decline.
  6. `Run_demo_processes_whole_inbox` — run-demo processes >=5, engine=offline.

## Smoke (run-demo over seed mailbox)
`POST /api/cases/run-demo` processed **7/7** submissions into differentiated cases, e.g.:
- **Atlas Steel Fabrication Inc** (Property, TIV $85M, limit $50M) → Refer to Underwriter / P2 / risk 73 / fit 86 / indicated premium **$382.5K**; triggers: High limit ≥ $25M; flags: catastrophe-exposed.
- **Meridian Health Systems** (Cyber, $10M) → triaged with regulatory/healthcare exposure signal.
- **Big Bang Fireworks Manufacturing** (prohibited class) → **Out of Appetite / Decline**.
- **SEO spam** → disqualified/decline.

## Browser verification (localhost:5241, offline engine)
All **5 surfaces** screenshot-verified rendering correctly with the Atlas Steel case:
1. **Submission Desk** — broker mailbox + ad-hoc trigger + submission queue.
2. **Risk Records** — Producer / Insured / Risk-Submission cards + confidence + missing-info chips.
3. **Appetite & Triage** — risk(73)/fit(86) gauges, routing (Refer, P2, SLA 48h, Commercial Property desk), referral triggers, risk flags, rationale.
4. **Exposure Research** — account overview, binding-intent gauge(73), categorised exposure/demand signals (IndustryHazard / CatastropheExposure / LossHistory) + broker questions.
5. **Underwriting Study** — executive recommendation banner (Refer) + indicated premium $382.5K, pricing rationale, key risk flags, recommended conditions, sections, next actions.

## Infrastructure
- `az bicep build --file bicep/main.bicep` → **exit 0, no warnings** (App Service Standard S1, Foundry gpt-4o, Storage MI-only `allowSharedKeyAccess:false`, KV RBAC, Log Analytics/App Insights, 4 MI role assignments).
- Workflows present at `proj41_intake_origination_foundry_enterprise/.github/workflows/` and mirrored at repo-root `.github/workflows/`: `proj41_intake_origination_infra.yml`, `proj41_intake_origination_deploy.yml`.

## Hygiene
- proj41-scoped `.gitignore` excludes `**/bin/`, `**/obj/`, `**/App_Data/`, `bicep/main.json` (commit is source-only).

**Result: PASS (build phase).** Handed to QA (toadette); deployment by yoshi mandatory after QA PASS.

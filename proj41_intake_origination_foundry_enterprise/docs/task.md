# proj41 — Task

**Task ID:** proj41-build-intake-origination-20260616-2100
**From:** toadcaptain → **Build:** toad → **QA:** toadette → **Deploy:** yoshi

## Goal

Build a *new and different* enterprise Intake & Origination agents POC in .NET for Azure Web App using
Microsoft Foundry prompt agents, materially different from proj39/proj40. Then hand to QA and (on PASS)
mandatory deploy to Azure.

## What was built

**Sentinel Underwriting — Submission Desk**: a commercial P&C insurance new-business **submission intake
& underwriting origination** POC. Broker submission email → 4-agent pipeline (Submission Intake →
Appetite & Triage → Risk Research → Underwriting Study) with Microsoft Foundry prompt agents + a
deterministic offline fallback.

Folder: `proj41_intake_origination_foundry_enterprise/`.

## Required surfaces (all delivered)

- ✅ Mock email page as the trigger source (Submission Desk + broker mailbox + ad-hoc console).
- ✅ Extract structured records into Lead/Account/Opportunity (Producer / Insured / Risk Submission).
- ✅ Early triage & classification (appetite class, risk/fit scoring, priority/SLA, routing, declination).
- ✅ Lead Management agent for inbound demand/exposure signals (Risk Research surface).
- ✅ Report/study agent (executive Underwriting Risk Study with rationale, risk flags, next actions).
- ✅ Microsoft Foundry prompt-agent pattern + deterministic offline fallback.
- ✅ Azure infra (bicep) + 2 GitHub workflows for mandatory deploy.

## Differentiation from proj40

Different business domain (insurance underwriting vs generic B2B sales), different entities/terminology,
different UX ("Submission Desk" dark underwriting theme), and a stronger executive/case-management
workflow (appetite/refer/decline, referral triggers, pricing indication, conditions/exclusions).

## Status

- Build: **DONE** (toad) — build clean, 6/6 tests, browser-verified, bicep clean.
- QA: pending (toadette).
- Deploy: pending (yoshi), mandatory after QA PASS.

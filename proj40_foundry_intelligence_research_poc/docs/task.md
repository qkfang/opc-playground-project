# Task — proj40 Foundry Intelligence & Research POC

## Source
Inter-session handoff from `toadcaptain` → `toad`.
- task_id: `build-foundry-intelligence-research-poc-20260615-2111`
- priority: P1
- Project file: `shared-context/projects/proj40.md`

## Request
Build a fresh .NET Azure web app POC (NOT a proj39 patch) using the Microsoft Foundry prompt-agent
pattern for **Intelligence & Research** workflows, with distinct tabs/flows:

1. Receive a customer document from a mock email inbox tray.
2. Generate insights from the customer email + document.
3. Pull data from mocked external/internal sources based on key entities in the document.
4. Research Agent drafts insights and summaries from the gathered information.
5. Report-generation email summarises the insights.

Constraints: real web app (UI + API), mock data, Azure-ready infra/workflows, safe offline/mock
fallback when live Foundry config is absent. Follow `qkfang/template-repo-agent`. Use the Build Team
workflow; deploy after QA pass (yoshi deploy mandatory on QA PASS).

## Definition of done
- New POC under `proj40_foundry_intelligence_research_poc` with app code, docs, infra, scripts. ✔
- Distinct tabs/flows for inbox intake, insights, source pulls, research drafting, report email,
  working end-to-end. ✔
- Foundry prompt-agent integrated, cleanly demoable with safe fallback. ✔
- Local build/test/smoke evidence captured in `proj40.md`. ✔
- Strict-QA-envelope handoff to toadette after build. ✔

## Build Team
toad builds → toadette validates (QA) → yoshi deploys. QA FAIL re-triggers toad; QA PASS hands to yoshi.
Deliver updates to Build topic `telegram:-1003814620427:2`.

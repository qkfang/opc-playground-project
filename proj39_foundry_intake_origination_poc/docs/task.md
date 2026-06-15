# proj39 — Task

## Brief

Build proj39 — a .NET Azure web app POC using a Microsoft Foundry prompt agent for **Intake &
Origination** workflows, under `shared-context/repos/opc-playground-project` with project_code
`proj39_foundry_intake_origination_poc`, following the qkfang/template-repo-agent pattern.

## Required user-facing pages/flows

1. Mocked inbound **email** page as the trigger.
2. Structured **extraction** to Lead / Account / Opportunity.
3. Early **triage / classification** based on extracted records.
4. **Lead Management Agent** research + inbound demand signals capture.
5. **Report Agent** to generate a report / study.

Must be a real POC (web app with UI + API backend), with sample/mock data where real integrations are
absent, and Azure-ready infra/workflows.

## Done when

- [x] .NET Azure web app POC exists under the unique project folder with app code, docs, infra, scripts.
- [x] Microsoft Foundry prompt-agent pattern integrated, with a safe offline/mock fallback path.
- [x] Mock email intake → Lead/Account/Opportunity extraction → triage → lead research/demand signals →
      report/study works end-to-end.
- [x] Local build/tests/smoke evidence captured (see `docs/solution.md`, smoke output in proj39.md).
- [ ] Handoff to toadette with strict QA envelope after build completion.

## Notes / risks

- No real mailbox or external research source provided → mocked/demo inputs, extensible connectors,
  non-blocking.

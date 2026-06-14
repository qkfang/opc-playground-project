# legopuzzle — Tasks

## T1 — Provision Azure infrastructure
- Add Bicep in `legopuzzle/bicep`
- Provision:
  - Azure App Service (backend API)
  - Azure Cosmos DB SQL API (scores)
  - Azure Static Web App (frontend)
- Target resource group: `rg-playground-01`

## T2 — Infra GitHub Actions workflow
- Add `.github/workflows/legopuzzle_infra.yml`
- Requirements:
  - Workflow name prefixed with `legopuzzle_`
  - Use `azure/login@v1`
  - Deploy Bicep and report outputs

## T3 — App deployment workflow
- Add `.github/workflows/legopuzzle_deploy.yml`
- Requirements:
  - Workflow name prefixed with `legopuzzle_`
  - Deploy backend API package to App Service
  - Deploy frontend with SWA deployment token
  - Do not use SWA repo integration

## T4 — Required secrets and parameters
Required secrets:
- `AZURE_CREDENTIALS`
- `LEGOPUZZLE_SWA_DEPLOYMENT_TOKEN`

Workflow inputs:
- `baseName`
- `location`
- `swaLocation`
- `backendPath`
- `frontendPath`
- `deployBackend`

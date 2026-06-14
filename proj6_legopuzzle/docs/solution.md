# legopuzzle — Solution

## Goal
Provision and deploy legopuzzle on Azure with:
- Backend API on Azure App Service
- Scores data in Azure Cosmos DB (SQL API)
- Frontend on Azure Static Web Apps

## Infrastructure
`legopuzzle/bicep/main.bicep` deploys to `rg-playground-01`:
- `Microsoft.Web/serverfarms` (Linux App Service plan)
- `Microsoft.Web/sites` (backend API web app)
- `Microsoft.DocumentDB/databaseAccounts` + SQL database + `scores` container
- `Microsoft.Web/staticSites` (frontend SWA, no repo integration)

Resource names are deterministic from:
- `baseName` (default `legopuzzle`)
- `uniqueString(resourceGroup().id, baseName)` short suffix

## Deployment Workflows
- `.github/workflows/legopuzzle_infra.yml`
  - Uses `azure/login@v1`
  - Deploys Bicep to `rg-playground-01`
  - Outputs API hostname, Cosmos endpoint, and SWA hostname in job summary
- `.github/workflows/legopuzzle_deploy.yml`
  - Uses `azure/login@v1` for backend deploy
  - Deploys backend zip package to discovered App Service
  - Deploys frontend using SWA deployment token (`Azure/static-web-apps-deploy@v1`)

## Required GitHub Secrets
- `AZURE_CREDENTIALS`: service principal JSON for Azure login
- `LEGOPUZZLE_SWA_DEPLOYMENT_TOKEN`: SWA deployment token used by `legopuzzle_deploy`

## Deployment Parameters
Infra workflow inputs:
- `baseName` (default `legopuzzle`)
- `location` (default `eastasia`)
- `swaLocation` (default `eastasia`)

Deploy workflow inputs:
- `baseName` (default `legopuzzle`)
- `backendPath` (default `legopuzzle/apps/backend`)
- `frontendPath` (default `legopuzzle/apps/frontend`)
- `deployBackend` (default `true`)

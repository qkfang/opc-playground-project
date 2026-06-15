# 20260606_fruit_robotics_news deployment

## Required GitHub secret(s)

The following repository secrets must be configured before running workflows:

- `AZURE_CREDENTIALS`: JSON output from `az ad sp create-for-rbac --sdk-auth`.
- `AZURE_RESOURCE_GROUP`: Azure resource group that will host the project resources.
- `AZURE_WEBAPP_NAME_20260606_FRUIT_ROBOTICS_NEWS`: Web App name output by the infra deployment workflow.
- `AZURE_STATIC_WEB_APPS_API_TOKEN_20260606_FRUIT_ROBOTICS_NEWS`: Static Web App deployment token.

## Optional GitHub variable(s)

- `AZURE_LOCATION`: Azure region for infrastructure deployment (defaults to `eastus2`).

## Get and store the Static Web App deployment token

After infrastructure deployment has created the Static Web App, use Azure CLI:

```bash
az staticwebapp secrets list \
  --name <static-web-app-name> \
  --resource-group <resource-group-name> \
  --query "properties.apiKey" \
  --output tsv
```

Store the value in repository secret:

- `AZURE_STATIC_WEB_APPS_API_TOKEN_20260606_FRUIT_ROBOTICS_NEWS`

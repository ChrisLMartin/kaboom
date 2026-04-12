# Kaboom Azure Infrastructure

This folder contains the Azure infrastructure template for Kaboom.

The GitHub-to-Azure trust bootstrap is intentionally split out as a one-time step under `infra/bootstrap`. After that trust exists, the normal infra pipeline can update the reusable infrastructure in `infra/main.bicep`.

## What It Provisions

- Linux App Service plan
- Linux App Service web app for Kaboom
- Log Analytics workspace
- Application Insights
- Azure Database for PostgreSQL Flexible Server
- PostgreSQL database
- PostgreSQL firewall rules for Azure services and an optional developer IP

## One-Time GitHub Bootstrap

Run this once per repo/branch/environment to let GitHub Actions authenticate to Azure with OIDC.

Copy and edit the bootstrap parameters file:

```powershell
copy infra\bootstrap\github-oidc.parameters.example.bicepparam infra\bootstrap\github-oidc.prod.bicepparam
```

Deploy the bootstrap template:

```powershell
az deployment group create `
  --resource-group rg-kaboom-prod `
  --template-file infra/bootstrap/github-oidc.bicep `
  --parameters infra/bootstrap/github-oidc.prod.bicepparam
```

Capture these outputs and add them to GitHub secrets:

- `githubDeploymentClientId` -> `AZURE_CLIENT_ID`
- `azureTenantId` -> `AZURE_TENANT_ID`
- `azureSubscriptionId` -> `AZURE_SUBSCRIPTION_ID`

Also add this GitHub secret:

- `AZURE_WEBAPP_NAME`

And these GitHub repository variables for the infra pipeline:

- `AZURE_RESOURCE_GROUP`
- `KABOOM_NAME_PREFIX`
- `KABOOM_POSTGRES_SERVER_NAME`
- `KABOOM_PUBLIC_ORIGIN`
- `GOOGLE_CLIENT_ID`

## Deploy With Azure CLI

Create a resource group if you do not already have one:

```powershell
az group create --name rg-kaboom-prod --location australiaeast
```

Copy and edit the example parameters file:

```powershell
copy infra\main.parameters.example.bicepparam infra\main.parameters.prod.bicepparam
```

Deploy:

```powershell
az deployment group create `
  --resource-group rg-kaboom-prod `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.prod.bicepparam
```

After deployment, capture these outputs:

- `webAppName`
- `webAppUrl`

## Notes

- `AllowAzureServices` is enabled by default for PostgreSQL because it is the simplest way to let App Service connect without private networking.
- If you later want tighter network isolation, move to VNet integration + private access for PostgreSQL.
- If you add a custom domain later, update both `Application__PublicOrigin` and your Google OAuth redirect URIs.

# Kaboom Azure Infrastructure

This folder contains the Azure infrastructure template for Kaboom.

## What It Provisions

- Linux App Service plan
- Linux App Service web app for Kaboom
- Log Analytics workspace
- Application Insights
- Azure Database for PostgreSQL Flexible Server
- PostgreSQL database
- PostgreSQL firewall rules for Azure services and an optional developer IP
- Optional user-assigned managed identity and GitHub OIDC federated credential for deployments

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
- `githubDeploymentClientId`
- `azureTenantId`
- `azureSubscriptionId`

## Notes

- `AllowAzureServices` is enabled by default for PostgreSQL because it is the simplest way to let App Service connect without private networking.
- If you later want tighter network isolation, move to VNet integration + private access for PostgreSQL.
- If you add a custom domain later, update both `Application__PublicOrigin` and your Google OAuth redirect URIs.

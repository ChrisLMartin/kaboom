# Kaboom Azure Infrastructure

This folder contains the Azure infrastructure template for Kaboom.

The GitHub-to-Azure trust bootstrap is intentionally split out as a one-time step under `infra/bootstrap`. After that trust exists, the normal infra pipeline can update the reusable infrastructure in `infra/main.bicep`.

## What It Provisions

- Linux App Service plan
- Linux App Service web app for Kaboom
- Log Analytics workspace
- Application Insights
- Virtual network with dedicated subnets for App Service integration, private endpoints, and PostgreSQL
- Azure Key Vault
- Private DNS zones for Key Vault and PostgreSQL
- Azure Database for PostgreSQL Flexible Server
- PostgreSQL database
- Private Key Vault access via private endpoint
- Private PostgreSQL access via delegated subnet

## One-Time GitHub Bootstrap

Run this once per repo/branch/environment to let GitHub Actions authenticate to Azure with OIDC.

Copy and edit the bootstrap parameters file:

```powershell
copy infra\bootstrap\github-oidc.parameters.example.bicepparam infra\bootstrap\github-oidc.dev.bicepparam
```

Deploy the bootstrap template:

```powershell
az deployment group create `
  --resource-group rg-kaboom-dev `
  --template-file infra/bootstrap/github-oidc.bicep `
  --parameters infra/bootstrap/github-oidc.dev.bicepparam
```

Capture these outputs and add them to GitHub secrets:

- `githubDeploymentClientId` -> `AZURE_CLIENT_ID`
- `azureTenantId` -> `AZURE_TENANT_ID`
- `azureSubscriptionId` -> `AZURE_SUBSCRIPTION_ID`

Also add this GitHub secret:

- none required for the app name

And these GitHub repository variables for the infra pipeline:

- `AZURE_RESOURCE_GROUP`
- `AZURE_WEBAPP_NAME`
- `KABOOM_NAME_PREFIX`
- `KABOOM_POSTGRES_SERVER_NAME`
- `KABOOM_PUBLIC_ORIGIN`
- `GOOGLE_CLIENT_ID`

## Deploy With Azure CLI

Create a resource group if you do not already have one:

```powershell
az group create --name rg-kaboom-dev --location australiaeast
```

Copy and edit the example parameters file:

```powershell
copy infra\main.parameters.example.bicepparam infra\main.parameters.dev.bicepparam
```

Deploy:

```powershell
$postgresPassword = -join ((48..57) + (65..90) + (97..122) + 33,35,36,37,42,43,45,61 | Get-Random -Count 32 | ForEach-Object { [char]$_ })
$googleClientSecret = Read-Host "Google client secret (optional, leave blank if not using Google login)"

az deployment group create `
  --resource-group rg-kaboom-dev `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.dev.bicepparam `
  --parameters postgresAdminPassword="$postgresPassword" `
  --parameters googleClientSecret="$googleClientSecret"
```

After deployment, capture these outputs:

- `webAppName`
- `webAppUrl`
- `keyVaultName`
- `keyVaultUri`

## Notes

- The app remains publicly reachable through App Service, but the PostgreSQL server and Key Vault are now private to the virtual network.
- App Service is integrated into the VNet and reads runtime secrets through Key Vault references.
- Do not commit real `.dev.bicepparam` or `.prod.bicepparam` files. They are ignored by `.gitignore`.
- If you add a custom domain later, update both `Application__PublicOrigin` and your Google OAuth redirect URIs.

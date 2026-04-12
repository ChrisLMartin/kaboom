# Azure Setup

This guide starts from a blank slate and gets Kaboom to the point where Azure infrastructure and GitHub deployments are ready.

## Recommended Starting Shape

For room to grow without overengineering, start with:

- one dedicated Microsoft Entra tenant for Kaboom
- one Azure subscription for production
- one production resource group
- local development on your machine

Do not start with management groups, multiple subscriptions, or hub-and-spoke networking unless you already know you need them. Azure management groups are useful once you have multiple subscriptions to govern, but they are unnecessary overhead for a single-subscription app. Source: [Azure management groups overview](https://learn.microsoft.com/en-us/azure/governance/management-groups/).

Good first naming:

- tenant: dedicated to Kaboom
- subscription: `Kaboom Production`
- resource group: `rg-kaboom-prod`
- region: `australiaeast` or the Azure region closest to your users

## What You Must Do Manually

These steps are not worth trying to automate from this repo:

1. Create the Azure account.
2. Create or choose the Microsoft Entra tenant.
3. Create or choose the Azure subscription.
4. Decide who owns billing and who should have admin rights.

Why manual:

- billing setup is interactive
- tenant creation is a tenant-level admin action
- Azure account signup and tenant creation flows are portal-driven

Official guidance:

- Azure signup / subscription: [Azure account options](https://azure.microsoft.com/pricing/purchase-options/azure-account)
- Create a Microsoft Entra tenant: [Create a new tenant](https://learn.microsoft.com/en-us/entra/fundamentals/create-new-tenant)

## Blank Slate Steps

### 1. Create the Azure account and subscription

If you do not already have an Azure account:

- sign up for Azure
- choose either a free account or pay-as-you-go
- note which Microsoft account or work account owns billing

If you want clean isolation for Kaboom production, use a dedicated tenant/account path rather than mixing it into a personal or company-wide playground subscription.

### 2. Create or confirm the Microsoft Entra tenant

If your new Azure signup already created a tenant, that is often enough for a first production setup.

If you want a separate dedicated tenant for Kaboom:

- go to Microsoft Entra admin center
- create a new workforce tenant
- record:
  - tenant name
  - tenant ID
  - initial `onmicrosoft.com` domain

Microsoft notes that tenant creation requires the right tenant permissions and, for extra tenants, usually a paid customer context. Source: [Create a Microsoft Entra tenant](https://learn.microsoft.com/en-us/entra/fundamentals/create-new-tenant).

### 3. Install local tools

Install:

- Azure CLI
- PowerShell 7
- .NET 10 SDK
- Node LTS
- Git

Verify:

```powershell
az version
dotnet --version
node --version
npm --version
git --version
```

### 4. Sign in and select the subscription

```powershell
az login
az account list --output table
az account set --subscription "<your-subscription-id-or-name>"
```

### 5. Bootstrap GitHub-to-Azure trust

Run the repo script:

```powershell
pwsh -File c:\repos\kaboom\scripts\azure\Initialize-KaboomGitHubOidc.ps1 `
  -SubscriptionId "<subscription-id>" `
  -ResourceGroupName "rg-kaboom-prod" `
  -Location "australiaeast" `
  -NamePrefix "kaboomprod" `
  -GitHubOwner "<github-owner>" `
  -GitHubRepository "kaboom"
```

This script will:

- set the active subscription
- create the resource group if needed
- deploy the GitHub OIDC bootstrap template
- print the values to add to GitHub

### 6. Add GitHub secrets and variables

Add these GitHub secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `POSTGRES_ADMIN_PASSWORD`
- `GOOGLE_CLIENT_SECRET` if using Google login

Add these GitHub variables:

- `AZURE_RESOURCE_GROUP`
- `AZURE_WEBAPP_NAME`
- `KABOOM_NAME_PREFIX`
- `KABOOM_POSTGRES_SERVER_NAME`
- `KABOOM_PUBLIC_ORIGIN`
- `GOOGLE_CLIENT_ID`

Prefer GitHub Environments over plain repository secrets if you want approvals and separation later.

### 7. Deploy the private Azure infrastructure

Run the repo script:

```powershell
pwsh -File c:\repos\kaboom\scripts\azure\Deploy-KaboomAzureInfra.ps1 `
  -SubscriptionId "<subscription-id>" `
  -ResourceGroupName "rg-kaboom-prod" `
  -Location "australiaeast" `
  -NamePrefix "kaboomprod" `
  -WebAppName "<globally-unique-web-app-name>" `
  -PostgresServerName "<globally-unique-postgres-server-name>" `
  -PublicOrigin "https://<your-web-app-name>.azurewebsites.net"
```

The script will:

- create the resource group if needed
- generate a strong PostgreSQL admin password if you do not supply one
- deploy the main private-network + Key Vault Bicep
- print the outputs
- remind you what to store in GitHub

### 8. Configure Google OAuth

In Google Cloud:

- create the OAuth client
- add redirect URI:

```text
https://<your-web-app-host>/signin-google
```

Then make sure:

- `GOOGLE_CLIENT_ID` is set as a GitHub variable
- `GOOGLE_CLIENT_SECRET` is set as a GitHub secret

### 9. Trigger the pipelines

Infra changes:

- push changes under `infra/**`
- the infra pipeline updates Azure resources only

App changes:

- push normal application changes
- the app deployment pipeline updates the App Service only

## Secret Handling Model

Best practice in this repo now is:

- local secrets: ASP.NET Core user-secrets
- deployment auth: GitHub OIDC to Azure
- deployment-time sensitive inputs: GitHub secrets
- runtime secrets: Azure Key Vault
- app runtime access to secrets: Key Vault references in App Service settings

That keeps real secrets out of:

- committed `appsettings.json`
- committed `.bicepparam` files
- long-lived Azure client secrets in GitHub

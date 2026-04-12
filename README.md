# Kaboom

Kaboom is a budgeting app with:

- an ASP.NET Core `.NET 10` backend
- a React frontend
- PostgreSQL persistence
- email/password signup and login
- optional Google OAuth sign-in

It now runs as a multi-user app. Each user gets their own budget workspace, and each new budget is seeded from `App_Data/budget-data.json` when that file exists, otherwise from demo data.

## What Changed For Hosting

Kaboom is no longer a single global budget. The app now has:

- ASP.NET Core Identity users
- cookie-based auth for the SPA
- budget ownership and membership tables
- budget-scoped accounts, categories, transactions, and monthly budgets

That makes it suitable to host publicly once you configure a real PostgreSQL instance and OAuth settings.

## Local Database Setup

Recommended for local development: run PostgreSQL in Docker rather than installing it directly on Windows.

1. Install Docker Desktop for Windows.
2. Start Docker Desktop.
3. From the repo root, start PostgreSQL:

```powershell
docker compose up -d postgres
```

Default connection string:

```text
Host=localhost;Port=5432;Database=kaboom;Username=postgres;Password=postgres
```

To stop the database:

```powershell
docker compose down
```

Important: this auth/multi-user release introduces a new schema. If you already have an older local Kaboom/Spendwise database volume, reset it once before running this version:

```powershell
docker compose down -v
docker compose up -d postgres
```

## Run Locally

### Backend + Vite dev mode

```powershell
cd c:\repos\kaboom
dotnet run --no-launch-profile --urls http://localhost:5085
```

In a second terminal:

```powershell
cd c:\repos\kaboom\ClientApp
npm install
npm run dev
```

Open `http://localhost:5173`.

### Built SPA mode

```powershell
cd c:\repos\kaboom\ClientApp
npm install
npm run build
cd ..
dotnet run --no-launch-profile --urls http://localhost:5085
```

Open `http://localhost:5085`.

If you want HTTPS locally:

```powershell
dotnet dev-certs https --trust
dotnet run
```

## Google OAuth Setup

Google sign-in is optional. Email/password signup works without it.

To enable Google login:

1. Create a Google Cloud project.
2. Configure the OAuth consent screen.
3. Create an OAuth client for a web application.
4. Add these redirect URIs:

Development:

```text
http://localhost:5085/signin-google
https://localhost:7085/signin-google
```

Production:

```text
https://your-domain.example.com/signin-google
```

5. Set app settings or environment variables:

```text
Authentication__Google__ClientId=...
Authentication__Google__ClientSecret=...
Application__PublicOrigin=https://your-domain.example.com
```

Notes:

- `Application__PublicOrigin` is used to validate absolute return URLs.
- In Vite dev mode, Google login returns to `http://localhost:5173` after the backend finishes the OAuth flow.
- In hosted mode, the app usually runs on a single origin, so the return path stays relative.

## Hosting Recommendation

The cleanest managed setup for this repo is:

- Azure App Service for the ASP.NET host
- Azure Database for PostgreSQL Flexible Server for the database

Infrastructure as code is included in [infra/main.bicep](c:\repos\kaboom\infra\main.bicep), with a sample parameters file at [main.parameters.example.bicepparam](c:\repos\kaboom\infra\main.parameters.example.bicepparam) and deployment notes in [infra/README.md](c:\repos\kaboom\infra\README.md).

### One-Time GitHub Bootstrap

The GitHub-to-Azure trust setup is now split out as a one-off bootstrap template in [github-oidc.bicep](c:\repos\kaboom\infra\bootstrap\github-oidc.bicep).

Run that once first:

```powershell
copy c:\repos\kaboom\infra\bootstrap\github-oidc.parameters.example.bicepparam c:\repos\kaboom\infra\bootstrap\github-oidc.prod.bicepparam
az deployment group create `
  --resource-group rg-kaboom-prod `
  --template-file c:\repos\kaboom\infra\bootstrap\github-oidc.bicep `
  --parameters c:\repos\kaboom\infra\bootstrap\github-oidc.prod.bicepparam
```

That creates the GitHub deployment identity and OIDC federated credential. Take its outputs and add these GitHub secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Also set:

- `AZURE_WEBAPP_NAME` as a GitHub secret

### Deploy The Azure Infrastructure

1. Install Azure CLI and sign in.
2. Create or choose an Azure resource group.
3. Copy `infra/main.parameters.example.bicepparam` to your own parameters file and fill in:
   - a globally unique `webAppName`
   - a globally unique `postgresServerName`
   - a strong `postgresAdminPassword`
   - optionally your Google OAuth client ID and secret
4. Deploy the Bicep:

```powershell
az group create --name rg-kaboom-prod --location australiaeast
az deployment group create `
  --resource-group rg-kaboom-prod `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.prod.bicepparam
```

The Bicep deployment creates:

- Azure App Service plan
- Linux Web App
- Application Insights + Log Analytics
- PostgreSQL Flexible Server + `kaboom` database
- PostgreSQL firewall rules

Required app settings in Azure App Service:

```text
ConnectionStrings__Kaboom=Host=...;Port=5432;Database=kaboom;Username=...;Password=...
Authentication__Google__ClientId=...
Authentication__Google__ClientSecret=...
Application__PublicOrigin=https://your-domain.example.com
ASPNETCORE_ENVIRONMENT=Production
```

### Deploy From GitHub Into Azure

The GitHub Actions workflow now uses Azure OIDC via `azure/login`, which GitHub and Microsoft both recommend over long-lived credentials.

After the bootstrap deployment, add these GitHub secrets or, preferably for a public repo, put them in a protected GitHub Environment:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_WEBAPP_NAME
```

How to map them:

- `AZURE_CLIENT_ID` = `githubDeploymentClientId` output from the bootstrap deployment
- `AZURE_TENANT_ID` = `azureTenantId` output
- `AZURE_SUBSCRIPTION_ID` = `azureSubscriptionId` output
- `AZURE_WEBAPP_NAME` = your deployed web app name

For the infra pipeline, also add these GitHub repository variables:

```text
AZURE_RESOURCE_GROUP
KABOOM_NAME_PREFIX
KABOOM_POSTGRES_SERVER_NAME
KABOOM_PUBLIC_ORIGIN
GOOGLE_CLIENT_ID
```

And these GitHub secrets:

```text
POSTGRES_ADMIN_PASSWORD
GOOGLE_CLIENT_SECRET
```

Then:

1. Run the one-time bootstrap.
2. Deploy the main infra once manually or let the infra workflow do it.
3. Push infra changes to `main` to trigger [infra-azure.yml](c:\repos\kaboom\.github\workflows\infra-azure.yml).
4. Push app changes to `main` to trigger [azure-appservice.yml](c:\repos\kaboom\.github\workflows\azure-appservice.yml).

The infra workflow:

- only runs when `infra/**` changes
- updates Azure resources from `infra/main.bicep`

The app deploy workflow:

- installs Node
- builds `ClientApp`
- publishes the ASP.NET app
- deploys the publish output to Azure App Service

### Google OAuth For Azure

Once the Azure web app exists, add this redirect URI in Google Cloud:

```text
https://<your-web-app-host>/signin-google
```

For example:

```text
https://kaboom-prod-example.azurewebsites.net/signin-google
```

If you later attach a custom domain, also add the custom-domain redirect URI and update `Application__PublicOrigin`.

## API Surface

Protected app endpoints live under `/api`:

- `/api/accounts`
- `/api/categories`
- `/api/budget`
- `/api/budget/allocations`
- `/api/transactions`
- `/api/reports`

Authentication endpoints:

- `/api/auth/me`
- `/api/auth/register`
- `/api/auth/login`
- `/api/auth/logout`
- `/api/auth/google/login`
- `/api/auth/google/callback`

## Container Notes

The included `Dockerfile` builds the ASP.NET host. For local development, the recommended workflow is still:

- PostgreSQL in Docker
- the app running locally with `dotnet run`

If you later want to run the app itself in a container too, pass the connection string and auth settings as environment variables.

## Documentation

- User guide: `docs/USER_GUIDE.md`

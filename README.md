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

For local secrets, do not put real values into [appsettings.json](c:\repos\kaboom\appsettings.json). Use ASP.NET Core user-secrets instead:

```powershell
cd c:\repos\kaboom
dotnet user-secrets set "ConnectionStrings:Kaboom" "Host=localhost;Port=5432;Database=kaboom;Username=postgres;Password=postgres"
dotnet user-secrets set "Application:PublicOrigin" "http://localhost:5085"
dotnet user-secrets set "Authentication:Google:ClientId" "<optional-google-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<optional-google-client-secret>"
```

A local file example is provided at [appsettings.Development.example.json](c:\repos\kaboom\appsettings.Development.example.json), but keep any real `appsettings.Development.json` file untracked.

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
- In Azure, the Google client secret should live in Key Vault rather than directly in App Service settings.

## Hosting Recommendation

The cleanest managed setup for this repo is:

- Azure App Service for the ASP.NET host
- Azure Database for PostgreSQL Flexible Server for the database
- Azure Key Vault for runtime secrets

Infrastructure as code is included in [infra/main.bicep](c:\repos\kaboom\infra\main.bicep), with a sample parameters file at [main.parameters.example.bicepparam](c:\repos\kaboom\infra\main.parameters.example.bicepparam) and deployment notes in [infra/README.md](c:\repos\kaboom\infra\README.md).

### One-Time GitHub Bootstrap

The GitHub-to-Azure trust setup is now split out as a one-off bootstrap template in [github-oidc.bicep](c:\repos\kaboom\infra\bootstrap\github-oidc.bicep).

Run that once first:

```powershell
copy c:\repos\kaboom\infra\bootstrap\github-oidc.parameters.example.bicepparam c:\repos\kaboom\infra\bootstrap\github-oidc.dev.bicepparam
az deployment group create `
  --resource-group rg-kaboom-dev `
  --template-file c:\repos\kaboom\infra\bootstrap\github-oidc.bicep `
  --parameters c:\repos\kaboom\infra\bootstrap\github-oidc.dev.bicepparam
```

That creates the GitHub deployment identity and OIDC federated credential. Take its outputs and add these GitHub secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Also set:

- `AZURE_WEBAPP_NAME` as a GitHub variable

### Deploy The Azure Infrastructure

1. Install Azure CLI and sign in.
2. Create or choose an Azure resource group.
3. Copy `infra/main.parameters.example.bicepparam` to your own parameters file and fill in:
   - a globally unique `webAppName`
   - a globally unique `postgresServerName`
   - optionally your Google OAuth client ID and secret
4. Deploy the Bicep:

```powershell
az group create --name rg-kaboom-dev --location australiaeast
$postgresPassword = -join ((48..57) + (65..90) + (97..122) + 33,35,36,37,42,43,45,61 | Get-Random -Count 32 | ForEach-Object { [char]$_ })
$googleClientSecret = Read-Host "Google client secret (optional, leave blank if not using Google login)"
az deployment group create `
  --resource-group rg-kaboom-dev `
  --template-file infra/main.bicep `
  --parameters infra/main.parameters.dev.bicepparam `
  --parameters postgresAdminPassword="$postgresPassword" `
  --parameters googleClientSecret="$googleClientSecret"
```

The Bicep deployment creates:

- Azure App Service plan
- Linux Web App
- Application Insights + Log Analytics
- Azure Key Vault
- virtual network + private DNS
- PostgreSQL Flexible Server + `kaboom` database
- private PostgreSQL connectivity
- private Key Vault connectivity

Required app settings in Azure App Service:

```text
Authentication__Google__ClientId=...
Application__PublicOrigin=https://your-domain.example.com
ASPNETCORE_ENVIRONMENT=Production
```

You no longer need to store the database connection string or Google client secret directly in App Service configuration. The app reads them from Key Vault through Key Vault references.

### Deploy From GitHub Into Azure

The GitHub Actions workflow now uses Azure OIDC via `azure/login`, which GitHub and Microsoft both recommend over long-lived credentials.

After the bootstrap deployment, add these GitHub secrets or, preferably for a public repo, put them in a protected GitHub Environment:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
```

How to map them:

- `AZURE_CLIENT_ID` = `githubDeploymentClientId` output from the bootstrap deployment
- `AZURE_TENANT_ID` = `azureTenantId` output
- `AZURE_SUBSCRIPTION_ID` = `azureSubscriptionId` output

For the infra pipeline, also add these GitHub repository variables:

```text
AZURE_RESOURCE_GROUP
AZURE_WEBAPP_NAME
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

Best practice:

- keep runtime secrets in Azure Key Vault
- keep only deployment-time bootstrap secrets in GitHub
- use GitHub OIDC for Azure auth so there is no Azure client secret in GitHub
- use GitHub Environment secrets/variables if you want approval gates between dev/staging/prod

Then:

1. Run the one-time bootstrap.
2. Deploy the main infra once manually or let the infra workflow do it.
3. Push infra changes to `main` to trigger [infra-azure.yml](c:\repos\kaboom\.github\workflows\infra-azure.yml).
4. Push app changes to `main` to trigger [azure-appservice.yml](c:\repos\kaboom\.github\workflows\azure-appservice.yml).

The infra workflow:

- only runs when `infra/**` changes
- updates Azure resources from `infra/main.bicep`
- passes secure inputs only at deployment time
- writes the real runtime secrets into Key Vault through the Bicep deployment

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
https://kaboom-dev-example.azurewebsites.net/signin-google
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
- Azure setup: `docs/AZURE_SETUP.md`

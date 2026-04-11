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

Required app settings in Azure App Service:

```text
ConnectionStrings__Kaboom=Host=...;Port=5432;Database=kaboom;Username=...;Password=...
Authentication__Google__ClientId=...
Authentication__Google__ClientSecret=...
Application__PublicOrigin=https://your-domain.example.com
ASPNETCORE_ENVIRONMENT=Production
```

The included GitHub Actions workflow at `.github/workflows/azure-appservice.yml` expects these repository secrets:

```text
AZURE_WEBAPP_NAME
AZURE_WEBAPP_PUBLISH_PROFILE
```

That workflow:

- installs Node
- builds `ClientApp`
- publishes the ASP.NET app
- deploys the publish output to Azure App Service

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

# Spendwise

Spendwise is a self-contained ASP.NET Core budgeting app inspired by zero-based budgeting principles:

- Give every dollar a job.
- Prepare for less frequent expenses.
- Fund goals intentionally.
- Reassign money when priorities change.

## Included

- Account tracking with current balances.
- Category groups and category targets.
- Monthly budget screen with assigned, activity, and available amounts.
- Move-money workflow to adapt when plans change.
- Transaction entry that updates account balances.
- Dashboard and reports for spending and category progress.
- PostgreSQL persistence with automatic schema creation on startup.
- One-time import from `App_Data/budget-data.json` when the database is empty.

## Local Database Setup

Recommended for local development: run PostgreSQL in Docker rather than installing it directly on Windows.

1. Install Docker Desktop for Windows.
   Docker's Windows install docs: https://docs.docker.com/desktop/setup/install/windows-install/
2. Start Docker Desktop.
3. From the repo root, start PostgreSQL:

```powershell
docker compose up -d postgres
```

4. The app expects this default local connection string:

```text
Host=localhost;Port=5432;Database=spendwise;Username=postgres;Password=postgres
```

That connection string is already configured in `appsettings.json`.

To stop the database later:

```powershell
docker compose down
```

To reset the local database and re-import from `App_Data/budget-data.json`:

```powershell
docker compose down -v
docker compose up -d postgres
```

## Run With .NET 10 SDK

With PostgreSQL running:

```powershell
dotnet run --no-launch-profile --urls http://localhost:5085
```

Then open `http://localhost:5085`.

On first startup, Spendwise will:

- create the PostgreSQL schema automatically
- import `App_Data/budget-data.json` if the database is empty
- otherwise just use the existing database data

If you want the launch profile with HTTPS support, use:

```powershell
dotnet dev-certs https --trust
dotnet run
```

The project launch settings point at `http://localhost:5085` and `https://localhost:7085`.

If port `5432` is already in use on your machine, change the published port in `compose.yaml` and update the `Port=` value in the `Spendwise` connection string to match.

## Run the App Container

The included `Dockerfile` builds the app itself. For local development, the best workflow is usually:

- PostgreSQL in Docker
- the ASP.NET app running natively with `dotnet run`

That gives you fast rebuilds while keeping the database isolated. If you later want to run the app in a container too, pass a PostgreSQL connection string via environment variable, for example `ConnectionStrings__Spendwise=Host=postgres;Port=5432;Database=spendwise;Username=postgres;Password=postgres`.

## Notes

- The app is intentionally original and does not use YNAB branding or proprietary assets.
- For local development, a Postgres container is the easiest setup path and keeps the database isolated from the rest of your machine.
- The schema is created automatically on startup. On first boot against an empty database, the app imports `App_Data/budget-data.json` if present.

## Documentation

- User guide: `docs/USER_GUIDE.md`

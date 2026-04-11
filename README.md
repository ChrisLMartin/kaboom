# Spendwise

Spendwise is a budgeting app with an ASP.NET Core API backend, a React frontend, and PostgreSQL persistence.

It follows a zero-based budgeting approach:

- Give every dollar a job.
- Prepare for less frequent expenses.
- Fund goals intentionally.
- Reassign money when priorities change.

## Included

- Account tracking with current balances.
- Category groups and categories managed from the budget screen.
- Monthly budget screen with assigned, activity, and available amounts.
- Transaction register with inline editing and newest-first ordering.
- Reports for spending and category progress.
- React SPA frontend served by the ASP.NET host after build.
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

## Run the App

There are two good local workflows:

1. React dev mode:
   - Vite serves the frontend on `http://localhost:5173`
   - ASP.NET serves the API on `http://localhost:5085`
2. Built SPA mode:
   - build the frontend into `ClientApp/dist`
   - ASP.NET serves both the API and the built frontend on `http://localhost:5085`

### React Dev Mode

With PostgreSQL running:

```powershell
dotnet run --no-launch-profile --urls http://localhost:5085
```

In a second terminal:

```powershell
cd ClientApp
npm install
npm run dev
```

Then open `http://localhost:5173`.

### Built SPA Mode

With PostgreSQL running:

```powershell
cd ClientApp
npm install
npm run build
cd ..
dotnet run --no-launch-profile --urls http://localhost:5085
```

Then open `http://localhost:5085`.

On first startup, Spendwise will:

- create the PostgreSQL schema automatically
- import `App_Data/budget-data.json` if the database is empty
- otherwise just use the existing database data

If you want the ASP.NET launch profile with HTTPS support, use:

```powershell
dotnet dev-certs https --trust
dotnet run
```

The project launch settings point at `http://localhost:5085` and `https://localhost:7085`.

If port `5432` is already in use on your machine, change the published port in `compose.yaml` and update the `Port=` value in the `Spendwise` connection string to match.

## API Surface

The frontend talks to narrower screen/domain-focused endpoints under `/api`:

- `/api/budget?month=2026-04&months=3`
- `/api/budget/allocations`
- `/api/transactions`
- `/api/accounts`
- `/api/categories`
- `/api/reports?month=2026-04`

That keeps the frontend from depending on a single monolithic app-state response.

## Run the App Container

The included `Dockerfile` builds the ASP.NET host. For local development, the best workflow is usually:

- PostgreSQL in Docker
- the ASP.NET app running natively with `dotnet run`

That gives you fast rebuilds while keeping the database isolated. If you later want to run the app in a container too, pass a PostgreSQL connection string via environment variable, for example `ConnectionStrings__Spendwise=Host=postgres;Port=5432;Database=spendwise;Username=postgres;Password=postgres`.

## Notes

- The app is intentionally original and does not use YNAB branding or proprietary assets.
- For local development, a Postgres container is the easiest setup path and keeps the database isolated from the rest of your machine.
- The schema is created automatically on startup. On first boot against an empty database, the app imports `App_Data/budget-data.json` if present.

## Documentation

- User guide: `docs/USER_GUIDE.md`

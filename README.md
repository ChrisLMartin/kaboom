# Spendwise

Spendwise is a self-contained ASP.NET Core budgeting app inspired by zero-based budgeting principles:

- Give every dollar a job.
- Prepare for less frequent expenses.
- Build a buffer for next month.
- Fund goals intentionally.
- Reassign money when priorities change.

## Included

- Account tracking with current balances.
- Category groups and category targets.
- Monthly budget screen with assigned, activity, and available amounts.
- Move-money workflow to adapt when plans change.
- Transaction entry that updates account balances.
- Dashboard and reports for spending, targets, and buffer progress.
- JSON file persistence in `App_Data/budget-data.json`.

## Run With .NET 10 SDK

Tested local command:

```powershell
dotnet run --no-launch-profile --urls http://localhost:5085
```

Then open `http://localhost:5085`.

If you want the launch profile with HTTPS support, use:

```powershell
dotnet dev-certs https --trust
dotnet run
```

The project launch settings now point at `http://localhost:5085` and `https://localhost:7085`.

## Run With Docker

```powershell
docker build -t spendwise .
docker run --rm -p 8080:8080 spendwise
```

Then open `http://localhost:8080`.

## Notes

- The app is intentionally original and does not use YNAB branding or proprietary assets.
- The native app was locally verified on `http://localhost:5085` with a successful `200 OK` response.

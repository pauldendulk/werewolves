# Local Development

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) with npm
- [Python 3.11+](https://www.python.org/) with `mkdocs` and `mkdocs-material` (for docs)

## Quick Start

```powershell
# From the project root — starts both frontend and backend
.\start.ps1
```

| Service | URL |
|---|---|
| Angular frontend | http://localhost:4200 |
| Backend API | http://localhost:5000/api |
| Swagger UI | http://localhost:5000/swagger |

---

## Starting Services Individually

```powershell
# Backend
cd backend/WerewolvesAPI
dotnet run

# Frontend
cd frontend/werewolves-app
npm start
```

The VS Code **Start Backend** and **Start Angular** tasks in `.vscode/tasks.json` automatically kill any existing process on the port before starting, so they are always safe to run.

---

## Building

```powershell
# Backend
cd backend/WerewolvesAPI
dotnet build

# Frontend
cd frontend/werewolves-app
npm run build
```

!!! warning "Stop the backend before building"
    The .NET build cannot overwrite the locked `.exe` while the backend process is running. Stop it first:
    ```powershell
    $conn = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
    if ($conn) { $conn | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue } }
    Start-Sleep 2
    ```
    Or simply run `.\stop.ps1` to stop everything.

---

## Testing

```powershell
# Backend unit tests
cd backend/WerewolvesAPI.Tests
dotnet test

# Frontend unit tests
cd frontend/werewolves-app
npm test

# E2E tests (requires both services running)
cd frontend/werewolves-app
npx playwright test
```

All tests must pass before committing. The E2E tests are in `frontend/werewolves-app/e2e/` and cover full gameplay flows for each special role.

---

## Code Style Rules

- **One class per file** — every C# class, record, or DTO lives in its own `.cs` file. Do not stack multiple classes in one file.
- Follow existing TypeScript and C# patterns in the respective projects.

---

## Development Workflow

1. Make your change
2. Build (`dotnet build` or `npm run build`) — read all errors and warnings
3. Fix any build errors before continuing
4. Write or update tests for your change
5. Run all tests (`dotnet test` and `npm test`)
6. Verify tests pass — all must be green
7. For visual changes, verify the app in the browser

---

## Stopping the App

```powershell
.\stop.ps1
```

Or kill individual ports:

```powershell
# Port 5000 (backend)
$conn = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
if ($conn) { $conn | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force } }

# Port 4200 (frontend)
$conn = Get-NetTCPConnection -LocalPort 4200 -ErrorAction SilentlyContinue
if ($conn) { $conn | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force } }
```

---

## Common Build Issues

### Frontend

| Issue | Fix |
|---|---|
| Import errors | Check file paths and module imports |
| TypeScript type errors | Verify type annotations and interfaces |
| Missing packages | Run `npm install` |

### Backend

| Issue | Fix |
|---|---|
| Namespace errors | Verify `using` statements |
| Type mismatches | Check method signatures and return types |
| Missing NuGet packages | Run `dotnet restore` |
| `error MSB3027` (locked binary) | Stop the running backend first, then rebuild |

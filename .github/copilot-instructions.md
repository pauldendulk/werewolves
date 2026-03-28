# Development Instructions for GitHub Copilot

## Project Overview

**Werewolves App** is a mobile companion for playing the in-person game of Werewolf without a human moderator. Players join a shared session on their phones, receive secret roles (Werewolves or Villagers), and progress through voting phases to eliminate suspects. The app manages game flow, timing, and voting via text-to-speech narration.

**Tech Stack**: Angular 20 frontend (port 4200) + .NET 9 backend API (port 5000)

## Code Style Rules

- **One class per file** – Every class, record, or DTO must be in its own file. Do not define multiple classes in a single file (e.g., do not put request DTOs at the bottom of a controller).
- **No silent error swallowing** – Never catch an exception just to log it and continue. Errors must surface loudly. Do not use `_ = SomeAsync()` fire-and-forget patterns. If a background task must run without being awaited, use the `ThrowOnFailure(Task)` helper in `GameService` so that any failure crashes the process immediately. The preference is always: **break hard and in your face**.
- **Named methods over comments** – If code needs a comment to explain what it does, extract it into a well-named method instead. Comments explain *why*; method names explain *what*.
- **Pure functions** – Prefer methods that take inputs and return a value without mutating shared state (i.e., no side effects). In C#, these are typically `private static`. In TypeScript, use `private static` inside a class or a module-level function (unexported) outside the class. This makes logic easier to test and reason about in isolation.

## Critical: Build and Test After Every Change

**Before stopping work on any task, you MUST:**

1. **Build the project** - Every change requires a fresh build
2. **Inspect build errors** - Carefully review all compilation errors and warnings
3. **Fix all errors** - Do not proceed until the build is clean
4. **Fix deprecation warnings** - Resolve any `[Obsolete]` (C#) or `@deprecated` (TypeScript/Angular) warnings introduced or surfaced by your changes. Do not leave deprecated API usage behind.
5. **Run tests** - Execute the test suite to verify your changes
6. **Ensure tests pass** - All tests must be green before you stop working

## Building the Project

### Backend (.NET)
```powershell
cd backend/WerewolvesAPI
dotnet build
```

Check for compilation errors and warnings. Fix any issues before proceeding.

### Frontend (Angular)
```powershell
cd frontend/werewolves-app
npm run build
```

Review the build output for errors. The Angular compiler will show detailed error messages if anything is wrong.

## Running Tests

### Backend Tests
```powershell
cd backend/WerewolvesAPI.Tests
dotnet test
```

All tests must pass. If tests fail:
- Review the test output
- Fix the failing tests or your code
- Re-run until all tests are green

### Frontend Tests
```powershell
cd frontend/werewolves-app
npm test
```

Ensure all tests pass before completing the task.

## Adding Tests

**Every feature or bug fix should include tests:**

- **Backend**: Add tests in `backend/WerewolvesAPI.Tests`
  - Use FluentAssertions for readable assertions
  - Follow existing test patterns
  - Test both success and error cases

- **Frontend**: Add tests in component `.spec.ts` files
  - Test component logic
  - Test user interactions
  - Mock dependencies appropriately

## Development Workflow

1. **Make your change** - Edit code, add features, fix bugs
2. **Build immediately** - `dotnet build` or `npm run build`
3. **Check build output** - Read all errors and warnings carefully
4. **Fix build errors** - Resolve issues before moving forward
5. **Write/update tests** - Add tests for your changes
6. **Run all tests** - Ensure nothing broke
7. **Verify tests pass** - All tests must be green
8. **Repeat** - If tests fail, fix and rebuild

## Common Build Issues

### Frontend
- **Import errors**: Check file paths and module imports
- **Type errors**: Ensure TypeScript types are correct
- **Dependency issues**: Run `npm install` if packages are missing

### Backend
- **Namespace errors**: Verify using statements
- **Type mismatches**: Check method signatures and return types
- **NuGet issues**: Run `dotnet restore` if packages are missing
- **Locked binary** (`error MSB3027`): The backend process is still running and has locked the `.exe`. Stop it first (see below), then rebuild.

## Port / Process Management

If port 5000 or 4200 is already in use, assume the app is already running. **Stop the process first**, then rebuild or restart.

### Stop the backend (port 5000)
```powershell
$conn = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
if ($conn) { $conn | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue } }
Start-Sleep 2
```

### Stop the frontend (port 4200)
```powershell
$conn = Get-NetTCPConnection -LocalPort 4200 -ErrorAction SilentlyContinue
if ($conn) { $conn | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue } }
Start-Sleep 2
```

### Stop both at once
```powershell
.\stop.ps1
```

**Important**: Always stop the backend before running `dotnet build` if the backend is currently running. The .NET build cannot overwrite the locked `.exe` while the process is alive.

The VS Code **Start Backend** and **Start Angular** tasks automatically stop any existing process on the port before starting, so running them is always safe.

## Starting the Application

Use the provided scripts to start both frontend and backend:

```powershell
# From project root
.\start.ps1
```

This will:
- Stop any existing processes on ports 5000 and 4200
- Start the .NET backend on http://localhost:5000
- Start the Angular frontend on http://localhost:4200

## Port Information

- **Frontend**: http://localhost:4200
- **Backend**: http://localhost:5000/api
- **Swagger**: http://localhost:5000/swagger

## E2E Screenshots

Screenshots are generated by a Playwright spec and appear in `docs-src/docs/screenshots/`. They are referenced in `docs-src/docs/game-concept.md`.

### When to regenerate screenshots

If you change any screen's layout, content, or UI logic, regenerate the affected screenshot(s). Check which screenshots correspond to the modified screen by looking at the filenames in `docs-src/docs/screenshots/` (they are numbered to match the Playwright test names, e.g. `22-game-over.png` → test `22-game-over`).

### How to regenerate a screenshot

The Angular dev server must be running on http://localhost:4200 (use `Start Angular` task).

```powershell
# Regenerate a single screenshot
cd frontend/werewolves-app
npx playwright test --grep "<test-name>"

# Regenerate all screenshots
npx playwright test
```

Screenshots are saved to `docs-src/docs/screenshots/` automatically.

### Mock data tips

- The Playwright spec mocks all API calls; no backend is needed.
- Player data lives in `makePlayer()` in `e2e/screenshots.spec.ts`. The `PlayerOverride` interface controls per-player fields.
- When a new field is added to `PlayerState`, update `makePlayer()` with a sensible default so all tests keep working.

## Running E2E Tests

```powershell
cd frontend/werewolves-app
npx playwright test
```

After implementing a feature that touches a particular screen, run the Playwright test(s) for that screen to verify they still pass. If the screen's appearance changed, also regenerate the screenshot.

## Task Completion Checklist

Before marking a task as complete:

- [ ] Code builds without errors (`dotnet build` and `npm run build`)
- [ ] No deprecation warnings (`[Obsolete]` in C#, `@deprecated` in TypeScript/Angular) introduced or left unresolved
- [ ] All existing tests pass
- [ ] New tests added for your changes
- [ ] All new tests pass
- [ ] No console errors when running the application
- [ ] Changes have been verified in the running application
- [ ] If any screen changed visually: relevant Playwright screenshot(s) regenerated

---

**Remember: Never stop work without ensuring the build is clean and all tests pass!**

## Skills

The following skill files contain domain-specific knowledge. Read the relevant file when working on that topic.

| Skill | When to use |
|---|---|
| [gcp-cloud-sql](.github/skills/gcp-cloud-sql/SKILL.md) | Setting up or modifying GCP infrastructure: Cloud Run, Cloud SQL, Secret Manager, Terraform, GitHub Actions CI/CD |

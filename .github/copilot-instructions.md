# Development Instructions for GitHub Copilot

## Speech-to-Text Corrections

The user dictates via speech-to-text. Apply these corrections silently when interpreting requests:

- **"faces"** → **"phases"** — e.g. "the night faces" means "the night phases", "LoverReveal face" means "LoverReveal phase"

---

## Project Overview

**Werewolves App** is a mobile companion for playing the in-person game of Werewolf without a human moderator. Players join a shared session on their phones, receive secret roles (Werewolves or Villagers), and progress through voting phases to eliminate suspects. The app manages game flow, timing, and voting via text-to-speech narration.

**Tech Stack**: Angular 20 frontend (port 4200) + .NET 9 backend API (port 5000)

## Code Style Rules

- **Never commit or push** – Do not run `git commit`, `git push`, or any other git write command. The user manages source control manually.
- **One class per file** – Every class, record, or DTO must be in its own file. Do not define multiple classes in a single file (e.g., do not put request DTOs at the bottom of a controller).
- **No silent error swallowing** – Never catch an exception just to log it and continue. Errors must surface loudly. Do not use `_ = SomeAsync()` fire-and-forget patterns. If a background task must run without being awaited, use the `ThrowOnFailure(Task)` helper in `GameService` so that any failure crashes the process immediately. The preference is always: **break hard and in your face**.
- **Named methods over comments** – If code needs a comment to explain what it does, extract it into a well-named method instead. Comments explain *why*; method names explain *what*.
- **Pure functions** – Prefer methods that take inputs and return a value without mutating shared state (i.e., no side effects). In C#, these are typically `private static`. In TypeScript, use `private static` inside a class or a module-level function (unexported) outside the class. This makes logic easier to test and reason about in isolation.

## CRITICAL: Build and Test After Every Single Change — No Exceptions

> **This is not optional. There are no exceptions. One line of code changed? Build. One file renamed? Test. If you skip this step, you WILL introduce regressions that are painful to track down.**

A change in one file can break tests in five others. You do not know what you broke until you run the tests. **Do not assume tests pass. Run them and read the output.**

**Before stopping work on any task, you MUST:**

1. **Build the project** — Every change requires a fresh build, no matter how small
2. **Inspect build errors** — Carefully review all compilation errors and warnings
3. **Fix all errors** — Do not proceed until the build is completely clean
4. **Fix deprecation warnings** — Resolve any `[Obsolete]` (C#) or `@deprecated` (TypeScript/Angular) warnings introduced or surfaced by your changes. Do not leave deprecated API usage behind.
5. **Run ALL tests** — Backend unit tests AND frontend/E2E tests, every time
6. **Read the test output** — Don't just check the summary count; read failing test names and messages
7. **Ensure all tests pass** — Every single test must be green before you stop working
8. **If a test fails that you didn't write** — You still own it. Investigate and fix it before stopping.

### Zero Tolerance for Red Tests

> **A failing test is a failing test. It does not matter whether YOU caused it, whether it was broken before you started, or whether it is "unrelated" to your task. If any test is red, you MUST fix it or report it to the user before stopping. There is no such thing as a "pre-existing" or "known" failure that can be ignored.**

- Do NOT dismiss a failure as "pre-existing" or "not my fault" — every red test is your problem right now.
- Do NOT say "59/60 passed" as if that is acceptable. It is not. 60/60 is the only acceptable result.
- If you genuinely cannot fix a failing test (e.g., it requires credentials or infrastructure you don't have), you MUST explicitly flag it to the user as a blocker and ask for guidance. Do not silently move on.
- If a test is flaky (sometimes passes, sometimes fails), that is also a bug. Investigate and fix the flakiness.

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

If you change any screen's layout, content, or UI logic, regenerate the affected screenshot(s). Check which screenshots correspond to the modified screen by looking at the filenames in `docs-src/docs/screenshots/` (they match the Playwright test names, e.g. `final-scores-reveal.png` → test `final-scores-reveal`).

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

### CRITICAL: E2E Tests Must Simulate Real Game Flow — No Moderator Shortcuts

> **E2E tests must simulate how real players interact with the game. Never use moderator shortcut buttons to bypass phases that real players are required to complete.** Tests that skip real player actions give false confidence — they pass even when the actual player interaction is broken.

**Phase-by-phase rules:**

- **`WerewolvesMeeting`** — Every wolf player **must** click the **"Ready"** button. Do **NOT** use `Skip night`. Find all wolves with `players.filter(p => p.role === 'Werewolf')` and have each one click `getByRole('button', { name: 'Ready' })`.
- **`Werewolves` (round 2+)** — The wolf player **must** select a victim and click **"Confirm kill"**. Do **NOT** use `Skip night`. The phase auto-advances after all wolves have voted.
- **`NightAnnouncement` / `DayAnnouncement`** — These are timed auto-advance phases with no required player action. The moderator `Skip` button is acceptable here.
- **`Discussion`** — The moderator `Force end discussion` button is acceptable (no specific player action required).
- **`LoversReveal` / skill phases** — The moderator `Skip` button is acceptable when the test's purpose is not to test that specific phase.

### Playwright blocked or hanging

If Playwright tests hang, time out immediately, or fail with connection errors, the cause is usually a conflicting process — either the dev server is already running in a terminal, or another Playwright instance is still alive. **Do not wait or retry. Stop immediately and ask the user to kill the blocking process before trying again.**

## Task Completion Checklist

Before marking a task as complete:

- [ ] Code builds without errors (`dotnet build` and `npm run build`)
- [ ] No deprecation warnings (`[Obsolete]` in C#, `@deprecated` in TypeScript/Angular) introduced or left unresolved
- [ ] **ALL tests pass — zero failures, zero exceptions** (not "all tests I touched", ALL tests in the entire suite)
- [ ] New tests added for your changes
- [ ] All new tests pass
- [ ] No console errors when running the application
- [ ] Changes have been verified in the running application
- [ ] If any screen changed visually: relevant Playwright screenshot(s) regenerated

---

**Remember: Never stop work without ensuring the build is clean and ALL tests pass — every single one, with zero failures. If any test is red for any reason, fix it or escalate it to the user. "Pre-existing failure" is not an excuse to move on.**

## Authorization Model

**Do not conflate `IsCreator` and `IsModerator`.** These are distinct concepts on `PlayerState`:

- `IsCreator` — informational only; shown as the HOST badge in the lobby. Grants no permissions.
- `IsModerator` — the action gate for all privileged operations (start game, update settings, remove player, force-advance phase).

The creator is automatically a moderator, but moderator rights are independent of who created the session.

**Always gate privileged actions on `IsModerator`, never on `game.CreatorId`.**

See [`docs-src/docs/authorization.md`](docs-src/docs/authorization.md) for the full design, code patterns, and the reasoning behind this separation.

## Skills

The following skill files contain domain-specific knowledge. Read the relevant file when working on that topic.

| Skill | When to use |
|---|---|
| [gcp-cloud-sql](.github/skills/gcp-cloud-sql/SKILL.md) | Setting up or modifying GCP infrastructure: Cloud Run, Cloud SQL, Secret Manager, Terraform, GitHub Actions CI/CD |
| [localization](.github/skills/localization/SKILL.md) | Working with translatables, or understanding the localization architecture |
| [audio](.github/skills/audio/SKILL.md) | Working with audio translatables, narration.json, AudioKey constants, NarrationStudio, or writing `description` fields for audio entries |

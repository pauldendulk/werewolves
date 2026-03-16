# Werewolves App

A mobile companion for playing the in-person game of **Werewolf** without a human moderator. Players join a shared session on their phones, receive secret roles, and work through timed night and day phases — all orchestrated by the app.

## What it does

- A host creates a game and shares a join link or QR code
- Players join from their own phones and receive secret roles (Werewolves or Villagers)
- The app narrates phases with pre-generated audio, runs countdown timers, and tallies votes
- No dedicated moderator is needed — the app handles all facilitation

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 20 + PrimeNG v18 (TypeScript) |
| Backend | .NET 9 ASP.NET Core Web API (C#) |
| E2E Tests | Playwright (Chromium) |
| Hosting | Firebase Hosting (frontend) + Google Cloud Run (backend) |
| Audio | Pre-generated MP3s via Azure AI Speech |

## Key Links

| Resource | URL |
|---|---|
| Frontend | http://localhost:4200 (local) |
| Backend API | http://localhost:5000/api (local) |
| Swagger UI | http://localhost:5000/swagger (local) |
| Repo | https://github.com/pauldendulk/werewolves |

## Documentation Map

- **[Game Design → Concept & Rules](game-concept.md)** — How the game works, roles, win conditions, configurable settings
- **[Game Design → Screen Inventory](screens.md)** — All screens, timer design, overtime behaviour
- **[Technical → Architecture](architecture.md)** — System design, API, state polling, frontend structure
- **[Technical → Audio Narration](audio.md)** — How audio is generated and served
- **[Developer Guide → Local Development](development.md)** — Build, test, and run the app locally
- **[Developer Guide → Deployment](deployment.md)** — Firebase, Cloud Run, and Terraform

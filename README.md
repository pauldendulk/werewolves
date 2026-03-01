# Werewolves App

A mobile companion for playing the in-person game of Werewolf without a human moderator. Players join a shared session on their phones, receive secret roles (Werewolves or Villagers), and progress through voting phases to eliminate suspects.

## Tech Stack

- **Frontend**: Angular 20 + PrimeNG v18 (TypeScript)
- **Backend**: .NET 9 ASP.NET Core Web API (C#)
- **E2E Tests**: Playwright (Chromium)

## Quick Start

```powershell
# Start both frontend and backend
.\start.ps1
```

- Frontend: http://localhost:4200
- Backend API: http://localhost:5000/api
- Swagger: http://localhost:5000/swagger

## Project Structure

```
backend/
  WerewolvesAPI/          # .NET Web API
    Controllers/           # API endpoints
    DTOs/                  # Data transfer objects
    Models/                # Game state, player state
    Services/              # Game logic (GameService)
  WerewolvesAPI.Tests/     # xUnit + FluentAssertions tests

frontend/
  werewolves-app/          # Angular application
    src/app/
      components/          # create-game, join-game, lobby, session
      models/              # TypeScript interfaces
      services/            # GameService, PollingService, AudioService
    e2e/                   # Playwright end-to-end tests
```

## Game Flow

1. **Create Game** – Host creates a game and shares the QR code / join link
2. **Join Game** – Players scan QR or open link to join the lobby
3. **Lobby** – Host configures settings (min/max players, werewolf count, discussion time) and starts the game
4. **Role Reveal** – Each player privately views their assigned role (Werewolf or Villager)
5. **Night Phase** – Werewolves secretly choose a victim
6. **Day Phase** – Village discusses and votes to eliminate a suspect
7. **Game Over** – Villagers win when all werewolves are eliminated; Werewolves win when no villagers remain

## Development

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for build, test, and workflow details.

```powershell
# Backend build & test
cd backend/WerewolvesAPI; dotnet build
cd backend/WerewolvesAPI.Tests; dotnet test

# Frontend build & test
cd frontend/werewolves-app; npm run build
cd frontend/werewolves-app; npm test
```

## Deployment

- **Backend**: Docker → Google Artifact Registry → Cloud Run (europe-west4)
- **Frontend**: `ng build --configuration production` → Firebase Hosting

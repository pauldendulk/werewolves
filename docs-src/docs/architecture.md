# Architecture

## System Overview

```
┌─────────────────────────────────┐         ┌──────────────────────────────┐
│  Angular SPA (Firebase Hosting) │ ──HTTP──▶│  .NET 9 API (Cloud Run)      │
│  Angular 20 + PrimeNG v18       │◀──JSON── │  ASP.NET Core Web API        │
└─────────────────────────────────┘         └──────────────────────────────┘
         (port 4200 locally)                         (port 5000 locally)
```

The frontend is a single-page application; the backend is a stateless (in-process) REST API. There is no persistent database — all game state is held in memory on the backend for the lifetime of the process.

---

## Backend

### Project: `backend/WerewolvesAPI`

| Folder | Contents |
|---|---|
| `Controllers/` | `GameController` — all HTTP endpoints under `/api/game` |
| `Services/` | `GameService` + `IGameService` — all game logic |
| `Models/` | `GameState`, `PlayerState`, `EliminationEntry`, `PlayerSkill`, `EliminationCause` |
| `DTOs/` | One request/response DTO per action (one class per file) |

### API Conventions

- All endpoints are under `/api/game/{gameId}`
- `POST` for mutating actions (join, vote, night actions, etc.)
- `GET /api/game/{gameId}` returns the full `GameState` for a given version (used by polling)
- Swagger UI is available at `/swagger` in development

### Game State Model

`GameState` is the central document returned to every client. It contains:

- `phase` — current game phase (e.g. `Discussion`, `WerewolvesTurn`)
- `players` — list of `PlayerState` with name, role (only visible to the owning client), alive status
- `phaseEndsAt` — UTC timestamp driving the countdown timer (null when no timer)
- `version` — monotonically incrementing integer; clients send their current version and only receive a response when the version changes
- `eliminations` — ordered history of eliminations with cause

### Game Logic

`GameService` is the single source of truth for all state transitions. It is registered as a singleton so all HTTP requests share the same in-memory state. There is no external state store.

---

## Frontend

### Project: `frontend/werewolves-app`

| Path | Contents |
|---|---|
| `src/app/components/create-game/` | Create a new game session |
| `src/app/components/join-game/` | Join via direct link or QR code scan |
| `src/app/components/lobby/` | Waiting room, settings panel, player list |
| `src/app/components/session/` | In-game shell + all phase sub-components |
| `src/app/services/` | `GameService`, `PollingService`, `AudioService` |
| `src/app/models/` | TypeScript interfaces mirroring backend DTOs |

### State Polling

The frontend uses **version-based long-poll**:

1. Client sends `GET /api/game/{id}?version={n}`
2. Server holds the request until the game version exceeds `n`, then responds
3. Client immediately re-polls on receipt
4. `PollingService` encapsulates this loop; components subscribe to an observable

This avoids the overhead of websockets while keeping latency low (typically sub-second).

### Routing

| Route | Component |
|---|---|
| `/` | `CreateGameComponent` |
| `/game/:id` | `JoinGameComponent` |
| `/game/:id/lobby` | `LobbyComponent` |
| `/game/:id/session` | `SessionComponent` |

### Audio

Pre-generated MP3 files are served from `public/assets/audio/en-US/`. `AudioService` plays the appropriate track at each phase transition, based on a manifest. See [Audio Narration](audio.md) for details.

---

## Infrastructure

| Component | Technology | Region |
|---|---|---|
| Frontend hosting | Firebase Hosting | Global CDN |
| Backend API | Google Cloud Run | europe-west4 |
| Container registry | Google Artifact Registry | europe-west4 |
| IaC | Terraform | — |

The backend scales to zero when idle (min instances = 0) and is limited to one instance maximum to keep costs at zero for a demo workload.

See [Deployment](deployment.md) for build and deploy instructions.

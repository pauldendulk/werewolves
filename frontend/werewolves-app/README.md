# Werewolves App – Frontend

Angular 20 single-page application with PrimeNG v18 UI components.

## Development

```bash
npm start          # Dev server at http://localhost:4200
npm run build      # Production build → dist/
npm test           # Unit tests (Karma + Jasmine)
```

## Components

| Component | Route | Purpose |
|-----------|-------|---------|
| `CreateGameComponent` | `/` | Create a new game session |
| `JoinGameComponent` | `/game/:id` | Join an existing game via link/QR |
| `LobbyComponent` | `/game/:id/lobby` | Waiting room, settings, player list |
| `SessionComponent` | `/game/:id/session` | In-game: role reveal, night, discussion, voting, game over |

## Services

- **GameService** – HTTP calls to the backend API
- **PollingService** – Periodic game state polling (1s interval, version-based)
- **AudioService** – Text-to-speech narration via Web Speech API

## Environment

API URL is configured in `src/environments/environment.ts`.

## E2E Tests

Playwright tests are in the `e2e/` directory. See the root README for how to run the full stack.

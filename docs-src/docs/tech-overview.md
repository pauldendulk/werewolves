# Technology Overview

The Werewolves app is a full-stack, cloud-native application built with modern, production-grade technologies across every layer — from infrastructure-as-code to AI-guided end-to-end tests. Here is what powers it.

---

## Frontend — Angular 20 + PrimeNG

The player-facing UI is a **TypeScript** single-page application built on **Angular 20**, one of the most mature and well-structured frontend frameworks available. It is componentised around the game's 16 distinct screens — from the pre-game lobby through 13 timed in-game phases — with reactive state driven by RxJS observables. **PrimeNG v18** provides the UI component library, keeping the visual layer consistent and professional without reinventing widgets.

Angular's strict compilation, dependency injection container, and routing system make the codebase highly predictable and easy to navigate as it grows.

---

## Backend — .NET 9 ASP.NET Core

The game server is written in **C#** targeting **.NET 9**, Microsoft's latest high-performance runtime. It exposes a clean REST API over ASP.NET Core, with Swagger/OpenAPI auto-generated for every endpoint.

All game logic lives in a single `GameService` singleton — a carefully designed **finite-state machine** that governs every legal state transition in the game: role assignment, special night actions (Cupid, Seer, Witch, Hunter), day voting with tiebreak resolution, lover-pair mechanics, and win-condition evaluation across three competing win conditions (Village, Werewolves, Lovers). The state machine is intentionally free of external storage; all game state is held in memory and delivered to clients on demand.

---

## Version-Based Long Polling

Rather than the heavy infrastructure of WebSockets, the app uses **version-based long polling**: clients hold open a `GET` request tagged with their current game version; the server blocks until the version increments, then responds instantly. This delivers sub-second latency for all players with no persistent connection overhead and no third-party message broker. It is a simple, elegant solution that scales to zero between requests.

---

## Audio Narration — Azure AI Speech

Phase narration is delivered as **pre-generated MP3 files** synthesised via **Azure AI Cognitive Services (Speech)**. Audio is generated once from a script manifest, committed to the repository, and served as static assets — no runtime synthesis, no latency, no per-request cost. The narration pipeline is a small .NET console tool in `audio-scripts/` that reads `narration.json` and writes the audio files ready for deployment.

---

## 16 Screens, Purpose-Built for Mobile

The app covers **16 screens** across the full game lifecycle:

- **3 pre-game screens** — Create Game, Join Game, Lobby
- **13 timed in-game phases** — Role Reveal, Werewolves Meeting, Cupid Turn, Lover Reveal, Werewolves Turn, Seer Turn, Witch Turn, Hunter Turn, Night Elimination, Discussion, Tiebreak Discussion, Day Elimination, Game Over

Each phase is an independent Angular component. Night-action phases include a **countdown timer with overtime** — once the clock hits zero, the display flips to an upward-counting red overtime indicator, applying social pressure while never forcing a player's hand. The phase architecture is designed so adding a new role means adding one new component and one new state transition, nothing more.

---

## End-to-End Tests — Playwright + AI Guidance

The full gameplay loop is covered by **Playwright** E2E tests running against Chromium. The test suite spins up **multiple browser contexts in parallel** to simulate several players joining from separate devices, progresses through real game phases, and asserts correct outcomes at each step. There are dedicated specs for every special role:

| Spec file | Coverage |
|---|---|
| `full-game.spec.ts` | Complete game from lobby to Game Over |
| `seer.spec.ts` | Seer inspection and alignment reveal |
| `witch.spec.ts` | Witch save and poison paths |
| `hunter.spec.ts` | Hunter elimination and retaliation shot |
| `cupid.spec.ts` | Cupid linking, Lover Reveal, lover win condition |
| `lobby.spec.ts` | Lobby setup and settings |
| `session.spec.ts` | Session shell and timer behaviour |
| `audio.spec.ts` | Audio playback at phase transitions |

Playwright also serves as a **living specification for AI-assisted development** — GitHub Copilot uses the test suite as ground truth when generating or reviewing new features, ensuring that every code suggestion can be verified against a real browser running the full application.

---

## Hosting — Firebase + Google Cloud Run

| Layer | Platform |
|---|---|
| Frontend SPA | **Firebase Hosting** — global CDN, instant cache invalidation, SPA rewrites |
| Developer docs | Embedded in the Firebase deployment as static MkDocs output |
| Backend API | **Google Cloud Run** — fully managed, containerised, serverless |

The backend runs in a Docker container deployed to Cloud Run in `europe-west4`. It **scales to zero** when not in use, meaning the demo workload costs nothing at idle. A single deployment command (`.\deploy.ps1`) builds the Docker image, pushes it to **Google Artifact Registry**, deploys to Cloud Run, builds the frontend, and publishes everything to Firebase in one shot.

---

## Infrastructure as Code — Terraform

All Google Cloud infrastructure — the Artifact Registry repository, the Cloud Run service, and its IAM access policy — is defined declaratively in **Terraform** (`infra/`). There is no manual cloud console configuration; the entire cloud footprint is reproducible from a `terraform apply`. Variables are cleanly separated in `variables.tf`, and state is tracked locally in `terraform.tfstate`.

---

## Developer Documentation — MkDocs Material

This documentation site is built with **MkDocs** using the **Material theme** and deployed as part of every release. It lives at `/docs/` inside the Firebase-hosted frontend, served as pre-built static HTML. The docs cover game rules, all 16 screens, architecture decisions, audio generation, local development, and deployment — making the project fully self-documenting for any new contributor or AI coding agent working in the repository.

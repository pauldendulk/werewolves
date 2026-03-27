# Tournament Mode

## Overview

A **tournament** is a sequence of Werewolves games played by roughly the same group during a single game night. Players accumulate points across games and a tournament winner is determined at the end of the evening.

A key design goal is that tournament mode requires **zero up-front commitment**: players can play a single game and decide afterwards to continue as a tournament. The app makes this frictionless by keeping the same lobby URL throughout.

---

## Identifiers

Three identifiers are in play:

| Identifier | Format | Stability | Purpose |
|---|---|---|---|
| `TournamentCode` | 6 uppercase chars (e.g. `ABCDEF`) | Permanent — shared once at game night start | Stable URL key; what players bookmark/scan |
| `TournamentId` | UUID | Permanent | Database FK linking all games in a session |
| `GameId` | 8 hex chars | Regenerated each game | Uniquely identifies one game in the DB |

`TournamentCode` appears in the join URL (`/join/ABCDEF`) and never changes across games. `GameId` changes at the start of every new game.

---

## Game Flow

### Creating a game

1. Host opens the app and creates a game.
2. A `TournamentCode`, `GameId`, and `TournamentId` are generated.
3. The tournament row is written to the database immediately.
4. The host receives a join link and QR code.

### Joining

Players scan the QR code or navigate to the join URL. If a game is already in progress when they arrive, they are placed in a **waiting state** (`ParticipationStatus = Spectating`) and join the next game automatically when the lobby resets.

### In-game

Once started, all players see the same screen. The backend drives a shared phase state (`GamePhase`) that advances via timers and player actions. Screens are synchronised via 1 Hz polling.

### End of a game — Show Scores

When one side wins:

1. `Winner` is set and the phase advances to `GameOver`.
2. Per-player scores are shown on the **Show Scores** screen.
3. Each player taps **Done — back to lobby** to set `IsDone = true`.
4. A countdown timer runs in the background (GameOver phase timer).
5. When **all** participating players have set `IsDone = true`, or the timer expires, `ResetForNextGame` fires.
6. Game results (`games` + `game_players` rows) are persisted to the database.

### Returning to the lobby

`ResetForNextGame` in `GameService`:

1. Generates a new `GameId`.
2. Sets `Status = WaitingForPlayers`.
3. Calls `GameState.ResetSessionState()` (resets phase, votes, skill state, player roles/scores).
4. `TournamentCode` and `TournamentId` are **preserved**.
5. Players who were spectating during the previous game remain in the lobby for the next game.

The lobby shows a card "**Others are still viewing results**" with a countdown while stragglers are still on the Show Scores screen.

### Starting the next game

The host presses **Start Game** in the lobby. The flow repeats from the in-game step above.

---

## Scoring

Scoring is calculated at the end of each game. Players earn points based on:

- Surviving to the end of the game.
- Voting correctly (voting for an actual werewolf during day phases earns more points).

Exact formulas are in the scoring logic in `GameService.CalculateScores`.

**Tournament totals** are not yet aggregated in the UI. Per-game scores are stored in `game_players.score` but `tournament_participants.total_score` is not yet written (planned).

---

## Data Persistence (current)

Two write events occur per session:

| Event | DB tables written |
|---|---|
| `CreateGame` | `tournaments` (one row, once per session) |
| `ResetForNextGame` (game end) | `games` + `game_players` (one game's results) |

All live game state (phases, votes, player roles, timers) is held **in memory only** in a `ConcurrentDictionary<string, GameState>` keyed by `TournamentCode`. **A server restart loses all active game state.**

### Not yet persisted

- `tournament_participants.total_score` — column exists, never written.
- Live game state for mid-game recovery.

---

## Handling Players Joining Mid-game

If a player arrives while a game is in progress:

- `ParticipationStatus = Spectating`.
- `IsDone = true` (so they don't block the GameOver transition).
- They are shown the lobby (not the game session) while waiting.
- When the game resets they become active participants in the next game.

---

## Known Gaps and Future Work

### Server reboot recovery

The entire game state is in-memory. A server reboot while a game is in progress causes all active games to be lost. Players navigating back to the same URL would find an empty lobby. See the [robustness analysis](#robustness) below.

### Score accumulation

`tournament_participants.total_score` is never written. Implementing a tournament leaderboard requires summing `game_players.score` per player across games and writing the total at the end of each game.

### `gameIndex` counter

There is no game number tracked within a session. Adding a `GameIndex` field to `GameState` (incrementing in `ResetForNextGame`) would enable "Game 3 of N" displays and the cumulative scores table.

### IsTournament flag / premium mode

The design doc originally described an opt-in "convert to tournament" step as a paid feature. This is not implemented; every session behaves as a tournament from game 1.

---

## Robustness Analysis {#robustness}

### Current DB write points

| Trigger | What is written |
|---|---|
| `CreateGame` API call | `tournaments` row |
| `ResetForNextGame` (game ends or timer fires) | `games` + `game_players` rows |

Nothing is written during gameplay.

### Failure scenarios

#### Server restart mid-game

**Impact (today):** All in-memory state is lost. Players polling see a 404 or empty lobby. Game progress is unrecoverable.

**Proposed fix:** Persist `GameState` as a JSONB column (`games.state`) at each phase transition. On startup, load all games with `status IN ('WaitingForPlayers', 'ReadyToStart', 'InProgress')` from the DB back into the in-memory dictionary. This makes the server stateless-resumable.

#### Player browser refresh / reconnect

**Impact (today):** Handled correctly as long as the server is still running. The next poll returns the full current state. No special logic needed.

#### Player network drop (doesn't refresh, just goes silent)

**Impact (today):** The server continues unaware. Timers still run. If a phase requires player votes and one player is disconnected, the phase advances when the timer expires — the disconnected player simply hasn't voted.

This is tolerable for most phases. The more problematic case is a skill action phase (Seer, Witch, Hunter) where only one player can act — if that player is offline, the phase timer must expire before the game can continue.

**Proposed mitigation:** No change needed for MVP. For a better experience: detect "last poll > 30 s ago" as offline; if the only remaining actor for a phase is offline, reduce the phase timeout or skip the action automatically.

#### Partial write failure

**Impact (today):** If `SaveGameAsync` succeeds but `SaveGamePlayersAsync` fails, the game row exists with no players. The `ON CONFLICT DO UPDATE` upsert means a retry is safe.

**Proposed fix:** Wrap both operations in a single DB transaction.

### Proposed JSONB state column

Add a `state JSONB` column to `games`:

```sql
-- V003
ALTER TABLE games ADD COLUMN IF NOT EXISTS state JSONB;
```

Write the serialised `GameState` on every phase transition (already a natural sync point). On `Program.cs` startup, replay all active games from the DB.

This gives recovery from server restarts for free without changing the polling architecture, and keeps the normalised columns (`status`, `winner`, `settings`, `ended_at`) accurate for reporting queries.

The column can be cleared (set to `NULL`) once the game reaches `GameOver` and results are persisted, since it is only needed for recovery, not long-term storage.

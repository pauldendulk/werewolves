# Tournament Mode

## Context

The app already supports a complete single game of Werewolves from start to finish — role assignment, night phases, voting rounds, and a final result. With the addition of a persistent database (Docker Postgres locally, Google Cloud SQL in production), it is now feasible to build **Tournament Mode**: a sequence of games played by the same group, with cumulative scoring tracked across every game.

This is the concrete implementation of the Priority 1 feature described in [feature-roadmap.md](feature-roadmap.md), and it builds directly on the outreach strategy outlined in [camping-competition.md](camping-competition.md).

---

## What Is a Tournament?

A **tournament** is a named series of Werewolves games played by the same group in one sitting (or over several evenings). The database stores:

- The tournament ID and creation timestamp
- The list of players who participated across all games in the tournament
- Each individual game result (winners, eliminations, round-by-round votes)
- A cumulative score per player, updated after every game

### Scoring

The scoring system from [feature-roadmap.md](feature-roadmap.md) applies directly:

- **Win / loss**: +1 point for being on the winning team
- **Correct early votes**: bonus for correctly identifying a Werewolf before they are eliminated
- **Survival bonus**: points for surviving to the end of a game
- **Role-specific bonuses**: Seer, Witch, Hunter bonuses for correct plays

At the end of each game the lobby shows the running tournament score so players can see who is leading before the next game starts.

---

## Campaigns (Future Extension)

A **campaign** is a collection of tournaments played by the same group over a longer period — for example, a camping animation organisation running a summer programme across June, July, and August. Each week could be a separate tournament; the campaign aggregates the results.

Seasonal use cases:

- **Camping animation summer programme**: one tournament per evening, results accumulate over the holiday week, culminating in a championship final (see [camping-competition.md](camping-competition.md))
- **Company team building**: quarterly tournament series, leaderboard shown at the end of each quarter
- **School or youth club**: recurring game nights over an academic term

Campaigns are a natural next step once the tournament entity and persistent scoring are in place. No separate build is required upfront — the data model simply allows grouping tournaments under a campaign ID.

---

## Paywall Flow

The goal is to let every group experience the app for free, then convert at the moment of highest willingness to pay: the instant the first game ends and they want to keep playing together.

### Step-by-step

1. **Free**: the group starts and completes one full game of Werewolves — no payment, no account required.
2. **After the game ends**, the host sees the result screen with the final scores. A "Play another game" button is visible.
3. **On tap**, the app explains: *"Turn this into a tournament — track scores across all your games tonight."* A paywall appears.
4. **Stripe hosted checkout** opens, pre-filled with the tournament ID that will be created on payment.
5. After successful payment, Stripe sends a webhook to the backend; the backend creates the tournament record and marks it as **premium**.
6. The host is returned to the lobby, the first game's score is recorded, and the group can play as many games as they like within this tournament.

### Why this moment works

The group is already together, phones in hand, having just had fun. The social friction of stopping now is high. Paying €4 to keep the evening going is an easy decision for a group of ten — that is €0.40 per person.

### Pricing

- **~€4 per tournament** (one-off, no subscription, no account)
- No per-game charges once the tournament is unlocked
- The purchase covers the entire group for as many games as they want to play that tournament

---

## Technical Mechanism

This reuses the same pattern described in [short-term-profits.md](short-term-profits.md) for gating advanced roles behind a session purchase — but applied at the tournament level.

### Key design decisions

- **No accounts required.** The app already stores a `playerId` in `localStorage` on each player's device, which is used to reconnect players who lose their connection mid-game. This is sufficient to associate a player with a tournament without a login.
- **The unlock is tied to a tournament ID** stored server-side. There is no shareable key or client-side flag. A player cannot unlock someone else's tournament by copying a value.
- **Stripe checkout flow** (same as for role unlocks):
  1. Frontend calls the backend to create a pending tournament record; receives a `tournamentId`.
  2. Backend creates a Stripe Checkout session with the `tournamentId` in the metadata.
  3. After payment, Stripe webhook fires; backend marks the tournament as `isPremium = true`.
  4. Frontend polls or receives a push notification that the tournament is now active.

### Backend entities to add

- `Tournament` — id, createdAt, isPremium, hostPlayerId, stripePaymentId
- `TournamentGame` — tournamentId, gameId, gameIndex (order within the tournament)
- `TournamentScore` — tournamentId, playerId, totalPoints (updated after each game)

### Frontend changes

- "Play another game" button on the game-over screen
- Paywall component (reusable; same pattern as role-unlock paywall)
- Tournament lobby screen showing the running score table after each game

---

## Value Proposition

| What the group gets | Why it matters |
|---|---|
| Persistent scores across every game | Creates a real competition, not just a one-off |
| Running leaderboard between games | Keeps players engaged and coming back for the next game |
| One-tap "next game" flow | No manual setup — the group, roles, and scores carry over |
| No accounts needed | Zero friction; works for first-time users |

### Differentiator vs. competitor apps

Neither Wolvesville nor One Night Ultimate Werewolf offers structured multi-game play with persistent scoring for a fixed group in one session. Tournament Mode is a direct response to the gap identified in [feature-roadmap.md](feature-roadmap.md) and positions the app as the right tool for organised game nights.

---

## Next Steps

1. **Tournament entity** — add `Tournament`, `TournamentGame`, and `TournamentScore` to the backend data model and expose API endpoints.
2. **Score aggregation** — after each game result is saved, compute the delta score per player and update `TournamentScore`.
3. **Premium flag** — add `isPremium` to `Tournament`; free tournaments are capped at one game (or zero, if we want the first game to always be free outside a tournament).
4. **Stripe integration** — Stripe Checkout session creation endpoint + webhook to set `isPremium = true`. Reuse the pattern from [short-term-profits.md](short-term-profits.md).
5. **Frontend paywall** — paywall component triggered from the game-over screen when the host taps "Play another game".
6. **Tournament lobby** — extend the existing lobby screen to show the running score table when inside a tournament.

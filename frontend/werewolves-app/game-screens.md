# Game Screens & Countdown Design

## Overview

This document lists all screens/forms in the app, describes their purpose and user interactions, and analyses where a countdown timer and overtime behaviour are appropriate.

---

## All Screens

All in-game phases (everything below the Lobby) share a common shell rendered by `SessionComponent`:
- **Header bar** — game ID (left) and round number (right); center slot currently unused
- **Eliminated banner** — shown if the player is eliminated
- **Timer bar** — shown below the header whenever `phaseEndsAt` is set on the game state

Pre-game screens (Create Game, Join Game, Lobby) do not use the session shell and have no timer infrastructure.

| Phase | Time of day | Who acts | What happens | Countdown useful? | Overtime useful? |
|---|---|---|---|---|---|
| **Create Game** | Pre-game | Host | Enter name and settings, create game | No — no time pressure on setup | No |
| **Join Game** | Pre-game | Joining player | Enter name, join via link/QR | No | No |
| **Lobby** | Pre-game | All / host | Wait for players; host starts game | No | No |
| **`RoleReveal`** | Day | All players | Press-and-hold card to peek at role; confirm ready | Optional — useful if the host wants to keep pace | Low value — players need a moment to register their role; nobody is waiting on a secret |
| **`WerewolvesMeeting`** | Night | Werewolves only | Wolves identify each other; non-wolves wait with eyes closed | **Yes** — wolves should be brief | **Yes** — non-wolves are sitting with eyes closed; overtime signals "hurry up" |
| **`CupidTurn`** | Night | Cupid only | Cupid links two players as lovers | **Yes** — one-time, short action | **Yes** — rest of village is waiting |
| **`LoverReveal`** | Day | All players (passive) | Everyone opens eyes; check role card for lover name; auto-advances after 20 s | Built-in — 20 s fixed timer, auto-advance | N/A — auto-advances; no overtime needed |
| **`WerewolvesTurn`** | Night | Werewolves only | Wolves pick a victim from a dropdown; non-wolves wait | **Yes** — decisive action | **Yes** — same reasoning; non-wolves are waiting blind |
| **`SeerTurn`** | Night | Seer only | Seer inspects one player's alignment | **Yes** — secret action should not drag | **Yes** — village is waiting; subtle overtime pressure fits the theme |
| **`WitchTurn`** | Night | Witch only | Witch saves victim, poisons a target, or does nothing | **Yes** — meaningful decision | **Yes** — pressure to choose; "do nothing" is always available as quick escape |
| **`HunterTurn`** | Night | Hunter only | Eliminated Hunter shoots one player | **Yes** — dramatic final act | **Yes** — the shot hangs dramatically; overtime heightens tension |
| **`NightElimination`** | Night | Passive (auto-advance) | Dawn reveal: who died in the night | Built-in — auto-advance with numeric countdown | N/A — auto-advances; no overtime needed |
| **`Discussion`** | Day | All living players | Players debate, cast votes, signal ready | **Yes — primary use case** | **Yes** — overtime is natural: discussion ran long, village must still vote |
| **`TiebreakDiscussion`** | Day | All living players | Same as Discussion; votes restricted to tied candidates | **Yes** | **Yes** — same reasoning |
| **`DayElimination`** | Day | Passive (auto-advance) | Verdict reveal: who the village eliminated | Built-in — auto-advance with numeric countdown | N/A — auto-advances; no overtime needed |
| **`GameOver`** | Day | All players | Winner announced; full role summary shown | No — game is over | No |

---

## The Top Bar

The header bar is **reused for every in-game phase**:

```html
<div class="session-header">
  <span class="game-name">Game: {{ gameId }}</span>          <!-- left -->
  <span class="round-badge">Round {{ roundNumber }}</span>   <!-- right -->
</div>
```

The **timer bar** sits _below_ the header as a separate element, displayed only when `phaseEndsAt` is set. The center of the header is currently unused and could host a compact countdown if the timer bar is removed or merged into the header.

---

## Countdown & Overtime Design

### Timer states

```
Normal:   ⏱ 1:23      (white / muted)
Urgent:   ⏱ 0:09      (amber — .urgent class already exists at ≤10 s)
Overtime: ⏱ +0:14     (red — new .overtime class, triggers at < 0 s)
```

### Overtime behaviour

When the countdown reaches zero on a phase that requires human action (no auto-advance):

- The timer continues ticking **upward**: `+0:01`, `+0:02`, ...
- Display switches to **red** (`.overtime` CSS class)
- No player is forced to act — they can still complete their turn
- Social pressure naturally increases; the host also has skip buttons on every phase as a safety valve

This works well for all the night skill phases (Werewolves, Cupid, Seer, Witch, Hunter) and is the natural continuation of the Discussion timer's existing behaviour.

### Future consideration — Discussion extension

A "extend discussion" action could be added where any player spends a one-time token to add e.g. 60 seconds to the Discussion timer. This would reset the countdown rather than entering overtime. Not planned for current implementation but compatible with the proposed timer model.

### Where to display the countdown

**Option A — Existing timer bar (below header)**  
Extend it to support overtime. Least disruptive; already in place for timed phases.

**Option B — Center of header**  
Move into the header's unused center slot. Cleaner hierarchy; header becomes busier.

**Recommended**: Extend the existing timer bar (Option A). Revisit placement if the UI feels crowded.

---

## Implementation Notes

- `phaseEndsAt: string | null` on `GameState` already drives the current timer bar.
- `secondsRemaining` is computed in `session.ts` from `phaseEndsAt`.
- Overtime requires allowing `secondsRemaining` to go **negative** (currently clamped at 0).
- New computed property `isOvertime = secondsRemaining < 0` drives the `.overtime` CSS class.
- `timerLabel` formatter needs to handle negative values: `-` becomes `+` prefix, e.g. `+0:14`.
- The `.urgent` threshold (≤ 10 s) remains unchanged; `.overtime` triggers when the value crosses below 0.

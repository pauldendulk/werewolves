# Screen Inventory & Timer Design

## Session Shell

All in-game phases share a common shell rendered by `SessionComponent`:

- **Header bar** — game ID (left) and round number (right)
- **Eliminated banner** — shown when the local player has been eliminated
- **Timer bar** — displayed below the header whenever `phaseEndsAt` is set on `GameState`

Pre-game screens (Create Game, Join Game, Lobby) are independent components with no timer infrastructure.

---

## Screen Inventory

| Phase | Time | Who acts | Description | Countdown | Overtime |
|---|---|---|---|---|---|
| **Create Game** | Pre-game | Host | Enter name and settings, create game | No | No |
| **Join Game** | Pre-game | Joining player | Enter name, join via link or QR | No | No |
| **Lobby** | Pre-game | All / host | Wait for players; host starts game | No | No |
| **`RoleReveal`** | Day | All players | Press-and-hold card to peek at role; confirm ready | Optional | Low value |
| **`WerewolvesMeeting`** | Night | Werewolves | Wolves identify each other; others wait with eyes closed | **Yes** | **Yes** |
| **`CupidTurn`** | Night | Cupid | Cupid links two players as lovers | **Yes** | **Yes** |
| **`LoverReveal`** | Day | Passive | Everyone checks role card for lover name; auto-advances after 20 s | Built-in | N/A |
| **`WerewolvesTurn`** | Night | Werewolves | Wolves pick a victim; others wait | **Yes** | **Yes** |
| **`SeerTurn`** | Night | Seer | Seer inspects one player's alignment | **Yes** | **Yes** |
| **`WitchTurn`** | Night | Witch | Save the victim, poison a target, or pass | **Yes** | **Yes** |
| **`HunterTurn`** | Night | Hunter | Eliminated Hunter shoots one player | **Yes** | **Yes** |
| **`NightElimination`** | Night | Passive | Dawn reveal: who died in the night; auto-advances | Built-in | N/A |
| **`Discussion`** | Day | All living | Players debate and cast votes | **Yes** | **Yes** |
| **`TiebreakDiscussion`** | Day | All living | Same as Discussion; votes restricted to tied candidates | **Yes** | **Yes** |
| **`DayElimination`** | Day | Passive | Verdict reveal: who the village eliminated; auto-advances | Built-in | N/A |
| **`GameOver`** | Day | All players | Winner announced; full role summary; auto-resets to lobby | **Yes** (60 s) | No |

---

## Timer Design

### Timer States

```
Normal:   ⏱ 1:23      white / muted
Urgent:   ⏱ 0:09      amber  — triggers at ≤ 10 s  (.urgent CSS class)
Overtime: ⏱ +0:14     red    — triggers below 0 s   (.overtime CSS class)
```

### Overtime Behaviour

When the countdown reaches zero on a phase that requires human action (no auto-advance):

- The timer continues ticking **upward**: `+0:01`, `+0:02`, …
- Display switches to **red** (`.overtime` CSS class)
- No player is forced to act — they can still complete their turn at any time
- Social pressure naturally increases; the host also has skip buttons on every phase as a safety valve

This applies to all night-skill phases (Werewolves, Cupid, Seer, Witch, Hunter) and extends the Discussion timer's existing behaviour.

### Implementation Notes

- `phaseEndsAt: string | null` on `GameState` drives the timer bar
- `secondsRemaining` is computed in `session.ts` from `phaseEndsAt`
- Overtime requires allowing `secondsRemaining` to go **negative** (currently clamped at 0)
- New computed property `isOvertime = secondsRemaining < 0` drives the `.overtime` CSS class
- `timerLabel` formatter handles negative values: negative sign becomes `+` prefix (e.g. `+0:14`)
- The `.urgent` threshold (≤ 10 s) remains unchanged

---

## Timer Bar Placement

**Current approach**: The timer bar sits below the header as a separate element, displayed only when `phaseEndsAt` is set.

**Alternative considered**: Move into the header's unused center slot — cleaner hierarchy but a busier header.

**Recommendation**: Extend the existing timer bar (current approach). Revisit placement if the UI feels crowded.

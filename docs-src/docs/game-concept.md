# Game Concept & Rules

## Overview

Werewolves is a hidden-role social deduction game for groups. The app replaces the human moderator, handling role assignment, phase narration via text-to-speech, countdown timers, and vote tallying while the players sit together in the same room. Players join the session on their own phones; each phone shows only what that player is allowed to see.

---

## Roles

| Role | Team | Goal |
|---|---|---|
| **Werewolf** | Wolves | Eliminate all villagers before being outnumbered |
| **Villager** | Village | Identify and eliminate all werewolves through discussion and voting |
| **Seer** | Village | Each night, secretly learn one player's true alignment (wolf or villager) |
| **Witch** | Village | One-use heal potion to save tonight's victim; one-use poison to eliminate any player |
| **Hunter** | Village | When eliminated (night or day), immediately shoots one other surviving player |
| **Cupid** | Village | On the very first night, secretly links two players as lovers |

### Special Rule: Lovers

When Cupid links two players, each receives a private notification of their partner's name on their role card.

- If one lover is eliminated for any reason, the other dies of heartbreak **immediately**
- Lovers share a secondary win condition: if both survive to the end they win **regardless** of team
- A Werewolf-Villager lover pair effectively becomes a third faction — they must outlast both sides

---

## Win Condition Details

Win conditions are evaluated after **every** elimination — night kill, witch poison, and day vote all trigger a check. The precedence is:

1. **Lovers win** checked first (if applicable) — if both lovers are among the survivors and one of the standard win conditions is also met, the lovers take priority
2. **Werewolves win** — wolves ≥ living villagers
3. **Village wins** — all wolves eliminated

---

## Configurable Settings Reference

| Setting | Range | Description |
|---|---|---|
| Min players | 2–20 | Prevents the host from starting below this threshold |
| Max players | 4–20 | Cap on lobby size |
| Number of werewolves | 1–10 | Must be less than total player count minus special roles |
| Discussion duration | 1–30 min | Countdown for each Discussion and Tiebreak phase |
| Seer | on/off | Enables the Seer night action |
| Cupid | on/off | Enables Cupid + Lover Reveal on round 1 |
| Witch | on/off | Enables the Witch night action |
| Hunter | on/off | Enables the Hunter's last-shot ability |

---

## Future Feature Ideas

- **Token system** — spend tokens for special abilities (delay vote, peek hint, protect a player)
- **Multiple rounds per session** — session leaderboard across several games
- **Additional roles** — Doctor, Mayor, Bodyguard, etc.

# Game Concept & Rules

## Overview

Werewolves is a hidden-role social deduction game for groups. The app replaces the human moderator, handling role assignment, phase narration, countdown timers, and vote tallying while the players sit together in the same room.

---

## Roles

| Role | Team | Goal |
|---|---|---|
| **Werewolf** | Wolves | Eliminate all villagers |
| **Villager** | Village | Identify and eliminate all werewolves |
| **Seer** | Village | Each night, secretly learn one player's alignment |
| **Witch** | Village | One-use save and one-use poison each game |
| **Hunter** | Village | When eliminated, immediately shoots one other player |
| **Cupid** | Village | On the first night, links two players as lovers |

!!! note "Lovers"
    If one lover is eliminated, the other dies of heartbreak immediately. Lovers win together — their team affiliation becomes secondary to survival as a pair. If both lovers are from opposite teams they effectively become a third win condition.

---

## Game Flow

### 1. Setup

1. A host creates a game and shares the join link or QR code
2. Players join the lobby on their phones
3. The host configures settings (number of werewolves, discussion time)
4. The host starts the game; the app assigns secret roles

### 2. Role Reveal

Each player privately views their assigned role by pressing and holding their role card. Werewolves also see who the other werewolves are.

### 3. First Night — Special Roles

On the opening night, special roles act in sequence before the standard werewolf turn:

- **Cupid** links two players as lovers
- Lovers receive a private reveal of their partner's name

### 4. Night Phase

1. The app narrates "close your eyes" — all players close their eyes
2. **Werewolves** silently choose a victim from the player list
3. **Seer** (if alive) inspects one player's alignment
4. **Witch** (if alive) may save the victim and/or poison another player
5. A countdown timer governs each night action; overtime ticking signals urgency

### 5. Dawn — Night Elimination

The app announces who was eliminated overnight (or that the village slept safely). If the **Hunter** was eliminated during the night, they immediately shoot one surviving player before the day phase begins.

### 6. Day Phase — Discussion & Voting

1. Players discuss who they suspect is a werewolf
2. Each player casts a vote within a configurable timer
3. When the timer ends (or all players confirm), votes are tallied
4. The player with the most votes is eliminated
5. **Ties** result in no elimination unless a tiebreak vote is triggered

### 7. Day Elimination

The eliminated player's role is publicly revealed. Win conditions are checked after each elimination.

### 8. Game Over

| Winner | Condition |
|---|---|
| Village | All werewolves have been eliminated |
| Werewolves | Werewolves equal or outnumber living villagers |
| Lovers | Both lovers survive to the end (if from opposing teams) |

All roles are revealed on the game-over screen.

---

## Configurable Settings

| Setting | Default | Description |
|---|---|---|
| Minimum players | — | Lobby won't allow start below this count |
| Maximum players | — | Cap on lobby size |
| Number of werewolves | — | How many wolf roles are assigned |
| Discussion duration | — | Minutes allotted for the day discussion phase |

---

## Future Feature Ideas

- **Token system** — spend tokens for special abilities (delay vote, peek hint, protect a player)
- **Multiple rounds per session** — session leaderboard across several games
- **Additional roles** — Doctor, Mayor, Bodyguard, etc.

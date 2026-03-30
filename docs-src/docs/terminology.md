# Terminology

- **Tournament** — a shared session that groups one or more games together. Players join a tournament via a code or QR link and stay connected across multiple rounds. Each tournament has a unique tournament code.
- **Game** — a single round of Werewolf played within a tournament. When a game ends, the tournament can start the next game with the same group of players. Tracked by `gameIndex` (1, 2, 3, …).
- **Phase** — a distinct step in the game flow, shown as a screen on every player's device. Each phase has its own rules for duration, who can act, and how it advances. The full list of phases is defined in `PhaseDescriptor`.
- **Role** — the team alignment assigned to each player at game start: Werewolf or Villager.
- **Skill** — a special ability layered on top of a role (Seer, Witch, Cupid, Hunter). Only Villagers receive skills. A player can have at most one skill.

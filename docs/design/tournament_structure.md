# Functional Design: Tournament Mode
## 1. Purpose and Player Experience
A **tournament** is a sequence of individual Werewolves games played by roughly the same group during a single game night. Players accumulate points across games, and at the end of the evening there is a clear **tournament winner**.
This mirrors real-life play: a group of friends gathers, plays several rounds (typically around four games), and wants an overall winner at the end of the evening.
A key product feature: **tournament mode does not need to be chosen before the first game starts**. Players can play a single game and decide *after* it ends to convert the session into a tournament (e.g., as a paid feature). This makes the experience low-friction and supports monetisation.

---
## 2. Definitions
### 2.1 Single Game
A **single game** is one complete Werewolves session:
- Players join a lobby.
- Roles are dealt.
- The game proceeds through rounds and voting phases until one side wins.
- A results screen is shown with per-player points for that game.
### 2.2 Tournament
A **tournament** is an ordered sequence of single games:
- Game 1, Game 2, … Game N.
- Points accumulate per player across all games.
- At the end of the tournament a winner is determined by total points.

---
## 3. Scoring
### 3.1 Per-game scoring
Each player earns points at the end of a single game. A distinctive feature of this app's scoring model is that **voting correctly — i.e., voting for the actual werewolf — earns more points**. Exact point values and formulas are defined separately; this document covers the flow and requirements.
### 3.2 Tournament totals
In tournament mode the app aggregates:
- Each player's per-game points across all completed games.
- A running **total** per player.
Optionally, an **average points per game** can be calculated and displayed, which is useful when players have participated in a different number of games.
---
## 4. User Flow
### 4.1 Lobby
1. A lobby is created (identified by a session/room URL).
2. Players join the lobby.
3. The host starts the game.
### 4.2 In-game: synchronized screens
Once the game is started:
- Players no longer navigate back to the lobby independently.
- All players always see the same active screen; the session state is **shared and synchronized**.
- This is a core constraint of the app: the experience is a shared, host-driven flow, not independent per-player navigation.
### 4.3 End of a single game
When a game ends:
- The app shows a final score and end screen.
- **Tournament requirement:** the end of a game is a transition point:
  1. Save the completed game's results.
  2. Show the per-game results screen.
  3. Optionally show a tournament totals screen (for game 2 onward).
  4. All players return to the lobby for the next game.
### 4.4 Returning to the lobby
After the results screens, all players return to the lobby in a synchronized manner. Here the host can:
- Adjust roles or game settings.
- Start the next game.
The same session URL is used throughout; starting a new game creates a new game instance within the same session.
### 4.5 Starting the next game
The host presses "Start Game" and the new game begins. The flow repeats from step 4.2.
---
## 5. Session and Game Identifiers
### 5.1 One shared session
A tournament is represented as a **single shared session** with:
- A session/room identifier (reflected in the URL).
- A current state (lobby, night phase, voting, results, etc.).
- A current game number (`gameIndex = 1, 2, 3, …`).
This keeps the "everyone sees the same thing" model intact without requiring players to navigate to different URLs between games.
### 5.2 Game IDs within the session
Each new game increments the game index inside the same session. From the players' perspective, the URL and room do not change; only the game state progresses.
---
## 6. Converting a Single Game into a Tournament
Functional requirement:
- A session starts as a single game.
- After the first game ends, the host can choose to **enable tournament mode** (e.g., as a paid upgrade).
- Once enabled, subsequent games are tracked as part of the tournament.
- Optionally, game 1 results can be retroactively included if they were already persisted.
This supports a "try before you buy" onboarding path.
---
## 7. Handling Players Joining Late or Leaving Early
### 7.1 Joining while a game is in progress
If someone joins the session URL while a game is already in progress, they are placed in a **waiting state** for the next game. They do not participate in the current game and therefore receive no points for it.
When the group reaches the next lobby, the late joiner is included in the next game.
### 7.2 Leaving before the game ends
If a player leaves before a game ends, they receive **no points** for that game.
### 7.3 Missing early games
If a player joins starting from game 2, they receive no points for game 1. Their tournament totals reflect only the games they participated in.
### 7.4 Fairness considerations
To reduce the sense of disadvantage for late joiners:
- The results screen can show a "games played" count alongside each player's total.
- Optionally display an **average points per game**, so a player who joined late can still see how they compare on a per-game basis.
---
## 8. Tournament Results Presentation
### 8.1 Per-game results screen
After each game, show:
- Each player's score for that game.
- Voting correctness reflected in the score.
### 8.2 Tournament totals screen
After the per-game results screen (from game 2 onward), show cumulative standings in a table:
| Player   | Game 1 | Game 2 | … | Total |
|----------|--------|--------|---|-------|
| Alice    | 12     | 9      | … | 21    |
| Bob      | 7      | 14     | … | 21    |
Rows represent players; columns represent individual games plus a total column. This makes it easy to see progression across the evening.
An optional average column can be added for players with different numbers of games played.
---

## 9. Data Persistence

### 9.1 Minimum requirement

Game results (scores, votes, outcomes) must be stored so that tournament totals can be calculated and displayed after each game.
### 9.2 Recommended requirement

Persist enough of the game state that if the server restarts, players returning to the same URL can resume or review the session where they left off — ideally including mid-game state.

---
## 10. Open Questions and Side Considerations
The following topics arose during the initial brainstorm and are noted here for future discussion:
- **Refresh behaviour:** If a player refreshes the page on the same URL, the expected behaviour is that they rejoin and receive the current synchronized game state. This needs to be explicitly tested.
- **Joining via URL after game start:** What happens if someone navigates to the session URL mid-game without having been in the lobby? Options include: spectator mode, waiting in a lobby-for-next-game state, or being blocked until the next game.
- **End screen duration:** Should the final results screen persist indefinitely, or auto-advance after a configurable maximum duration?
- **Synchronized "back to lobby" transition:** Any "go back to lobby" action must keep all players in sync. One player independently navigating away would break the shared-screen model. Consider requiring all players to confirm readiness before the transition completes.
- **Disconnects and timed phases:** If the game uses fixed timers for phases, a player disconnect or server reboot mid-phase could create unfairness. Possible mitigations: pause the game when a player disconnects, and allow the host to remove a player who cannot reconnect so the game can continue.
- **Saving game state continuously:** Rather than saving only at end-of-game, consider recording votes and state transitions as they happen to support richer persistence and recovery scenarios.
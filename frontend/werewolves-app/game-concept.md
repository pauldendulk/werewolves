# Werewolves App – Concept Summary

## 1. Core Concept

This app is a mobile companion for playing a live, in-person game of Werewolf. Each player uses their own phone while sitting together in the same room.

The core purpose is to facilitate and manage a complete Werewolf game without needing a human moderator.

### Core gameplay flow

1. **Game setup**
   - Players join a shared session on their phones
   - The app assigns secret roles to each player
   - Roles are simplified to:
     - Werewolves
     - Civilians

2. **Game phases**
   - The game progresses through the usual phases (discussion and voting)
   - The app manages timing and transitions between phases

3. **Voting system**
   - Players vote on who they suspect is a werewolf
   - Voting includes:
     - A countdown timer
     - The ability to change your vote before the timer ends
   - When the timer ends:
     - Votes are finalized
     - The selected player is eliminated

4. **Round result**
   - The app determines the outcome of the round (e.g. werewolves vs civilians)
   - The game continues until a win condition is reached

5. **Session play (multiple rounds)**
   - Multiple games can be played in one session
   - The app keeps track of results across games

---

## 2. Additional Features

### Token system

Each player has a number of **tokens** that can be spent to perform special actions.  
This replaces many traditional special roles and gives every player strategic options, even when they are a civilian.

Tokens may be:
- Given at the start of a session
- Earned based on performance
- Potentially traded between players

### Token abilities (examples)

- **Delay Vote**  
  Spend tokens to extend the voting timer

- **Revote**  
  Restart the voting phase

- **Peek Hint**  
  Receive a limited hint from the system  
  Example: “At least one of your current suspects is innocent” or “Your vote is currently on a civilian”

- **Block Vote**  
  Prevent a specific player from voting during a round

- **Double Vote**  
  Your vote counts as two votes for the current round

- **Protect Player**  
  Prevent a player from being eliminated in the current round

This system allows flexible gameplay where strategic abilities are chosen dynamically rather than assigned as fixed roles.

### Social and meta features

Across multiple games in a session, the app tracks player performance and provides a ranking.

Possible tracked metrics:
- Number of wins
- Survival rate
- Voting accuracy
- Success when playing as werewolf

This creates a **leaderboard for the evening**, adding a meta-game layer to the experience.

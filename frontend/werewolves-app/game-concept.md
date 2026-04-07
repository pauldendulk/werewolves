# Werewolves App - Concept Summary

## 1. Core Concept

This app is a mobile companion for playing a live, in-person game of Werewolf. Each player uses their own phone while sitting together in the same room.

The core purpose is to facilitate and manage a complete Werewolf game without needing a human moderator.

### Core gameplay flow

1. **Game setup**
   - A host creates a game and shares the join link or QR code
   - Players join the lobby on their phones
   - The app assigns secret roles to each player
   - Roles:
     - **Werewolves** - try to eliminate all villagers
     - **Villagers** - try to identify and eliminate all werewolves

2. **Role reveal**
   - Each player privately views their assigned role (press and hold to peek)
   - Werewolves also see who the other werewolves are

3. **Night phase**
   - The app narrates "close your eyes"
   - Werewolves silently decide on a victim to eliminate
   - A countdown timer governs the night duration

4. **Day phase - Night elimination**
   - The app announces who was eliminated in the night (or that no one was)

5. **Day phase - Discussion and voting**
   - Players discuss who they suspect is a werewolf
   - Each player casts a vote within a configurable timer
   - When the timer ends or all players are done, votes are tallied
   - The player with the most votes is eliminated (ties result in no elimination or a tiebreak revote)

6. **Day elimination**
   - The eliminated player's role is revealed
   - The game checks win conditions

7. **Win conditions**
   - **Villagers win** when all werewolves have been eliminated
   - **Werewolves win** when no villagers remain alive

8. **Final Scores Reveal**
   - All roles are revealed
   - Players can return to the home screen to start a new game

### Configurable settings

- Minimum / maximum players
- Number of werewolves
- Discussion duration (minutes)

---

## 2. Future Feature Ideas

- **Token system** - Spend tokens for special abilities (delay vote, peek hint, protect player)
- **Multiple rounds per session** - Track wins across games with a session leaderboard
- **Additional roles** - Seer, Doctor, Hunter, etc.

# Screens

### 1. Create & Join

The host creates a game and receives a QR code and shareable link. Other players scan the QR code or open the link on their own phones and enter their name.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Create Game</strong></figcaption><img src="../screenshots/01-create-game.png" alt="Create Game screen"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Join Game</strong></figcaption><img src="../screenshots/02-join-game.png" alt="Join Game screen"></figure>
</div>

---

### 2. Lobby

All players wait in the lobby while the host configures the game. Non-host players see the settings in read-only mode; only the host can change them and start the game.

The Start Game button is only visible to the host and is disabled until sufficient players have joined and all skill/werewolf counts are consistent with the player count.

![Lobby screen (non-host view)](screenshots/03-lobby.png)

---

### 3. Role Reveal

Once the game starts, each player privately views their assigned role by pressing and holding their roll card. The card flips back the moment they release, so no-one nearby can glance at it.

Werewolves see the names of all fellow werewolves on their card. When everyone confirms they have seen their role, the first night begins.

![Role Reveal — card hidden](screenshots/04-role-reveal.png)

---

### 4. Night Announcement

Once Role Reveal ends, the app transitions into night with a brief full-screen announcement. The screen dims to the night theme and a countdown runs while the narration plays, giving everyone a moment to settle before the first night action begins.

![Night Announcement](screenshots/27-the-night-has-fallen.png)

---

### 5. Werewolves Meeting *(Round 1 only)*

On the very first night, before anything else happens, the werewolves open their eyes and identify each other silently. The werewolf screen shows the names of all pack members and an "I'm ready" button. Once all wolves have confirmed, the phase advances.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Werewolf view</strong></figcaption><img src="../screenshots/06-night-werewolves-meeting-werewolf.png" alt="Werewolves Meeting — werewolf"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Others' view</strong></figcaption><img src="../screenshots/05-night-werewolves-meeting-villager.png" alt="Werewolves Meeting — others"></figure>
</div>

---

### 6. Cupid Turn *(Round 1 only)*

Cupid wakes up and secretly links two players as lovers. Cupid selects two players from the alive-players list and confirms. The lovers are notified privately during the Lover Reveal phase that immediately follows.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Cupid view</strong></figcaption><img src="../screenshots/10-night-cupid-turn-cupid.png" alt="Cupid Turn — Cupid"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Others' view</strong></figcaption><img src="../screenshots/09-night-cupid-turn-non-cupid.png" alt="Cupid Turn — others"></figure>
</div>

---

### 7. Day Announcement

Once the night phase ends, the app transitions to day with a brief full-screen announcement. Everyone opens their eyes and the app switches to the day theme while the narration plays.

![Day Announcement](screenshots/28-the-night-has-ended.png)

---

### 8. Lover Reveal *(Round 1 only)*

Everyone opens their eyes and checks their role card to see if they are one of the lovers. If a player is a lover, their partner's name appears on the card when held down. All other players see their usual role with no lover name.

![Lover Reveal — card showing lover name](screenshots/11-lover-reveal.png)

---

### 9. Discussion & Voting

Players discuss freely, sharing suspicions and defending themselves. Every player — including those already eliminated — casts one vote for who they believe is a werewolf. A countdown timer governs the discussion period.

Each player's current vote is shown live on their chip as an arrow (e.g. **Alice → Bob**), making alliances and suspicions immediately visible and fuelling the discussion.

When a player has made up their mind and wants to wrap up early, they can press **End discussion**. How many players have pressed this is shown next to the button (e.g. `3 / 8`), but only once at least one player has done so — the counter stays hidden otherwise so it does not invite premature use.

Eliminated players see a notice explaining they have been eliminated but can still vote to earn bonus points. When the timer ends or all living players confirm, votes are tallied.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Alive player</strong></figcaption><img src="../screenshots/19-discussion.png" alt="Discussion — alive player with vote controls"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Eliminated player</strong></figcaption><img src="../screenshots/19b-discussion-eliminated.png" alt="Discussion — eliminated player can still vote"></figure>
</div>

---

### 10. Tiebreak Discussion *(if a tie occurs)*

If two or more players are tied for most votes, a second short discussion round takes place. Only the tied candidates can be voted for this time.

If the tiebreak vote is also tied, no elimination occurs and the game moves straight to night.

![Tiebreak Discussion](screenshots/20-tiebreak-discussion.png)

---

### 11. Day Elimination

The player with the most votes is eliminated and their role is publicly revealed to everyone. Win conditions are checked immediately after each elimination. If the Hunter was just eliminated, the Hunter Turn phase activates before the game continues.

![Day Elimination — verdict](screenshots/21-day-elimination.png)

---

### 12. The Hunter *(triggered on elimination)*

The Hunter phase activates whenever the Hunter is eliminated — either by werewolves at night or by the village vote during the day. The Hunter gets one last action: shooting a player of their choice. The selected player is immediately eliminated and win conditions are re-checked.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Hunter view</strong></figcaption><img src="../screenshots/17-night-hunter-turn-hunter.png" alt="Hunter Turn — Hunter"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Others' view</strong></figcaption><img src="../screenshots/16-night-hunter-turn-non-hunter.png" alt="Hunter Turn — others"></figure>
</div>

---

### 13. Werewolves *(Round 2 onwards)*

The app narrates "close your eyes". The werewolf screen shows a dropdown of all living non-wolf players. Once every wolf has voted the same target (or the timer expires), the kill is locked in.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Werewolf view</strong></figcaption><img src="../screenshots/08-night-werewolves-turn-werewolf.png" alt="Werewolves Turn — werewolf"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Others' view</strong></figcaption><img src="../screenshots/07-night-werewolves-turn-villager.png" alt="Werewolves Turn — others"></figure>
</div>

---

### 14. The Seer *(Round 2 onwards)*

The Seer wakes up and inspects one player. The result reveals whether that player is a Werewolf or a Villager, and shows their skill if they have one. The Seer receives the result immediately on their screen — this information is theirs alone to use strategically during the day discussion.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Seer view</strong></figcaption><img src="../screenshots/13-night-seer-turn-seer.png" alt="Seer Turn — Seer"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Others' view</strong></figcaption><img src="../screenshots/12-night-seer-turn-non-seer.png" alt="Seer Turn — others"></figure>
</div>

---

### 15. The Witch *(Round 2 onwards)*

The Witch wakes up last. She is shown tonight's werewolf victim and can use either, both, or none of her potions — each usable only once per game:

- 🧴 **Heal** — Saves tonight's werewolf victim; they survive the night
- ☠️ **Poison** — Eliminates any living player of the Witch's choice

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>Witch view</strong></figcaption><img src="../screenshots/15-night-witch-turn-witch.png" alt="Witch Turn — Witch"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>Others' view</strong></figcaption><img src="../screenshots/14-night-witch-turn-non-witch.png" alt="Witch Turn — others"></figure>
</div>

---

### 16. Victims *(Round 2 onwards)*

The app reveals what happened overnight. Everyone "opens their eyes" and sees the night's outcome.

Possible outcomes:

- One or more players were killed by werewolves (and possibly saved or poisoned by the witch)
- The village woke up safely — nobody was taken
- The Witch saved the victim, but also poisoned someone else

![Victims — night elimination revealed](screenshots/18-victims.png)

---

### 17. Final Scores Reveal

The game ends when a win condition is met. All roles are revealed in a summary table, sorted by score. From the second game onwards a running **total** column appears alongside each player's per-game score, so everyone can see the tournament standings at a glance.

Win conditions:

- **Village** — All werewolves have been eliminated
- **Werewolves** — Werewolves equal or outnumber the surviving villagers
- **Lovers** — Both lovers survive to the end (only applies when they are from opposing teams)

Players return to the lobby and can start a new game.

<div style="display:flex;gap:1rem;align-items:flex-start">
  <figure style="flex:1;margin:0"><figcaption><strong>After game 1</strong></figcaption><img src="../screenshots/22-game-over.png" alt="Final Scores Reveal — first game, per-game scores only"></figure>
  <figure style="flex:1;margin:0"><figcaption><strong>After game 2+ (with totals)</strong></figcaption><img src="../screenshots/23-game-over-game2.png" alt="Final Scores Reveal — second game, running totals shown"></figure>
</div>

---

### 18. Tournament Pass

Starting a second (or later) game requires a tournament pass. When the host presses **Start Game** from game 2 onwards, a modal appears asking for the pass code. Entering the correct code unlocks the tournament and starts the game immediately. An incorrect code shows an error and lets the host try again.

![Tournament Unlock — pass code dialog](screenshots/24-tournament-unlock.png)

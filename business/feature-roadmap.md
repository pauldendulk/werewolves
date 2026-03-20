# Werewolves App — Feature Roadmap

Features are grouped by strategic importance. The top section covers the ideas that both differentiate the app from competitors and directly support monetisation.

---

## Priority 1 — Multi-Game Sessions with Persistent Scoring

**Context**: None of the competing apps (Wolvesville, One Night Ultimate Werewolf) support the concept of a fixed group playing multiple games in sequence and accumulating a score. This is our strongest differentiator for the "organised game night" audience.

### What this looks like end-to-end

1. **After a game ends**, instead of the session dying, the app returns to the lobby.
2. The lobby shows the **running score** for all players based on the games played so far in this series.
3. The **QR code / join code is shown again** so that any player who dropped can rejoin and new players can join the next game.
4. The host sees a "Play next game" button. Players who are already in the lobby are automatically enrolled.
5. A player can leave between games; that's fine — they keep their historical score in the series.
6. The **series score** (not just one-game score) is what people care about: who won the most games, who was the best deductor over the evening.

### Scoring ideas

- **Win / loss**: simplest. +1 for being on the winning team.
- **Correct early votes**: if you vote to eliminate a Werewolf and they are indeed eliminated, you score a bonus — even in rounds where the Werewolf is NOT eliminated (you identified them correctly but were outvoted).
- **Survival bonus**: points for surviving to the end of the game.
- **Role-specific bonuses**:
  - Seer: +bonus if your revealed info directly led to a Werewolf elimination
  - Witch: +bonus for a correct save or poison
  - Hunter: +bonus for taking out a Werewolf with the final shot
- **"Best Guesser" award**: automatic end-of-series award to the player with the highest proportion of correct votes

### Individual awards (end of series / end of game)
These make the experience memorable and shareable, and could be premium-only:

| Award | Trigger |
|---|---|
| Best Guesser | Highest correct vote rate |
| Survivor | Never eliminated across the whole series |
| Wolf in Sheep's Clothing | Won as Werewolf without being voted once |
| Master Manipulator | Voted out an ally (non-Werewolf) and survived anyway |
| Devoted Lover | Both Cupid lovers survived to the end |
| Lone Wolf | Won as Werewolf when all other Werewolves were eliminated earlier |

### Monetisation link
- Basic scoring: free
- Full award history + detailed per-game breakdown: Premium
- Series history saved to profile (across different groups / sessions): Premium

---

## Priority 2 — Player Accounts & Persistent Profile

For scoring to have lasting value, players need a persistent identity across sessions.

- Sign in with Google / Apple / email
- Profile shows: games played, win rate by role, favourite role, awards earned
- Optional: global leaderboard (opt-in)
- **Monetisation**: accounts are the gateway to Premium subscriptions. The free game works without an account; an account unlocks history and competitive features.

---

## Priority 3 — Native Mobile App

Currently the app runs in a browser. A native iOS/Android app:
- Allows offline caching of audio files (eliminates repeat download cost)
- Enables push notifications ("Your group has started a new game!")
- Unlocks app-store billing for subscriptions and one-time purchases
- Improves discoverability

---

## Priority 4 — Role Unlocks & Content Packs

- Free tier: core roles (Werewolf, Villager, Seer, Witch)
- Premium or one-time purchase: Hunter, Cupid, and future roles
- Themed role packs (Horror, Medieval, Space) — cosmetic variants with re-skinned narration tone

---

## Priority 5 — Organiser / Power-User Features

- Session templates: save a preferred role configuration and reuse it
- Session size up to 30 players (vs. smaller limit for free tier)
- Custom game name / banner shown in the lobby
- Export post-game recap as shareable image

---

## Parked Ideas (mentioned but not prioritised)

### Organiser earnings / credit system
- Original idea: organiser charges players; earns a cut or credits
- **Cash payouts**: set aside due to payment regulation complexity and fake-match abuse risk
- **Credits variant**: organiser earns in-app credits (e.g., free Premium days) when inviting players who sign up and complete a verified game. Credits are not cashable. Abuse mitigation: require email verification + minimum session duration before credits vest.

### Marketplace for community-created roles
- Community designs new roles; sells them through an in-app store
- Revenue share with creators (e.g., 70/30)
- Requires significant moderation infrastructure; revisit when user base is established

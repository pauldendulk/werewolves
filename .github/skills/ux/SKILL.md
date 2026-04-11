---
name: ux
description: Context and reference material for UI/UX design discussions about the Werewolves app — screens, phases, screenshots, and design guidelines.
---

# UX Design Context

When discussing UI/UX topics, load the following context before responding.

---

## Step 1 — Read the design guidelines

Read `docs-src/docs/ux-guidelines.md` in full. This contains the core design principles (Clarity over Fun, two-level text hierarchy, role-aware subtitles, flow correctness) that all proposals must be evaluated against.

---

## Step 2 — Read the phase/screen inventory

Read `docs-src/docs/phases.md`. It contains:
- The complete ordered list of every game phase (pre-game and in-game)
- Who acts during each phase
- Timer behaviour
- The shared session shell (header bar, timer bar, eliminated banner)

Also read `docs-src/docs/screens.md` for a narrative description of each screen with its purpose.

---

## Step 3 — Look at relevant screenshots

Screenshots are in `docs-src/docs/screenshots/`. Each file name encodes what it shows:

| File name | What it covers |
|---|---|
| `create-game.png` | Create Game screen |
| `join-game.png` | Join Game screen |
| `lobby.png` | Lobby |
| `role-reveal.png` | RoleReveal phase |
| `night-announcement.png` | NightAnnouncement phase |
| `werewolves-meeting.png` | WerewolvesMeeting — werewolf view |
| `werewolves-close-eyes.png` | WerewolvesCloseEyes phase (blank night screen) |
| `werewolves-meeting-others.png` | WerewolvesMeeting — others' view |
| `werewolves.png` | Werewolves phase — werewolf view |
| `werewolves-others.png` | Werewolves phase — others' view |
| `cupid.png` | Cupid phase — Cupid view |
| `cupid-close-eyes.png` | CupidCloseEyes phase (blank night screen) |
| `cupid-others.png` | Cupid phase — others' view |
| `lovers-reveal.png` | LoversReveal phase |
| `seer.png` | Seer phase — Seer view |
| `seer-close-eyes.png` | SeerCloseEyes phase (blank night screen) |
| `seer-others.png` | Seer phase — others' view |
| `witch.png` | Witch phase — Witch view |
| `witch-close-eyes.png` | WitchCloseEyes phase (blank night screen) |
| `witch-others.png` | Witch phase — others' view |
| `hunter.png` | Hunter phase — Hunter view |
| `hunter-others.png` | Hunter phase — others' view |
| `day-announcement.png` | DayAnnouncement phase |
| `night-elimination-reveal.png` | NightEliminationReveal phase |
| `discussion.png` | Discussion phase — alive player |
| `discussion-eliminated.png` | Discussion phase — eliminated player |
| `tiebreak-discussion.png` | TiebreakDiscussion phase |
| `day-elimination-reveal.png` | DayEliminationReveal phase |
| `final-scores-reveal.png` | FinalScoresReveal — game 1 |
| `final-scores-reveal-game2.png` | FinalScoresReveal — game 2+ (running totals) |
| `tournament-unlock.png` | Tournament unlock / payment screen |
| `moderator-night.png` | Moderator overlay — night phase |
| `moderator-day.png` | Moderator overlay — day phase |

When the user mentions a screen by a general name (e.g. "the lobby", "the game over screen", "the tournament screen"), map it to the matching screenshot filename(s) above and call `view_image` on each relevant file before answering.

---

## Step 4 — Read topic-specific docs if relevant

| Topic | File |
|---|---|
| Tournament rules and unlock flow | `docs-src/docs/tournament.md` |
| Game concept, roles, win conditions | `docs-src/docs/game-concept.md` |
| Authorization model (moderator vs creator) | `docs-src/docs/authorization.md` |

Read only the file(s) directly relevant to the question — no need to load all of them every time.

---

## Angular component code

If the discussion requires understanding the current implementation of a specific screen, find the matching Angular component under `frontend/werewolves-app/src/`. Use `file_search` or `grep_search` to locate it by phase/screen name, then read the template and component class before commenting on changes.

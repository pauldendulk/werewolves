# Monetization and Marketing — Phase One

## Position

The core game is playable today. A group can run a full Werewolves session through the app — roles, phases, narration, voting — without a human moderator. Phase One is about generating early revenue and building a small advocate base around this working product, not waiting for new features.

---

## Revenue

### Premium Role Unlock (per session)

Hunter and Cupid are already in the codebase. Gate them behind a small payment.

- Host enables Hunter/Cupid → paywall appears
- Stripe checkout, scoped to the session ID (nothing to share or leak)
- Backend marks the session as premium via webhook

**Price**: €3–5 per session.  
**Effort**: ~2–3 days (Stripe wiring + paywall UI).

### Fix the Coupon System

The current `UnlockTournament` implementation validates the entered code against a single hardcoded bypass string in app settings. This is not usable for distribution.

**What's needed**: a simple coupon table in the database — each row is a unique code, a usage limit, and an expiry date. When a code is redeemed it is marked used. This enables:
- Handing out individual codes to beta testers
- Codes for event organizers
- One-use promotional codes

**Priority**: high. Without this, there is no real way to run promotions or reward early users.

---

## Marketing & Community

### Beta Tester Group

Invite the people from the last session to a WhatsApp or Discord group. They already played, they gave feedback, and they're interested. Benefits:
- Direct feedback loop
- Give them early access and free coupon codes as reward
- They become advocates; they'll play it again with their own friends

Keep it small and personal. A tight group of 10–20 engaged players is more valuable than a broad mailing list.

### Local Sessions

Organise a werewolf session at a local social centre — post a notice saying "Playing Werewolves this Friday, looking for players, contact me here." Goals:
- Real playtesting with strangers, not just friends
- Direct user feedback in person
- Attendees become potential beta testers or advocates

Costs nothing. Spring and early summer are good timing.

### Schools and Youth

Daughters playing with friends in school or in the park is a natural distribution channel. A single enthusiastic group spreading it to other groups can move fast. Not something to force, but to make easy — if they want to use it, the setup should be frictionless enough for teenagers without any help.

# Short-Term Profit Ideas

Two concrete actions that can generate real revenue in the near term, without requiring player accounts.

---

## 1. Gate Advanced Roles Behind a Session Purchase

**What**: The free game includes Villager, Werewolf, Seer, and Witch — fully playable. Hunter and Cupid require a one-time payment to unlock for a session or for an evening (e.g., 24 hours).

**Why it works**: The roles already exist in the codebase. Nothing new to build on the gameplay side. The purchase is scoped to the session ID, not a portable key, so sharing is not a problem — by the time anyone could share it, the session is over anyway.

**How it works technically**:
1. Host tries to enable Hunter or Cupid in the role settings
2. App shows a paywall: *"These roles are part of Premium — unlock this session for €X"*
3. Stripe hosted checkout opens (pre-filled with the session code)
4. After payment, Stripe sends a webhook to the backend
5. Backend marks that session as premium; app shows the full role list

**Pricing suggestion**: €3–5 per session, or €8–10 for a 24-hour tournament evening. Annual pricing and accounts can come later.

**Effort**: ~2–3 days (Stripe account setup, webhook endpoint in the backend, paywall UI in Angular).

**Security**: no shareable key exists. The unlock is tied to the session ID in the database. There is nothing to leak.

---

## 2. Camping Outreach (No Code Required)

**What**: Email 5 Dutch camping animation coordinators and offer the app for free this summer in exchange for feedback and a testimonial. Charge from the second season.

**Targets**: Center Parcs, Landal GreenParks, Roompot, Molecaten, or any camping with a structured animation programme.

**Why it works**: Animation teams are paid to fill evenings with group activities. The app solves their problem — 30 players, no human moderator needed, tournament format over multiple evenings. They have a strong incentive to use it.

**What you're selling them** (from the second season onwards): not software access, but a reliable product that works during their event, with support if something goes wrong. Suggested price: €200–500 per camping per summer season.

**The conversion flywheel**: players who experience the app on holiday want to play it at home → natural Premium session buyers. Marketing cost: zero.

**Feature gap**: parallel session tournament brackets don't exist yet, but the animation team can coordinate manually for now. The app already handles the hard part — moderating individual games without a human.

**Effort to start**: one email. Draft it, send it, see if anyone replies before writing a single line of code.

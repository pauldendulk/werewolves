# Werewolves App — Monetization Ideas

## Positioning

The primary target audience is **serious social deduction players** — people who play Werewolf regularly with a fixed group, care about winning, and want to track their performance over time. This is deliberately different from Wolvesville, which targets casual younger players with anime avatars and cosmetics.

This positioning affects every monetisation choice: our players are more likely to pay a fair and transparent subscription than to engage with a virtual currency shop. Avoid anything that feels like a mobile free-to-play cash grab — it will repel exactly the audience we want.

---

## Guiding Principles

- **Recurring revenue is king** — one-time sales are nice; monthly/annual subscriptions compound.
- **Segment your buyers** — different people are prepared to pay very different amounts. Design tiers so you capture value from all of them: the casual free player, the regular player, and the power organizer.
- **Keep the core playable for free** — lowering the barrier to entry grows the user base, which is the asset everything else is built on.
- **Marginal cost matters** — audio file delivery is the main variable cost. Factor this into pricing.

---

## Monetisation Options

### Freemium / Subscription

The primary model. Highest lifetime value per user.

**Free tier**
- Full game playable (to maximise adoption)
- A limited set of roles (e.g., Werewolf, Villager, Seer, Witch)
- Basic text-to-speech narration
- Ads between rounds (see *Advertising* below)

**Premium subscription (~€3–5 / month or ~€25 / year)**
- No ads
- All roles unlocked (Hunter, Cupid, and any future roles)
- Expanded narration audio packs (different voices, languages, tone)
- Longer game history / statistics
- Customisable game settings (phase timers, house rules)
- Priority support

**Pro / Organiser subscription (~€8–12 / month)**
- Everything in Premium
- Larger session sizes (e.g., up to 30 players vs. 15 for free)
- Custom branding (upload a logo / game name for your group)
- Pre-game lobby waiting room with shareable invite link/QR code
- Post-game recap exported as shareable image or PDF
- Advanced game analytics (who voted for whom, role performance)

> **Note on pricing psychology**: annual plans with a ~2-month discount convert well and reduce churn. Offer both.

---

### One-Time In-App Purchases

Good complement to subscriptions. Works well for content packs.

- **Role packs** — themed bundles of new roles (e.g., "Horror Pack" with Vampire, Ghost Hunter)
- **Narration voice packs** — different narrator personalities (spooky, comedic, cinematic)
- **Language packs** — localised narration for non-English speakers (Dutch, German, French, etc.)
- **Theme packs** — reskin the UI/audio to a specific setting (Medieval, Space, Western)
- **Lifetime access** — one large payment (~€30–50) for permanent Premium access; appeals to people who hate subscriptions

---

### Advertising (Free-Tier Monetisation)

Low yield per user but zero friction and low implementation cost.

- Show interstitial or banner ads during lobby wait / between game phases
- Even if CPM is low, if revenue > hosting cost the free tier pays for itself
- Use as a conversion nudge: "Remove ads forever — upgrade to Premium"
- Preferred ad networks for games: Google AdMob, Unity Ads, ironSource

---

### B2B / Events

Higher value per transaction. Less volume, more margin.

**Team-building / corporate events**
- Companies regularly pay €500–5000 for a team-building activity
- Sell a "hosted session" package: the app handles moderation, organiser gets a dashboard & report
- Could be sold directly or through event-planning agencies (affiliate share)

**Party & escape room venues**
- License the app to venues (e.g., escape rooms, game cafés) as a white-label product
- Monthly SaaS fee for venue operators (€20–100/month depending on session volume)
- Venue puts their own branding on it

**Schools / youth clubs**
- Educational subscription (group licence, e.g., €10/month for up to 5 game sessions/week)
- Social deduction is genuinely used in classroom settings for critical thinking exercises

---

### Platform / Marketplace

Longer-term, higher complexity.

**Creator marketplace**
- Allow community members to design and submit custom roles or narration scripts
- Sell those packs in a store; creator earns a revenue share (e.g., 70/30)
- Turns the user base into content producers

**API access for developers**
- Expose the game state API to third-party developers who want to build their own frontend or integration (e.g., Discord bot)
- Charge per API key or per session on a usage-based model

---

### Referral / Credits System

Growth mechanic with minor monetisation potential.

- When an organiser invites players who sign up, the organiser earns in-app credits
- Credits unlock extra sessions or premium features temporarily
- **Deliberately not cashable** — avoids payment processing, abuse, and regulatory issues
- Abuse mitigation: require email verification + minimum session duration before credits are granted

> *Variant mentioned during brainstorm*: letting organisers charge players and keep a cut. Set aside for now — introduces payment complexity, potential for fake sessions, and regulatory liability (you'd be running a payment intermediary). Revisit only if there is strong demand.

---

### Virtual Currency

Players buy an in-game currency (e.g. "Coins") with real money, then spend it on in-game items rather than paying directly. The conversion deliberately obscures the real-money price.

- Standard mobile game pattern — used by Wolvesville (Gold/Gems), Fortnite (V-Bucks), etc.
- Can generate more revenue than a subscription from a small % of "whale" spenders
- Downside: feels cheap and manipulative; at odds with a serious/competitive brand image
- **Assessment**: probably not a good fit. Our audience wants a fair, skill-based game — not one cluttered with currency manipulations. Could be kept as a very light optional layer (e.g. buy a themed narration pack) but should not be the primary model.

---

### Native Mobile App

A distribution channel, not a revenue model on its own. Enables the other options.

- Publish to App Store (iOS) and Google Play
- App stores handle payment & subscription billing natively (simplifies implementation)
- Discoverability boost — people search for party games on app stores
- One-time paid download (€1–2) as an alternative entry point to subscription
- **Important**: Apple takes 15–30% of subscription revenue. Factor into pricing.

---

## Cost Considerations

| Cost driver | Notes |
|---|---|
| Hosting (backend) | Currently minimal; scales with concurrent sessions |
| Audio file delivery | Main variable cost; consider CDN caching aggressively |
| Payment processing | Stripe ~2.9% + €0.30; App Store/Play 15–30% |
| Support | Grows with user base; self-service docs reduce this |

Audio cost mitigation: cache audio files in the browser/app after first download so repeat players don't re-fetch them.

---

## Suggested MVPs (in priority order)

1. **Ads** — fastest to implement, starts covering costs immediately
2. **Premium subscription** (Stripe + feature flags) — core recurring revenue
3. **Native app** — opens app store distribution and built-in billing
4. **Role / voice packs** — easy upsell once you have paying users
5. **B2B events package** — high margin, pursue once product is more polished

---

---

## Competitor Landscape

### Wolvesville (formerly Werewolf Online)
- Available on iOS, Android, browser, and Steam (released on Steam September 2023)
- Free to play; **no subscription model** — relies entirely on cosmetic microtransactions
- In-app currency: Gold and Gems (bought with real money)
- Seasonal **Battle Pass** (~1,240 Gold per season ≈ roughly €4–8 equivalent); 100 tiers of cosmetic rewards (outfits, icons, emotes, backgrounds). 45+ seasons as of 2026
- One-off cosmetic purchases: outfits, avatar items, role card upgrades (legendary → mythical tier)
- Ranked mode requires 80+ reputation points before unlock
- Up to 16–25 players per session; matched with strangers globally (online matchmaking)
- Very Positive reviews on Steam (80%, ~1,148 reviews); 92–94% on mobile stores
- Targeted at younger, casual players — anime-style avatars, cartoon visual style

**Key insight**: Wolvesville chose cosmetics + battle pass over subscriptions. They rely on volume (very large player base) and repeat cosmetic spending rather than recurring fees. Their monetisation requires a large, active online-matchmaking player base to sustain.

### One Night Ultimate Werewolf (Bézier Games)
- A digital companion app for the physical One Night card game
- Functions as a **moderator/narration app** — plays audio instructions at the start of a game round
- Single purchase or bundled with the card game; no subscription, no ongoing monetisation
- Very different scope: it assumes the physical cards are present and just handles the night-phase audio. It does not track votes, phases, or game state.

### Werewords (Bézier Games)
- Another companion app from the same publisher; narrates a word-guessing variant of Werewolf
- Same model: simple paid companion app, no subscription

### What the competition does NOT offer
- **None of them serve the "organised group plays repeatedly together" use case well.** Wolvesville is online-only random matchmaking. The Bézier apps are single-session companions with no state.
- **Persistent scoring across sessions for a fixed group** is completely absent from all competitors — this is a genuine differentiator (see [feature-roadmap.md](feature-roadmap.md))
- **No competitor supports the "play another round immediately with the same group" flow**

---

## Open Questions

- What is the current monthly hosting cost at scale (100 concurrent sessions)?
- Is there a realistic B2B lead (a company, venue, school) we could pilot with?
- Which roles / features do playtesters find most "worth paying for"?
- Should we lean into cosmetics (Wolvesville model) or subscriptions, given our different audience (organised groups vs. random matchmaking)?

# Stripe Configuration

This page explains everything you need to set up in Stripe before the payment integration can be developed. No code changes are needed yet — this is the account-configuration groundwork first.

---

## How the payment flow works

The integration uses **Stripe Checkout** — Stripe's hosted payment page. You never handle card details yourself, so there is zero PCI compliance burden.

The flow will be:

1. Host taps **"Buy Tournament Pass"** in the app.
2. The backend calls Stripe to create a **Checkout Session** and returns a redirect URL.
3. The user's browser is sent to Stripe's hosted page (your branding, Stripe's infrastructure).
4. User pays. Stripe redirects them back to the app.
5. Stripe simultaneously fires a **webhook** to the backend with a `checkout.session.completed` event.
6. The backend verifies the webhook signature, sets `is_premium = true` for the tournament, and the next game unlocks.

The webhook (step 5–6) is the authoritative signal — the redirect URL alone is never trusted.

---

## Step 1 — Create a Stripe account

Go to [https://stripe.com](https://stripe.com) and sign up if you don't already have one.

You will spend time in **Test mode** first (no real money moves). Test mode and Live mode have completely separate keys and products.

---

## Step 2 — Create a Product and Price

Stripe needs to know what you are selling and for how much.

1. In the Stripe Dashboard, click **Product catalog** in the left nav.
2. Click **+ Create product** (top right).
3. **Name:** `WerewolvesTournamentPass` (visible to customers at checkout).
4. **Description:** optional, e.g. `Allows you to go into tournament mode`.
5. Under **Pricing**, select **One-off**.
6. **Amount:** set your price and currency (e.g. `3.50`, `EUR`).
7. Click **Add product**.

**To find the Price ID afterwards:**

1. You land on the Product catalog page — click on the product name **WerewolvesTournamentPass**.
2. On the product detail page you see a **Pricing** table with a row showing `€3.50 EUR`.
3. Click that row — it opens the price detail page.
4. The **Price ID** (`price_1...`) is shown in the top-right corner of that page.

Copy it. You will need this value as a config setting in the backend.

---

## Step 3 — Get your API keys

Go to **Developers → API keys**.

You need two keys:

| Key | Name in app config | Notes |
|---|---|---|
| **Publishable key** | Not needed server-side | Starts with `pk_test_…` / `pk_live_…` |
| **Secret key** | `Stripe:SecretKey` | Starts with `sk_test_…` / `sk_live_…` — treat like a password |

Copy the **Secret key**. You will add it to GCP Secret Manager in [Step 6](#step-6--add-secrets-to-gcp-secret-manager).

---

## Step 4 — Configure a Webhook endpoint

Stripe needs a URL to POST payment events to. The backend will expose `POST /api/stripe/webhook`.

Because this endpoint must be reachable from the internet, you need the **deployed** backend URL — not localhost. Use the Cloud Run URL.

1. Go to **Developers → Webhooks → + Add endpoint**.
2. **Endpoint URL:** `https://<your-cloud-run-url>/api/stripe/webhook`
3. **Events to listen to:** select `checkout.session.completed` only.
4. Click **Add endpoint**.

After creating it, open the endpoint and click **Reveal** under **Signing secret**. Copy this value — it starts with `whsec_…`. You will need it as `Stripe:WebhookSecret` in config.

!!! note "Testing webhooks locally"
    During development you can use the [Stripe CLI](https://stripe.com/docs/stripe-cli) to forward webhook events to localhost:

    ```bash
    stripe listen --forward-to localhost:5000/api/stripe/webhook
    ```

    The CLI prints a local signing secret (`whsec_…`) that you use in `appsettings.Development.json` instead of the live one.

---

## Step 5 — Set up success and cancel redirect URLs

When checkout completes (or is abandoned), Stripe redirects the user back to the app. You will need to decide on these two URLs and make sure they exist as routes in the Angular app:

| Situation | Redirect URL |
|---|---|
| Payment succeeded | `https://<app-url>/game/{tournamentCode}/lobby?payment=success` |
| User cancelled | `https://<app-url>/game/{tournamentCode}/lobby?payment=cancelled` |

You do not need to create these routes yet — just keep the pattern in mind when the development phase starts.

---

## Step 6 — Add secrets to GCP Secret Manager

Follow the same pattern as the existing `werewolves-db-connection-string` secret (see [Secret Management](secrets.md)).

Add two new secrets:

| Secret name in GCP | Config key mounted in Cloud Run | Value |
|---|---|---|
| `stripe-secret-key` | `Stripe__SecretKey` | `sk_live_…` from Step 3 |
| `stripe-webhook-secret` | `Stripe__WebhookSecret` | `whsec_…` from Step 4 |

Add them to `infra/main.tf` alongside the existing db connection string, and grant the Cloud Run service account `secretAccessor` on both.

For local development, add them to `appsettings.Development.json` (git-ignored values, or a `.env` file if you prefer):

```json
"Stripe": {
  "SecretKey": "sk_test_...",
  "WebhookSecret": "whsec_...",
  "PriceId": "price_..."
}
```

---

## What the developer will need from you

Once the above steps are done, hand over these three values:

- `sk_test_…` — Stripe secret key (test mode)
- `whsec_…` — Webhook signing secret (from the Stripe CLI in dev, or the dashboard endpoint in prod)
- `price_…` — Price ID for the Tournament Pass product

These go into config / secrets and are never committed to source control.

---

## Development phases (after account setup)

Once the Stripe account is configured the development work splits into two phases:

**Phase 1 — Backend**

- Add the `Stripe.net` NuGet package.
- Implement `POST /api/game/{tournamentCode}/checkout` — creates a Checkout Session and returns `{ checkoutUrl }`.
- Implement `POST /api/stripe/webhook` — verifies the Stripe signature, handles `checkout.session.completed`, sets `is_premium = true` in the database and in-memory.

**Phase 2 — Frontend**

- Replace (or augment) the bypass-code dialog in the lobby with a **"Buy Tournament Pass"** button.
- On click, call the checkout endpoint, then redirect the browser to `checkoutUrl`.
- Handle the `?payment=success` / `?payment=cancelled` query params on return.

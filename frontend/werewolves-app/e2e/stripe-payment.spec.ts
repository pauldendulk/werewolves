/**
 * Stripe payment end-to-end test.
 *
 * Unlike the screenshot tests, this test hits the real backend and Stripe.
 * The following must be running before executing this test:
 *
 *   docker compose up -d
 *   cd backend/WerewolvesAPI && dotnet run
 *   npm start  (Angular dev server on port 4200)
 *
 * The backend must have an empty Stripe:WebhookSecret in
 * appsettings.Development.json so that signature verification is skipped
 * and the test can POST a fake webhook payload directly.
 *
 * Run:
 *   cd frontend/werewolves-app
 *   npx playwright test e2e/stripe-payment.spec.ts
 *
 * Card details used: Stripe test card 4242 4242 4242 4242
 * No real money is charged. The Stripe account must be in test mode.
 *
 * NOTE ON STRIPE SELECTORS:
 *   Stripe's hosted checkout page (checkout.stripe.com) renders card input
 *   fields inside iframes for PCI compliance. The exact selector structure
 *   may change when Stripe updates their checkout UI.
 *   If the test fails on card input, open the Stripe checkout page in a
 *   browser and inspect the iframe structure to update these selectors.
 */

import { test, expect } from '@playwright/test';

const API = 'http://localhost:5000/api';

test('Stripe payment marks game as premium', async ({ page, request }) => {
  test.skip(!!process.env['CI'], 'Stripe API key not available in CI');
  test.setTimeout(90_000);
  // ── 1. Create a game via the real backend API ─────────────────────────────
  const createRes = await request.post(`${API}/game/create`, {
    data: { creatorName: 'StripeTestHost', frontendBaseUrl: 'http://localhost:4200' },
  });
  expect(createRes.ok()).toBeTruthy();
  const { tournamentCode: gameId } = await createRes.json();

  // ── 2. Get a Stripe Checkout URL from the backend ─────────────────────────
  const checkoutRes = await request.post(`${API}/game/${gameId}/checkout`, {
    data: {
      successUrl: `http://localhost:4200/game/${gameId}/lobby?payment=success`,
      cancelUrl:  `http://localhost:4200/game/${gameId}/lobby?payment=cancelled`,
    },
  });
  expect(checkoutRes.ok()).toBeTruthy();
  const { checkoutUrl } = await checkoutRes.json();

  // ── 3. Navigate to Stripe's hosted checkout page ──────────────────────────
  await page.goto(checkoutUrl);
  await page.waitForURL(/checkout\.stripe\.com/, { timeout: 15_000 });

  // ── 4. Fill the test card details ─────────────────────────────────────────
  // Email — direct input on Stripe's page (no iframe)
  await page.getByLabel('Email').fill('test@example.com');
  await page.keyboard.press('Tab'); // confirm email

  // Ensure Card is selected (it may already be, but click forces the card fields to render)
  await page.getByRole('radio', { name: 'Card' }).click({ force: true });

  // Stripe renders each card field in its own iframe for PCI compliance.
  // Poll frames until the card number input appears.
  const findFrame = async (placeholder: string) => {
    for (let i = 0; i < 30; i++) {
      for (const frame of page.frames()) {
        try {
          if ((await frame.getByPlaceholder(placeholder).count()) > 0) return frame;
        } catch { /* frame not ready */ }
      }
      await page.waitForTimeout(500);
    }
    throw new Error(`Timed out waiting for Stripe input: ${placeholder}`);
  };

  const cardFrame   = await findFrame('1234 1234 1234 1234');
  await cardFrame.getByPlaceholder('1234 1234 1234 1234').fill('4242 4242 4242 4242');
  const expiryFrame = await findFrame('MM / YY');
  await expiryFrame.getByPlaceholder('MM / YY').fill('12 / 28');
  const cvcFrame    = await findFrame('CVC');
  await cvcFrame.getByPlaceholder('CVC').fill('123');

  // Cardholder name — regular input outside iframes
  await page.getByPlaceholder('Full name on card').fill('Test Player');

  // ── 5. Submit payment ─────────────────────────────────────────────────────
  await page.getByTestId('hosted-payment-submit-button').click();

  // ── 6. Wait for Stripe to redirect back to our lobby ─────────────────────
  await page.waitForURL(/localhost:4200.*payment=success/, { timeout: 30_000 });

  // ── 7. Simulate the Stripe webhook by POSTing directly to the backend ──
  // In development mode (empty Stripe:WebhookSecret), the backend skips
  // signature verification, so we can send a fake checkout.session.completed
  // event with the tournament code in the metadata.
  const webhookRes = await request.post(`${API}/stripe/webhook`, {
    headers: { 'Content-Type': 'application/json' },
    data: JSON.stringify({
      type: 'checkout.session.completed',
      data: {
        object: {
          metadata: { tournamentCode: gameId },
        },
      },
    }),
  });
  expect(webhookRes.ok()).toBeTruthy();

  // ── 8. Verify the game is now in tournament mode via the backend API ─────────
  const stateRes = await request.get(`${API}/game/${gameId}`);
  expect(stateRes.ok()).toBeTruthy();
  const state = await stateRes.json();
  expect(state.game.isTournamentModeUnlocked).toBe(true);
});

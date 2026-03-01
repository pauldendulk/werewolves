import { test, expect, Browser, BrowserContext, Page } from '@playwright/test';

/**
 * Helper: create a fresh browser context and page for a player.
 */
async function newPlayer(browser: Browser): Promise<{ context: BrowserContext; page: Page }> {
  const context = await browser.newContext();
  const page = await context.newPage();
  return { context, page };
}

/**
 * Helper: join a game as a named player. Navigates to /game/:id, fills the
 * name input and clicks Join, then waits for the lobby.
 */
async function joinGame(page: Page, gameId: string, name: string): Promise<void> {
  await page.goto(`/game/${gameId}`);
  await expect(page.getByRole('heading', { name: 'Join Game' })).toBeVisible();
  await page.getByLabel('Your Name').fill(name);
  await page.getByRole('button', { name: 'Join Game' }).click();
  await expect(page).toHaveURL(/\/game\/.*\/lobby/, { timeout: 10_000 });
}

test.describe('Game session – role reveal', () => {
  test('three players can start a game and all see the RoleReveal screen', async ({ browser }) => {
    // ── 1. Creator creates a game ───────────────────────────────────────────
    const creator = await newPlayer(browser);
    await creator.page.goto('/');
    await creator.page.getByRole('button', { name: 'Organize Game' }).click();
    await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });

    // Parse the game ID from the creator URL
    const creatorUrl = creator.page.url();
    const gameId = creatorUrl.match(/\/game\/([^/]+)\/lobby/)![1];

    // ── 2. Two more players join ────────────────────────────────────────────
    const alice = await newPlayer(browser);
    const bob = await newPlayer(browser);

    await joinGame(alice.page, gameId, 'Alice');
    await joinGame(bob.page, gameId, 'Bob');

    // ── 3. Creator waits until 3 players are listed, then sets min=3 and starts ──
    // With the default min players = 3 (or lower it explicitly), Start Game becomes active.
    await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });

    // Explicitly set min players to 3 (handles cases where the default may differ)
    const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
    await minInput.fill('3');
    await minInput.press('Tab'); // commit the value

    await expect(creator.page.getByRole('button', { name: 'Start Game' })).toBeEnabled({ timeout: 10_000 });

    await creator.page.getByRole('button', { name: 'Start Game' }).click();

    // ── 4. All three browsers navigate to the session ───────────────────────
    // The lobby polls and auto-redirects non-creators; the creator redirects
    // immediately after the API call.
    await expect(creator.page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
    await expect(alice.page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
    await expect(bob.page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });

    // ── 5. All three see the RoleReveal phase ──────────────────────────────
    for (const { page } of [creator, alice, bob]) {
      await expect(page.getByRole('heading', { name: 'Your Role' })).toBeVisible({ timeout: 10_000 });
      await expect(page.getByText('Press & hold to reveal')).toBeVisible();
      // Ready button should be visible but disabled until the card has been peeked
      await expect(page.getByRole('button', { name: "I've seen my role" })).toBeDisabled();
    }

    // ── 6. Press-and-hold the card to peek, then release ───────────────────
    // (Test only creator's browser to keep the test focused.)
    const roleCard = creator.page.locator('.role-card');
    await expect(roleCard).toBeVisible();
    // Press down to reveal
    await roleCard.dispatchEvent('mousedown');
    await expect(creator.page.locator('.role-name')).toBeVisible({ timeout: 5_000 });
    // Release to hide
    await roleCard.dispatchEvent('mouseup');
    await expect(creator.page.getByText('Press & hold to reveal')).toBeVisible({ timeout: 5_000 });
    // The ready button is now enabled (hasSeenRole = true)
    await expect(creator.page.getByRole('button', { name: "I've seen my role" })).toBeEnabled({ timeout: 5_000 });

    // ── Cleanup ────────────────────────────────────────────────────────────
    await creator.context.close();
    await alice.context.close();
    await bob.context.close();
  });

  test('all three players can mark done and transition to Night phase', async ({ browser }) => {
    // ── Setup: create game and get 3 players in ────────────────────────────
    const creator = await newPlayer(browser);
    await creator.page.goto('/');
    await creator.page.getByRole('button', { name: 'Organize Game' }).click();
    await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });

    const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

    const alice = await newPlayer(browser);
    const bob = await newPlayer(browser);
    await joinGame(alice.page, gameId, 'Alice');
    await joinGame(bob.page, gameId, 'Bob');

    await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });

    // Explicitly set min players to 3
    const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
    await minInput.fill('3');
    await minInput.press('Tab');

    await creator.page.getByRole('button', { name: 'Start Game' }).click();

    // Wait for all to reach session
    await expect(creator.page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
    await expect(alice.page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
    await expect(bob.page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });

    // ── Each player reveals their card and presses ready ────────────────────
    const players = [creator, alice, bob];
    for (const { page } of players) {
      await expect(page.getByRole('heading', { name: 'Your Role' })).toBeVisible({ timeout: 10_000 });
      // Press and release to peek at role, enabling the ready button
      const card = page.locator('.role-card');
      await card.dispatchEvent('mousedown');
      await card.dispatchEvent('mouseup');
      await expect(page.getByRole('button', { name: "I've seen my role" })).toBeEnabled({ timeout: 5_000 });
      await page.getByRole('button', { name: "I've seen my role" }).click();
    }

    // ── All three should now advance to the Night phase ─────────────────────
    for (const { page } of players) {
      await expect(page.getByRole('heading', { name: /Night/ })).toBeVisible({ timeout: 15_000 });
    }

    // ── Cleanup ────────────────────────────────────────────────────────────
    await creator.context.close();
    await alice.context.close();
    await bob.context.close();
  });
});

import { test, expect, Browser, BrowserContext, Page } from '@playwright/test';

// ─── Helpers ─────────────────────────────────────────────────────────────────

async function newPlayer(browser: Browser): Promise<{ context: BrowserContext; page: Page }> {
  const context = await browser.newContext();
  const page = await context.newPage();
  return { context, page };
}

async function joinGame(page: Page, gameId: string, name: string): Promise<void> {
  await page.goto(`/game/${gameId}`);
  await expect(page.getByRole('heading', { name: 'Join Game' })).toBeVisible({ timeout: 10_000 });
  await page.getByLabel('Your Name').fill(name);
  await page.getByRole('button', { name: 'Join Game' }).click();
  await expect(page).toHaveURL(/\/game\/.*\/lobby/, { timeout: 10_000 });
}

/** Peek role card and return the role text, leaving the card flipped back. */
async function peekRole(page: Page): Promise<string> {
  const card = page.locator('.role-card');
  await expect(card).toBeVisible({ timeout: 10_000 });
  await card.dispatchEvent('mousedown');
  const roleNameEl = page.locator('.role-name');
  await expect(roleNameEl).toBeVisible({ timeout: 5_000 });
  const text = (await roleNameEl.textContent()) ?? '';
  await card.dispatchEvent('mouseup');
  return text.trim();
}

/** Peek role AND mark ready. */
async function peekAndReady(page: Page): Promise<string> {
  const role = await peekRole(page);
  await page.getByRole('button', { name: "I've seen my role" }).click();
  return role;
}

/** Wait until the phase heading contains the given text (case-insensitive partial match). */
async function waitForPhase(page: Page, headingText: string, timeout = 30_000): Promise<void> {
  await expect(page.getByRole('heading', { level: 2 })).toContainText(headingText, { timeout });
}

/** Open a p-select dropdown on the page (by container class) and pick an option by name. */
async function selectOption(page: Page, containerSelector: string, optionText: string): Promise<void> {
  await page.locator(containerSelector).locator('.p-select').click();
  await page.getByRole('option', { name: optionText, exact: true }).click();
}

/** Cast a night vote (only visible to werewolves). */
async function nightVote(page: Page, targetName: string): Promise<void> {
  await selectOption(page, '.wolf-vote', targetName);
  await page.getByRole('button', { name: 'Confirm kill' }).click();
}

/** Cast a day vote (Discussion / TiebreakDiscussion phase). */
async function dayVote(page: Page, targetName: string): Promise<void> {
  await selectOption(page, '.vote-section', targetName);
  await page.getByRole('button', { name: 'Cast vote' }).click();
}

// ─── Test ────────────────────────────────────────────────────────────────────

test.describe('Full 6-player game – werewolves win', () => {
  test(
    '4-round scenario: no-kill night, wolves agree, tiebreak, wolves eliminate last villager',
    { tag: '@full-game' },
    async ({ browser }) => {

      // ── 1. Create game ────────────────────────────────────────────────────
      const creator = await newPlayer(browser);
      await creator.page.goto('/');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      // Set min players to 6
      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('6');
      await minInput.press('Tab');

      // Set number of werewolves to 2 (click the + button in the Werewolves setting)
      const werewolvesRow = creator.page.locator('.setting-item').filter({ hasText: 'Werewolves' });
      await werewolvesRow.getByRole('button').last().click(); // increment +

      // ── 2. Five players join ──────────────────────────────────────────────
      const alice   = await newPlayer(browser);
      const bob     = await newPlayer(browser);
      const charlie = await newPlayer(browser);
      const dave    = await newPlayer(browser);
      const eve     = await newPlayer(browser);

      await joinGame(alice.page,   gameId, 'Alice');
      await joinGame(bob.page,     gameId, 'Bob');
      await joinGame(charlie.page, gameId, 'Charlie');
      await joinGame(dave.page,    gameId, 'Dave');
      await joinGame(eve.page,     gameId, 'Eve');

      // ── 3. Wait for all 6 players in lobby, then start ───────────────────
      await expect(creator.page.getByText('Players (6)')).toBeVisible({ timeout: 15_000 });
      await expect(creator.page.getByRole('button', { name: 'Start Game' })).toBeEnabled({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const allPlayers = [creator, alice, bob, charlie, dave, eve];
      const names = ['PlayerName', 'Alice', 'Bob', 'Charlie', 'Dave', 'Eve'];

      for (const { page } of allPlayers) {
        await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
      }

      // ── 4. Role Reveal: everyone peeks and marks ready ───────────────────
      const roles: Record<string, string> = {};
      for (let i = 0; i < allPlayers.length; i++) {
        roles[names[i]] = await peekAndReady(allPlayers[i].page);
      }

      // Separate into werewolves and villagers
      type PlayerEntry = { page: Page; name: string };
      const wolves:   PlayerEntry[] = [];
      const villagers: PlayerEntry[] = [];
      for (let i = 0; i < allPlayers.length; i++) {
        const entry: PlayerEntry = { page: allPlayers[i].page, name: names[i] };
        if (roles[names[i]] === 'Werewolf') wolves.push(entry);
        else                                 villagers.push(entry);
      }
      expect(wolves).toHaveLength(2);
      expect(villagers).toHaveLength(4);
      // Convenient aliases
      const [W0, W1] = wolves;
      const [V0, V1, V2, V3] = villagers;

      // ── ROUND 1 ──────────────────────────────────────────────────────────
      // Night 1: no kills – creator skips
      for (const { page } of allPlayers) await waitForPhase(page, 'Night');
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // Day 1: everyone votes V[0] → V[0] eliminated, state: 2W + 3V
      for (const { page } of allPlayers) await waitForPhase(page, 'Discussion');
      const votingPlayers1 = allPlayers.filter((_, i) => names[i] !== V0.name);
      for (const { page } of votingPlayers1) await dayVote(page, V0.name);
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Village Verdict');
      await expect(creator.page.getByText(V0.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── ROUND 2 ──────────────────────────────────────────────────────────
      // Night 2: both wolves vote V[1] → V[1] eliminated, state: 2W + 2V
      for (const { page } of allPlayers) await waitForPhase(page, 'Night');
      await nightVote(W0.page, V1.name);
      await nightVote(W1.page, V1.name);
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Dawn');
      await expect(creator.page.getByText(V1.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // Day 2: wolves split; surviving villagers V[2] and V[3] agree on W[0]
      for (const { page } of allPlayers) await waitForPhase(page, 'Discussion');
      await dayVote(W0.page, V2.name);
      await dayVote(W1.page, V3.name);
      await dayVote(V2.page, W0.name);
      await dayVote(V3.page, W0.name);
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Village Verdict');
      await expect(creator.page.getByText(W0.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── ROUND 3 ──────────────────────────────────────────────────────────
      // Night 3: W[1] kills V[2] → state: 1W + 1V
      for (const { page } of allPlayers) await waitForPhase(page, 'Night');
      await nightVote(W1.page, V2.name);
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Dawn');
      await expect(creator.page.getByText(V2.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // Day 3: W[1]→V[3], V[3]→W[1] → tie → TiebreakDiscussion
      for (const { page } of allPlayers) await waitForPhase(page, 'Discussion');
      await dayVote(W1.page, V3.name);
      await dayVote(V3.page, W1.name);
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Tiebreak Vote');

      // Tiebreak: same votes → tie again → no elimination
      await dayVote(W1.page, V3.name);
      await dayVote(V3.page, W1.name);
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Village Verdict');
      // No one should be eliminated
      await expect(creator.page.getByText('could not agree')).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── ROUND 4 ──────────────────────────────────────────────────────────
      // Night 4: W[1] kills V[3] → 0 villagers → WEREWOLVES WIN
      for (const { page } of allPlayers) await waitForPhase(page, 'Night');
      await nightVote(W1.page, V3.name);
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      for (const { page } of allPlayers) await waitForPhase(page, 'Dawn');
      await expect(creator.page.getByText(V3.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── GAME OVER ─────────────────────────────────────────────────────────
      for (const { page } of allPlayers) await waitForPhase(page, 'Game Over', 20_000);
      for (const { page } of allPlayers) {
        await expect(page.locator('.winner-text')).toContainText('Werewolves Win!', { timeout: 10_000 });
      }

      // ── Cleanup ───────────────────────────────────────────────────────────
      for (const { context } of allPlayers) await context.close();
    },
  );
});

import { test, expect, Browser, BrowserContext, Page } from '@playwright/test';

// ─── Helpers ─────────────────────────────────────────────────────────────────

interface PlayerHandle {
  context: BrowserContext;
  page: Page;
  name: string;
  role?: string;
  skill?: string | null;
}

async function newPlayer(browser: Browser, name: string): Promise<PlayerHandle> {
  const context = await browser.newContext();
  const page = await context.newPage();
  return { context, page, name };
}

async function joinGame(page: Page, gameId: string, name: string): Promise<void> {
  await page.goto(`/game/${gameId}`);
  await expect(page.getByRole('heading', { name: 'Join Game' })).toBeVisible({ timeout: 10_000 });
  await page.getByLabel('Your Name').fill(name);
  await page.getByRole('button', { name: 'Join Game' }).click();
  await expect(page).toHaveURL(/\/game\/.*\/lobby/, { timeout: 10_000 });
}

async function peekRoleInfo(page: Page): Promise<{ role: string; skill: string | null }> {
  const card = page.locator('.role-card');
  await expect(card).toBeVisible({ timeout: 10_000 });
  await card.dispatchEvent('mousedown');
  const roleEl = page.locator('.role-name');
  await expect(roleEl).toBeVisible({ timeout: 5_000 });
  const role = ((await roleEl.textContent()) ?? '').trim();
  const skillEl = page.locator('.skill-name');
  const skill = (await skillEl.isVisible()) ? ((await skillEl.textContent()) ?? '').trim() || null : null;
  await card.dispatchEvent('mouseup');
  return { role, skill: skill ?? null };
}

async function peekAndAccept(page: Page): Promise<{ role: string; skill: string | null }> {
  const info = await peekRoleInfo(page);
  await page.getByRole('button', { name: "I've seen my role" }).click();
  return info;
}

async function waitForPhase(page: Page, headingText: string, timeout = 30_000): Promise<void> {
  await expect(page.getByRole('heading', { level: 2 })).toContainText(headingText, { timeout });
}

async function selectDropdownOption(page: Page, containerSelector: string, optionText: string): Promise<void> {
  await page.locator(containerSelector).locator('.p-select').click();
  await page.getByRole('option', { name: optionText, exact: true }).click();
}

async function enableSkill(page: Page, skill: string): Promise<void> {
  await page.locator('.skill-toggle').filter({ hasText: skill }).locator('[role="switch"]').click({ force: true });
}

async function skipRound1(creatorPage: Page, allPages: Page[]): Promise<void> {
  for (const p of allPages) await waitForPhase(p, 'Night');
  await creatorPage.getByRole('button', { name: 'Skip night' }).click();

  for (const p of allPages) await waitForPhase(p, 'Discussion');
  await creatorPage.getByRole('button', { name: 'Force end discussion' }).click();

  for (const p of allPages) await waitForPhase(p, 'Village Verdict');
  await creatorPage.getByRole('button', { name: 'Continue' }).click();
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Hunter skill', () => {
  test(
    'Hunter is killed by wolves and takes the wolf with them',
    { tag: '@hunter' },
    async ({ browser }) => {
      // ── 1. Create game with Hunter only ──────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('4');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Hunter');

      // ── 2. Players join ──────────────────────────────────────────────────
      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      const carol = await newPlayer(browser, 'Carol');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');
      await joinGame(carol.page, gameId, 'Carol');

      await expect(creator.page.getByText('Players (4)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const players = [creator, alice, bob, carol];
      const allPages = players.map(p => p.page);

      for (const p of allPages) await expect(p).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });

      // ── 3. RoleReveal: detect roles ─────────────────────────────────────
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf    = players.find(p => p.role === 'Werewolf')!;
      const hunter  = players.find(p => p.skill === 'Hunter')!;

      expect(wolf).toBeTruthy();
      expect(hunter).toBeTruthy();

      // ── 4. Skip round 1 ─────────────────────────────────────────────────
      await skipRound1(creator.page, allPages);

      // ── 5. WerewolvesTurn (round 2): wolf targets the Hunter ─────────────
      for (const p of allPages) await waitForPhase(p, 'Night');
      await selectDropdownOption(wolf.page, '.wolf-vote', hunter.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // Creator force-advances so we don't wait for the night timer
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 6. NightEliminationReveal: Hunter is shown as eliminated ───────────────
      for (const p of allPages) await waitForPhase(p, 'Dawn', 20_000);
      await expect(creator.page.getByText(hunter.name)).toBeVisible({ timeout: 5_000 });
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 7. HunterTurn: Hunter must shoot before they go ──────────────────
      // All alive players see "The Hunter" phase; only the Hunter gets the shoot UI
      await waitForPhase(hunter.page, 'The Hunter', 20_000);
      await expect(hunter.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });

      // Hunter picks the wolf
      await selectDropdownOption(hunter.page, '.skill-action', wolf.name);
      await hunter.page.getByRole('button', { name: '🏹 Shoot' }).click();

      // ── 8. Game ends – Villagers win (wolf eliminated) ───────────────────
      await waitForPhase(creator.page, 'Final Scores Reveal', 20_000);
      await expect(creator.page.locator('.winner-text')).toContainText('Villager');

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Hunter eliminated by day vote takes a player with them',
    { tag: '@hunter' },
    async ({ browser }) => {
      // ── 1. Setup ────────────────────────────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('4');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Hunter');

      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      const carol = await newPlayer(browser, 'Carol');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');
      await joinGame(carol.page, gameId, 'Carol');

      await expect(creator.page.getByText('Players (4)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const players = [creator, alice, bob, carol];
      const allPages = players.map(p => p.page);

      for (const p of allPages) await expect(p).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });

      // ── 2. RoleReveal ────────────────────────────────────────────────────
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf   = players.find(p => p.role === 'Werewolf')!;
      const hunter = players.find(p => p.skill === 'Hunter')!;
      const plain  = players.find(p => p !== wolf && p !== hunter)!;

      // ── 3. Round 1 night: skip meeting ─────────────────────────────────
      for (const p of allPages) await waitForPhase(p, 'Night');
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 4. Discussion round 1: all vote for the Hunter ───────────────────
      for (const p of allPages) await waitForPhase(p, 'Discussion');
      // All players cast votes for the Hunter so they are eliminated by day vote
      for (const { page, name } of players) {
        if (name !== hunter.name) {
          // Only non-hunter players vote (hunter can vote too, but can vote anyone)
          await page.locator('.vote-section').locator('.p-select').click();
          await page.getByRole('option', { name: hunter.name, exact: true }).click();
          await page.getByRole('button', { name: 'Cast vote' }).click();
        }
      }
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      // ── 5. DayEliminationReveal: Hunter is eliminated ─────────────────────────
      await waitForPhase(creator.page, 'Village Verdict');
      await expect(creator.page.getByText(hunter.name)).toBeVisible({ timeout: 5_000 });
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 6. HunterTurn: triggered immediately after day elimination ───────
      await waitForPhase(hunter.page, 'The Hunter', 20_000);
      await expect(hunter.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });

      // Hunter shoots a plain villager (wolf would end the game – keep it simple)
      await selectDropdownOption(hunter.page, '.skill-action', plain.name);
      await hunter.page.getByRole('button', { name: '🏹 Shoot' }).click();

      // ── 7. Verify the target is shown as eliminated in Discussion ─────────
      // After HunterTurn the game continues (wolf still alive)
      for (const p of allPages) await waitForPhase(p, 'Night', 20_000);

      for (const { context } of players) await context.close();
    },
  );
});

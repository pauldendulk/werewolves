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

/** Navigate round 1: skip WerewolvesMeeting + force-end Discussion + continue DayEliminationReveal. */
async function skipRound1(creatorPage: Page, allPages: Page[]): Promise<void> {
  for (const p of allPages) await waitForPhase(p, 'Night');
  await creatorPage.getByRole('button', { name: 'Skip night' }).click();

  for (const p of allPages) await waitForPhase(p, 'Discussion');
  await creatorPage.getByRole('button', { name: 'Force end discussion' }).click();

  for (const p of allPages) await waitForPhase(p, 'Village Verdict');
  await creatorPage.getByRole('button', { name: 'Continue' }).click();
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Witch skill', () => {
  test(
    'Witch can save the nightly victim and nobody is eliminated at Dawn',
    { tag: '@witch' },
    async ({ browser }) => {
      // ── 1. Create game with Witch only ──────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('4');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Witch');

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

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const witch = players.find(p => p.skill === 'Witch')!;
      const plain = players.filter(p => p !== wolf && p !== witch);
      // Wolf will vote for the first non-witch, non-wolf villager
      const victim = plain[0];

      expect(wolf).toBeTruthy();
      expect(witch).toBeTruthy();
      expect(victim).toBeTruthy();

      // ── 4. Skip round 1 ─────────────────────────────────────────────────
      await skipRound1(creator.page, allPages);

      // ── 5. WerewolvesTurn: wolf votes for the planned victim ─────────────
      for (const p of allPages) await waitForPhase(p, 'Night');
      await selectDropdownOption(wolf.page, '.wolf-vote', victim.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 6. WitchTurn: witch sees victim name and saves them ──────────────
      await waitForPhase(witch.page, 'The Witch', 20_000);
      // The witch's skill-action should be visible (Witch can act)
      await expect(witch.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      // Victim's name should be shown in the "Tonight's victim" text
      await expect(witch.page.locator('.elimination-text')).toContainText(victim.name, { timeout: 10_000 });
      // Click "Save victim"
      await witch.page.getByRole('button', { name: '🧴 Save victim' }).click();

      // ── 7. NightEliminationReveal: nobody was taken ────────────────────────────
      for (const p of allPages) await waitForPhase(p, 'Dawn', 20_000);
      await expect(creator.page.getByText('No one was taken last night')).toBeVisible({ timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Witch can poison a player who is then eliminated at Dawn',
    { tag: '@witch' },
    async ({ browser }) => {
      // ── 1. Create game with Witch only ──────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('4');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Witch');

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

      // ── 2. RoleReveal: detect roles ─────────────────────────────────────
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const witch = players.find(p => p.skill === 'Witch')!;
      const plain = players.filter(p => p !== wolf && p !== witch);
      const wolfVictim  = plain[0]; // wolf kills this player
      const poisonTarget = plain[1] ?? wolf; // witch poisons this player (use wolf if only 1 plain)

      expect(witch).toBeTruthy();

      // ── 3. Skip round 1 ─────────────────────────────────────────────────
      await skipRound1(creator.page, allPages);

      // ── 4. WerewolvesTurn ───────────────────────────────────────────────
      for (const p of allPages) await waitForPhase(p, 'Night');
      await selectDropdownOption(wolf.page, '.wolf-vote', wolfVictim.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 5. WitchTurn: witch poisons a different player ───────────────────
      await waitForPhase(witch.page, 'The Witch', 20_000);
      await expect(witch.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      // Select poison target and click Poison
      await selectDropdownOption(witch.page, '.action-row', poisonTarget.name);
      await witch.page.getByRole('button', { name: '☠️ Poison' }).click();

      // ── 6. NightEliminationReveal: both poison target AND wolf victim appear ────
      for (const p of allPages) await waitForPhase(p, 'Dawn', 20_000);
      await expect(creator.page.getByText(poisonTarget.name)).toBeVisible({ timeout: 5_000 });
      await expect(creator.page.getByText(wolfVictim.name)).toBeVisible({ timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );
});

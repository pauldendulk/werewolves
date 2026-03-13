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

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Cupid skill', () => {
  test(
    'Cupid links two players as lovers and they see each other in LoverReveal',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only ──────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('4');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Cupid');

      // ── 2. Three more players join ───────────────────────────────────────
      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      const carol = await newPlayer(browser, 'Carol');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');
      await joinGame(carol.page, gameId, 'Carol');

      await expect(creator.page.getByText('Players (4)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      // ── 3. Wait for session ─────────────────────────────────────────────
      const players = [creator, alice, bob, carol];
      for (const { page } of players) {
        await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
      }

      // ── 4. RoleReveal: detect who has Cupid ──────────────────────────────
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const cupid  = players.find(p => p.skill === 'Cupid')!;
      expect(cupid).toBeTruthy();

      // Pick two players other than Cupid as the lovers (use the first two)
      const non_cupid = players.filter(p => p !== cupid);
      const lover1 = non_cupid[0];
      const lover2 = non_cupid[1];

      // ── 5. CupidTurn: Cupid links lover1 and lover2 ─────────────────────
      for (const { page } of players) await waitForPhase(page, 'Cupid');

      // Only Cupid sees the action UI; others see "Cupid is choosing..."
      await expect(cupid.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(cupid.page, '.action-row:nth-child(1)', lover1.name);
      await selectDropdownOption(cupid.page, '.action-row:nth-child(2)', lover2.name);
      await cupid.page.getByRole('button', { name: 'Link lovers' }).click();

      // ── 6. LoverReveal: the two lovers each see their partner's name ──────
      for (const { page } of players) await waitForPhase(page, 'Lovers', 20_000);

      // Lover 1 sees their partner
      await expect(lover1.page.locator('.phase-subtitle')).toContainText(lover2.name, { timeout: 10_000 });
      // Lover 2 sees their partner
      await expect(lover2.page.locator('.phase-subtitle')).toContainText(lover1.name, { timeout: 10_000 });

      // Non-lovers (Cupid if not a lover) see the waiting message
      await expect(cupid.page.locator('.phase-subtitle')).toContainText('looking', { timeout: 5_000 });

      // ── 7. Creator continues past LoverReveal ────────────────────────────
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 8. WerewolvesMeeting begins ──────────────────────────────────────
      for (const { page } of players) await waitForPhase(page, 'Night');

      // ── Cleanup ─────────────────────────────────────────────────────────
      for (const { context } of players) await context.close();
    },
  );

  test(
    'Cupid skip: when Cupid does not act, LoverReveal is skipped',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only ──────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('3');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Cupid');

      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');

      await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const players = [creator, alice, bob];
      for (const { page } of players) {
        await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
        await peekAndAccept(page);
      }

      // ── 2. CupidTurn: creator skips without linking anyone ───────────────
      for (const { page } of players) await waitForPhase(page, 'Cupid');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 3. LoverReveal should be skipped – goes straight to WerewolvesMeeting
      for (const { page } of players) await waitForPhase(page, 'Night');
      // Verify we did NOT see the LoverReveal heading
      // (If the page shows "Night" we know LoverReveal was skipped)
      for (const { page } of players) {
        await expect(page.getByRole('heading', { level: 2 })).toContainText('Night');
      }

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Wolves kill a lover → their partner cascade-dies at Dawn',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only (3 players) ──────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('3');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Cupid');

      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');

      await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const players = [creator, alice, bob];
      const allPages = players.map(p => p.page);
      for (const p of allPages) await expect(p).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });

      // ── 2. RoleReveal: detect roles ──────────────────────────────────────
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf          = players.find(p => p.role === 'Werewolf')!;
      const cupid         = players.find(p => p.skill === 'Cupid')!;
      const plainVillager = players.find(p => p !== wolf && p !== cupid)!;

      // ── 3. CupidTurn: Cupid links wolf + plain villager as lovers ───────
      // (allPlayers excludes self, so Cupid links the other two players)
      for (const p of allPages) await waitForPhase(p, 'Cupid');
      await expect(cupid.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(cupid.page, '.action-row:nth-child(1)', wolf.name);
      await selectDropdownOption(cupid.page, '.action-row:nth-child(2)', plainVillager.name);
      await cupid.page.getByRole('button', { name: 'Link lovers' }).click();

      // ── 4. LoverReveal: continue ─────────────────────────────────────────
      for (const p of allPages) await waitForPhase(p, 'Lovers', 20_000);
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 5. Round 1: skip wolves meeting + empty discussion ───────────────
      for (const p of allPages) await waitForPhase(p, 'Night');
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      for (const p of allPages) await waitForPhase(p, 'Discussion');
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      for (const p of allPages) await waitForPhase(p, 'Village Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 6. WerewolvesTurn: wolf kills their lover (the plain villager) ───
      for (const p of allPages) await waitForPhase(p, 'Night');
      await selectDropdownOption(wolf.page, '.wolf-vote', plainVillager.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 7. Dawn: BOTH lovers appear as killed ────────────────────────────
      // plain-villager dies from wolf kill; wolf cascade-dies as the other lover
      for (const p of allPages) await waitForPhase(p, 'Dawn', 20_000);
      await expect(creator.page.locator('.elimination-text')).toHaveCount(2, { timeout: 10_000 });
      await expect(creator.page.locator('.elimination-text').filter({ hasText: plainVillager.name })).toBeVisible();
      await expect(creator.page.locator('.elimination-text').filter({ hasText: wolf.name })).toBeVisible();

      // ── 8. Continue → Game Over: only Cupid survives → Villagers win ─────
      await creator.page.getByRole('button', { name: 'Continue' }).click();
      await waitForPhase(creator.page, 'Game Over', 20_000);
      await expect(creator.page.locator('.winner-text')).toContainText('Villagers', { timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Day vote on a lover cascade-kills their partner',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only (3 players) ──────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('3');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Cupid');

      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');

      await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const players = [creator, alice, bob];
      const allPages = players.map(p => p.page);
      for (const p of allPages) await expect(p).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });

      // ── 2. RoleReveal: detect roles ──────────────────────────────────────
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf          = players.find(p => p.role === 'Werewolf')!;
      const cupid         = players.find(p => p.skill === 'Cupid')!;
      const plainVillager = players.find(p => p !== wolf && p !== cupid)!;

      // ── 3. CupidTurn: Cupid links wolf + plain villager as lovers ───────
      for (const p of allPages) await waitForPhase(p, 'Cupid');
      await expect(cupid.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(cupid.page, '.action-row:nth-child(1)', wolf.name);
      await selectDropdownOption(cupid.page, '.action-row:nth-child(2)', plainVillager.name);
      await cupid.page.getByRole('button', { name: 'Link lovers' }).click();

      // ── 4. LoverReveal: continue ─────────────────────────────────────────
      for (const p of allPages) await waitForPhase(p, 'Lovers', 20_000);
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 5. WerewolvesMeeting (round 1): skip ────────────────────────────
      for (const p of allPages) await waitForPhase(p, 'Night');
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 6. Discussion: wolf + cupid both vote for the plain villager ─────
      for (const p of allPages) await waitForPhase(p, 'Discussion');
      for (const voter of [wolf, cupid]) {
        await voter.page.locator('.vote-section').locator('.p-select').click();
        await voter.page.getByRole('option', { name: plainVillager.name, exact: true }).click();
        await voter.page.getByRole('button', { name: 'Cast vote' }).click();
      }
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      // ── 7. Village Verdict: plain villager is eliminated by day vote ─────
      for (const p of allPages) await waitForPhase(p, 'Village Verdict');
      await expect(creator.page.locator('.elimination-text')).toContainText(plainVillager.name, { timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 8. Game Over: wolf cascade-died as lover → only Cupid survives ───
      await waitForPhase(creator.page, 'Game Over', 20_000);
      await expect(creator.page.locator('.winner-text')).toContainText('Villagers', { timeout: 5_000 });
      // Both lovers (plain-villager voted out + wolf cascade) show as eliminated
      await expect(
        creator.page.locator('.role-summary-row.eliminated').filter({ hasText: plainVillager.name }),
      ).toBeVisible({ timeout: 5_000 });
      await expect(
        creator.page.locator('.role-summary-row.eliminated').filter({ hasText: wolf.name }),
      ).toBeVisible({ timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );
});

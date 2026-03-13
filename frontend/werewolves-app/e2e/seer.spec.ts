import { test, expect, Browser, BrowserContext, Page } from '@playwright/test';

// ─── Shared types ─────────────────────────────────────────────────────────────

interface PlayerHandle {
  context: BrowserContext;
  page: Page;
  name: string;
  role?: string;
  skill?: string | null;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

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

/** Peek role card, read role + skill, then release the card. */
async function peekRoleInfo(page: Page): Promise<{ role: string; skill: string | null }> {
  const card = page.locator('.role-card');
  await expect(card).toBeVisible({ timeout: 10_000 });
  await card.dispatchEvent('mousedown');
  const roleNameEl = page.locator('.role-name');
  await expect(roleNameEl).toBeVisible({ timeout: 5_000 });
  const role = ((await roleNameEl.textContent()) ?? '').trim();
  const skillEl = page.locator('.skill-name');
  const skillVisible = await skillEl.isVisible();
  const skill = skillVisible ? ((await skillEl.textContent()) ?? '').trim() || null : null;
  await card.dispatchEvent('mouseup');
  return { role, skill: skill ?? null };
}

/** Peek role + skill, then click the "I've seen my role" button to mark done. */
async function peekAndAccept(page: Page): Promise<{ role: string; skill: string | null }> {
  const info = await peekRoleInfo(page);
  await page.getByRole('button', { name: "I've seen my role" }).click();
  return info;
}

/** Wait until the phase h2 heading contains the given text. */
async function waitForPhase(page: Page, headingText: string, timeout = 30_000): Promise<void> {
  await expect(page.getByRole('heading', { level: 2 })).toContainText(headingText, { timeout });
}

/** Click a p-select dropdown inside a container and pick an option by name. */
async function selectDropdownOption(page: Page, containerSelector: string, optionText: string): Promise<void> {
  await page.locator(containerSelector).locator('.p-select').click();
  await page.getByRole('option', { name: optionText, exact: true }).click();
}

/** Enable a skill toggle in the lobby (by skill label text). */
async function enableSkill(page: Page, skill: string): Promise<void> {
  // PrimeNG ToggleSwitch renders an element with role="switch"
  await page.locator('.skill-toggle').filter({ hasText: skill }).locator('[role="switch"]').click({ force: true });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Seer skill', () => {
  test(
    'Seer can inspect a player and see whether they are a Werewolf',
    { tag: '@seer' },
    async ({ browser }) => {
      // ── 1. Create the game ──────────────────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      // Set min players to 3 and enable only Seer
      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('3');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Seer');

      // ── 2. Two more players join ────────────────────────────────────────
      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');

      await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      // ── 3. Everyone arrives at the session ─────────────────────────────
      for (const { page } of [creator, alice, bob]) {
        await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
      }

      // ── 4. RoleReveal: peek each card and detect roles ─────────────────
      const players = [creator, alice, bob];
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const seer  = players.find(p => p.skill === 'Seer')!;
      const other = players.find(p => p !== wolf && p !== seer)!;

      expect(wolf).toBeTruthy();
      expect(seer).toBeTruthy();

      // ── 5. WerewolvesMeeting (round 1): creator skips ──────────────────
      await waitForPhase(creator.page, 'Night');
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 6. Discussion (day 1): creator force-ends with no votes ─────────
      await waitForPhase(creator.page, 'Discussion');
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      // ── 7. DayElimination: no one eliminated (tied/empty votes) ─────────
      await waitForPhase(creator.page, 'Village Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 8. WerewolvesTurn (round 2): wolf votes for the plain villager ──
      for (const { page } of players) await waitForPhase(page, 'Night');
      // Vote for the player who is neither wolf nor seer, to keep the Seer alive
      await selectDropdownOption(wolf.page, '.wolf-vote', other.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // Creator force-advances from WerewolvesTurn to SeerTurn
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 9. SeerTurn: Seer inspects the wolf ────────────────────────────
      await waitForPhase(seer.page, 'The Seer', 20_000);
      await expect(seer.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(seer.page, '.skill-action', wolf.name);
      await seer.page.getByRole('button', { name: 'Reveal' }).click();

      // ── 10. Verify the Seer result shows "Werewolf" ────────────────────
      await expect(seer.page.locator('.seer-result .seer-verdict')).toBeVisible({ timeout: 10_000 });
      await expect(seer.page.locator('.seer-result .seer-verdict')).toContainText('Werewolf');

      // ── 11. Seer marks done → NightElimination shows the victim ────────
      await seer.page.getByRole('button', { name: 'Done' }).click();
      for (const { page } of players) await waitForPhase(page, 'Dawn', 20_000);
      await expect(creator.page.getByText(other.name)).toBeVisible({ timeout: 5_000 });

      // ── Cleanup ────────────────────────────────────────────────────────
      for (const { context } of players) {
        await context.close();
      }
    },
  );

  test(
    'Seer sees Villager result when inspecting a non-werewolf player',
    { tag: '@seer' },
    async ({ browser }) => {
      // ── 1. Create game ──────────────────────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
      await minInput.fill('3');
      await minInput.press('Tab');
      await enableSkill(creator.page, 'Seer');

      // ── 2. Join & start ─────────────────────────────────────────────────
      const alice = await newPlayer(browser, 'Alice');
      const bob   = await newPlayer(browser, 'Bob');
      await joinGame(alice.page, gameId, 'Alice');
      await joinGame(bob.page,   gameId, 'Bob');

      await expect(creator.page.getByText('Players (3)')).toBeVisible({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      for (const { page } of [creator, alice, bob]) {
        await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
      }

      // ── 3. RoleReveal ────────────────────────────────────────────────────
      const players = [creator, alice, bob];
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const seer  = players.find(p => p.skill === 'Seer')!;
      const other = players.find(p => p !== wolf && p !== seer)!;

      // ── 4. Round 1: skip through night and day ──────────────────────────
      for (const { page } of players) await waitForPhase(page, 'Night');
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      for (const { page } of players) await waitForPhase(page, 'Discussion');
      await creator.page.getByRole('button', { name: 'Force end discussion' }).click();

      for (const { page } of players) await waitForPhase(page, 'Village Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 5. WerewolvesTurn: wolf votes ───────────────────────────────────
      for (const { page } of players) await waitForPhase(page, 'Night');
      await selectDropdownOption(wolf.page, '.wolf-vote', other.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      await creator.page.getByRole('button', { name: 'Skip night' }).click();

      // ── 6. SeerTurn: Seer inspects the plain villager ─────────────────
      await waitForPhase(seer.page, 'The Seer', 20_000);
      await expect(seer.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(seer.page, '.skill-action', other.name);
      await seer.page.getByRole('button', { name: 'Reveal' }).click();

      // ── 7. Verify result shows "Villager" ──────────────────────────
      await expect(seer.page.locator('.seer-result .seer-verdict')).toBeVisible({ timeout: 10_000 });
      await expect(seer.page.locator('.seer-result .seer-verdict')).toContainText('Villager');

      for (const { context } of players) {
        await context.close();
      }
    },
  );
});

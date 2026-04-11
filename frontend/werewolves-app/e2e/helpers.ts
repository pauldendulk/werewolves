import { expect, Browser, BrowserContext, Page } from '@playwright/test';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface PlayerHandle {
  context: BrowserContext;
  page: Page;
  name: string;
  role?: string;
  skill?: string | null;
}

// ─── Player / session lifecycle ───────────────────────────────────────────────

export async function newPlayer(browser: Browser, name: string): Promise<PlayerHandle> {
  const context = await browser.newContext();
  const page = await context.newPage();
  return { context, page, name };
}

export async function joinGame(page: Page, gameId: string, name: string): Promise<void> {
  await page.goto(`/game/${gameId}`);
  await expect(page.getByRole('heading', { name: 'Join Game' })).toBeVisible({ timeout: 10_000 });
  await page.getByLabel('Your Name').fill(name);
  await page.getByRole('button', { name: 'Join Game' }).click();
  await expect(page).toHaveURL(/\/game\/.*\/lobby/, { timeout: 10_000 });
}

/**
 * Creates a game (creator navigates to /), adds extra players, configures
 * min-players and skills, starts the game, and waits for everyone to reach
 * the session page.
 *
 * @param browser        Playwright Browser fixture
 * @param extraNames     Names of non-creator players to add (e.g. ['Alice', 'Bob'])
 * @param skills         Skill names to enable in lobby (e.g. ['Witch'])
 * @param minPlayers     Min-players setting (default: extraNames.length + 1)
 * @returns              creator + all players array
 */
export async function createAndStartGame(
  browser: Browser,
  extraNames: string[],
  skills: string[] = [],
  minPlayers?: number,
): Promise<{ creator: PlayerHandle; players: PlayerHandle[]; gameId: string }> {
  const creator = await newPlayer(browser, 'PlayerName');
  await creator.page.goto('/');
  await creator.page.getByLabel('Your Name').fill('PlayerName');
  await creator.page.getByRole('button', { name: 'Organize Game' }).click();
  await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });

  const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

  const effectiveMin = minPlayers ?? extraNames.length + 1;
  const minInput = creator.page.getByRole('spinbutton', { name: 'Min Players' });
  await minInput.fill(String(effectiveMin));
  await minInput.press('Tab');

  for (const skill of skills) {
    await enableSkill(creator.page, skill);
  }

  const extras = await Promise.all(extraNames.map(name => newPlayer(browser, name)));
  for (const { page, name } of extras) {
    await joinGame(page, gameId, name);
  }

  const total = extraNames.length + 1;
  await expect(creator.page.getByText(`Players (${total})`)).toBeVisible({ timeout: 10_000 });
  await creator.page.getByRole('button', { name: 'Start Game' }).click();

  const players = [creator, ...extras];
  for (const { page } of players) {
    await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
  }

  return { creator, players, gameId };
}

// ─── Role reveal ─────────────────────────────────────────────────────────────

/** Press-hold the role card, read role + skill name, then release. */
export async function peekRoleInfo(page: Page): Promise<{ role: string; skill: string | null }> {
  const card = page.locator('.role-card');
  await expect(card).toBeVisible({ timeout: 10_000 });
  await card.dispatchEvent('mousedown');
  const roleNameEl = page.locator('.role-name');
  await expect(roleNameEl).toBeVisible({ timeout: 5_000 });
  const role = ((await roleNameEl.textContent()) ?? '').trim();
  const skillEl = page.locator('.skill-name');
  const skill = (await skillEl.isVisible()) ? ((await skillEl.textContent()) ?? '').trim() || null : null;
  await card.dispatchEvent('mouseup');
  return { role, skill: skill ?? null };
}

/** Peek role + skill, then click "I've seen my role" to mark the player done. */
export async function peekAndAccept(page: Page): Promise<{ role: string; skill: string | null }> {
  const info = await peekRoleInfo(page);
  await page.getByRole('button', { name: "I've seen my role" }).click();
  return info;
}

/**
 * Peek every player's role card and store the results on each PlayerHandle.
 * Returns the same array for convenience.
 */
export async function revealAllRoles(players: PlayerHandle[]): Promise<PlayerHandle[]> {
  for (const player of players) {
    const info = await peekAndAccept(player.page);
    player.role  = info.role;
    player.skill = info.skill;
  }
  return players;
}

// ─── Phase navigation ─────────────────────────────────────────────────────────

/** Wait until the phase h2 heading contains the given text. */
export async function waitForPhase(page: Page, headingText: string, timeout = 30_000): Promise<void> {
  await expect(page.getByRole('heading', { level: 2 })).toContainText(headingText, { timeout });
}

/**
 * Navigate through round 1 (no wolf kill):
 *   NightAnnouncement → WerewolvesMeeting (wolves click Ready) →
 *   DayAnnouncement → Discussion (all vote + end) → Verdict (continue)
 */
export async function skipRound1(
  creatorPage: Page,
  allPlayers: PlayerHandle[],
  wolves: PlayerHandle[],
): Promise<void> {
  const allPages = allPlayers.map(p => p.page);

  await waitForPhase(creatorPage, 'The Night Has Fallen');
  await creatorPage.getByRole('button', { name: 'Skip' }).click();

  await waitForPhase(creatorPage, 'Werewolves Meeting');
  for (const wolf of wolves) await wolf.page.getByRole('button', { name: 'Ready' }).click();

  await waitForPhase(creatorPage, 'The Night Has Ended');
  await creatorPage.getByRole('button', { name: 'Skip' }).click();

  for (const p of allPages) await waitForPhase(p, 'Discussion');
  // Circular vote → N-way tie → tiebreak → tie again → no elimination.
  await tieVoteAndEndDiscussion(allPlayers);

  for (const p of allPages) await waitForPhase(p, 'Verdict');
  await creatorPage.getByRole('button', { name: 'Continue' }).click();
}

/**
 * Skip the DayAnnouncement phase ("The Night Has Ended") and wait for
 * NightEliminationReveal ("Victims") on all pages.
 *
 * The moderator clicks Skip on DayAnnouncement to avoid the timed
 * auto-advance delay.
 */
export async function skipDayAnnouncementAndWaitForVictims(
  creatorPage: Page,
  allPages: Page[],
): Promise<void> {
  await waitForPhase(creatorPage, 'The Night Has Ended', 15_000);
  await creatorPage.getByRole('button', { name: 'Skip' }).click();
  for (const p of allPages) await waitForPhase(p, 'Victims', 15_000);
}

/**
 * End the Discussion / TiebreakDiscussion phase by having every alive
 * player click "End discussion". The backend auto-advances the phase
 * once all eligible players have marked done, so this is fully
 * deterministic (no timer dependency).
 *
 * Callers must ensure all alive players have already voted before
 * calling this — the button is disabled until the player has voted.
 */
export async function endDiscussion(alivePlayers: PlayerHandle[]): Promise<void> {
  for (const { page } of alivePlayers) {
    const btn = page.getByRole('button', { name: 'End discussion', exact: true });
    await expect(btn).toBeEnabled({ timeout: 10_000 });
    await btn.click();
  }
}

/**
 * Vote circularly so every player gets exactly 1 vote (N-way tie),
 * then end discussion. The backend sends all N players into a tiebreak;
 * vote circularly again → tie → no elimination.
 *
 * Use this when the test needs a Discussion round that eliminates nobody.
 */
export async function tieVoteAndEndDiscussion(allPlayers: PlayerHandle[]): Promise<void> {
  for (let i = 0; i < allPlayers.length; i++) {
    await dayVote(allPlayers[i].page, allPlayers[(i + 1) % allPlayers.length].name);
  }
  await endDiscussion(allPlayers);

  // N-way tie → TiebreakDiscussion. Vote circularly again → tie → no elimination.
  for (const { page } of allPlayers) await waitForPhase(page, 'Tiebreak Vote');
  for (let i = 0; i < allPlayers.length; i++) {
    await dayVote(allPlayers[i].page, allPlayers[(i + 1) % allPlayers.length].name);
  }
  await endDiscussion(allPlayers);
}

// ─── In-game actions ─────────────────────────────────────────────────────────

/** Open a p-select dropdown inside a container element and pick an option. */
export async function selectDropdownOption(page: Page, containerSelector: string, optionText: string): Promise<void> {
  await page.locator(containerSelector).locator('.p-select').click();
  await page.getByRole('option', { name: optionText, exact: true }).click();
}

/** Enable a skill toggle in the lobby settings panel. */
export async function enableSkill(page: Page, skill: string): Promise<void> {
  await page.locator('.setting-item').filter({ hasText: skill }).locator('[role="switch"]').click({ force: true });
}

/** Cast a werewolf night vote (selects victim and clicks "Confirm kill"). */
export async function nightVote(page: Page, targetName: string): Promise<void> {
  await selectDropdownOption(page, '.wolf-vote', targetName);
  await page.getByRole('button', { name: 'Confirm kill' }).click();
}

/** Cast a day vote during Discussion or TiebreakDiscussion. */
export async function dayVote(page: Page, targetName: string): Promise<void> {
  await selectDropdownOption(page, '.vote-section', targetName);
  await page.getByRole('button', { name: 'Vote' }).click();
}

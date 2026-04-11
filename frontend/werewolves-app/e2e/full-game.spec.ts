import { test, expect } from '@playwright/test';
import {
  newPlayer, joinGame, revealAllRoles,
  waitForPhase, nightVote, dayVote, endDiscussion, skipDayAnnouncementAndWaitForVictims,
} from './helpers';

// ─── Test ────────────────────────────────────────────────────────────────────

test.describe('Full 6-player game – werewolves win', () => {
  test(
    '4-round scenario: no-kill night, wolves agree, tiebreak, wolves eliminate last villager',
    { tag: '@full-game' },
    async ({ browser }) => {
      test.setTimeout(180_000); // 6-player, 4-round game needs more than 30 s

      // ── 1. Create game ────────────────────────────────────────────────────
      const creator = await newPlayer(browser, 'PlayerName');
      await creator.page.goto('/');
      await creator.page.getByLabel('Your Name').fill('PlayerName');
      await creator.page.getByRole('button', { name: 'Organize Game' }).click();
      await expect(creator.page).toHaveURL(/\/game\/([^/]+)\/lobby/, { timeout: 10_000 });
      const gameId = creator.page.url().match(/\/game\/([^/]+)\/lobby/)![1];

      // ── 2. Five players join ──────────────────────────────────────────────
      const alice   = await newPlayer(browser, 'Alice');
      const bob     = await newPlayer(browser, 'Bob');
      const charlie = await newPlayer(browser, 'Charlie');
      const dave    = await newPlayer(browser, 'Dave');
      const eve     = await newPlayer(browser, 'Eve');

      await joinGame(alice.page,   gameId, 'Alice');
      await joinGame(bob.page,     gameId, 'Bob');
      await joinGame(charlie.page, gameId, 'Charlie');
      await joinGame(dave.page,    gameId, 'Dave');
      await joinGame(eve.page,     gameId, 'Eve');

      // ── 3. Wait for all 6 players, configure settings, then start ────────
      await expect(creator.page.getByText('Players (6)')).toBeVisible({ timeout: 15_000 });

      // Set wolves=2 and disable skills via direct API call.
      // PrimeNG spinbutton buttons are aria-hidden so getByRole won't find them;
      // fill/keyboard don't reliably trigger ngModelChange.
      // UpdateNumberOfWerewolves validates count < activeCount, so all 6 players
      // must be present before this call.
      const moderatorId = await creator.page.evaluate(() => localStorage.getItem('playerId'));
      const settingsResp = await creator.page.request.post(
        `http://localhost:5000/api/game/${gameId}/settings`,
        { data: { moderatorId, minPlayers: 3, maxPlayers: 20, discussionDurationMinutes: 5, tiebreakDiscussionDurationSeconds: 60, numberOfWerewolves: 2, enabledSkills: [] } },
      );
      expect(settingsResp.ok()).toBeTruthy();
      // Wait for lobby poll to reflect the change before starting
      await expect(creator.page.getByRole('spinbutton', { name: 'Werewolves' })).toHaveValue('2', { timeout: 10_000 });

      await expect(creator.page.getByRole('button', { name: 'Start Game' })).toBeEnabled({ timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Start Game' }).click();

      const allPlayers = [creator, alice, bob, charlie, dave, eve];

      for (const { page } of allPlayers) {
        await expect(page).toHaveURL(/\/game\/.*\/session/, { timeout: 15_000 });
      }

      // ── 4. Role Reveal: everyone peeks and marks ready ───────────────────
      await revealAllRoles(allPlayers);

      // Separate into werewolves and villagers
      const wolves    = allPlayers.filter(p => p.role === 'Werewolf');
      const villagers = allPlayers.filter(p => p.role !== 'Werewolf');
      expect(wolves).toHaveLength(2);
      expect(villagers).toHaveLength(4);
      // Convenient aliases
      const [W0, W1] = wolves;
      const [V0, V1, V2, V3] = villagers;

      const allPages = allPlayers.map(p => p.page);

      // ── ROUND 1 ──────────────────────────────────────────────────────────
      // Night 1: no kills — wolves click Ready, then DayAnnouncement is skipped
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      for (const wolf of wolves) await wolf.page.getByRole('button', { name: 'Ready' }).click();
      await waitForPhase(creator.page, 'The Night Has Ended');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // Day 1: everyone votes V[0] → V[0] eliminated, state: 2W + 3V
      for (const { page } of allPlayers) await waitForPhase(page, 'Discussion');
      const votingPlayers1 = allPlayers.filter(p => p.name !== V0.name);
      for (const { page } of votingPlayers1) await dayVote(page, V0.name);
      await dayVote(V0.page, V1.name); // V0 must also vote to enable End discussion
      await endDiscussion(allPlayers);

      for (const { page } of allPlayers) await waitForPhase(page, 'Verdict');
      await expect(creator.page.getByText(V0.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── ROUND 2 ──────────────────────────────────────────────────────────
      // Night 2: both wolves vote V[1] → V[1] eliminated, state: 2W + 2V
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      // Playwright auto-waits for .wolf-vote to appear (Werewolves phase after NightAnnouncement)
      await nightVote(W0.page, V1.name);
      await nightVote(W1.page, V1.name);
      await skipDayAnnouncementAndWaitForVictims(creator.page, allPages);
      await expect(creator.page.getByText(V1.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // Day 2: wolves split; surviving villagers V[2] and V[3] agree on W[0]
      // Alive: W0, W1, V2, V3
      const aliveRound2 = [W0, W1, V2, V3];
      for (const { page } of allPlayers) await waitForPhase(page, 'Discussion');
      await dayVote(W0.page, V2.name);
      await dayVote(W1.page, V3.name);
      await dayVote(V2.page, W0.name);
      await dayVote(V3.page, W0.name);
      await endDiscussion(aliveRound2);

      for (const { page } of allPlayers) await waitForPhase(page, 'Verdict');
      await expect(creator.page.getByText(W0.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── ROUND 3 ──────────────────────────────────────────────────────────
      // Night 3: W[1] kills V[2] → state: 1W + 1V
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await nightVote(W1.page, V2.name);
      await skipDayAnnouncementAndWaitForVictims(creator.page, allPages);
      await expect(creator.page.getByText(V2.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // Day 3: W[1]→V[3], V[3]→W[1] → tie → TiebreakDiscussion
      // Alive: W1, V3
      const aliveRound3 = [W1, V3];
      for (const { page } of allPlayers) await waitForPhase(page, 'Discussion');
      await dayVote(W1.page, V3.name);
      await dayVote(V3.page, W1.name);
      await endDiscussion(aliveRound3);

      for (const { page } of allPlayers) await waitForPhase(page, 'Tiebreak Vote');

      // Tiebreak: same votes → tie again → no elimination
      await dayVote(W1.page, V3.name);
      await dayVote(V3.page, W1.name);
      await endDiscussion(aliveRound3);

      for (const { page } of allPlayers) await waitForPhase(page, 'Verdict');
      // No one should be eliminated
      await expect(creator.page.getByText('could not agree')).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── ROUND 4 ──────────────────────────────────────────────────────────
      // Night 4: W[1] kills V[3] → 0 villagers → WEREWOLVES WIN
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await nightVote(W1.page, V3.name);
      await skipDayAnnouncementAndWaitForVictims(creator.page, allPages);
      await expect(creator.page.getByText(V3.name)).toBeVisible();
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── FINAL SCORES REVEAL ───────────────────────────────────────────────
      for (const { page } of allPlayers) await waitForPhase(page, 'Final Scores Reveal', 20_000);
      for (const { page } of allPlayers) {
        await expect(page.locator('.winner-text')).toContainText('Werewolves Win!', { timeout: 10_000 });
      }

      // ── Cleanup ───────────────────────────────────────────────────────────
      for (const { context } of allPlayers) await context.close();
    },
  );
});

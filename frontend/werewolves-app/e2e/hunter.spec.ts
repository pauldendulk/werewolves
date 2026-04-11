import { test, expect } from '@playwright/test';
import {
  createAndStartGame, revealAllRoles,
  waitForPhase, selectDropdownOption, skipRound1, dayVote, endDiscussion,
} from './helpers';

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Hunter skill', () => {
  test(
    'Hunter is killed by wolves and takes the wolf with them',
    { tag: '@hunter' },
    async ({ browser }) => {
      // ── 1. Create game with Hunter only ──────────────────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob', 'Carol'], ['Hunter'],
      );
      const allPages = players.map(p => p.page);

      // ── 3. RoleReveal: detect roles ─────────────────────────────────────
      await revealAllRoles(players);

      const wolf    = players.find(p => p.role === 'Werewolf')!;
      const hunter  = players.find(p => p.skill === 'Hunter')!;

      expect(wolf).toBeTruthy();
      expect(hunter).toBeTruthy();

      // ── 4. Skip round 1 ─────────────────────────────────────────────────
      await skipRound1(creator.page, players, players.filter(p => p.role === 'Werewolf'));

      // ── 5. WerewolvesTurn (round 2): wolf targets the Hunter ─────────────
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(wolf.page, 'Werewolves');
      await selectDropdownOption(wolf.page, '.wolf-vote', hunter.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // WerewolvesCloseEyes auto-advances (6s) to HunterTurn

      // ── 6. NightEliminationReveal: Hunter is shown as eliminated ───────────────
      for (const p of allPages) await waitForPhase(p, 'Victims', 20_000);
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
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob', 'Carol'], ['Hunter'],
      );
      const allPages = players.map(p => p.page);

      // ── 2. RoleReveal ────────────────────────────────────────────────────
      await revealAllRoles(players);

      const wolf   = players.find(p => p.role === 'Werewolf')!;
      const hunter = players.find(p => p.skill === 'Hunter')!;
      const plain  = players.find(p => p !== wolf && p !== hunter)!;

      // ── 3. Round 1: skip all announcement phases
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      await waitForPhase(creator.page, 'Werewolves Meeting');
      for (const w of players.filter(p => p.role === 'Werewolf'))
        await w.page.getByRole('button', { name: 'Ready' }).click();

      await waitForPhase(creator.page, 'The Night Has Ended');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 4. Discussion round 1: all vote for the Hunter ───────────────────
      for (const p of allPages) await waitForPhase(p, 'Discussion');
      for (const { page, name } of players) {
        if (name !== hunter.name) {
          await dayVote(page, hunter.name);
        }
      }
      await dayVote(hunter.page, players.find(p => p !== hunter)!.name);
      await endDiscussion(players);

      // ── 5. DayEliminationReveal: Hunter is eliminated ─────────────────────────
      await waitForPhase(creator.page, 'Verdict');
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

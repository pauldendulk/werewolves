import { test, expect } from '@playwright/test';
import {
  createAndStartGame, revealAllRoles,
  waitForPhase, selectDropdownOption, skipRound1, skipDayAnnouncementAndWaitForVictims,
} from './helpers';

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Witch skill', () => {
  test(
    'Witch can save the nightly victim and nobody is eliminated at night',
    { tag: '@witch' },
    async ({ browser }) => {
      // ── 1. Create game with Witch only ──────────────────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob', 'Carol'], ['Witch'],
      );
      const allPages = players.map(p => p.page);

      // ── 3. RoleReveal: detect roles ─────────────────────────────────────
      await revealAllRoles(players);

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const witch = players.find(p => p.skill === 'Witch')!;
      const plain = players.filter(p => p !== wolf && p !== witch);
      // Wolf will vote for the first non-witch, non-wolf villager
      const victim = plain[0];

      expect(wolf).toBeTruthy();
      expect(witch).toBeTruthy();
      expect(victim).toBeTruthy();

      // ── 4. Skip round 1 ─────────────────────────────────────────────────
      await skipRound1(creator.page, players, players.filter(p => p.role === 'Werewolf'));

      // ── 5. WerewolvesTurn: wolf votes for the planned victim ─────────────
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(wolf.page, 'Werewolves');
      await selectDropdownOption(wolf.page, '.wolf-vote', victim.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // WerewolvesCloseEyes auto-advances (~6s) to WitchTurn

      // ── 6. WitchTurn: witch sees victim name and saves them ──────────────
      await waitForPhase(witch.page, 'The Witch', 20_000);
      // The witch's skill-action should be visible (Witch can act)
      await expect(witch.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      // Victim's name should be shown in the "Tonight's victim" text
      await expect(witch.page.locator('.elimination-text')).toContainText(victim.name, { timeout: 10_000 });
      // Click "Save victim"
      await witch.page.getByRole('button', { name: '🧴 Save victim' }).click();

      // ── 7. WitchCloseEyes (6s) → DayAnnouncement → NightEliminationReveal ──────
      await skipDayAnnouncementAndWaitForVictims(creator.page, allPages);
      await expect(creator.page.getByText('No one was eliminated last night')).toBeVisible({ timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Witch can poison a player who is then eliminated at night',
    { tag: '@witch' },
    async ({ browser }) => {
      // ── 1. Create game with Witch only ──────────────────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob', 'Carol'], ['Witch'],
      );
      const allPages = players.map(p => p.page);

      // ── 2. RoleReveal: detect roles ─────────────────────────────────────
      await revealAllRoles(players);

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const witch = players.find(p => p.skill === 'Witch')!;
      const plain = players.filter(p => p !== wolf && p !== witch);
      const wolfVictim  = plain[0]; // wolf kills this player
      const poisonTarget = plain[1] ?? wolf; // witch poisons this player (use wolf if only 1 plain)

      expect(witch).toBeTruthy();

      // ── 3. Skip round 1 ─────────────────────────────────────────────────
      await skipRound1(creator.page, players, players.filter(p => p.role === 'Werewolf'));

      // ── 4. WerewolvesTurn ───────────────────────────────────────────────
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(wolf.page, 'Werewolves');
      await selectDropdownOption(wolf.page, '.wolf-vote', wolfVictim.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // WerewolvesCloseEyes auto-advances (~6s) to WitchTurn

      // ── 5. WitchTurn: witch poisons a different player ───────────────────
      await waitForPhase(witch.page, 'The Witch', 20_000);
      await expect(witch.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      // Select poison target and click Poison
      await selectDropdownOption(witch.page, '.action-row', poisonTarget.name);
      await witch.page.getByRole('button', { name: '☠️ Poison' }).click();

      // ── 6. WitchCloseEyes (6s) → DayAnnouncement → NightEliminationReveal ──────
      await skipDayAnnouncementAndWaitForVictims(creator.page, allPages);
      await expect(creator.page.getByText(poisonTarget.name)).toBeVisible({ timeout: 5_000 });
      await expect(creator.page.getByText(wolfVictim.name)).toBeVisible({ timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );
});

import { test, expect } from '@playwright/test';
import {
  createAndStartGame, revealAllRoles,
  waitForPhase, selectDropdownOption, skipDayAnnouncementAndWaitForVictims,
  tieVoteAndEndDiscussion,
} from './helpers';

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Seer skill', () => {
  test(
    'Seer can inspect a player and see whether they are a Werewolf',
    { tag: '@seer' },
    async ({ browser }) => {
      // ── 1. Create the game ──────────────────────────────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob'], ['Seer'],
      );
      const allPages = players.map(p => p.page);

      // ── 4. RoleReveal: peek each card and detect roles ─────────────────
      await revealAllRoles(players);

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const seer  = players.find(p => p.skill === 'Seer')!;
      const other = players.find(p => p !== wolf && p !== seer)!;

      expect(wolf).toBeTruthy();
      expect(seer).toBeTruthy();

      // ── 5. Round 1: skip NightAnnouncement + wolves click Ready + skip DayAnnouncement ─
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      await wolf.page.getByRole('button', { name: 'Ready' }).click();
      await waitForPhase(creator.page, 'The Night Has Ended');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 6. Discussion (day 1): tie vote → tiebreak → no elimination ──────
      await waitForPhase(creator.page, 'Discussion');
      await tieVoteAndEndDiscussion(players);

      // ── 7. DayEliminationReveal: no one eliminated (tied votes) ─────────
      await waitForPhase(creator.page, 'Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 8. Round 2: skip NightAnnouncement, wolf votes for the plain villager ──
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      // Vote for the player who is neither wolf nor seer, to keep the Seer alive
      await waitForPhase(wolf.page, 'Werewolves');
      await selectDropdownOption(wolf.page, '.wolf-vote', other.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // WerewolvesCloseEyes (6s) auto-advances to SeerTurn

      // ── 9. SeerTurn: Seer inspects the wolf ────────────────────────────
      await waitForPhase(seer.page, 'The Seer', 20_000);
      await expect(seer.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(seer.page, '.skill-action', wolf.name);
      await seer.page.getByRole('button', { name: 'Reveal' }).click();

      // ── 10. Verify the Seer result shows "Werewolf" ────────────────────
      await expect(seer.page.locator('.seer-result .seer-verdict')).toBeVisible({ timeout: 10_000 });
      await expect(seer.page.locator('.seer-result .seer-verdict')).toContainText('Werewolf');

      // ── 11. SeerCloseEyes (6s) → DayAnnouncement → NightEliminationReveal ─────
      await seer.page.getByRole('button', { name: 'Done' }).click();
      await skipDayAnnouncementAndWaitForVictims(creator.page, allPages);
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
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob'], ['Seer'],
      );

      // ── 3. RoleReveal ────────────────────────────────────────────────────
      await revealAllRoles(players);

      const wolf  = players.find(p => p.role === 'Werewolf')!;
      const seer  = players.find(p => p.skill === 'Seer')!;
      const other = players.find(p => p !== wolf && p !== seer)!;

      // ── 4. Round 1: skip NightAnnouncement + wolves click Ready + skip DayAnnouncement ─
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      await wolf.page.getByRole('button', { name: 'Ready' }).click();
      await waitForPhase(creator.page, 'The Night Has Ended');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      for (const { page } of players) await waitForPhase(page, 'Discussion');
      await tieVoteAndEndDiscussion(players);

      for (const { page } of players) await waitForPhase(page, 'Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 5. Round 2: skip NightAnnouncement, wolf votes ──────────────────
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(wolf.page, 'Werewolves');
      await selectDropdownOption(wolf.page, '.wolf-vote', other.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // WerewolvesCloseEyes (6s) auto-advances to SeerTurn

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

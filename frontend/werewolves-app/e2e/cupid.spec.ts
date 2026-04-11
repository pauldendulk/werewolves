import { test, expect } from '@playwright/test';
import {
  newPlayer, joinGame, createAndStartGame, revealAllRoles, peekAndAccept,
  waitForPhase, selectDropdownOption, dayVote, endDiscussion, tieVoteAndEndDiscussion,
} from './helpers';

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Cupid skill', () => {
  test(
    'Cupid links two players as lovers and they see each other in LoverReveal',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only ──────────────────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob', 'Carol'], ['Cupid'],
      );

      // ── 4. RoleReveal: detect who has Cupid ──────────────────────────────
      await revealAllRoles(players);

      const cupid  = players.find(p => p.skill === 'Cupid')!;
      expect(cupid).toBeTruthy();

      // Pick two players other than Cupid as the lovers (use the first two)
      const non_cupid = players.filter(p => p !== cupid);
      const lover1 = non_cupid[0];
      const lover2 = non_cupid[1];

      // ── 5. Skip NightAnnouncement + wolves click Ready so Cupid phase starts ─
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      for (const w of players.filter(p => p.role === 'Werewolf'))
        await w.page.getByRole('button', { name: 'Ready' }).click();

      // ── 6. CupidTurn: Cupid links lover1 and lover2 ─────────────────────
      for (const { page } of players) await waitForPhase(page, 'Cupid');

      // Only Cupid sees the action UI; others see "Cupid is choosing..."
      await expect(cupid.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(cupid.page, '.action-row:has-text("Lover 1")', lover1.name);
      await selectDropdownOption(cupid.page, '.action-row:has-text("Lover 2")', lover2.name);
      await cupid.page.getByRole('button', { name: 'Link lovers' }).click();

      // ── 7. LoverReveal: each lover reveals the card and sees their partner ─
      for (const { page } of players) await waitForPhase(page, 'Lovers', 20_000);

      // Lovers press-hold the card to reveal their partner's name
      await lover1.page.locator('.role-card').dispatchEvent('mousedown');
      await expect(lover1.page.locator('.lover-name')).toContainText(lover2.name, { timeout: 10_000 });
      await lover1.page.locator('.role-card').dispatchEvent('mouseup');

      await lover2.page.locator('.role-card').dispatchEvent('mousedown');
      await expect(lover2.page.locator('.lover-name')).toContainText(lover1.name, { timeout: 10_000 });
      await lover2.page.locator('.role-card').dispatchEvent('mouseup');

      // ── 8. Moderator skips LoversReveal → Discussion → tie vote → no elimination ─
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      for (const { page } of players) await waitForPhase(page, 'Discussion');
      await tieVoteAndEndDiscussion(players);
      for (const { page } of players) await waitForPhase(page, 'Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 9. Round 2 Night begins ──────────────────────────────────────────
      await waitForPhase(creator.page, 'The Night Has Fallen');

      // ── Cleanup ─────────────────────────────────────────────────────────
      for (const { context } of players) await context.close();
    },
  );

  test(
    'Cupid skip: when Cupid does not act, LoverReveal is skipped',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only ──────────────────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob'], ['Cupid'],
      );
      for (const player of players) {
        const info = await peekAndAccept(player.page);
        player.role  = info.role;
        player.skill = info.skill;
      }

      // ── 1b. Skip NightAnnouncement + wolves click Ready so Cupid phase starts ─
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      for (const w of players.filter(p => p.role === 'Werewolf'))
        await w.page.getByRole('button', { name: 'Ready' }).click();

      // ── 2. CupidTurn: creator skips without linking anyone ───────────────
      for (const { page } of players) await waitForPhase(page, 'Cupid');
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 3. LoverReveal should be skipped – goes straight to Night
      for (const { page } of players) await waitForPhase(page, 'Night');

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Wolves kill a lover → their partner cascade-dies at night',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only (3 players) ──────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob'], ['Cupid'],
      );
      const allPages = players.map(p => p.page);

      // ── 2. RoleReveal: detect roles ──────────────────────────────────────
      await revealAllRoles(players);

      const wolf          = players.find(p => p.role === 'Werewolf')!;
      const cupid         = players.find(p => p.skill === 'Cupid')!;
      const plainVillager = players.find(p => p !== wolf && p !== cupid)!;

      // ── 3b. Skip NightAnnouncement + WerewolvesMeeting so Cupid phase starts ─
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      await wolf.page.getByRole('button', { name: 'Ready' }).click();

      // ── 3. CupidTurn: Cupid links wolf + plain villager as lovers ───────
      // (WerewolvesCloseEyes auto-advances to Cupid; wait up to 20s)
      for (const p of allPages) await waitForPhase(p, 'Cupid', 20_000);
      await expect(cupid.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(cupid.page, '.action-row:has-text("Lover 1")', wolf.name);
      await selectDropdownOption(cupid.page, '.action-row:has-text("Lover 2")', plainVillager.name);
      await cupid.page.getByRole('button', { name: 'Link lovers' }).click();

      // ── 4. LoverReveal: skip (CupidCloseEyes auto-advances → DayAnnouncement → LoversReveal) ─
      for (const p of allPages) await waitForPhase(p, 'Lovers', 25_000);
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 5. Round 1 day: tie vote → tiebreak → no elimination ───────
      for (const p of allPages) await waitForPhase(p, 'Discussion');
      await tieVoteAndEndDiscussion(players);

      for (const p of allPages) await waitForPhase(p, 'Verdict');
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 6. Round 2: skip NightAnnouncement, wolf kills their lover ────────
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(wolf.page, 'Werewolves');
      await selectDropdownOption(wolf.page, '.wolf-vote', plainVillager.name);
      await wolf.page.getByRole('button', { name: 'Confirm kill' }).click();
      // WerewolvesCloseEyes auto-advances → DayAnnouncement → skip it
      await waitForPhase(creator.page, 'The Night Has Ended', 20_000);
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 7. Victims: BOTH lovers appear as killed ────────────────────────────
      // plain-villager dies from wolf kill; wolf cascade-dies as the other lover
      for (const p of allPages) await waitForPhase(p, 'Victims', 20_000);
      await expect(creator.page.locator('.elimination-text')).toHaveCount(2, { timeout: 10_000 });
      await expect(creator.page.locator('.elimination-text').filter({ hasText: plainVillager.name })).toBeVisible();
      await expect(creator.page.locator('.elimination-text').filter({ hasText: wolf.name })).toBeVisible();

      // ── 8. Continue → Game Over: only Cupid survives → Villagers win ─────
      await creator.page.getByRole('button', { name: 'Continue' }).click();
      await waitForPhase(creator.page, 'Final Scores Reveal', 20_000);
      await expect(creator.page.locator('.winner-text')).toContainText('Villagers', { timeout: 5_000 });

      for (const { context } of players) await context.close();
    },
  );

  test(
    'Day vote on a lover cascade-kills their partner',
    { tag: '@cupid' },
    async ({ browser }) => {
      // ── 1. Create game with Cupid only (3 players) ──────────────────────
      const { creator, players } = await createAndStartGame(
        browser, ['Alice', 'Bob'], ['Cupid'],
      );
      const allPages = players.map(p => p.page);

      // ── 2. RoleReveal: detect roles ──────────────────────────────────────
      await revealAllRoles(players);

      const wolf          = players.find(p => p.role === 'Werewolf')!;
      const cupid         = players.find(p => p.skill === 'Cupid')!;
      const plainVillager = players.find(p => p !== wolf && p !== cupid)!;

      // ── 3b. Skip NightAnnouncement + WerewolvesMeeting so Cupid phase starts ─
      await waitForPhase(creator.page, 'The Night Has Fallen');
      await creator.page.getByRole('button', { name: 'Skip' }).click();
      await waitForPhase(creator.page, 'Werewolves Meeting');
      await wolf.page.getByRole('button', { name: 'Ready' }).click();

      // ── 3. CupidTurn: Cupid links wolf + plain villager as lovers ───
      for (const p of allPages) await waitForPhase(p, 'Cupid', 20_000);
      await expect(cupid.page.locator('.skill-action')).toBeVisible({ timeout: 10_000 });
      await selectDropdownOption(cupid.page, '.action-row:has-text("Lover 1")', wolf.name);
      await selectDropdownOption(cupid.page, '.action-row:has-text("Lover 2")', plainVillager.name);
      await cupid.page.getByRole('button', { name: 'Link lovers' }).click();

      // ── 4. LoverReveal: skip (CupidCloseEyes auto → DayAnnouncement → LoversReveal) ─
      for (const p of allPages) await waitForPhase(p, 'Lovers', 25_000);
      await creator.page.getByRole('button', { name: 'Skip' }).click();

      // ── 6. Discussion: wolf + cupid both vote for the plain villager ─────
      for (const p of allPages) await waitForPhase(p, 'Discussion');
      for (const voter of [wolf, cupid]) {
        await dayVote(voter.page, plainVillager.name);
      }
      await dayVote(plainVillager.page, wolf.name);
      await endDiscussion(players);

      // ── 7. Village Verdict: plain villager is eliminated by day vote ─────
      for (const p of allPages) await waitForPhase(p, 'Verdict');
      await expect(creator.page.locator('.elimination-text')).toContainText(plainVillager.name, { timeout: 10_000 });
      await creator.page.getByRole('button', { name: 'Continue' }).click();

      // ── 8. Game Over: wolf cascade-died as lover → only Cupid survives ───
      await waitForPhase(creator.page, 'Final Scores Reveal', 20_000);
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

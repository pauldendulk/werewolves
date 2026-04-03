import { test, expect, Browser, BrowserContext, Page } from '@playwright/test';

const AUDIO_KEYS = [
  'role-reveal',
  'werewolves-meeting',
  'werewolves-turn',
  'cupid-turn',
  'lover-reveal',
  'seer-turn',
  'witch-turn',
  'hunter-turn',
  'night-end-no-deaths',
  'night-end-one-death',
  'night-end-many-deaths',
  'discussion',
  'tiebreak-discussion',
  'day-elimination-tie',
  'day-elimination',
  'game-over-villagers',
  'game-over-werewolves',
  'wolves-close-eyes',
];

test.describe('Audio assets', () => {
  test('all narration MP3s return 200', async ({ page }) => {
    await page.goto('/');

    const failed: string[] = [];

    for (const key of AUDIO_KEYS) {
      const url = `/assets/audio/en-US/${key}.mp3`;
      const response = await page.request.get(url);
      if (!response.ok()) {
        failed.push(`${key}.mp3 → ${response.status()}`);
      }
    }

    expect(
      failed,
      `Missing audio files:\n${failed.join('\n')}`
    ).toHaveLength(0);
  });

  test('no 404 errors in browser console during game start', async ({ browser }) => {
    const context: BrowserContext = await browser.newContext();
    const page: Page = await context.newPage();

    const failures: string[] = [];

    page.on('response', response => {
      if (!response.ok() && response.url().includes('/assets/audio/')) {
        failures.push(`${response.status()} ${response.url()}`);
      }
    });

    // Navigate and start a game to trigger the first audio play
    await page.goto('/');
    await page.getByLabel('Your Name').fill('AudioTester');
    await page.getByRole('button', { name: 'Organize Game' }).click();
    await expect(page).toHaveURL(/\/game\/.*\/lobby/);

    // Add enough players via a second context to meet the minimum
    const gameId = page.url().match(/\/game\/([^/]+)\/lobby/)?.[1] ?? '';
    const players: { context: BrowserContext; page: Page }[] = [];
    for (let i = 1; i <= 2; i++) {
      const ctx = await browser.newContext();
      const p = await ctx.newPage();
      await p.goto(`/game/${gameId}`);
      await p.getByLabel('Your Name').fill(`Player${i}`);
      await p.getByRole('button', { name: 'Join Game' }).click();
      players.push({ context: ctx, page: p });
    }

    // Start the game — this unlocks audio and triggers role-reveal.mp3
    await page.getByRole('button', { name: 'Start Game' }).click();
    await expect(page).toHaveURL(/\/game\/.*\/session/);

    // Wait briefly for audio requests to complete
    await page.waitForTimeout(2000);

    for (const { context: ctx } of players) await ctx.close();
    await context.close();

    expect(
      failures,
      `Audio 404s during game:\n${failures.join('\n')}`
    ).toHaveLength(0);
  });
});

import { test } from '@playwright/test';
import path from 'path';

const GAME_ID = 'DEMO';
const HOST = 'p0';

// Test at multiple viewport widths
const viewports = [320, 375, 390, 428];

for (const vpw of viewports) {
  test(`lobby-warning-debug-${vpw}px`, async ({ page }) => {
    await page.setViewportSize({ width: vpw, height: 844 });

    await page.route('**/api/**', route => {
      route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    // 3 players, 4 special roles + 2 werewolves = 6 needed → warning!
    const lobbyState = {
      game: {
        gameId: GAME_ID, creatorId: HOST,
        minPlayers: 3, maxPlayers: 10,
        joinLink: `http://localhost:4200/game/${GAME_ID}`,
        qrCodeBase64: 'iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAADklEQVQI12NgYGD4DwABBAEAWB/aHQAAAABJRU5ErkJggg==',
        status: 'WaitingForPlayers',
        version: 1, discussionDurationMinutes: 3, numberOfWerewolves: 2,
        enabledSkills: ['Seer', 'Cupid', 'Witch', 'Hunter'],
        phaseStartedAt: null, phase: '', roundNumber: 0, phaseEndsAt: null,
        audioPlayAt: null, nightDeaths: [], dayDeaths: [], winner: null,
        tiebreakCandidates: [], gameIndex: 1, isPremium: false, tournamentCode: null,
      },
      players: [
        { playerId: HOST, displayName: 'Host', isCreator: true, isModerator: false, isConnected: true, participationStatus: 'Participating', role: null, skill: null, isEliminated: false, isDone: false, score: 0, totalScore: 0, joinedAt: '2026-03-17T10:00:00Z' },
        { playerId: 'p1', displayName: 'Alice', isCreator: false, isModerator: false, isConnected: true, participationStatus: 'Participating', role: null, skill: null, isEliminated: false, isDone: false, score: 0, totalScore: 0, joinedAt: '2026-03-17T10:01:00Z' },
        { playerId: 'p2', displayName: 'Bob', isCreator: false, isModerator: false, isConnected: true, participationStatus: 'Participating', role: null, skill: null, isEliminated: false, isDone: false, score: 0, totalScore: 0, joinedAt: '2026-03-17T10:02:00Z' },
      ],
      hasDuplicateNames: false,
    };

    await page.route(`**/api/game/${GAME_ID}*`, route => {
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(lobbyState) });
    });

    await page.addInitScript(id => localStorage.setItem('playerId', id), HOST);
    await page.goto('http://localhost:4200/game/DEMO/lobby');
    await page.waitForSelector('.lobby-container', { timeout: 10000 });

    // Remove overflow-x: hidden to see real overflow
    await page.evaluate(() => {
      (document.body as HTMLElement).style.overflowX = 'auto';
      (document.documentElement as HTMLElement).style.overflowX = 'auto';
    });

    const widths = await page.evaluate(() => {
      return {
        vw: window.innerWidth,
        doc: document.documentElement.scrollWidth,
        playersCard: (document.querySelector('.players-card') as HTMLElement)?.offsetWidth,
        settingsCard: (document.querySelector('.settings-card') as HTMLElement)?.offsetWidth,
        warningBox: (document.querySelector('.warning-box') as HTMLElement)?.offsetWidth,
        overflow: document.documentElement.scrollWidth > window.innerWidth,
      };
    });
    console.log(`${vpw}px widths:`, JSON.stringify(widths));

    await page.screenshot({ path: `C:/code/github/werewolves/.playwright-mcp/lobby-warning-${vpw}.png`, fullPage: true });
  });
}

/**
 * Screenshot suite — 22 screenshots covering every screen and role-variant.
 *
 * Strategy: all API calls are intercepted with mock JSON so the Angular app
 * renders realistic state without a live backend or a real running game.
 *
 * Screenshots are written to  docs-src/docs/screenshots/  so MkDocs picks
 * them up automatically for the game-concept documentation.
 *
 * Run:
 *   cd frontend/werewolves-app
 *   npx playwright test e2e/screenshots.spec.ts --project=chromium
 *
 * The Angular dev server must be running on http://localhost:4200.
 */

import { test, type Page } from '@playwright/test';
import path from 'path';
import fs from 'fs';

// ─── Output directory ─────────────────────────────────────────────────────────
// __dirname = …/frontend/werewolves-app/e2e  →  ../../.. = repo root
const SHOTS_DIR = path.join(__dirname, '../../../docs-src/docs/screenshots');

// ─── Constants ────────────────────────────────────────────────────────────────
const GAME_ID = 'DEMO';
const VIEWPORT = { width: 390, height: 844 }; // iPhone 14 size

// Player IDs — p0 is always the host/creator, never the viewer
const HOST  = 'p0';
const ALICE = 'p1';  // Villager
const BOB   = 'p2';  // Werewolf
const CAROL = 'p3';  // Seer
const DAVE  = 'p4';  // Cupid
const EVE   = 'p5';  // Witch
const FRANK = 'p6';  // Hunter
const GRACE = 'p7';  // Werewolf 2

// ─── Mock-data builders ───────────────────────────────────────────────────────

interface PlayerOverride {
  role?: string | null;
  skill?: string | null;
  isEliminated?: boolean;
  isDone?: boolean;
  score?: number;
  totalScore?: number;
}

function makePlayer(
  id: string,
  name: string,
  isCreator: boolean,
  overrides: PlayerOverride = {}
) {
  return {
    playerId: id,
    displayName: name,
    isCreator,
    isModerator: false,
    isConnected: true,
    participationStatus: 'Participating',
    role: overrides.role ?? null,
    skill: overrides.skill ?? null,
    isEliminated: overrides.isEliminated ?? false,
    isDone: overrides.isDone ?? false,
    score: overrides.score ?? 0,
    totalScore: overrides.totalScore ?? 0,
    joinedAt: '2026-03-17T10:00:00Z',
  };
}

// Standard 8-player roster used in all session screenshots
const BASE_PLAYERS = [
  makePlayer(HOST,  'Host',  true),
  makePlayer(ALICE, 'Alice', false),
  makePlayer(BOB,   'Bob',   false),
  makePlayer(CAROL, 'Carol', false),
  makePlayer(DAVE,  'Dave',  false),
  makePlayer(EVE,   'Eve',   false),
  makePlayer(FRANK, 'Frank', false),
  makePlayer(GRACE, 'Grace', false),
];

// Small base64 QR placeholder (5×5 white PNG) so the <img> renders
const QR_PLACEHOLDER =
  'iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAADklEQVQI12NgYGD4DwABBAEAWB/aHQAAAABJRU5ErkJggg==';

function makeGameState(
  phase: string,
  round: number,
  extra: Record<string, unknown> = {},
  players = BASE_PLAYERS
) {
  return {
    game: {
      gameId: GAME_ID,
      creatorId: HOST,
      minPlayers: 5,
      maxPlayers: 10,
      joinLink: `http://localhost:4200/game/${GAME_ID}`,
      qrCodeBase64: QR_PLACEHOLDER,
      status: 'InGame',
      version: 1,
      discussionDurationMinutes: 3,
      tiebreakDiscussionDurationSeconds: 60,
      numberOfWerewolves: 2,
      enabledSkills: ['Seer', 'Cupid', 'Witch', 'Hunter'],
      phaseStartedAt: '2026-03-17T10:00:00Z',
      phase,
      roundNumber: round,
      phaseEndsAt: null,
      audioPlayAt: null,
      nightDeaths: [],
      dayDeaths: [],
      winner: null,
      tiebreakCandidates: [],
      gameIndex: 1,
      isPremium: false,
      ...extra,
    },
    players,
    hasDuplicateNames: false,
  };
}

function makeLobbyState(players = BASE_PLAYERS) {
  return {
    game: {
      gameId: GAME_ID,
      creatorId: HOST,
      minPlayers: 5,
      maxPlayers: 10,
      joinLink: `http://localhost:4200/game/${GAME_ID}`,
      qrCodeBase64: QR_PLACEHOLDER,
      status: 'WaitingForPlayers',
      version: 1,
      discussionDurationMinutes: 3,
      tiebreakDiscussionDurationSeconds: 60,
      numberOfWerewolves: 2,
      enabledSkills: ['Seer', 'Cupid', 'Witch', 'Hunter'],
      phaseStartedAt: null,
      phase: '',
      roundNumber: 0,
      phaseEndsAt: null,
      audioPlayAt: null,
      nightDeaths: [],
      dayDeaths: [],
      winner: null,
      tiebreakCandidates: [],
      gameIndex: 1,
      isPremium: false,
    },
    players,
    hasDuplicateNames: false,
  };
}

function makeRoleDto(
  role: string,
  skill: string | null = null,
  opts: {
    fellowWerewolves?: string[];
    loverName?: string | null;
    nightKillTargetName?: string | null;
    witchHealUsed?: boolean;
    witchPoisonUsed?: boolean;
  } = {}
) {
  return {
    role,
    skill,
    fellowWerewolves: opts.fellowWerewolves ?? [],
    loverName: opts.loverName ?? null,
    nightKillTargetName: opts.nightKillTargetName ?? null,
    witchHealUsed: opts.witchHealUsed ?? false,
    witchPoisonUsed: opts.witchPoisonUsed ?? false,
  };
}

// ─── Playwright helpers ───────────────────────────────────────────────────────

/**
 * Mock the two GET endpoints the session/lobby components rely on.
 *
 * Trailing * on the game-state pattern stops it matching /role (no / in *).
 * The role pattern is registered last so it wins if both somehow matched.
 * All other API requests (POST actions, clock-sync) return 200 {}.
 */
async function setupMocks(
  page: Page,
  gameState: unknown,
  roleDto: unknown
): Promise<void> {
  // Catch-all fallback: silence any API calls we don't explicitly mock
  await page.route('**/api/**', route => {
    route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });

  // Game state polling — trailing * matches ?version=N but NOT /role (no /)
  await page.route(`**/api/game/${GAME_ID}*`, route => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(gameState),
    });
  });

  // Role endpoint — registered last, wins for /role URLs
  await page.route(`**/api/game/${GAME_ID}/role*`, route => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(roleDto),
    });
  });
}

async function setViewer(page: Page, playerId: string): Promise<void> {
  await page.addInitScript(id => localStorage.setItem('playerId', id), playerId);
}

async function clearViewer(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.removeItem('playerId'));
}

async function shot(page: Page, name: string): Promise<void> {
  await page.screenshot({ path: path.join(SHOTS_DIR, `${name}.png`) });
}

// ─── Test suite ───────────────────────────────────────────────────────────────

test.use({ viewport: VIEWPORT });

test.beforeAll(() => {
  fs.mkdirSync(SHOTS_DIR, { recursive: true });
});

// ── 01 · Create Game ──────────────────────────────────────────────────────────
test('01-create-game', async ({ page }) => {
  await clearViewer(page);
  await page.goto('/');
  await page.waitForSelector('.create-game-container');
  await shot(page, '01-create-game');
});

// ── 02 · Join Game ────────────────────────────────────────────────────────────
test('02-join-game', async ({ page }) => {
  // Clear any stored player ID so the component shows the join form
  await clearViewer(page);
  const state = makeLobbyState();
  await setupMocks(page, state, {});
  await page.goto(`/game/${GAME_ID}`);
  await page.waitForSelector('h2:has-text("Join Game")');
  await shot(page, '02-join-game');
});

// ── 03 · Lobby (non-host view) ────────────────────────────────────────────────
test('03-lobby', async ({ page }) => {
  const state = makeLobbyState();
  await setupMocks(page, state, {});
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/lobby`);
  await page.waitForSelector('.lobby-container');
  await shot(page, '03-lobby');
});

// ── 04 · Role Reveal — card hidden ───────────────────────────────────────────
test('04-role-reveal', async ({ page }) => {
  const state = makeGameState('RoleReveal', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Your Role")');
  await shot(page, '04-role-reveal');
});

// ── 05 · Werewolves Meeting — Villager (eyes-closed / waiting) ───────────────
test('05-night-werewolves-meeting-villager', async ({ page }) => {
  const state = makeGameState('WerewolvesMeeting', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Night")');
  await shot(page, '05-night-werewolves-meeting-villager');
});

// ── 06 · Werewolves Meeting — Werewolf (sees fellow wolves + Ready button) ───
test('06-night-werewolves-meeting-werewolf', async ({ page }) => {
  const state = makeGameState('WerewolvesMeeting', 1);
  const role = makeRoleDto('Werewolf', null, { fellowWerewolves: ['Grace'] });
  await setupMocks(page, state, role);
  await setViewer(page, BOB);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Night")');
  await shot(page, '06-night-werewolves-meeting-werewolf');
});

// ── 07 · Werewolves Turn — Villager (passive / eyes closed) ──────────────────
test('07-night-werewolves-turn-villager', async ({ page }) => {
  const state = makeGameState('WerewolvesTurn', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Night")');
  await shot(page, '07-night-werewolves-turn-villager');
});

// ── 08 · Werewolves Turn — Werewolf (victim selector + Confirm Kill) ─────────
test('08-night-werewolves-turn-werewolf', async ({ page }) => {
  const state = makeGameState('WerewolvesTurn', 2);
  const role = makeRoleDto('Werewolf', null, { fellowWerewolves: ['Grace'] });
  await setupMocks(page, state, role);
  await setViewer(page, BOB);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.wolf-vote');
  await shot(page, '08-night-werewolves-turn-werewolf');
});

// ── 09 · Cupid Turn — non-Cupid (waiting text) ───────────────────────────────
test('09-night-cupid-turn-non-cupid', async ({ page }) => {
  const state = makeGameState('CupidTurn', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Cupid")');
  await shot(page, '09-night-cupid-turn-non-cupid');
});

// ── 10 · Cupid Turn — Cupid (two selectors + Link Lovers) ────────────────────
test('10-night-cupid-turn-cupid', async ({ page }) => {
  const state = makeGameState('CupidTurn', 1);
  const role = makeRoleDto('Villager', 'Cupid');
  await setupMocks(page, state, role);
  await setViewer(page, DAVE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.skill-action');
  await shot(page, '10-night-cupid-turn-cupid');
});

// ── 11 · Lover Reveal — card revealed with lover name ────────────────────────
test('11-lover-reveal', async ({ page }) => {
  const state = makeGameState('LoverReveal', 1);
  const role = makeRoleDto('Villager', null, { loverName: 'Dave' });
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Lovers")');
  // Press and hold the role card to reveal it
  const card = page.locator('.role-card');
  await card.dispatchEvent('mousedown');
  await page.waitForSelector('.lover-name');
  await shot(page, '11-lover-reveal');
  await card.dispatchEvent('mouseup');
});

// ── 12 · Seer Turn — non-Seer (waiting text) ─────────────────────────────────
test('12-night-seer-turn-non-seer', async ({ page }) => {
  const state = makeGameState('SeerTurn', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Seer")');
  await shot(page, '12-night-seer-turn-non-seer');
});

// ── 13 · Seer Turn — Seer (player selector + Reveal button) ──────────────────
test('13-night-seer-turn-seer', async ({ page }) => {
  const state = makeGameState('SeerTurn', 2);
  const role = makeRoleDto('Villager', 'Seer');
  await setupMocks(page, state, role);
  await setViewer(page, CAROL);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.skill-action');
  await shot(page, '13-night-seer-turn-seer');
});

// ── 14 · Witch Turn — non-Witch (waiting text) ───────────────────────────────
test('14-night-witch-turn-non-witch', async ({ page }) => {
  const state = makeGameState('WitchTurn', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Witch")');
  await shot(page, '14-night-witch-turn-non-witch');
});

// ── 15 · Witch Turn — Witch (victim shown + both potions available) ───────────
test('15-night-witch-turn-witch', async ({ page }) => {
  const state = makeGameState('WitchTurn', 2);
  const role = makeRoleDto('Villager', 'Witch', { nightKillTargetName: 'Alice' });
  await setupMocks(page, state, role);
  await setViewer(page, EVE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.witch-buttons');
  await shot(page, '15-night-witch-turn-witch');
});

// ── 16 · Hunter Turn — non-Hunter (waiting text) ─────────────────────────────
test('16-night-hunter-turn-non-hunter', async ({ page }) => {
  const state = makeGameState('HunterTurn', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Hunter")');
  await shot(page, '16-night-hunter-turn-non-hunter');
});

// ── 17 · Hunter Turn — Hunter (player selector + Shoot button) ───────────────
test('17-night-hunter-turn-hunter', async ({ page }) => {
  const state = makeGameState('HunterTurn', 2);
  const role = makeRoleDto('Villager', 'Hunter');
  await setupMocks(page, state, role);
  await setViewer(page, FRANK);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.skill-action');
  await shot(page, '17-night-hunter-turn-hunter');
});

// ── 18 · Dawn — someone was taken in the night ───────────────────────────────
test('18-dawn-night-elimination', async ({ page }) => {
  const state = makeGameState('NightEliminationReveal', 2, {
    nightDeaths: [{ playerId: ALICE, playerName: 'Alice', cause: 'WerewolfKill' }],
  });
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, BOB);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Dawn")');
  await shot(page, '18-dawn-night-elimination');
});

// ── 19 · Discussion — alive player with vote selector ────────────────────────
test('19-discussion', async ({ page }) => {
  const state = makeGameState('Discussion', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.vote-section');
  await shot(page, '19-discussion');
});

// ── 19b · Discussion — eliminated player (sees notice + vote controls) ───────
test('19b-discussion-eliminated', async ({ page }) => {
  const players = BASE_PLAYERS.map(p =>
    p.playerId === ALICE ? { ...p, isEliminated: true } : p
  );
  const state = makeGameState('Discussion', 2, {}, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.vote-section');
  await shot(page, '19b-discussion-eliminated');
});

// ── 20 · Tiebreak Discussion ─────────────────────────────────────────────────
test('20-tiebreak-discussion', async ({ page }) => {
  const state = makeGameState('TiebreakDiscussion', 2, {
    tiebreakCandidates: [BOB, CAROL],
  });
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Tiebreak Vote")');
  await shot(page, '20-tiebreak-discussion');
});

// ── 21 · Day Elimination — player eliminated by village ──────────────────────
test('21-day-elimination', async ({ page }) => {
  // Pass role in players so getEliminatedRole() shows it
  const players = BASE_PLAYERS.map(p =>
    p.playerId === BOB ? { ...p, role: 'Werewolf', isEliminated: true } : p
  );
  const state = makeGameState(
    'DayEliminationReveal',
    2,
    { dayDeaths: [{ playerId: BOB, playerName: 'Bob', cause: 'DayVote' }] },
    players
  );
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Village Verdict")');
  await shot(page, '21-day-elimination');
});

// ── 22 · Game Over — Villagers win ───────────────────────────────────────────
test('22-game-over', async ({ page }) => {
  const players = [
    makePlayer(HOST,  'Host',  true,  { role: 'Villager', isEliminated: false, score: 11 }),
    makePlayer(ALICE, 'Alice', false, { role: 'Villager', isEliminated: false, score: 9 }),
    makePlayer(BOB,   'Bob',   false, { role: 'Werewolf', isEliminated: true,  score: 0 }),
    makePlayer(CAROL, 'Carol', false, { role: 'Villager', skill: 'Seer',   isEliminated: false, score: 10 }),
    makePlayer(DAVE,  'Dave',  false, { role: 'Villager', skill: 'Cupid',  isEliminated: false, score: 8 }),
    makePlayer(EVE,   'Eve',   false, { role: 'Villager', skill: 'Witch',  isEliminated: false, score: 8 }),
    makePlayer(FRANK, 'Frank', false, { role: 'Villager', skill: 'Hunter', isEliminated: false, score: 9 }),
    makePlayer(GRACE, 'Grace', false, { role: 'Werewolf', isEliminated: true,  score: 0 }),
  ];
  const state = makeGameState('GameOver', 3, { winner: 'Villagers' }, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Show Scores")');
  await shot(page, '22-game-over');
});

// ── 23 · Game Over — after game 2, showing running totals ────────────────────
test('23-game-over-game2', async ({ page }) => {
  const players = [
    makePlayer(HOST,  'Host',  true,  { role: 'Villager', isEliminated: false, score: 9,  totalScore: 20 }),
    makePlayer(ALICE, 'Alice', false, { role: 'Villager', isEliminated: false, score: 11, totalScore: 20 }),
    makePlayer(BOB,   'Bob',   false, { role: 'Werewolf', isEliminated: true,  score: 0,  totalScore: 7  }),
    makePlayer(CAROL, 'Carol', false, { role: 'Villager', skill: 'Seer',   isEliminated: false, score: 10, totalScore: 20 }),
    makePlayer(DAVE,  'Dave',  false, { role: 'Villager', skill: 'Cupid',  isEliminated: false, score: 8,  totalScore: 16 }),
    makePlayer(EVE,   'Eve',   false, { role: 'Villager', skill: 'Witch',  isEliminated: false, score: 8,  totalScore: 16 }),
    makePlayer(FRANK, 'Frank', false, { role: 'Villager', skill: 'Hunter', isEliminated: false, score: 10, totalScore: 19 }),
    makePlayer(GRACE, 'Grace', false, { role: 'Werewolf', isEliminated: true,  score: 0,  totalScore: 9  }),
  ];
  const state = makeGameState('GameOver', 2, { winner: 'Villagers', gameIndex: 2 }, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Show Scores")');
  await shot(page, '23-game-over-game2');
});

// ── 24 · Tournament Unlock — pass code dialog ─────────────────────────────────
test('24-tournament-unlock', async ({ page }) => {
  const state = {
    game: {
      gameId: GAME_ID,
      tournamentCode: GAME_ID,
      creatorId: HOST,
      minPlayers: 5,
      maxPlayers: 10,
      joinLink: `http://localhost:4200/game/${GAME_ID}`,
      qrCodeBase64: QR_PLACEHOLDER,
      status: 'ReadyToStart',
      version: 1,
      discussionDurationMinutes: 3,
      tiebreakDiscussionDurationSeconds: 60,
      numberOfWerewolves: 2,
      enabledSkills: ['Seer', 'Cupid', 'Witch', 'Hunter'],
      phaseStartedAt: null,
      phase: '',
      roundNumber: 0,
      phaseEndsAt: null,
      audioPlayAt: null,
      nightDeaths: [],
      dayDeaths: [],
      winner: null,
      tiebreakCandidates: [],
      gameIndex: 2,
      isPremium: false,
    },
    players: BASE_PLAYERS,
    hasDuplicateNames: false,
  };
  await setupMocks(page, state, {});
  await setViewer(page, HOST);
  await page.goto(`/game/${GAME_ID}/lobby`);
  await page.waitForSelector('.lobby-container');
  await page.click('button:has-text("Start Game")');
  await page.waitForSelector('.p-dialog-header:has-text("Tournament Pass")');
  await page.waitForTimeout(400); // allow PrimeNG backdrop animation to settle
  await shot(page, '24-tournament-unlock');
});

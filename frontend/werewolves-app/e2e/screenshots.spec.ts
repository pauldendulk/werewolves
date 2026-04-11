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
  currentVoteTargetId?: string | null;
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
    currentVoteTargetId: overrides.currentVoteTargetId ?? null,
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

// Same roster but Alice (the viewer in most tests) is also moderator — used for moderator panel screenshots
const MODERATOR_PLAYERS = BASE_PLAYERS.map(p =>
  p.playerId === ALICE ? { ...p, isModerator: true } : p
);

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
      isTournamentModeUnlocked: false,
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
      isTournamentModeUnlocked: false,
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
test('create-game', async ({ page }) => {
  await clearViewer(page);
  await page.goto('/');
  await page.waitForSelector('.create-game-container');
  await shot(page, 'create-game');
});

// ── 02 · Join Game ────────────────────────────────────────────────────────────
test('join-game', async ({ page }) => {
  // Clear any stored player ID so the component shows the join form
  await clearViewer(page);
  const state = makeLobbyState();
  await setupMocks(page, state, {});
  await page.goto(`/game/${GAME_ID}`);
  await page.waitForSelector('h2:has-text("Join Game")');
  await shot(page, 'join-game');
});

// ── 03 · Lobby (non-host view) ────────────────────────────────────────────────
test('lobby', async ({ page }) => {
  const state = makeLobbyState();
  await setupMocks(page, state, {});
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/lobby`);
  await page.waitForSelector('.lobby-container');
  await shot(page, 'lobby');
});

// ── 04 · Role Reveal — card hidden ───────────────────────────────────────────
test('role-reveal', async ({ page }) => {
  const state = makeGameState('RoleReveal', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Your Role")');
  await shot(page, 'role-reveal');
});

// ── 05 · Werewolves Meeting — Villager (eyes-closed / waiting) ───────────────
test('werewolves-meeting-others', async ({ page }) => {
  const state = makeGameState('WerewolvesMeeting', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Werewolves")');
  await shot(page, 'werewolves-meeting-others');
});

// ── 06 · Werewolves Meeting — Werewolf (sees fellow wolves + Ready button) ───
test('werewolves-meeting', async ({ page }) => {
  const state = makeGameState('WerewolvesMeeting', 1);
  const role = makeRoleDto('Werewolf', null, { fellowWerewolves: ['Grace'] });
  await setupMocks(page, state, role);
  await setViewer(page, BOB);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Werewolves")');
  await shot(page, 'werewolves-meeting');
});

// ── Werewolves Close Eyes — blank night screen (auto-advance) ───────────────
test('werewolves-close-eyes', async ({ page }) => {
  const state = makeGameState('WerewolvesCloseEyes', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.session-container');
  await shot(page, 'werewolves-close-eyes');
});

// ── 07 · Werewolves Turn — Villager (passive / eyes closed) ──────────────────
test('werewolves-others', async ({ page }) => {
  const state = makeGameState('Werewolves', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Werewolves")');
  await shot(page, 'werewolves-others');
});

// ── 08 · Werewolves Turn — Werewolf (victim selector + Confirm Kill) ─────────
test('werewolves', async ({ page }) => {
  const state = makeGameState('Werewolves', 2);
  const role = makeRoleDto('Werewolf', null, { fellowWerewolves: ['Grace'] });
  await setupMocks(page, state, role);
  await setViewer(page, BOB);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.wolf-vote');
  await shot(page, 'werewolves');
});

// ── 09 · Cupid Turn — non-Cupid (waiting text) ───────────────────────────────
test('cupid-others', async ({ page }) => {
  const state = makeGameState('Cupid', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Cupid")');
  await shot(page, 'cupid-others');
});

// ── 10 · Cupid Turn — Cupid (two selectors + Link Lovers) ────────────────────
test('cupid', async ({ page }) => {
  const state = makeGameState('Cupid', 1);
  const role = makeRoleDto('Villager', 'Cupid');
  await setupMocks(page, state, role);
  await setViewer(page, DAVE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.skill-action');
  await shot(page, 'cupid');
});

// ── Cupid Close Eyes — blank night screen (auto-advance) ────────────────────
test('cupid-close-eyes', async ({ page }) => {
  const state = makeGameState('CupidCloseEyes', 1);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.session-container');
  await shot(page, 'cupid-close-eyes');
});

// ── 11 · Lover Reveal — card revealed with lover name ────────────────────────
test('lovers-reveal', async ({ page }) => {
  const state = makeGameState('LoversReveal', 1, { phaseEndsAt: new Date(Date.now() + 15 * 1000).toISOString() });
  const role = makeRoleDto('Villager', null, { loverName: 'Dave' });
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Lovers")');
  // Press and hold the role card to reveal it
  const card = page.locator('.role-card');
  await card.dispatchEvent('mousedown');
  await page.waitForSelector('.lover-name');
  await shot(page, 'lovers-reveal');
  await card.dispatchEvent('mouseup');
});

// ── 12 · Seer Turn — non-Seer (waiting text) ─────────────────────────────────
test('seer-others', async ({ page }) => {
  const state = makeGameState('Seer', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Seer")');
  await shot(page, 'seer-others');
});

// ── 13 · Seer Turn — Seer (player selector + Reveal button) ──────────────────
test('seer', async ({ page }) => {
  const state = makeGameState('Seer', 2);
  const role = makeRoleDto('Villager', 'Seer');
  await setupMocks(page, state, role);
  await setViewer(page, CAROL);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.skill-action');
  await shot(page, 'seer');
});

// ── Seer Close Eyes — blank night screen (auto-advance) ─────────────────────
test('seer-close-eyes', async ({ page }) => {
  const state = makeGameState('SeerCloseEyes', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.session-container');
  await shot(page, 'seer-close-eyes');
});

// ── 14 · Witch Turn — non-Witch (waiting text) ───────────────────────────────
test('witch-others', async ({ page }) => {
  const state = makeGameState('Witch', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Witch")');
  await shot(page, 'witch-others');
});

// ── 15 · Witch Turn — Witch (victim shown + both potions available) ───────────
test('witch', async ({ page }) => {
  const state = makeGameState('Witch', 2);
  const role = makeRoleDto('Villager', 'Witch', { nightKillTargetName: 'Alice' });
  await setupMocks(page, state, role);
  await setViewer(page, EVE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.witch-buttons');
  await shot(page, 'witch');
});

// ── Witch Close Eyes — blank night screen (auto-advance) ────────────────────
test('witch-close-eyes', async ({ page }) => {
  const state = makeGameState('WitchCloseEyes', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.session-container');
  await shot(page, 'witch-close-eyes');
});

// ── 16 · Hunter Turn — non-Hunter (waiting text) ─────────────────────────────
test('hunter-others', async ({ page }) => {
  const state = makeGameState('Hunter', 2);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Hunter")');
  await shot(page, 'hunter-others');
});

// ── 17 · Hunter Turn — Hunter (player selector + Shoot button) ───────────────
test('hunter', async ({ page }) => {
  const state = makeGameState('Hunter', 2);
  const role = makeRoleDto('Villager', 'Hunter');
  await setupMocks(page, state, role);
  await setViewer(page, FRANK);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.skill-action');
  await shot(page, 'hunter');
});

// ── 18 · Victims — someone was eliminated in the night ───────────────────────────
test('night-elimination-reveal', async ({ page }) => {
  const state = makeGameState('NightEliminationReveal', 2, {
    nightDeaths: [{ playerId: ALICE, playerName: 'Alice', cause: 'WerewolfKill' }],
    phaseEndsAt: new Date(Date.now() + 7 * 1000).toISOString(),
  });
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, BOB);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Victims")');
  await shot(page, 'night-elimination-reveal');
});

// ── 19 · Discussion — alive player with vote selector ────────────────────────
test('discussion', async ({ page }) => {
  const players = BASE_PLAYERS.map(p => {
    if (p.playerId === BOB)   return { ...p, currentVoteTargetId: DAVE };
    if (p.playerId === CAROL) return { ...p, currentVoteTargetId: BOB };
    if (p.playerId === EVE)   return { ...p, currentVoteTargetId: BOB };
    return p;
  });
  const state = makeGameState('Discussion', 2, { phaseEndsAt: new Date(Date.now() + 154 * 1000).toISOString() }, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.vote-section');
  await shot(page, 'discussion');
});

// ── 19b · Discussion — eliminated player (sees notice + vote controls) ───────
test('discussion-eliminated', async ({ page }) => {
  const players = BASE_PLAYERS.map(p =>
    p.playerId === ALICE ? { ...p, isEliminated: true } : p
  );
  const state = makeGameState('Discussion', 2, { phaseEndsAt: new Date(Date.now() + 154 * 1000).toISOString() }, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.vote-section');
  await shot(page, 'discussion-eliminated');
});

// ── 20 · Tiebreak Discussion ─────────────────────────────────────────────────
test('tiebreak-discussion', async ({ page }) => {
  const state = makeGameState('TiebreakDiscussion', 2, {
    tiebreakCandidates: [BOB, CAROL],
    phaseEndsAt: new Date(Date.now() + 42 * 1000).toISOString(),
  });
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Tiebreak Vote")');
  await shot(page, 'tiebreak-discussion');
});

// ── 21 · Day Elimination — player eliminated by village ──────────────────────
test('day-elimination-reveal', async ({ page }) => {
  // Pass role in players so getEliminatedRole() shows it
  const players = BASE_PLAYERS.map(p =>
    p.playerId === BOB ? { ...p, role: 'Werewolf', isEliminated: true } : p
  );
  const state = makeGameState(
    'DayEliminationReveal',
    2,
    { dayDeaths: [{ playerId: BOB, playerName: 'Bob', cause: 'DayVote' }], phaseEndsAt: new Date(Date.now() + 7 * 1000).toISOString() },
    players
  );
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Verdict")');
  await shot(page, 'day-elimination-reveal');
});

// ── 22 · Final Scores Reveal — Villagers win ────────────────────────────────
test('final-scores-reveal', async ({ page }) => {
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
  const state = makeGameState('FinalScoresReveal', 3, { winner: 'Villagers', phaseEndsAt: new Date(Date.now() + 47 * 1000).toISOString() }, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Final Scores Reveal")');
  await shot(page, 'final-scores-reveal');
});

// ── 23 · Final Scores Reveal — after game 2, showing running totals ──────────
test('final-scores-reveal-game2', async ({ page }) => {
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
  const state = makeGameState('FinalScoresReveal', 2, { winner: 'Villagers', gameIndex: 2, phaseEndsAt: new Date(Date.now() + 47 * 1000).toISOString() }, players);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("Final Scores Reveal")');
  await shot(page, 'final-scores-reveal-game2');
});

// ── 25 · Moderator panel — night theme (Werewolves Turn, "Skip night") ────────
test('moderator-night', async ({ page }) => {
  const state = makeGameState('Werewolves', 2, {}, MODERATOR_PLAYERS);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.moderator-panel');
  await shot(page, 'moderator-night');
});

// ── 26 · Moderator panel — day theme (Discussion, "Force end discussion") ─────
test('moderator-day', async ({ page }) => {
  const state = makeGameState('Discussion', 2, { phaseEndsAt: new Date(Date.now() + 154 * 1000).toISOString() }, MODERATOR_PLAYERS);
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('.moderator-panel');
  await shot(page, 'moderator-day');
});

// ── 24 · Tournament Unlock — pass code dialog ─────────────────────────────────
test('tournament-unlock', async ({ page }) => {
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
      isTournamentModeUnlocked: false,
    },
    players: BASE_PLAYERS.map(p => p.playerId === HOST ? { ...p, isModerator: true } : p),
    hasDuplicateNames: false,
  };
  await setupMocks(page, state, {});
  await setViewer(page, HOST);
  await page.goto(`/game/${GAME_ID}/lobby`);
  await page.waitForSelector('.tournament-gate');
  await shot(page, 'tournament-unlock');
});

// ── 27 · Night Announcement — "The Night Has Fallen" ─────────────────────────
test('night-announcement', async ({ page }) => {
  const state = makeGameState('NightAnnouncement', 2, {
    phaseEndsAt: new Date(Date.now() + 5 * 1000).toISOString(),
  });
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("The Night Has Fallen")');
  await shot(page, 'night-announcement');
});

// ── 28 · Day Announcement — "The Night Has Ended" ───────────────────────────
test('day-announcement', async ({ page }) => {
  const state = makeGameState('DayAnnouncement', 2, {
    phaseEndsAt: new Date(Date.now() + 5 * 1000).toISOString(),
  });
  const role = makeRoleDto('Villager');
  await setupMocks(page, state, role);
  await setViewer(page, ALICE);
  await page.goto(`/game/${GAME_ID}/session`);
  await page.waitForSelector('h2:has-text("The Night Has Ended")');
  await shot(page, 'day-announcement');
});

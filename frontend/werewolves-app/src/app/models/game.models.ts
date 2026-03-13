export interface EliminationEntryDto {
  playerId: string;
  playerName: string;
  cause: string;
}

export interface GameState {
  gameId: string;
  gameName: string;
  creatorId: string;
  minPlayers: number;
  maxPlayers: number;
  joinLink: string;
  qrCodeBase64: string;
  status: string;
  version: number;
  discussionDurationMinutes: number;
  numberOfWerewolves: number;
  enabledSkills: string[];
  phaseStartedAt: string | null;
  // Session
  phase: string;
  roundNumber: number;
  phaseEndsAt: string | null;
  nightDeaths: EliminationEntryDto[];
  dayDeaths: EliminationEntryDto[];
  winner: string | null;
  tiebreakCandidates: string[];
}

export interface PlayerState {
  playerId: string;
  displayName: string;
  isCreator: boolean;
  isModerator: boolean;
  isConnected: boolean;
  participationStatus: string;
  role: string | null;
  skill: string | null;
  isEliminated: boolean;
  isDone: boolean;
  joinedAt?: string;
}

export interface LobbyState {
  game: GameState;
  players: PlayerState[];
  hasDuplicateNames: boolean;
}

export interface CreateGameRequest {
  gameName: string;
  creatorName: string;
  maxPlayers: number;
  frontendBaseUrl: string;
}

export interface CreateGameResponse {
  gameId: string;
  playerId: string;
  joinLink: string;
  qrCodeBase64: string;
}

export interface JoinGameRequest {
  displayName: string;
  playerId?: string;
}

export interface JoinGameResponse {
  playerId: string;
  success: boolean;
  message?: string;
}

export interface PlayerRoleDto {
  role: string;
  skill: string | null;
  fellowWerewolves: string[];
  loverName: string | null;
  nightKillTargetName: string | null;
  witchHealUsed: boolean;
  witchPoisonUsed: boolean;
}

export interface SeerActionResponse {
  isWerewolf: boolean;
  skill: string | null;
}

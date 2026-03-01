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
}

export interface PlayerState {
  playerId: string;
  displayName: string;
  isCreator: boolean;
  isModerator: boolean;
  status: string;
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

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateGameRequest, CreateGameResponse, JoinGameRequest, JoinGameResponse, LobbyState, PlayerRoleDto } from '../models/game.models';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class GameService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  createGame(request: CreateGameRequest): Observable<CreateGameResponse> {
    return this.http.post<CreateGameResponse>(`${this.apiUrl}/game/create`, request);
  }

  joinGame(gameId: string, request: JoinGameRequest): Observable<JoinGameResponse> {
    return this.http.post<JoinGameResponse>(`${this.apiUrl}/game/${gameId}/join`, request);
  }

  getGameState(gameId: string): Observable<LobbyState> {
    return this.http.get<LobbyState>(`${this.apiUrl}/game/${gameId}`);
  }

  leaveGame(gameId: string, playerId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/leave`, { playerId });
  }

  removePlayer(gameId: string, targetPlayerId: string, moderatorId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/remove`, { playerId: targetPlayerId, moderatorId });
  }

  updateSettings(gameId: string, creatorId: string, minPlayers: number, maxPlayers: number, discussionDurationMinutes: number, numberOfWerewolves: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/settings`, { creatorId, minPlayers, maxPlayers, discussionDurationMinutes, numberOfWerewolves });
  }

  updateGameName(gameId: string, creatorId: string, gameName: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/name`, { creatorId, gameName });
  }

  updatePlayerName(gameId: string, playerId: string, displayName: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/player-name`, { playerId, displayName });
  }

  startGame(gameId: string, creatorId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/start`, { creatorId });
  }

  markDone(gameId: string, playerId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/done`, { playerId });
  }

  castVote(gameId: string, voterId: string, targetId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/vote`, { voterId, targetId });
  }

  forceAdvancePhase(gameId: string, creatorId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/force-advance`, { playerId: creatorId });
  }

  getRole(gameId: string, playerId: string): Observable<PlayerRoleDto> {
    return this.http.get<PlayerRoleDto>(`${this.apiUrl}/game/${gameId}/role`, { params: { playerId } });
  }

  getPlayerId(): string | null {
    return localStorage.getItem('playerId');
  }

  setPlayerId(playerId: string): void {
    localStorage.setItem('playerId', playerId);
  }

  clearPlayerId(): void {
    localStorage.removeItem('playerId');
  }
}

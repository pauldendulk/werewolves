import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateGameRequest, CreateGameResponse, JoinGameRequest, JoinGameResponse, LobbyState, PlayerRoleDto, SeerActionResponse } from '../models/game.models';
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

  updateSettings(gameId: string, moderatorId: string, minPlayers: number, maxPlayers: number, discussionDurationMinutes: number, tiebreakDiscussionDurationSeconds: number, numberOfWerewolves: number, enabledSkills?: string[]): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/settings`, { moderatorId, minPlayers, maxPlayers, discussionDurationMinutes, tiebreakDiscussionDurationSeconds, numberOfWerewolves, enabledSkills });
  }

  updatePlayerName(gameId: string, playerId: string, displayName: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/player-name`, { playerId, displayName });
  }

  startGame(gameId: string, moderatorId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/start`, { moderatorId });
  }

  markDone(gameId: string, playerId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/done`, { playerId });
  }

  castVote(gameId: string, voterId: string, targetId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/vote`, { voterId, targetId });
  }

  forceAdvancePhase(gameId: string, moderatorId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/force-advance`, { playerId: moderatorId });
  }

  getRole(gameId: string, playerId: string): Observable<PlayerRoleDto> {
    return this.http.get<PlayerRoleDto>(`${this.apiUrl}/game/${gameId}/role`, { params: { playerId } });
  }

  cupidAction(gameId: string, playerId: string, lover1Id: string, lover2Id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/cupid-action`, { playerId, lover1Id, lover2Id });
  }

  seerAction(gameId: string, seerId: string, targetId: string): Observable<SeerActionResponse> {
    return this.http.get<SeerActionResponse>(`${this.apiUrl}/game/${gameId}/seer-action`, { params: { seerId, targetId } });
  }

  witchAction(gameId: string, playerId: string, choice: string, poisonTargetId?: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/witch-action`, { playerId, choice, poisonTargetId });
  }

  hunterAction(gameId: string, playerId: string, targetId: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/hunter-action`, { playerId, targetId });
  }

  unlockTournament(gameId: string, code: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/unlock`, { code });
  }

  createCheckoutSession(gameId: string, successUrl: string, cancelUrl: string): Observable<{ checkoutUrl: string }> {
    return this.http.post<{ checkoutUrl: string }>(`${this.apiUrl}/game/${gameId}/checkout`, { successUrl, cancelUrl });
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

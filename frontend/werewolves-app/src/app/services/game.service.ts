import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateGameRequest, CreateGameResponse, JoinGameRequest, JoinGameResponse, LobbyState } from '../models/game.models';
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

  updateSettings(gameId: string, creatorId: string, maxPlayers: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/game/${gameId}/settings`, { creatorId, maxPlayers });
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

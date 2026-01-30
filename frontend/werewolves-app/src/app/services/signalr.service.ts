import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { GameState, PlayerState } from '../models/game.models';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection?: signalR.HubConnection;
  private lobbyUpdatedSubject = new Subject<GameState>();
  private playerJoinedSubject = new Subject<PlayerState[]>();
  private playerLeftSubject = new Subject<string>();
  private playerRemovedSubject = new Subject<string>();
  private maxPlayersUpdatedSubject = new Subject<number>();
  private gameNameUpdatedSubject = new Subject<string>();

  public lobbyUpdated$: Observable<GameState> = this.lobbyUpdatedSubject.asObservable();
  public playerJoined$: Observable<PlayerState[]> = this.playerJoinedSubject.asObservable();
  public playerLeft$: Observable<string> = this.playerLeftSubject.asObservable();
  public playerRemoved$: Observable<string> = this.playerRemovedSubject.asObservable();
  public maxPlayersUpdated$: Observable<number> = this.maxPlayersUpdatedSubject.asObservable();
  public gameNameUpdated$: Observable<string> = this.gameNameUpdatedSubject.asObservable();

  constructor() {}

  public async startConnection(gameId: string, playerId: string): Promise<void> {
    // Hub is at /hub/lobby, not under /api
    const hubUrl = environment.apiUrl.replace('/api', '') + '/hub/lobby';
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    this.setupEventHandlers();

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinLobby', gameId, playerId);
  }

  public async stopConnection(gameId: string, playerId: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('LeaveLobby', gameId, playerId);
      await this.hubConnection.stop();
    }
  }

  public async updateMaxPlayers(gameId: string, maxPlayers: number, creatorId: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('UpdateMaxPlayers', gameId, maxPlayers, creatorId);
    }
  }

  public async updateMinPlayers(gameId: string, minPlayers: number, creatorId: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('UpdateMinPlayers', gameId, minPlayers, creatorId);
    }
  }

  public async updateGameName(gameId: string, gameName: string, creatorId: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('UpdateGameName', gameId, gameName, creatorId);
    }
  }

  public async removePlayer(gameId: string, playerId: string, moderatorId: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('RemovePlayer', gameId, playerId, moderatorId);
    }
  }

  public async updatePlayerName(gameId: string, playerId: string, displayName: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('UpdatePlayerName', gameId, playerId, displayName);
    }
  }

  private setupEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('LobbyUpdated', (game: GameState) => {
      this.lobbyUpdatedSubject.next(game);
    });

    this.hubConnection.on('PlayerJoined', (players: PlayerState[]) => {
      this.playerJoinedSubject.next(players);
    });

    this.hubConnection.on('PlayerLeft', (playerId: string) => {
      this.playerLeftSubject.next(playerId);
    });

    this.hubConnection.on('PlayerRemoved', (playerId: string) => {
      this.playerRemovedSubject.next(playerId);
    });

    this.hubConnection.on('MaxPlayersUpdated', (maxPlayers: number) => {
      this.maxPlayersUpdatedSubject.next(maxPlayers);
    });

    this.hubConnection.on('GameNameUpdated', (gameName: string) => {
      this.gameNameUpdatedSubject.next(gameName);
    });
  }
}

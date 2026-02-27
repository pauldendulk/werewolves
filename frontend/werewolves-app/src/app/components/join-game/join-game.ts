import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { GameService } from '../../services/game.service';
import { JoinGameRequest, LobbyState } from '../../models/game.models';

@Component({
  selector: 'app-join-game',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './join-game.html',
  styleUrl: './join-game.scss'
})
export class JoinGameComponent implements OnInit {
  gameId: string = '';
  gameName: string = '';
  playerName: string = '';
  loading: boolean = false;
  loadingGame: boolean = true;
  gameState?: LobbyState;

  constructor(
    private gameService: GameService,
    private route: ActivatedRoute,
    private router: Router,
    private messageService: MessageService
  ) {}

  get creatorName(): string {
    const creator = this.gameState?.players.find((p) => p.playerId === this.gameState?.game.creatorId);
    return creator?.displayName || 'Unknown';
  }

  ngOnInit(): void {
    this.gameId = this.route.snapshot.paramMap.get('id') || '';
    this.loadGameInfo();
  }

  loadGameInfo(): void {
    this.gameService.getGameState(this.gameId).subscribe({
      next: (state) => {
        this.gameState = state;
        this.gameName = state.game.gameName;
        this.loadingGame = false;

        // Check if player already joined
        const playerId = this.gameService.getPlayerId();
        if (playerId) {
          const existingPlayer = state.players.find(p => p.playerId === playerId);
          if (existingPlayer) {
            this.router.navigate(['/game', this.gameId, 'lobby'], { replaceUrl: true });
          }
        }
      },
      error: (error) => {
        this.loadingGame = false;
        // If game not found, redirect to home
        if (error.status === 404 || error.status === 500) {
          this.router.navigate(['/']);
          return;
        }
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Game not found'
        });
      }
    });
  }

  joinGame(): void {
    if (this.playerName.trim().length === 0) {
      return;
    }

    this.loading = true;
    const request: JoinGameRequest = {
      displayName: this.playerName.trim(),
      playerId: this.gameService.getPlayerId() || undefined
    };

    this.gameService.joinGame(this.gameId, request).subscribe({
      next: (response) => {
        if (response.success) {
          this.gameService.setPlayerId(response.playerId);
          this.router.navigate(['/game', this.gameId, 'lobby'], { replaceUrl: true });
        } else {
          this.loading = false;
          this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: response.message || 'Failed to join game'
          });
        }
      },
      error: (error) => {
        this.loading = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: error.error?.message || 'Failed to join game'
        });
      }
    });
  }
}

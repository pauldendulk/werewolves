import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { GameService } from '../../services/game.service';
import { CreateGameRequest } from '../../models/game.models';

@Component({
  selector: 'app-create-game',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    ToastModule
  ],
  providers: [MessageService],
  templateUrl: './create-game.html',
  styleUrl: './create-game.scss'
})
export class CreateGameComponent {
  gameName: string = 'GameName';
  creatorName: string = 'PlayerName';
  maxPlayers: number = 20;
  loading: boolean = false;

  constructor(
    private gameService: GameService,
    private router: Router,
    private messageService: MessageService
  ) {}

  createGame(): void {
    this.loading = true;
    const request: CreateGameRequest = {
      gameName: this.gameName,
      creatorName: this.creatorName,
      maxPlayers: this.maxPlayers
    };

    this.gameService.createGame(request).subscribe({
      next: (response) => {
        this.gameService.setPlayerId(response.playerId);
        this.router.navigate(['/game', response.gameId, 'lobby']);
      },
      error: (error) => {
        this.loading = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to create game. Please try again.'
        });
      }
    });
  }
}

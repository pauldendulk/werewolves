import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService, ConfirmationService } from 'primeng/api';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { GameService } from '../../services/game.service';
import { SignalRService } from '../../services/signalr.service';
import { LobbyState, PlayerState } from '../../models/game.models';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputNumberModule,
    InputTextModule,
    ToastModule,
    ConfirmDialogModule,
    TooltipModule,
    MenuModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './lobby.html',
  styleUrl: './lobby.scss'
})
export class LobbyComponent implements OnInit, OnDestroy {
  gameId: string = '';
  playerId: string = '';
  lobbyState?: LobbyState;
  loading: boolean = true;
  qrCodeImage: SafeUrl = '';

  // Editing state
  editingGameName: boolean = false;
  editedGameName: string = '';
  editingPlayerName: boolean = false;
  editedPlayerName: string = '';

  playerMenuItems: MenuItem[] = [];
  @ViewChild('playerMenu') playerMenu!: Menu;
  selectedPlayer: PlayerState | null = null;

  constructor(
    private gameService: GameService,
    private signalRService: SignalRService,
    private route: ActivatedRoute,
    private router: Router,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.gameId = this.route.snapshot.paramMap.get('id') || '';
    this.playerId = this.gameService.getPlayerId() || '';

    if (!this.playerId) {
      this.router.navigate(['/game', this.gameId]);
      return;
    }

    this.loadLobbyState();
    this.setupSignalR();
  }

  ngOnDestroy(): void {
    if (this.playerId && this.gameId) {
      this.signalRService.stopConnection(this.gameId, this.playerId);
    }
  }

  loadLobbyState(): void {
    this.gameService.getGameState(this.gameId).subscribe({
      next: (state) => {
        this.lobbyState = state;
        this.qrCodeImage = this.sanitizer.bypassSecurityTrustUrl(
          `data:image/png;base64,${state.game.qrCodeBase64}`
        );
        this.loading = false;
      },
      error: (error) => {
        this.loading = false;
        // If game not found, redirect to home
        if (error.status === 404) {
          this.router.navigate(['/']);
          return;
        }
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to load game state'
        });
      }
    });
  }

  async setupSignalR(): Promise<void> {
    try {
      await this.signalRService.startConnection(this.gameId, this.playerId);

      this.signalRService.lobbyUpdated$.subscribe((game) => {
        if (this.lobbyState) {
          this.lobbyState.game = game;
          this.loadLobbyState(); // Reload to get latest state
        }
      });

      this.signalRService.playerJoined$.subscribe(() => {
        this.loadLobbyState();
        this.messageService.add({
          severity: 'info',
          summary: 'Player Joined',
          detail: 'A new player joined the game'
        });
      });

      this.signalRService.playerLeft$.subscribe(() => {
        this.loadLobbyState();
      });
    } catch (error) {
      console.error('SignalR connection error:', error);
    }
  }

  get isCreator(): boolean {
    return this.lobbyState?.game.creatorId === this.playerId;
  }

  get currentPlayer(): PlayerState | undefined {
    return this.lobbyState?.players.find(p => p.playerId === this.playerId);
  }

  get activePlayers(): PlayerState[] {
    return this.lobbyState?.players.filter(p =>
      p.status === 'Connected' || p.status === 'Disconnected'
    ) || [];
  }

  getPlayerStatusIcon(player: PlayerState): string {
    switch (player.status) {
      case 'Connected': return '🟢';
      case 'Disconnected': return '🔴';
      case 'Left': return '🟡';
      case 'Removed': return '⛔';
      default: return '';
    }
  }

  getPlayerLabel(player: PlayerState): string {
    let label = player.displayName;
    if (player.playerId === this.playerId) {
      label += ' (You)';
    } else if (player.isCreator) {
      label += ' (Creator)';
    } else if (player.isModerator) {
      label += ' (Moderator)';
    }

    if (player.status !== 'Connected') {
      label += ` (${player.status.toLowerCase()})`;
    }

    return label;
  }

  async copyLink(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.lobbyState!.game.joinLink);
      this.messageService.add({
        severity: 'success',
        summary: 'Link Copied',
        detail: 'Join link copied to clipboard'
      });
    } catch (error) {
      this.messageService.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to copy link'
      });
    }
  }

  async shareLink(): Promise<void> {
    if (navigator.share) {
      try {
        await navigator.share({
          title: this.lobbyState!.game.gameName,
          text: `Join my Werewolves game: ${this.lobbyState!.game.gameName}`,
          url: this.lobbyState!.game.joinLink
        });
      } catch (error) {
        console.error('Share failed:', error);
      }
    } else {
      this.copyLink();
    }
  }

  async updateMaxPlayers(value: number): Promise<void> {
    if (this.isCreator && this.lobbyState) {
      await this.signalRService.updateMaxPlayers(
        this.gameId,
        value,
        this.playerId
      );
    }
  }

  async updateMinPlayers(value: number): Promise<void> {
    if (this.isCreator && this.lobbyState) {
      await this.signalRService.updateMinPlayers(
        this.gameId,
        value,
        this.playerId
      );
    }
  }

  leaveGame(): void {
    this.gameService.leaveGame(this.gameId, this.playerId).subscribe({
      next: () => {
        this.gameService.clearPlayerId();
        this.router.navigate(['/']);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to leave game'
        });
      }
    });
  }

  confirmLeaveGame(): void {
    this.confirmationService.confirm({
      message: 'Are you sure you want to leave? This may disrupt the game for other players.',
      header: 'Leave Game',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Leave',
      rejectLabel: 'Stay',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.leaveGame();
      }
    });
  }

  confirmRemovePlayer(player: PlayerState): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to remove ${player.displayName} from the game?`,
      header: 'Remove Player',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Remove',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.removePlayer(player.playerId);
      }
    });
  }

  removePlayer(targetPlayerId: string): void {
    this.gameService.removePlayer(this.gameId, targetPlayerId, this.playerId).subscribe({
      next: () => {
        this.loadLobbyState();
        this.messageService.add({
          severity: 'success',
          summary: 'Player Removed',
          detail: 'Player has been removed from the game'
        });
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to remove player'
        });
      }
    });
  }

  // Game name editing
  startEditGameName(): void {
    this.editedGameName = this.lobbyState!.game.gameName;
    this.editingGameName = true;
  }

  cancelEditGameName(): void {
    this.editingGameName = false;
    this.editedGameName = '';
  }

  async saveGameName(): Promise<void> {
    if (this.editedGameName.trim().length === 0) {
      return;
    }
    await this.signalRService.updateGameName(this.gameId, this.editedGameName.trim(), this.playerId);
    this.editingGameName = false;
  }

  // Player name editing
  startEditPlayerName(): void {
    this.editedPlayerName = this.currentPlayer?.displayName || '';
    this.editingPlayerName = true;
  }

  cancelEditPlayerName(): void {
    this.editingPlayerName = false;
    this.editedPlayerName = '';
  }

  async savePlayerName(): Promise<void> {
    if (this.editedPlayerName.trim().length === 0) {
      return;
    }
    await this.signalRService.updatePlayerName(this.gameId, this.playerId, this.editedPlayerName.trim());
    this.editingPlayerName = false;
    this.loadLobbyState();
  }

  formatJoinTime(joinedAt: string): string {
    const date = new Date(joinedAt);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);

    if (diffMins < 1) return 'just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    return date.toLocaleDateString();
  }

  showPlayerMenu(event: Event, player: PlayerState): void {
    this.selectedPlayer = player;
    this.playerMenuItems = [];

    // Option for ME to leave
    if (player.playerId === this.playerId) {
      this.playerMenuItems.push({
        label: 'Leave Game',
        icon: 'pi pi-sign-out',
        command: () => this.confirmLeaveGame(),
        styleClass: 'text-red-500'
      });
    }
    // Option for CREATOR to remove OTHERS
    else if (this.isCreator) {
      this.playerMenuItems.push({
        label: 'Remove Player',
        icon: 'pi pi-trash',
        command: () => this.confirmRemovePlayer(player),
        styleClass: 'text-red-500'
      });
    }

    if (this.playerMenuItems.length > 0) {
      this.playerMenu.toggle(event);
    }
  }
}

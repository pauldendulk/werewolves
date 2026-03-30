import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService, ConfirmationService } from 'primeng/api';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { GameService } from '../../services/game.service';
import { PollingService } from '../../services/polling.service';
import { AudioService } from '../../services/audio.service';
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
    DialogModule,
    MenuModule,
    ToggleSwitchModule
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
  enabledSkills: string[] = [];
  private pollSubscription?: Subscription;
  secondsRemaining = 0;
  private timerHandle?: ReturnType<typeof setInterval>;

  // Editing state
  showRenameDialog: boolean = false;
  editedPlayerName: string = '';

  // Unlock state
  unlockCode: string = ''
  unlocking: boolean = false;
  showCodeInput: boolean = false;
  buyingPass: boolean = false;

  playerMenuItems: MenuItem[] = [];
  @ViewChild('playerMenu') playerMenu!: Menu;
  selectedPlayer: PlayerState | null = null;

  constructor(
    private gameService: GameService,
    private pollingService: PollingService,
    private route: ActivatedRoute,
    private router: Router,
    private messageService: MessageService,
    private confirmationService: ConfirmationService,
    private sanitizer: DomSanitizer,
    private audioService: AudioService
  ) {}

  ngOnInit(): void {
    this.gameId = this.route.snapshot.paramMap.get('id') || '';
    this.playerId = this.gameService.getPlayerId() || '';

    if (!this.playerId) {
      this.router.navigate(['/game', this.gameId]);
      return;
    }

    const payment = this.route.snapshot.queryParamMap.get('payment');
    if (payment === 'cancelled') {
      this.messageService.add({ severity: 'warn', summary: 'Cancelled', detail: 'Payment was cancelled. You can try again when ready.' });
    }

    this.loadLobbyState();
    this.startPolling();
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    clearInterval(this.timerHandle);
  }

  loadLobbyState(): void {
    this.gameService.getGameState(this.gameId).subscribe({
      next: (state) => {
        const isParticipant = state.players.some(p => p.playerId === this.playerId);
        if (!isParticipant) {
          this.router.navigate(['/game', this.gameId], { replaceUrl: true });
          return;
        }

        this.lobbyState = state;
        this.qrCodeImage = this.sanitizer.bypassSecurityTrustUrl(
          `data:image/png;base64,${state.game.qrCodeBase64}`
        );
        this.loading = false;
      },
      error: (error) => {
        this.loading = false;
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

  startPolling(): void {
    this.pollSubscription = this.pollingService.startPolling(this.gameId).subscribe({
      next: (state) => {
        const prevPlayerCount = this.lobbyState?.players.length ?? 0;
        this.lobbyState = state;
        this.enabledSkills = state.game.enabledSkills ?? [];
        this.qrCodeImage = this.sanitizer.bypassSecurityTrustUrl(
          `data:image/png;base64,${state.game.qrCodeBase64}`
        );
        this.loading = false;

        // Role is null for mid-game joiners; they wait in the lobby for the next game.
        if (state.game.status === 'InProgress' && this.currentPlayer?.role != null) {
          this.router.navigate(['/game', this.gameId, 'session'], { replaceUrl: true });
          return;
        }

        this.updateTimer(state.game.phaseEndsAt);

        if (state.players.length > prevPlayerCount && prevPlayerCount > 0) {
          this.messageService.add({
            severity: 'info',
            summary: 'Player Joined',
            detail: 'A new player joined the game'
          });
        }
      },
      error: (error) => {
        this.loading = false;
        if (error.status === 404) {
          this.router.navigate(['/']);
        }
      }
    });
  }

  get timerLabel(): string {
    const m = Math.floor(this.secondsRemaining / 60).toString().padStart(2, '0');
    const s = (this.secondsRemaining % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  }

  private updateTimer(phaseEndsAt: string | null): void {
    clearInterval(this.timerHandle);
    if (!phaseEndsAt) { this.secondsRemaining = 0; return; }
    const update = () => {
      const diff = new Date(phaseEndsAt).getTime() - Date.now();
      this.secondsRemaining = Math.max(0, Math.ceil(diff / 1000));
    };
    update();
    this.timerHandle = setInterval(update, 500);
  }

  get isCreator(): boolean {
    const playerId = this.playerId || this.gameService.getPlayerId();
    return this.lobbyState?.game.creatorId === playerId;
  }

  get currentPlayer(): PlayerState | undefined {
    const playerId = this.playerId || this.gameService.getPlayerId();
    return this.lobbyState?.players.find(p => p.playerId === playerId);
  }

  get activePlayers(): PlayerState[] {
    return this.lobbyState?.players.filter(p =>
      p.participationStatus === 'Participating'
    ) || [];
  }

  get hasEnoughPlayers(): boolean {
    const minPlayers = this.lobbyState?.game.minPlayers || 2;
    return this.activePlayers.length >= minPlayers;
  }

  get hasEnoughPlayersForSkills(): boolean {
    const werewolves = this.lobbyState?.game.numberOfWerewolves ?? 1;
    const villagerSlots = this.activePlayers.length - werewolves;
    return this.enabledSkills.length <= villagerSlots;
  }

  get canStartGame(): boolean {
    return this.hasEnoughPlayers && !this.lobbyState?.hasDuplicateNames && this.hasEnoughPlayersForSkills;
  }

  get needsTournamentPass(): boolean {
    return !!this.lobbyState && this.lobbyState.game.gameIndex >= 2 && !this.lobbyState.game.isPremium;
  }

  get startGameIcon(): string {
    if (this.needsTournamentPass) return 'pi pi-lock';
    if (this.lobbyState?.game.isPremium) return 'pi pi-star-fill';
    return '';
  }

  get creatorName(): string {
    const creator = this.lobbyState?.players.find(p => p.playerId === this.lobbyState?.game.creatorId);
    return creator?.displayName || 'Unknown';
  }

  getPlayerStatusIcon(player: PlayerState): string {
    if (player.participationStatus === 'Left') return '🟡';
    if (player.participationStatus === 'Removed') return '⛔';
    return player.isConnected ? '🟢' : '🔴';
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

    if (!player.isConnected) {
      label += ' (disconnected)';
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
          title: 'Werewolves',
          text: `Join my Werewolves game`,
          url: this.lobbyState!.game.joinLink
        });
      } catch (error) {
        console.error('Share failed:', error);
      }
    } else {
      this.copyLink();
    }
  }

  updateMaxPlayers(value: number): void {
    if (this.isCreator && this.lobbyState) {
      const minPlayers = this.lobbyState.game.minPlayers;
      const duration = this.lobbyState.game.discussionDurationMinutes;
      const tiebreak = this.lobbyState.game.tiebreakDiscussionDurationSeconds;
      const werewolves = this.lobbyState.game.numberOfWerewolves;
      this.gameService.updateSettings(this.gameId, this.playerId, minPlayers, value, duration, tiebreak, werewolves, this.enabledSkills).subscribe();
    }
  }

  updateMinPlayers(value: number): void {
    if (this.isCreator && this.lobbyState) {
      const maxPlayers = this.lobbyState.game.maxPlayers;
      const duration = this.lobbyState.game.discussionDurationMinutes;
      const tiebreak = this.lobbyState.game.tiebreakDiscussionDurationSeconds;
      const werewolves = this.lobbyState.game.numberOfWerewolves;
      this.gameService.updateSettings(this.gameId, this.playerId, value, maxPlayers, duration, tiebreak, werewolves, this.enabledSkills).subscribe();
    }
  }

  updateDiscussionDuration(value: number): void {
    if (this.isCreator && this.lobbyState) {
      const minPlayers = this.lobbyState.game.minPlayers;
      const maxPlayers = this.lobbyState.game.maxPlayers;
      const tiebreak = this.lobbyState.game.tiebreakDiscussionDurationSeconds;
      const werewolves = this.lobbyState.game.numberOfWerewolves;
      this.gameService.updateSettings(this.gameId, this.playerId, minPlayers, maxPlayers, value, tiebreak, werewolves, this.enabledSkills).subscribe();
    }
  }

  updateTiebreakDiscussionDuration(value: number): void {
    if (this.isCreator && this.lobbyState) {
      const minPlayers = this.lobbyState.game.minPlayers;
      const maxPlayers = this.lobbyState.game.maxPlayers;
      const duration = this.lobbyState.game.discussionDurationMinutes;
      const werewolves = this.lobbyState.game.numberOfWerewolves;
      this.gameService.updateSettings(this.gameId, this.playerId, minPlayers, maxPlayers, duration, value, werewolves, this.enabledSkills).subscribe();
    }
  }

  updateNumberOfWerewolves(value: number): void {
    if (this.isCreator && this.lobbyState) {
      const minPlayers = this.lobbyState.game.minPlayers;
      const maxPlayers = this.lobbyState.game.maxPlayers;
      const duration = this.lobbyState.game.discussionDurationMinutes;
      const tiebreak = this.lobbyState.game.tiebreakDiscussionDurationSeconds;
      this.gameService.updateSettings(this.gameId, this.playerId, minPlayers, maxPlayers, duration, tiebreak, value, this.enabledSkills).subscribe();
    }
  }

  isSkillEnabled(skill: string): boolean {
    return this.enabledSkills.includes(skill);
  }

  toggleSkill(skill: string): void {
    if (!this.isCreator || !this.lobbyState) return;
    const skills = [...this.enabledSkills];
    const idx = skills.indexOf(skill);
    if (idx >= 0) skills.splice(idx, 1);
    else skills.push(skill);
    this.enabledSkills = skills;
    const g = this.lobbyState.game;
    this.gameService.updateSettings(this.gameId, this.playerId, g.minPlayers, g.maxPlayers, g.discussionDurationMinutes, g.tiebreakDiscussionDurationSeconds, g.numberOfWerewolves, this.enabledSkills).subscribe();
  }

  startGame(): void {
    this.audioService.unlock();
    this.gameService.startGame(this.gameId, this.playerId).subscribe({
      next: () => this.router.navigate(['/game', this.gameId, 'session'], { replaceUrl: true }),
      error: (err) => this.messageService.add({
        severity: 'error',
        summary: 'Error',
        detail: err.error?.message ?? 'Failed to start game'
      })
    });
  }

  submitUnlockCode(): void {
    if (!this.unlockCode.trim()) return;
    this.unlocking = true;
    this.gameService.unlockTournament(this.gameId, this.unlockCode.trim()).subscribe({
      next: () => {
        this.unlocking = false;
        this.audioService.unlock();
        this.gameService.startGame(this.gameId, this.playerId).subscribe({
          next: () => this.router.navigate(['/game', this.gameId, 'session'], { replaceUrl: true }),
          error: (err) => this.messageService.add({
            severity: 'error',
            summary: 'Error',
            detail: err.error?.message ?? 'Failed to start game'
          })
        });
      },
      error: (err) => {
        this.unlocking = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Invalid Code',
          detail: err.error?.message ?? 'The code you entered is incorrect'
        });
      }
    });
  }

  buyTournamentPass(): void {
    this.buyingPass = true;
    const successUrl = `${window.location.origin}/game/${this.gameId}/lobby?payment=success`;
    const cancelUrl = `${window.location.origin}/game/${this.gameId}/lobby?payment=cancelled`;
    this.gameService.createCheckoutSession(this.gameId, successUrl, cancelUrl).subscribe({
      next: ({ checkoutUrl }) => window.location.href = checkoutUrl,
      error: (err) => {
        this.buyingPass = false;
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: err.error?.message ?? 'Could not start payment'
        });
      }
    });
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

  // Player name editing
  startEditPlayerName(): void {
    this.editedPlayerName = this.currentPlayer?.displayName || '';
    this.showRenameDialog = true;
  }

  cancelEditPlayerName(): void {
    this.showRenameDialog = false;
    this.editedPlayerName = '';
  }

  savePlayerName(): void {
    if (this.editedPlayerName.trim().length === 0) {
      return;
    }
    this.gameService.updatePlayerName(this.gameId, this.playerId, this.editedPlayerName.trim()).subscribe();
    this.showRenameDialog = false;
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

    // Option for ME to rename or leave
    if (player.playerId === this.playerId) {
      this.playerMenuItems.push({
        label: 'Rename',
        icon: 'pi pi-pencil',
        command: () => this.startEditPlayerName()
      });
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

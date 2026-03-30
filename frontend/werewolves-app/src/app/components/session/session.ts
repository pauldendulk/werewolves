import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { SelectModule } from 'primeng/select';
import { MessageService } from 'primeng/api';
import { GameService } from '../../services/game.service';
import { PollingService } from '../../services/polling.service';
import { AudioService } from '../../services/audio.service';
import { ClockSyncService } from '../../services/clock-sync.service';
import { AudioKey } from '../../models/audio-keys';
import { LobbyState, PlayerState, PlayerRoleDto, SeerActionResponse } from '../../models/game.models';

@Component({
  selector: 'app-session',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, ToastModule, SelectModule],
  providers: [MessageService],
  templateUrl: './session.html',
  styleUrl: './session.scss'
})
export class SessionComponent implements OnInit, OnDestroy {
  gameId = '';
  playerId = '';
  lobbyState?: LobbyState;
  roleDto?: PlayerRoleDto;
  roleRevealed = false;
  hasSeenRole = false;
  selectedVoteTarget: string | null = null;
  hasVotedThisPhase = false;
  // Skill action state
  cupidLover1: string | null = null;
  cupidLover2: string | null = null;
  seerTarget: string | null = null;
  seerResult?: SeerActionResponse;
  witchPoisonTarget: string | null = null;
  hunterTarget: string | null = null;
  private pollSubscription?: Subscription;
  private lastPhase = '';
  private lastRound = 0;
  secondsRemaining = 0;
  private timerHandle?: ReturnType<typeof setInterval>;
  private nightWarningSpoken = false;

  constructor(
    private gameService: GameService,
    private pollingService: PollingService,
    private audioService: AudioService,
    private clockSyncService: ClockSyncService,
    private route: ActivatedRoute,
    private router: Router,
    private messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.gameId = this.route.snapshot.paramMap.get('id') || '';
    this.playerId = this.gameService.getPlayerId() || '';

    if (!this.playerId) {
      this.router.navigate(['/game', this.gameId], { replaceUrl: true });
      return;
    }

    this.clockSyncService.start();

    this.gameService.getRole(this.gameId, this.playerId).subscribe({
      next: dto => this.roleDto = dto,
      error: () => {}
    });

    this.pollSubscription = this.pollingService.startPolling(this.gameId).subscribe({
      next: state => this.handleStateUpdate(state),
      error: err => {
        if (err.status === 404) this.router.navigate(['/']);
      }
    });
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    this.clockSyncService.stop();
    clearInterval(this.timerHandle);
  }

  private handleStateUpdate(state: LobbyState): void {
    if (state.game.status === 'WaitingForPlayers' || state.game.status === 'ReadyToStart') {
      this.goToLobby();
      return;
    }

    const phaseChanged = state.game.phase !== this.lastPhase;
    const roundChanged = state.game.roundNumber !== this.lastRound;

    this.lobbyState = state;

    if (state.game.phase === 'GameOver' && this.currentPlayer?.isDone) {
      this.goToLobby();
      return;
    }
    if (phaseChanged || roundChanged) {
      this.lastPhase = state.game.phase;
      this.lastRound = state.game.roundNumber;
      this.roleRevealed = false;
      this.selectedVoteTarget = null;
      this.hasVotedThisPhase = false;
      this.onPhaseEntered(state.game.phase, state.game.roundNumber, state.game.audioPlayAt);

      // Refresh role on any night-skill phase so loverName, nightKillTargetName, etc. stay current
      const skillPhases = ['WerewolvesMeeting', 'WerewolvesTurn', 'LoverReveal', 'SeerTurn', 'WitchTurn', 'HunterTurn'];
      if (skillPhases.includes(state.game.phase)) {
        this.gameService.getRole(this.gameId, this.playerId).subscribe({
          next: dto => this.roleDto = dto,
          error: () => {}
        });
      }
    }

    this.updateTimer(state.game.phaseEndsAt);
  }

  private onPhaseEntered(phase: string, round: number, audioPlayAt: string | null): void {
    this.nightWarningSpoken = false;
    const playAt = audioPlayAt ? this.clockSyncService.serverIsoToLocal(audioPlayAt) : new Date();
    switch (phase) {
      case 'RoleReveal':
        this.audioService.schedulePlay(AudioKey.RoleReveal, playAt);
        break;
      case 'WerewolvesMeeting':
        this.audioService.schedulePlay(AudioKey.WerewolvesMeeting, playAt);
        break;
      case 'WerewolvesTurn':
        this.audioService.schedulePlay(AudioKey.WerewolvesTurn, playAt);
        break;
      case 'CupidTurn':
        this.audioService.schedulePlay(AudioKey.CupidTurn, playAt);
        break;
      case 'LoverReveal':
        this.audioService.schedulePlay(AudioKey.LoverReveal, playAt);
        break;
      case 'SeerTurn':
        this.audioService.schedulePlay(AudioKey.SeerTurn, playAt);
        break;
      case 'WitchTurn':
        this.audioService.schedulePlay(AudioKey.WitchTurn, playAt);
        break;
      case 'HunterTurn':
        this.audioService.schedulePlay(AudioKey.HunterTurn, playAt);
        break;
      case 'NightEliminationReveal': {
        const deaths = this.lobbyState?.game.nightDeaths ?? [];
        if (deaths.length === 0) {
          this.audioService.schedulePlay(AudioKey.NightEndNoDeaths, playAt);
        } else {
          this.audioService.schedulePlay(deaths.length === 1 ? AudioKey.NightEndOneDeath : AudioKey.NightEndManyDeaths, playAt);
        }
        break;
      }
      case 'Discussion':
        this.audioService.schedulePlay(AudioKey.Discussion, playAt);
        break;
      case 'TiebreakDiscussion':
        this.audioService.schedulePlay(AudioKey.TiebreakDiscussion, playAt);
        break;
      case 'DayEliminationReveal': {
        const dayDeaths = this.lobbyState?.game.dayDeaths ?? [];
        if (dayDeaths.length === 0) {
          this.audioService.schedulePlay(AudioKey.DayEliminationRevealTie, playAt);
        } else {
          this.audioService.schedulePlay(AudioKey.DayEliminationReveal, playAt);
        }
        break;
      }
      case 'GameOver': {
        const winner = this.lobbyState?.game.winner;
        this.audioService.schedulePlay(winner === 'Villagers' ? AudioKey.GameOverVillagers : AudioKey.GameOverWerewolves, playAt);
        break;
      }
    }
  }

  private updateTimer(phaseEndsAt: string | null): void {
    clearInterval(this.timerHandle);
    if (!phaseEndsAt) { this.secondsRemaining = 0; return; }

    const update = () => {
      const diff = new Date(phaseEndsAt).getTime() - Date.now();
      this.secondsRemaining = Math.max(0, Math.ceil(diff / 1000));

      // 3-second warning before night ends
      if (!this.nightWarningSpoken && (this.phase === 'WerewolvesMeeting' || this.phase === 'WerewolvesTurn') && this.secondsRemaining <= 3 && this.secondsRemaining > 0) {
        this.nightWarningSpoken = true;
        this.audioService.play(AudioKey.NightWarning);
      }
    };
    update();
    this.timerHandle = setInterval(update, 500);
  }

  get currentPlayer(): PlayerState | undefined {
    return this.lobbyState?.players.find(p => p.playerId === this.playerId);
  }

  get isCreator(): boolean {
    return this.lobbyState?.game.creatorId === this.playerId;
  }

  get phase(): string {
    return this.lobbyState?.game.phase ?? '';
  }

  get alivePlayers(): PlayerState[] {
    return this.lobbyState?.players.filter(p =>
      !p.isEliminated && p.participationStatus === 'Participating'
    ) ?? [];
  }

  get participatingPlayers(): PlayerState[] {
    return this.lobbyState?.players.filter(p =>
      p.participationStatus === 'Participating'
    ) ?? [];
  }

  get sortedPlayersForGameOver(): PlayerState[] {
    return [...(this.lobbyState?.players ?? [])]
      .filter(p => p.participationStatus === 'Participating')
      .sort((a, b) => (b.score ?? 0) - (a.score ?? 0));
  }

  get voteTargets(): { label: string; value: string }[] {
    const candidates = this.phase === 'TiebreakDiscussion'
      ? (this.lobbyState?.game.tiebreakCandidates ?? [])
      : null;

    if (this.phase === 'WerewolvesTurn') {
      return this.alivePlayers
        .filter(p => p.playerId !== this.playerId)
        .map(p => ({ label: p.displayName, value: p.playerId }));
    }

    return this.alivePlayers
      .filter(p => p.playerId !== this.playerId && (!candidates || candidates.includes(p.playerId)))
      .map(p => ({ label: p.displayName, value: p.playerId }));
  }

  get doneCount(): number {
    return this.alivePlayers.filter(p => p.isDone).length;
  }

  get timerLabel(): string {
    const m = Math.floor(this.secondsRemaining / 60).toString().padStart(2, '0');
    const s = (this.secondsRemaining % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  }

  get canVoteNight(): boolean {
    return this.phase === 'WerewolvesTurn' &&
      this.roleDto?.role === 'Werewolf' &&
      !(this.currentPlayer?.isEliminated ?? false);
  }

  get canVoteDay(): boolean {
    return this.phase === 'Discussion' || this.phase === 'TiebreakDiscussion';
  }

  get showDoneButton(): boolean {
    if (this.currentPlayer?.isEliminated ?? false) return false;
    return (this.phase === 'RoleReveal' || this.phase === 'WerewolvesMeeting' || this.phase === 'Discussion' || this.phase === 'TiebreakDiscussion')
      && !(this.currentPlayer?.isDone ?? false);
  }

  get allPlayers(): { label: string; value: string }[] {
    return this.alivePlayers
      .filter(p => p.playerId !== this.playerId)
      .map(p => ({ label: p.displayName, value: p.playerId }));
  }

  get canActAsCupid(): boolean {
    return this.phase === 'CupidTurn' && this.roleDto?.skill === 'Cupid' && !(this.currentPlayer?.isEliminated ?? false);
  }

  get canActAsSeer(): boolean {
    return this.phase === 'SeerTurn' && this.roleDto?.skill === 'Seer' && !(this.currentPlayer?.isEliminated ?? false);
  }

  get canActAsWitch(): boolean {
    return this.phase === 'WitchTurn' && this.roleDto?.skill === 'Witch' && !(this.currentPlayer?.isEliminated ?? false);
  }

  get canActAsHunter(): boolean {
    return this.phase === 'HunterTurn' && this.roleDto?.skill === 'Hunter';
  }

  submitCupidAction(): void {
    if (!this.cupidLover1 || !this.cupidLover2) return;
    this.gameService.cupidAction(this.gameId, this.playerId, this.cupidLover1, this.cupidLover2).subscribe({
      error: (err) => this.messageService.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to submit Cupid action' })
    });
  }

  submitSeerAction(): void {
    if (!this.seerTarget) return;
    this.gameService.seerAction(this.gameId, this.playerId, this.seerTarget).subscribe({
      next: result => {
        this.seerResult = result;
        this.messageService.add({ severity: 'info', summary: 'Seer result received', detail: '' });
      },
      error: (err) => this.messageService.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to submit Seer action' })
    });
  }

  submitWitchAction(choice: string): void {
    this.gameService.witchAction(this.gameId, this.playerId, choice, this.witchPoisonTarget ?? undefined).subscribe({
      error: (err) => this.messageService.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to submit Witch action' })
    });
  }

  submitHunterAction(): void {
    if (!this.hunterTarget) return;
    this.gameService.hunterAction(this.gameId, this.playerId, this.hunterTarget).subscribe({
      error: (err) => this.messageService.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to submit Hunter action' })
    });
  }

  revealRole(): void {
    this.roleRevealed = true;
    this.hasSeenRole = true;
  }

  hideRole(): void {
    this.roleRevealed = false;
  }

  markDone(): void {
    this.gameService.markDone(this.gameId, this.playerId).subscribe({
      error: () => this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to mark done' })
    });
  }

  submitVote(): void {
    if (!this.selectedVoteTarget) return;
    this.gameService.castVote(this.gameId, this.playerId, this.selectedVoteTarget).subscribe({
      next: () => {
        this.hasVotedThisPhase = true;
        this.messageService.add({ severity: 'success', summary: 'Vote cast', detail: 'Your vote has been recorded' });
      },
      error: (err) => this.messageService.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to submit vote' })
    });
  }

  forceAdvance(): void {
    this.gameService.forceAdvancePhase(this.gameId, this.playerId).subscribe({
      error: () => this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to advance phase' })
    });
  }

  getEliminatedRole(playerId: string | null): string {
    if (!playerId) return '?';
    return this.lobbyState?.players.find(p => p.playerId === playerId)?.role ?? '?';
  }

  goToLobby(): void {
    this.router.navigate(['/game', this.gameId, 'lobby']);
  }

  doneWithResults(): void {
    this.gameService.markDone(this.gameId, this.playerId).subscribe({ error: () => {} });
    this.goToLobby();
  }

  goHome(): void {
    this.router.navigate(['/']);
  }
}

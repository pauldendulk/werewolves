import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, EMPTY } from 'rxjs';
import { SessionComponent } from './session';
import { GameService } from '../../services/game.service';
import { PollingService } from '../../services/polling.service';
import { AudioService } from '../../services/audio.service';
import { LobbyState } from '../../models/game.models';

describe('SessionComponent', () => {
  let component: SessionComponent;
  let fixture: ComponentFixture<SessionComponent>;
  let gameServiceSpy: jasmine.SpyObj<GameService>;
  let pollingServiceSpy: jasmine.SpyObj<PollingService>;
  let audioServiceSpy: jasmine.SpyObj<AudioService>;
  let routerSpy: jasmine.SpyObj<Router>;

  const makeLobbyState = (overrides: Partial<LobbyState['game']> = {}): LobbyState => ({
    game: {
      gameId: 'game123',
      gameName: 'Test Game',
      creatorId: 'player1',
      minPlayers: 4,
      maxPlayers: 20,
      joinLink: '',
      qrCodeBase64: '',
      status: 'InProgress',
      version: 1,
      discussionDurationMinutes: 5,
      numberOfWerewolves: 1,
      phase: 'RoleReveal',
      roundNumber: 1,
      phaseEndsAt: null,
      lastEliminatedByNight: null,
      lastEliminatedByNightName: null,
      lastEliminatedByDay: null,
      lastEliminatedByDayName: null,
      winner: null,
      tiebreakCandidates: [],
      ...overrides
    },
    players: [
      {
        playerId: 'player1',
        displayName: 'Alice',
        isCreator: true,
        isModerator: false,
        isConnected: true,
        participationStatus: 'Participating',
        role: null,
        isEliminated: false,
        isDone: false
      },
      {
        playerId: 'player2',
        displayName: 'Bob',
        isCreator: false,
        isModerator: false,
        isConnected: true,
        participationStatus: 'Participating',
        role: null,
        isEliminated: false,
        isDone: false
      }
    ],
    hasDuplicateNames: false
  });

  beforeEach(async () => {
    gameServiceSpy = jasmine.createSpyObj('GameService', [
      'getPlayerId', 'getRole', 'markDone', 'castVote', 'forceAdvancePhase', 'getGameState'
    ]);
    gameServiceSpy.getPlayerId.and.returnValue('player1');
    gameServiceSpy.getRole.and.returnValue(of({ role: 'Villager', fellowWerewolves: [] }));

    pollingServiceSpy = jasmine.createSpyObj('PollingService', ['startPolling']);
    pollingServiceSpy.startPolling.and.returnValue(of(makeLobbyState()));

    audioServiceSpy = jasmine.createSpyObj('AudioService', ['speak']);
    audioServiceSpy.speak.and.returnValue(Promise.resolve());

    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [SessionComponent, HttpClientTestingModule],
      providers: [
        { provide: GameService, useValue: gameServiceSpy },
        { provide: PollingService, useValue: pollingServiceSpy },
        { provide: AudioService, useValue: audioServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'game123' } } }
        },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SessionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should identify current player', () => {
    expect(component.currentPlayer?.displayName).toBe('Alice');
  });

  it('should identify creator', () => {
    expect(component.isCreator).toBeTrue();
  });

  it('should return phase from lobby state', () => {
    expect(component.phase).toBe('RoleReveal');
  });

  it('should return alive players', () => {
    expect(component.alivePlayers.length).toBe(2);
  });

  it('should format timer label', () => {
    component.secondsRemaining = 65;
    expect(component.timerLabel).toBe('01:05');
  });

  it('should reveal and hide role', () => {
    component.revealRole();
    expect(component.roleRevealed).toBeTrue();
    expect(component.hasSeenRole).toBeTrue();

    component.hideRole();
    expect(component.roleRevealed).toBeFalse();
    expect(component.hasSeenRole).toBeTrue(); // stays true
  });

  it('should not submit vote without target', () => {
    component.selectedVoteTarget = null;
    component.submitVote();
    expect(gameServiceSpy.castVote).not.toHaveBeenCalled();
  });

  it('should submit vote with target', () => {
    gameServiceSpy.castVote.and.returnValue(of(void 0));
    component.selectedVoteTarget = 'player2';
    component.submitVote();
    expect(gameServiceSpy.castVote).toHaveBeenCalledWith('game123', 'player1', 'player2');
  });

  it('should call markDone', () => {
    gameServiceSpy.markDone.and.returnValue(of(void 0));
    component.markDone();
    expect(gameServiceSpy.markDone).toHaveBeenCalledWith('game123', 'player1');
  });

  it('should call forceAdvance', () => {
    gameServiceSpy.forceAdvancePhase.and.returnValue(of(void 0));
    component.forceAdvance();
    expect(gameServiceSpy.forceAdvancePhase).toHaveBeenCalledWith('game123', 'player1');
  });

  it('canVoteNight should be false for villagers', () => {
    expect(component.canVoteNight).toBeFalse();
  });

  it('canVoteDay should be false during RoleReveal', () => {
    expect(component.canVoteDay).toBeFalse();
  });
});

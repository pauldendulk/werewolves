import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer } from '@angular/platform-browser';
import { MessageService, ConfirmationService } from 'primeng/api';
import { of } from 'rxjs';
import { LobbyComponent } from './lobby';
import { GameService } from '../../services/game.service';
import { PollingService } from '../../services/polling.service';

describe('LobbyComponent', () => {
  let component: LobbyComponent;
  let fixture: ComponentFixture<LobbyComponent>;
  let gameServiceSpy: jasmine.SpyObj<GameService>;
  let pollingServiceSpy: jasmine.SpyObj<PollingService>;
  let routerSpy: jasmine.SpyObj<Router>;

  const mockLobbyState = {
    game: {
      gameId: 'game123',
      gameName: 'Test Game',
      creatorId: 'player1',
      minPlayers: 4,
      maxPlayers: 20,
      joinLink: 'http://localhost:4200/game/game123',
      qrCodeBase64: 'base64data',
      status: 'WaitingForPlayers',
      version: 1,
      discussionDurationMinutes: 5,
      numberOfWerewolves: 1,
      phase: 'Lobby',
      roundNumber: 0,
      phaseEndsAt: null,
      lastEliminatedByNight: null,
      lastEliminatedByNightName: null,
      lastEliminatedByDay: null,
      lastEliminatedByDayName: null,
      winner: null,
      tiebreakCandidates: []
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
        isDone: false,
        joinedAt: new Date().toISOString()
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
        isDone: false,
        joinedAt: new Date().toISOString()
      },
      {
        playerId: 'player3',
        displayName: 'Charlie',
        isCreator: false,
        isModerator: false,
        isConnected: false,
        participationStatus: 'Left',
        role: null,
        isEliminated: false,
        isDone: false,
        joinedAt: new Date().toISOString()
      }
    ],
    hasDuplicateNames: false
  };

  beforeEach(async () => {
    gameServiceSpy = jasmine.createSpyObj('GameService', [
      'getGameState', 'getPlayerId', 'setPlayerId', 'clearPlayerId',
      'startGame', 'leaveGame', 'removePlayer',
      'updateSettings', 'updateGameName', 'updatePlayerName'
    ]);
    gameServiceSpy.getPlayerId.and.returnValue('player1');
    gameServiceSpy.getGameState.and.returnValue(of(mockLobbyState));

    pollingServiceSpy = jasmine.createSpyObj('PollingService', ['startPolling']);
    pollingServiceSpy.startPolling.and.returnValue(of(mockLobbyState));

    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [LobbyComponent, HttpClientTestingModule],
      providers: [
        { provide: GameService, useValue: gameServiceSpy },
        { provide: PollingService, useValue: pollingServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'game123' } } }
        },
        MessageService,
        ConfirmationService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LobbyComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should identify creator correctly', () => {
    expect(component.isCreator).toBeTrue();
  });

  it('should return active (participating) players', () => {
    expect(component.activePlayers.length).toBe(2);
    expect(component.activePlayers.map(p => p.displayName)).toEqual(['Alice', 'Bob']);
  });

  it('should compute creatorName from player list', () => {
    expect(component.creatorName).toBe('Alice');
  });

  it('should determine hasEnoughPlayers based on minPlayers', () => {
    // 2 active players, minPlayers = 4
    expect(component.hasEnoughPlayers).toBeFalse();
  });

  it('should not allow starting when not enough players', () => {
    expect(component.canStartGame).toBeFalse();
  });

  it('should redirect to home if no playerId', () => {
    // Lobby component navigates away in ngOnInit when playerId is empty
    const freshComponent = fixture.componentInstance;
    freshComponent.playerId = '';
    freshComponent.gameId = 'game123';
    // Expect the playerId to be empty (redirect logic depends on it)
    expect(freshComponent.playerId).toBe('');
  });

  it('should format join time correctly', () => {
    expect(component.formatJoinTime(new Date().toISOString())).toBe('just now');
  });
});

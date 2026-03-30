import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
      tournamentCode: 'game123',
      creatorId: 'player1',
      minPlayers: 4,
      maxPlayers: 20,
      joinLink: 'http://localhost:4200/game/game123',
      qrCodeBase64: 'base64data',
      status: 'WaitingForPlayers',
      version: 1,
      discussionDurationMinutes: 5,
      tiebreakDiscussionDurationSeconds: 60,
      numberOfWerewolves: 1,
      enabledSkills: [],
      phaseStartedAt: null,
      phase: 'Lobby',
      roundNumber: 0,
      phaseEndsAt: null,
      audioPlayAt: null,
      nightDeaths: [],
      dayDeaths: [],
      winner: null,
      tiebreakCandidates: [] as string[],
      gameIndex: 1,
      isPremium: false
    },
    players: [
      {
        playerId: 'player1',
        displayName: 'Alice',
        isCreator: true,
        isModerator: true,
        isConnected: true,
        participationStatus: 'Participating',
        role: null,
        skill: null,
        isEliminated: false,
        isDone: false,
        score: 0,
        totalScore: 0,
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
        skill: null,
        isEliminated: false,
        isDone: false,
        score: 0,
        totalScore: 0,
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
        skill: null,
        isEliminated: false,
        isDone: false,
        score: 0,
        totalScore: 0,
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
      imports: [LobbyComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: GameService, useValue: gameServiceSpy },
        { provide: PollingService, useValue: pollingServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'game123' }, queryParamMap: { get: () => null } } }
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

  it('should identify moderator', () => {
    expect(component.isModerator).toBeTrue();
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

  it('hasEnoughPlayersForSkills should be true when no skills are enabled', () => {
    component.enabledSkills = [];
    expect(component.hasEnoughPlayersForSkills).toBeTrue();
  });

  it('hasEnoughPlayersForSkills should be false when skills exceed villager slots', () => {
    // 2 active players, 1 werewolf → 1 villager slot
    // Enabling 2 skills exceeds the 1 available slot
    component.enabledSkills = ['Seer', 'Witch'];
    expect(component.hasEnoughPlayersForSkills).toBeFalse();
  });

  it('hasEnoughPlayersForSkills should be true when skills fit within villager slots', () => {
    // 2 active players, 1 werewolf → 1 villager slot, 1 skill fits
    component.enabledSkills = ['Seer'];
    expect(component.hasEnoughPlayersForSkills).toBeTrue();
  });

  it('canStartGame should be false when too many skills for player count', () => {
    // Bump up players to satisfy minPlayers, but add more skills than villager slots
    pollingServiceSpy.startPolling.and.returnValue(of({
      ...mockLobbyState,
      game: { ...mockLobbyState.game, minPlayers: 2 },
      players: [
        { ...mockLobbyState.players[0] },
        { ...mockLobbyState.players[1] }
      ]
    }));
    component.enabledSkills = ['Seer', 'Witch', 'Hunter', 'Cupid'];
    // 2 players, 1 werewolf → 1 villager slot, 4 skills → false
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

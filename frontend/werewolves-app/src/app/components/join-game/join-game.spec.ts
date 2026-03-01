import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { JoinGameComponent } from './join-game';
import { GameService } from '../../services/game.service';

describe('JoinGameComponent', () => {
  let component: JoinGameComponent;
  let fixture: ComponentFixture<JoinGameComponent>;
  let gameServiceSpy: jasmine.SpyObj<GameService>;
  let routerSpy: jasmine.SpyObj<Router>;

  const mockLobbyState = {
    game: {
      gameId: 'game123',
      gameName: 'Test Game',
      creatorId: 'creator1',
      minPlayers: 4,
      maxPlayers: 20,
      joinLink: '',
      qrCodeBase64: '',
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
        playerId: 'creator1',
        displayName: 'Alice',
        isCreator: true,
        isModerator: false,
        isConnected: true,
        participationStatus: 'Participating',
        role: null,
        isEliminated: false,
        isDone: false
      }
    ],
    hasDuplicateNames: false
  };

  beforeEach(async () => {
    gameServiceSpy = jasmine.createSpyObj('GameService', [
      'getGameState', 'joinGame', 'getPlayerId', 'setPlayerId'
    ]);
    gameServiceSpy.getGameState.and.returnValue(of(mockLobbyState));
    gameServiceSpy.getPlayerId.and.returnValue(null);
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [JoinGameComponent, HttpClientTestingModule],
      providers: [
        { provide: GameService, useValue: gameServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'game123' } } }
        },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(JoinGameComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load game info on init', () => {
    expect(gameServiceSpy.getGameState).toHaveBeenCalledWith('game123');
    expect(component.gameName).toBe('Test Game');
    expect(component.loadingGame).toBeFalse();
  });

  it('should compute creatorName from players', () => {
    expect(component.creatorName).toBe('Alice');
  });

  it('should not join with empty name', () => {
    component.playerName = '';
    component.joinGame();
    expect(gameServiceSpy.joinGame).not.toHaveBeenCalled();
  });

  it('should join game and navigate on success', () => {
    gameServiceSpy.joinGame.and.returnValue(of({
      playerId: 'player2',
      success: true
    }));

    component.playerName = 'Bob';
    component.joinGame();

    expect(gameServiceSpy.joinGame).toHaveBeenCalledWith('game123', {
      displayName: 'Bob',
      playerId: undefined
    });
    expect(gameServiceSpy.setPlayerId).toHaveBeenCalledWith('player2');
    expect(routerSpy.navigate).toHaveBeenCalledWith(
      ['/game', 'game123', 'lobby'],
      { replaceUrl: true }
    );
  });

  it('should set loading false on join error', () => {
    gameServiceSpy.joinGame.and.returnValue(throwError(() => ({ error: { message: 'fail' } })));

    component.playerName = 'Bob';
    component.joinGame();

    expect(component.loading).toBeFalse();
  });
});

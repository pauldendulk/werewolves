import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { GameService } from './game.service';
import { CreateGameRequest, JoinGameRequest } from '../models/game.models';

describe('GameService', () => {
  let service: GameService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [GameService]
    });
    service = TestBed.inject(GameService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should create a game', () => {
    const request: CreateGameRequest = {
      gameName: 'Test Game',
      creatorName: 'John',
      maxPlayers: 30
    };

    const mockResponse = {
      gameId: 'abc123',
      playerId: 'player1',
      joinLink: 'http://localhost:4200/game/abc123',
      qrCodeBase64: 'base64string'
    };

    service.createGame(request).subscribe(response => {
      expect(response.gameId).toBe('abc123');
      expect(response.playerId).toBe('player1');
    });

    const req = httpMock.expectOne('http://localhost:5000/api/game/create');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('should join a game', () => {
    const gameId = 'abc123';
    const request: JoinGameRequest = {
      displayName: 'Alice'
    };

    const mockResponse = {
      playerId: 'player2',
      success: true
    };

    service.joinGame(gameId, request).subscribe(response => {
      expect(response.playerId).toBe('player2');
      expect(response.success).toBe(true);
    });

    const req = httpMock.expectOne(`http://localhost:5000/api/game/${gameId}/join`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('should store and retrieve playerId', () => {
    const playerId = 'player123';

    service.setPlayerId(playerId);
    expect(service.getPlayerId()).toBe(playerId);

    service.clearPlayerId();
    expect(service.getPlayerId()).toBeNull();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CreateGameComponent } from './create-game';
import { GameService } from '../../services/game.service';

describe('CreateGameComponent', () => {
  let component: CreateGameComponent;
  let fixture: ComponentFixture<CreateGameComponent>;
  let gameServiceSpy: jasmine.SpyObj<GameService>;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    gameServiceSpy = jasmine.createSpyObj('GameService', ['createGame', 'setPlayerId']);
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [CreateGameComponent, HttpClientTestingModule],
      providers: [
        { provide: GameService, useValue: gameServiceSpy },
        { provide: Router, useValue: routerSpy },
        MessageService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CreateGameComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have default values', () => {
    expect(component.gameName).toBe('GameName');
    expect(component.creatorName).toBe('');
    expect(component.maxPlayers).toBe(20);
    expect(component.loading).toBeFalse();
  });

  it('should call createGame and navigate on success', () => {
    const mockResponse = {
      gameId: 'game123',
      playerId: 'player1',
      joinLink: 'http://localhost:4200/game/game123',
      qrCodeBase64: 'base64'
    };
    gameServiceSpy.createGame.and.returnValue(of(mockResponse));

    component.createGame();

    expect(component.loading).toBeTrue();
    expect(gameServiceSpy.createGame).toHaveBeenCalled();
    expect(gameServiceSpy.setPlayerId).toHaveBeenCalledWith('player1');
    expect(routerSpy.navigate).toHaveBeenCalledWith(
      ['/game', 'game123', 'lobby'],
      { replaceUrl: true }
    );
  });

  it('should set loading false on error', () => {
    gameServiceSpy.createGame.and.returnValue(throwError(() => new Error('fail')));

    component.createGame();

    expect(component.loading).toBeFalse();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
      tournamentCode: 'TEST',
      creatorId: 'player1',
      minPlayers: 4,
      maxPlayers: 20,
      joinLink: '',
      qrCodeBase64: '',
      status: 'InProgress',
      version: 1,
      discussionDurationMinutes: 5,
      tiebreakDiscussionDurationSeconds: 60,
      numberOfWerewolves: 1,
      enabledSkills: [],
      phaseStartedAt: null,
      phase: 'RoleReveal',
      roundNumber: 1,
      phaseEndsAt: null,
      nightDeaths: [],
      dayDeaths: [],
      winner: null,
      tiebreakCandidates: [] as string[],
      audioPlayAt: null,
      gameIndex: 1,
      isPremium: false,
      ...overrides
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
        currentVoteTargetId: null,
        score: 0,
        totalScore: 0
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
        currentVoteTargetId: null,
        score: 0,
        totalScore: 0
      }
    ],
    hasDuplicateNames: false
  });

  beforeEach(async () => {
    gameServiceSpy = jasmine.createSpyObj('GameService', [
      'getPlayerId', 'getRole', 'markDone', 'castVote', 'forceAdvancePhase', 'extendDiscussion', 'getGameState'
    ]);
    gameServiceSpy.getPlayerId.and.returnValue('player1');
    gameServiceSpy.getRole.and.returnValue(of({ role: 'Villager', skill: null, fellowWerewolves: [], loverName: null, nightKillTargetName: null, witchHealUsed: false, witchPoisonUsed: false }));

    pollingServiceSpy = jasmine.createSpyObj('PollingService', ['startPolling']);
    pollingServiceSpy.startPolling.and.returnValue(of(makeLobbyState()));

    audioServiceSpy = jasmine.createSpyObj('AudioService', ['play', 'schedulePlay']);
    audioServiceSpy.play.and.returnValue(Promise.resolve());

    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [SessionComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
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

  it('should identify moderator', () => {
    expect(component.isModerator).toBeTrue();
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

  it('should call extendDiscussion', () => {
    gameServiceSpy.extendDiscussion.and.returnValue(of(void 0));
    component.extendDiscussion();
    expect(gameServiceSpy.extendDiscussion).toHaveBeenCalledWith('game123', 'player1');
  });

  it('showExtendButton should be true during Discussion', () => {
    component.lobbyState = makeLobbyState({ phase: 'Discussion' });
    expect(component.showExtendButton).toBeTrue();
  });

  it('showExtendButton should be true during TiebreakDiscussion', () => {
    component.lobbyState = makeLobbyState({ phase: 'TiebreakDiscussion' });
    expect(component.showExtendButton).toBeTrue();
  });

  it('showExtendButton should be false outside discussion', () => {
    component.lobbyState = makeLobbyState({ phase: 'RoleReveal' });
    expect(component.showExtendButton).toBeFalse();
  });

  it('canVoteNight should be false for villagers', () => {
    expect(component.canVoteNight).toBeFalse();
  });

  it('canVoteDay should be false during RoleReveal', () => {
    expect(component.canVoteDay).toBeFalse();
  });

  describe('hasVotedThisPhase blocks Done button during Discussion', () => {
    beforeEach(() => {
      component.lobbyState = makeLobbyState({ phase: 'Discussion' });
      component.playerId = 'player1';
    });

    it('hasVotedThisPhase starts false', () => {
      expect(component.hasVotedThisPhase).toBeFalse();
    });

    it('canVoteDay is true during Discussion for alive player', () => {
      expect(component.canVoteDay).toBeTrue();
    });

    it('sets hasVotedThisPhase to true on successful vote', () => {
      gameServiceSpy.castVote.and.returnValue(of(void 0));
      component.selectedVoteTarget = 'player2';
      component.submitVote();
      expect(component.hasVotedThisPhase).toBeTrue();
    });

    it('resets hasVotedThisPhase when phase changes', () => {
      component.hasVotedThisPhase = true;
      // Simulate a state update with a new phase
      const newState = makeLobbyState({ phase: 'TiebreakDiscussion', tiebreakCandidates: ['player1', 'player2'] });
      (component as any).handleStateUpdate(newState);
      expect(component.hasVotedThisPhase).toBeFalse();
    });
  });

  describe('voteTargets in TiebreakDiscussion with 3 tied candidates', () => {
    const makeThreePlayerTiebreakState = (): LobbyState => ({
      game: {
        gameId: 'game123',
        tournamentCode: 'TEST',
        creatorId: 'player1',
        minPlayers: 3,
        maxPlayers: 20,
        joinLink: '',
        qrCodeBase64: '',
        status: 'InProgress',
        version: 1,
        discussionDurationMinutes: 5,
        tiebreakDiscussionDurationSeconds: 60,
        numberOfWerewolves: 1,
        enabledSkills: [],
        phaseStartedAt: null,
        phase: 'TiebreakDiscussion',
        roundNumber: 1,
        phaseEndsAt: null,
        audioPlayAt: null,
        nightDeaths: [],
        dayDeaths: [],
        winner: null,
        tiebreakCandidates: ['player1', 'player2', 'player3'],
        gameIndex: 1,
        isPremium: false
      },
      players: [
        { playerId: 'player1', displayName: 'Alice', isCreator: true, isModerator: true, isConnected: true, participationStatus: 'Participating', role: null, skill: null, isEliminated: false, isDone: false, currentVoteTargetId: null, score: 0, totalScore: 0 },
        { playerId: 'player2', displayName: 'Bob', isCreator: false, isModerator: false, isConnected: true, participationStatus: 'Participating', role: null, skill: null, isEliminated: false, isDone: false, currentVoteTargetId: null, score: 0, totalScore: 0 },
        { playerId: 'player3', displayName: 'Carol', isCreator: false, isModerator: false, isConnected: true, participationStatus: 'Participating', role: null, skill: null, isEliminated: false, isDone: false, currentVoteTargetId: null, score: 0, totalScore: 0 }
      ],
      hasDuplicateNames: false
    });

    it('tied player should see the other two candidates as vote targets', () => {
      component.lobbyState = makeThreePlayerTiebreakState();
      component.playerId = 'player1'; // Alice is a tied candidate

      const targets = component.voteTargets;

      expect(targets.length).toBe(3, 'Alice should see exactly 3 options (No vote, Bob, Carol)');
      expect(targets.map(t => t.value)).toContain('');
      expect(targets.map(t => t.value)).toContain('player2');
      expect(targets.map(t => t.value)).toContain('player3');
      expect(targets.map(t => t.value)).not.toContain('player1', 'a player cannot vote for themselves');
    });

    it('non-tied player should see all three candidates as vote targets', () => {
      const state = makeThreePlayerTiebreakState();
      state.game.tiebreakCandidates = ['player2', 'player3', 'player4'];
      state.players.push({
        playerId: 'player4', displayName: 'Dave', isCreator: false, isModerator: false,
        isConnected: true, participationStatus: 'Participating', role: null, skill: null,
        isEliminated: false, isDone: false, currentVoteTargetId: null, score: 0, totalScore: 0
      });
      component.lobbyState = state;
      component.playerId = 'player1'; // Alice is NOT one of the tied candidates

      const targets = component.voteTargets;

      expect(targets.length).toBe(4, 'non-tied Alice should see all 3 tied candidates plus No vote');
      expect(targets.map(t => t.value)).toContain('');
      expect(targets.map(t => t.value)).toContain('player2');
      expect(targets.map(t => t.value)).toContain('player3');
      expect(targets.map(t => t.value)).toContain('player4');
    });
  });
});

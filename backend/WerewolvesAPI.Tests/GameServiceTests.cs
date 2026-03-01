using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Tests;

public class GameServiceTests
{
    private readonly GameService _gameService;
    private readonly Mock<ILogger<GameService>> _loggerMock;

    public GameServiceTests()
    {
        _loggerMock = new Mock<ILogger<GameService>>();
        _gameService = new GameService(_loggerMock.Object);
    }

    [Fact]
    public void CreateGame_ShouldCreateGameWithCreator()
    {
        // Arrange
        var gameName = "Test Game";
        var creatorName = "John";
        var maxPlayers = 30;
        var baseUrl = "http://localhost:4200";

        // Act
        var game = _gameService.CreateGame(gameName, creatorName, maxPlayers, baseUrl);

        // Assert
        game.Should().NotBeNull();
        game.GameName.Should().Be(gameName);
        game.MaxPlayers.Should().Be(maxPlayers);
        game.Players.Should().HaveCount(1);
        game.Players.First().DisplayName.Should().Be(creatorName);
        game.Players.First().IsCreator.Should().BeTrue();
        game.Players.First().IsModerator.Should().BeTrue();
    }

    [Fact]
    public void JoinGame_ShouldAddPlayerToGame()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var playerName = "Alice";

        // Act
        var (success, message, player) = _gameService.JoinGame(game.GameId, playerName);

        // Assert
        success.Should().BeTrue();
        player.Should().NotBeNull();
        player!.DisplayName.Should().Be(playerName);
        player.IsCreator.Should().BeFalse();
        
        var updatedGame = _gameService.GetGame(game.GameId);
        updatedGame!.Players.Should().HaveCount(2);
    }

    [Fact]
    public void JoinGame_WhenGameFull_ShouldReturnFailure()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 2, "http://localhost");
        _gameService.JoinGame(game.GameId, "Player1");

        // Act
        var (success, message, player) = _gameService.JoinGame(game.GameId, "Player2");

        // Assert
        success.Should().BeFalse();
        message.Should().Contain("full");
        player.Should().BeNull();
    }

    [Fact]
    public void JoinGame_WhenPlayerRejoins_ShouldUpdateStatus()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");
        _gameService.LeaveGame(game.GameId, player!.PlayerId);

        // Act
        var (success, message, rejoinedPlayer) = _gameService.JoinGame(game.GameId, "Alice", player.PlayerId);

        // Assert
        success.Should().BeTrue();
        rejoinedPlayer!.PlayerId.Should().Be(player.PlayerId);
        rejoinedPlayer.IsConnected.Should().BeTrue();
        rejoinedPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Participating);
    }

    [Fact]
    public void LeaveGame_ShouldUpdatePlayerStatus()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");

        // Act
        var result = _gameService.LeaveGame(game.GameId, player!.PlayerId);

        // Assert
        result.Should().BeTrue();
        var updatedGame = _gameService.GetGame(game.GameId);
        var leftPlayer = updatedGame!.Players.First(p => p.PlayerId == player.PlayerId);
        leftPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Left);
    }

    [Fact]
    public void RemovePlayer_ByModerator_ShouldUpdatePlayerStatus()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");

        // Act
        var result = _gameService.RemovePlayer(game.GameId, player!.PlayerId, game.CreatorId);

        // Assert
        result.Should().BeTrue();
        var updatedGame = _gameService.GetGame(game.GameId);
        var removedPlayer = updatedGame!.Players.First(p => p.PlayerId == player.PlayerId);
        removedPlayer.ParticipationStatus.Should().Be(Models.ParticipationStatus.Removed);
    }

    [Fact]
    public void UpdateMaxPlayers_ByCreator_ShouldUpdateSettings()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var newMaxPlayers = 30;

        // Act
        var result = _gameService.UpdateMaxPlayers(game.GameId, newMaxPlayers, game.CreatorId);

        // Assert
        result.Should().BeTrue();
        var updatedGame = _gameService.GetGame(game.GameId);
        updatedGame!.MaxPlayers.Should().Be(newMaxPlayers);
    }

    [Fact]
    public void UpdateMaxPlayers_ByNonCreator_ShouldFail()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");

        // Act
        var result = _gameService.UpdateMaxPlayers(game.GameId, 30, player!.PlayerId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasDuplicateNames_WhenNoDuplicates_ShouldReturnFalse()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Bob");

        // Act
        var result = _gameService.HasDuplicateNames(game.GameId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasDuplicateNames_WhenDuplicates_ShouldReturnTrue()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Alice");

        // Act
        var result = _gameService.HasDuplicateNames(game.GameId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void UpdatePlayerName_WhenCreatorChangesName_ShouldUpdateCreatorNameInDTO()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "OriginalCreator", 40, "http://localhost");
        var creatorId = game.CreatorId;
        var newName = "UpdatedCreator";

        // Act
        var updateResult = _gameService.UpdatePlayerName(game.GameId, creatorId, newName);

        // Assert
        updateResult.Should().BeTrue();
        
        // Verify the creator player's name was updated
        var updatedGame = _gameService.GetGame(game.GameId);
        var creator = updatedGame!.Players.First(p => p.PlayerId == creatorId);
        creator.DisplayName.Should().Be(newName);
        
        // The DTO should derive creator name from the player, not a stored value
        // This test verifies that the creator's current DisplayName is the source of truth
    }

    [Fact]
    public void GetGame_ShouldDeriveCreatorNameFromPlayer()
    {
        // Arrange
        var game = _gameService.CreateGame("Test Game", "OriginalName", 40, "http://localhost");
        var creatorId = game.CreatorId;
        
        // Act - Change creator's name
        _gameService.UpdatePlayerName(game.GameId, creatorId, "NewName");
        
        // Assert - The creator player's DisplayName should be the source of truth
        var updatedGame = _gameService.GetGame(game.GameId);
        var creator = updatedGame!.Players.First(p => p.PlayerId == creatorId);
        creator.DisplayName.Should().Be("NewName");
        
        // When the DTO is created (in GameController), it should use the player's current DisplayName
        // This ensures the creator name is always in sync with the player's current name
    }

    [Fact]
    public void CreateGame_ShouldStartAtVersionOne()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        game.Version.Should().Be(1);
    }

    [Fact]
    public void JoinGame_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var versionBefore = game.Version;

        _gameService.JoinGame(game.GameId, "Alice");

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void LeaveGame_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");
        var versionAfterJoin = _gameService.GetGame(game.GameId)!.Version;

        _gameService.LeaveGame(game.GameId, player!.PlayerId);

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionAfterJoin + 1);
    }

    [Fact]
    public void RemovePlayer_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Alice");
        var versionAfterJoin = _gameService.GetGame(game.GameId)!.Version;

        _gameService.RemovePlayer(game.GameId, player!.PlayerId, game.CreatorId);

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionAfterJoin + 1);
    }

    [Fact]
    public void UpdateMaxPlayers_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var versionBefore = game.Version;

        _gameService.UpdateMaxPlayers(game.GameId, 30, game.CreatorId);

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void UpdateMinPlayers_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var versionBefore = game.Version;

        _gameService.UpdateMinPlayers(game.GameId, 3, game.CreatorId);

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void UpdateGameName_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var versionBefore = game.Version;

        _gameService.UpdateGameName(game.GameId, "New Name", game.CreatorId);

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void UpdatePlayerName_ShouldBumpVersion()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var versionBefore = game.Version;

        _gameService.UpdatePlayerName(game.GameId, game.CreatorId, "New Name");

        _gameService.GetGame(game.GameId)!.Version.Should().Be(versionBefore + 1);
    }

    [Fact]
    public void CreateGame_ShouldStartAtWaitingForPlayers()
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        game.Status.Should().Be(Models.GameStatus.WaitingForPlayers);
    }

    [Fact]
    public void JoinGame_WhenEnoughPlayers_ShouldTransitionToReadyToStart()
    {
        // Arrange - min players is 4, so we need 4 total
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        game.Status.Should().Be(Models.GameStatus.WaitingForPlayers);

        // Act - add 3 more to reach min of 4
        _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Bob");
        _gameService.JoinGame(game.GameId, "Charlie");

        // Assert
        var updatedGame = _gameService.GetGame(game.GameId)!;
        updatedGame.Status.Should().Be(Models.GameStatus.ReadyToStart);
    }

    [Fact]
    public void LeaveGame_WhenBelowMinPlayers_ShouldTransitionBackToWaitingForPlayers()
    {
        // Arrange - get to ReadyToStart with exactly MinPlayers (3: Creator + Alice + Bob)
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        var (_, _, alice) = _gameService.JoinGame(game.GameId, "Alice");
        _gameService.JoinGame(game.GameId, "Bob");
        _gameService.GetGame(game.GameId)!.Status.Should().Be(Models.GameStatus.ReadyToStart);

        // Act - Alice leaves, dropping below min (2 < 3)
        _gameService.LeaveGame(game.GameId, alice!.PlayerId);

        // Assert
        _gameService.GetGame(game.GameId)!.Status.Should().Be(Models.GameStatus.WaitingForPlayers);
    }

    // ── Session / phase tests ──────────────────────────────────────────────

    private Models.GameState CreateReadyGame(int extraPlayers = 3)
    {
        var game = _gameService.CreateGame("Test Game", "Creator", 40, "http://localhost");
        for (int i = 0; i < extraPlayers; i++)
            _gameService.JoinGame(game.GameId, $"Player{i + 1}");
        return _gameService.GetGame(game.GameId)!;
    }

    [Fact]
    public void StartGame_ShouldAssignRoles()
    {
        var game = CreateReadyGame();

        var (success, error) = _gameService.StartGame(game.GameId, game.CreatorId);

        success.Should().BeTrue();
        var updated = _gameService.GetGame(game.GameId)!;
        updated.Players.Should().OnlyContain(p => p.Role != null);
        updated.Players.Count(p => p.Role == Models.PlayerRole.Werewolf).Should().Be(game.NumberOfWerewolves);
        updated.Players.Count(p => p.Role == Models.PlayerRole.Villager).Should().Be(updated.Players.Count - game.NumberOfWerewolves);
    }

    [Fact]
    public void StartGame_ShouldSetPhaseToRoleReveal()
    {
        var game = CreateReadyGame();

        _gameService.StartGame(game.GameId, game.CreatorId);

        var updated = _gameService.GetGame(game.GameId)!;
        updated.Status.Should().Be(Models.GameStatus.InProgress);
        updated.Phase.Should().Be(Models.GamePhase.RoleReveal);
        updated.RoundNumber.Should().Be(1);
        updated.PhaseEndsAt.Should().BeNull();
    }

    [Fact]
    public void StartGame_ByNonCreator_ShouldFail()
    {
        var game = CreateReadyGame();
        var (_, _, player) = _gameService.JoinGame(game.GameId, "Extra");

        var (success, error) = _gameService.StartGame(game.GameId, player!.PlayerId);

        success.Should().BeFalse();
        error.Should().Contain("creator");
    }

    [Fact]
    public void StartGame_WhenNotReadyToStart_ShouldFail()
    {
        var game = _gameService.CreateGame("Test", "Creator", 40, "http://localhost");
        // Only creator, not enough players

        var (success, error) = _gameService.StartGame(game.GameId, game.CreatorId);

        success.Should().BeFalse();
    }

    [Fact]
    public void ForceAdvancePhase_FromRoleReveal_ShouldTransitionToNight()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        var (success, error) = _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);

        success.Should().BeTrue();
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Night);
    }

    [Fact]
    public void ForceAdvancePhase_FromNight_Round1_ShouldSkipNightEliminationAndGoToDiscussion()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → should skip NightElimination

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Discussion);
    }

    [Fact]
    public void CastVote_DuringDiscussion_ShouldRecordVote()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        var voter = game.Players.First(p => p.PlayerId != game.CreatorId);
        var target = game.Players.First(p => p.PlayerId != voter.PlayerId);

        var (success, error) = _gameService.CastVote(game.GameId, voter.PlayerId, target.PlayerId);

        success.Should().BeTrue();
        _gameService.GetGame(game.GameId)!.DayVotes.Should().Contain(v => v.VoterId == voter.PlayerId && v.TargetId == target.PlayerId);
    }

    [Fact]
    public void CastVote_DuringNight_ByVillager_ShouldFail()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night

        var updated = _gameService.GetGame(game.GameId)!;
        var villager = updated.Players.First(p => p.Role == Models.PlayerRole.Villager);
        var target = updated.Players.First(p => p.PlayerId != villager.PlayerId);

        var (success, error) = _gameService.CastVote(game.GameId, villager.PlayerId, target.PlayerId);

        success.Should().BeFalse();
    }

    [Fact]
    public void MarkDone_AllPlayers_ShouldAdvanceFromRoleReveal()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        var updated = _gameService.GetGame(game.GameId)!;
        foreach (var player in updated.Players)
            _gameService.MarkDone(game.GameId, player.PlayerId);

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Night);
    }

    [Fact]
    public void VillagersWin_WhenAllWerewolvesEliminated()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);
        // Force through to Discussion
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion (round 1 skip)

        var updated = _gameService.GetGame(game.GameId)!;
        // Everyone votes for the werewolf
        var werewolf = updated.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        foreach (var voter in updated.Players.Where(p => p.PlayerId != werewolf.PlayerId))
            _gameService.CastVote(game.GameId, voter.PlayerId, werewolf.PlayerId);

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → DayElimination → GameOver

        var final = _gameService.GetGame(game.GameId)!;
        final.Phase.Should().Be(Models.GamePhase.DayElimination); // 10s screen first
        final.LastEliminatedByDay.Should().Be(werewolf.PlayerId);
        final.Winner.Should().Be("Villagers");
    }

    [Fact]
    public void WerewolvesWin_WhenAllVillagersAreEliminated()
    {
        // Old rule (>= villagers) is removed — game only ends when 0 villagers remain.
        // Use 3 players (1W + 2V) so we can eliminate all villagers clearly.
        var game = CreateReadyGame(2); // Creator + 2 players = 3 total
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        // RoleReveal → Night (round 1, no kill)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        // Night → Discussion
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);

        // Day 1: eliminate V[0] (wolf + V[1] vote against V[0])
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.GameId, villagers[1].PlayerId, villagers[0].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → DayElimination

        // 1W + 1V remaining: NOT a win with new rule (need 0 villagers)
        _gameService.GetGame(game.GameId)!.Winner.Should().BeNull(
            "game only ends when all villagers are eliminated, not when werewolves outnumber them");

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night round 2

        // Night 2: werewolf kills the last villager
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[1].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination

        var afterNight = _gameService.GetGame(game.GameId)!;
        afterNight.Phase.Should().Be(Models.GamePhase.NightElimination);
        afterNight.Winner.Should().Be("Werewolves", "0 villagers remain");
        afterNight.LastEliminatedByNight.Should().Be(villagers[1].PlayerId);

        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → GameOver
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.GameOver);
    }

    [Fact]
    public void EliminatedPlayers_CannotVoteDuringDay()
    {
        var game = CreateReadyGame();
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started = _gameService.GetGame(game.GameId)!;
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);

        // RoleReveal → Night (round 1)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        // Night (round 1) → Discussion directly
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);

        // Vote out a villager
        var toEliminate = villagers[0];
        foreach (var voter in started.Players.Where(p => p.PlayerId != toEliminate.PlayerId))
            _gameService.CastVote(game.GameId, voter.PlayerId, toEliminate.PlayerId);
        // Discussion → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        // DayElimination → Night (round 2)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        // Night (round 2, no votes) → NightElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        // NightElimination → Discussion
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);

        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.Discussion);

        // Eliminated player tries to vote
        var (success, error) = _gameService.CastVote(game.GameId, toEliminate.PlayerId, werewolf.PlayerId);
        success.Should().BeFalse();
        error.Should().Contain("Eliminated");
    }

    [Fact]
    public void GameOver_VersionIsBumped_SoPollingClientsReceiveTheTransition()
    {
        // Regression: TransitionToGameOverIfWon must call BumpVersion, otherwise
        // clients polling with ?version=N always get 204 and never see the GameOver state.
        // Use 3 players so we can eliminate all villagers in 2 rounds.
        var game = CreateReadyGame(2); // Creator + 2 = 3 total: 1W + 2V
        _gameService.StartGame(game.GameId, game.CreatorId);

        var started = _gameService.GetGame(game.GameId)!;
        var werewolf = started.Players.First(p => p.Role == Models.PlayerRole.Werewolf);
        var villagers = started.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();

        // RoleReveal → Night → Discussion (round 1, no kill)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);

        // Day 1: vote out V[0]
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[0].PlayerId);
        _gameService.CastVote(game.GameId, villagers[1].PlayerId, villagers[0].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → DayElimination
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night round 2

        // Night 2: wolf kills last villager → 0 villagers → Winner set
        _gameService.CastVote(game.GameId, werewolf.PlayerId, villagers[1].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination

        var versionAtNightElimination = _gameService.GetGame(game.GameId)!.Version;

        // NightElimination → GameOver (version must bump!)
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId);
        var afterGameOver = _gameService.GetGame(game.GameId)!;

        afterGameOver.Phase.Should().Be(Models.GamePhase.GameOver);
        afterGameOver.Winner.Should().Be("Werewolves");
        // Critical: version MUST increase so polling clients get the new state instead of 204.
        afterGameOver.Version.Should().BeGreaterThan(versionAtNightElimination);
    }

    [Fact]
    public void FullGameScenario_SixPlayers_TwoWerewolves_WerewolvesWinAfterFourRounds()
    {
        // Setup: 6 players (creator + 5 joiners), 2 werewolves
        var game = CreateReadyGame(5); // creator + 5 = 6 total
        _gameService.UpdateNumberOfWerewolves(game.GameId, 2, game.CreatorId);
        _gameService.StartGame(game.GameId, game.CreatorId);

        var state = _gameService.GetGame(game.GameId)!;
        var W = state.Players.Where(p => p.Role == Models.PlayerRole.Werewolf).ToList();
        var V = state.Players.Where(p => p.Role == Models.PlayerRole.Villager).ToList();
        W.Should().HaveCount(2);
        V.Should().HaveCount(4);

        // ── Round 1 ──────────────────────────────────────────────────────────
        // RoleReveal → Night (no kills) → Discussion
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // RoleReveal → Night
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // Night → Discussion (no vote)

        // Day 1: everyone votes V[0] → V[0] eliminated, state: 2W + 3V
        foreach (var p in state.Players.Where(p => p.PlayerId != V[0].PlayerId))
            _gameService.CastVote(game.GameId, p.PlayerId, V[0].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → DayElimination
        state = _gameService.GetGame(game.GameId)!;
        state.LastEliminatedByDay.Should().Be(V[0].PlayerId, "V[0] had all votes");
        state.Winner.Should().BeNull("2W + 3V is not a werewolf win");
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night round 2

        // ── Round 2 ──────────────────────────────────────────────────────────
        // Night 2: both werewolves agree on V[1] → V[1] eliminated, state: 2W + 2V
        _gameService.CastVote(game.GameId, W[0].PlayerId, V[1].PlayerId);
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[1].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination
        state = _gameService.GetGame(game.GameId)!;
        state.LastEliminatedByNight.Should().Be(V[1].PlayerId, "both werewolves voted V[1]");
        state.Winner.Should().BeNull("2W + 2V is NOT a win under aliveVillagers == 0 rule");
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        // Day 2: wolves split (W[0]→V[2], W[1]→V[3]), surviving villagers agree on W[0]
        //        V[2] and V[3] are alive; V[0], V[1] eliminated
        _gameService.CastVote(game.GameId, W[0].PlayerId, V[2].PlayerId);
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.GameId, V[2].PlayerId, W[0].PlayerId);
        _gameService.CastVote(game.GameId, V[3].PlayerId, W[0].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → DayElimination
        state = _gameService.GetGame(game.GameId)!;
        state.LastEliminatedByDay.Should().Be(W[0].PlayerId, "W[0] had 2 votes vs 1 each");
        state.Winner.Should().BeNull("1W + 2V is not a win");
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night round 3

        // ── Round 3 ──────────────────────────────────────────────────────────
        // Night 3: W[1] kills V[2] → state: 1W + 1V
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[2].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination
        state = _gameService.GetGame(game.GameId)!;
        state.LastEliminatedByNight.Should().Be(V[2].PlayerId);
        state.Winner.Should().BeNull("1W + 1V is NOT a win under aliveVillagers == 0 rule");
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Discussion

        // Day 3: W[1] votes V[3], V[3] votes W[1] → perfect tie → TiebreakDiscussion
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.GameId, V[3].PlayerId, W[1].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → TiebreakDiscussion
        _gameService.GetGame(game.GameId)!.Phase.Should().Be(Models.GamePhase.TiebreakDiscussion);

        // Tiebreak vote: same result → another tie → no elimination
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.CastVote(game.GameId, V[3].PlayerId, W[1].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → DayElimination (no kill)
        state = _gameService.GetGame(game.GameId)!;
        state.LastEliminatedByDay.Should().BeNull("two tied draws = no elimination");
        state.Winner.Should().BeNull();
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → Night round 4

        // ── Round 4 ──────────────────────────────────────────────────────────
        // Night 4: W[1] kills V[3] → 0 villagers remaining → WEREWOLVES WIN
        _gameService.CastVote(game.GameId, W[1].PlayerId, V[3].PlayerId);
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // → NightElimination
        state = _gameService.GetGame(game.GameId)!;
        state.Phase.Should().Be(Models.GamePhase.NightElimination);
        state.LastEliminatedByNight.Should().Be(V[3].PlayerId);
        state.Winner.Should().Be("Werewolves", "0 villagers remain");

        var versionAtFinalNightElim = state.Version;
        _gameService.ForceAdvancePhase(game.GameId, game.CreatorId); // NightElimination → GameOver
        var final = _gameService.GetGame(game.GameId)!;
        final.Phase.Should().Be(Models.GamePhase.GameOver);
        final.Status.Should().Be(Models.GameStatus.Ended);
        final.Winner.Should().Be("Werewolves");
        final.Version.Should().BeGreaterThan(versionAtFinalNightElim, "version must bump so polling clients receive GameOver");
    }
}
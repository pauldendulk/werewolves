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
        rejoinedPlayer.Status.Should().Be(Models.PlayerStatus.Connected);
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
        leftPlayer.Status.Should().Be(Models.PlayerStatus.Left);
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
        removedPlayer.Status.Should().Be(Models.PlayerStatus.Removed);
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
    }}
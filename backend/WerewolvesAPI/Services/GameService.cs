using System.Collections.Concurrent;
using QRCoder;
using WerewolvesAPI.Models;

namespace WerewolvesAPI.Services;

public class GameService
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    private readonly ILogger<GameService> _logger;

    public GameService(ILogger<GameService> logger)
    {
        _logger = logger;
    }

    public GameState CreateGame(string gameName, string creatorName, int maxPlayers, string baseUrl)
    {
        var gameId = Guid.NewGuid().ToString("N")[..8];
        var playerId = Guid.NewGuid().ToString();
        var joinLink = $"{baseUrl}/game/{gameId}";

        var game = new GameState
        {
            GameId = gameId,
            GameName = gameName,
            CreatorId = playerId,
            MaxPlayers = maxPlayers,
            JoinLink = joinLink,
            QrCodeUrl = GenerateQrCode(joinLink)
        };

        var creator = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = creatorName,
            IsCreator = true,
            IsModerator = true,
            IsConnected = true
        };

        game.Players.Add(creator);
        RecalculateGameState(game);
        _games[gameId] = game;

        _logger.LogInformation("Game created: {GameId} by {CreatorName}", gameId, creatorName);
        return game;
    }

    public GameState? GetGame(string gameId)
    {
        _games.TryGetValue(gameId, out var game);
        return game;
    }

    public (bool Success, string? Message, PlayerState? Player) JoinGame(string gameId, string displayName, string? existingPlayerId = null)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return (false, "Game not found", null);
        }

        // Check if returning player
        if (!string.IsNullOrEmpty(existingPlayerId))
        {
            var existingPlayer = game.Players.FirstOrDefault(p => p.PlayerId == existingPlayerId);
            if (existingPlayer != null)
            {
                // Rejoin logic
                if (existingPlayer.ParticipationStatus == ParticipationStatus.Removed)
                {
                    return (false, "You were removed from this game", null);
                }

                existingPlayer.IsConnected = true;
                existingPlayer.ParticipationStatus = ParticipationStatus.Participating;
                RecalculateGameState(game);
                BumpVersion(game);
                _logger.LogInformation("Player rejoined: {PlayerId} in game {GameId}", existingPlayerId, gameId);
                return (true, "Rejoined successfully", existingPlayer);
            }
        }

        // Check if game is full
        var activePlayers = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        if (activePlayers >= game.MaxPlayers)
        {
            return (false, $"Game is full ({activePlayers}/{game.MaxPlayers})", null);
        }

        // Create new player
        var playerId = Guid.NewGuid().ToString();
        var player = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = displayName,
            IsCreator = false,
            IsModerator = false,
            IsConnected = true
        };

        game.Players.Add(player);
        RecalculateGameState(game);
        BumpVersion(game);
        _logger.LogInformation("Player joined: {PlayerId} ({DisplayName}) in game {GameId}", playerId, displayName, gameId);
        return (true, null, player);
    }

    public bool LeaveGame(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player != null)
        {
            player.ParticipationStatus = ParticipationStatus.Left;
            player.IsConnected = false;
            RecalculateGameState(game);
            BumpVersion(game);
            _logger.LogInformation("Player left: {PlayerId} from game {GameId}", playerId, gameId);
            return true;
        }

        return false;
    }

    public bool RemovePlayer(string gameId, string playerId, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        var moderator = game.Players.FirstOrDefault(p => p.PlayerId == moderatorId && p.IsModerator);
        if (moderator == null)
        {
            return false;
        }

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player != null && !player.IsCreator)
        {
            player.ParticipationStatus = ParticipationStatus.Removed;
            player.IsConnected = false;
            RecalculateGameState(game);
            BumpVersion(game);
            _logger.LogInformation("Player removed: {PlayerId} from game {GameId} by {ModeratorId}", playerId, gameId, moderatorId);
            return true;
        }

        return false;
    }

    public bool UpdateMaxPlayers(string gameId, int maxPlayers, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        if (game.CreatorId != creatorId)
        {
            return false;
        }

        game.MaxPlayers = maxPlayers;
        RecalculateGameState(game);
        BumpVersion(game);
        _logger.LogInformation("Max players updated to {MaxPlayers} in game {GameId}", maxPlayers, gameId);
        return true;
    }

    public bool UpdateMinPlayers(string gameId, int minPlayers, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        if (game.CreatorId != creatorId)
        {
            return false;
        }

        game.MinPlayers = minPlayers;
        RecalculateGameState(game);
        BumpVersion(game);
        _logger.LogInformation("Min players updated to {MinPlayers} in game {GameId}", minPlayers, gameId);
        return true;
    }

    public bool UpdateGameName(string gameId, string gameName, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        if (game.CreatorId != creatorId)
        {
            return false;
        }

        game.GameName = gameName;
        BumpVersion(game);
        _logger.LogInformation("Game name updated to {GameName} in game {GameId}", gameName, gameId);
        return true;
    }

    public bool UpdatePlayerName(string gameId, string playerId, string displayName)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        // Only allow name changes before game starts
        if (game.Status == GameStatus.InProgress || game.Status == GameStatus.Ended)
        {
            return false;
        }

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player != null)
        {
            player.DisplayName = displayName;
            BumpVersion(game);
            _logger.LogInformation("Player {PlayerId} name updated to {DisplayName} in game {GameId}", playerId, displayName, gameId);
            return true;
        }

        return false;
    }

    public bool HasDuplicateNames(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        var activePlayerNames = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating)
            .Select(p => p.DisplayName)
            .ToList();

        return activePlayerNames.Count != activePlayerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    public bool UpdateDiscussionDuration(string gameId, int minutes, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return false;

        if (game.CreatorId != creatorId)
            return false;

        game.DiscussionDurationMinutes = minutes;
        BumpVersion(game);
        _logger.LogInformation("Discussion duration updated to {Minutes} in game {GameId}", minutes, gameId);
        return true;
    }

    public bool UpdateNumberOfWerewolves(string gameId, int count, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return false;

        if (game.CreatorId != creatorId)
            return false;

        var activeCount = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        if (count < 1 || count >= activeCount)
            return false;

        game.NumberOfWerewolves = count;
        BumpVersion(game);
        _logger.LogInformation("Number of werewolves updated to {Count} in game {GameId}", count, gameId);
        return true;
    }

    private static void RecalculateGameState(GameState game)
    {
        if (game.Status != GameStatus.WaitingForPlayers && game.Status != GameStatus.ReadyToStart)
            return;

        var activeCount = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        game.Status = activeCount >= game.MinPlayers ? GameStatus.ReadyToStart : GameStatus.WaitingForPlayers;
    }

    private static void BumpVersion(GameState game) => game.Version++;

    private string GenerateQrCode(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        // Parameters: pixelsPerModule, darkColor, lightColor, drawQuietZones
        var qrCodeBytes = qrCode.GetGraphic(20, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 }, false);
        return Convert.ToBase64String(qrCodeBytes);
    }
}

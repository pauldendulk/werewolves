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
            Status = PlayerStatus.Connected
        };

        game.Players.Add(creator);
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
                if (existingPlayer.Status == PlayerStatus.Removed)
                {
                    return (false, "You were removed from this game", null);
                }
                
                existingPlayer.Status = PlayerStatus.Connected;
                _logger.LogInformation("Player rejoined: {PlayerId} in game {GameId}", existingPlayerId, gameId);
                return (true, "Rejoined successfully", existingPlayer);
            }
        }

        // Check if game is full
        var activePlayers = game.Players.Count(p => p.Status != PlayerStatus.Left && p.Status != PlayerStatus.Removed);
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
            Status = PlayerStatus.Connected
        };

        game.Players.Add(player);
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
            player.Status = PlayerStatus.Left;
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
            player.Status = PlayerStatus.Removed;
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
        if (game.Status != GameStatus.Lobby)
        {
            return false;
        }

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player != null)
        {
            player.DisplayName = displayName;
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
            .Where(p => p.Status == PlayerStatus.Connected || p.Status == PlayerStatus.Disconnected)
            .Select(p => p.DisplayName)
            .ToList();

        return activePlayerNames.Count != activePlayerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    public bool UpdatePlayerConnection(string gameId, string playerId, string connectionId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player != null)
        {
            player.ConnectionId = connectionId;
            player.Status = PlayerStatus.Connected;
            return true;
        }

        return false;
    }

    public bool DisconnectPlayer(string gameId, string connectionId)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return false;
        }

        var player = game.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player != null && player.Status == PlayerStatus.Connected)
        {
            player.Status = PlayerStatus.Disconnected;
            _logger.LogInformation("Player disconnected: {PlayerId} from game {GameId}", player.PlayerId, gameId);
            return true;
        }

        return false;
    }

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

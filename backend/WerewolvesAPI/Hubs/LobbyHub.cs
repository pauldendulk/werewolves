using Microsoft.AspNetCore.SignalR;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Hubs;

public class LobbyHub : Hub
{
    private readonly GameService _gameService;
    private readonly ILogger<LobbyHub> _logger;

    public LobbyHub(GameService gameService, ILogger<LobbyHub> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public async Task JoinLobby(string gameId, string playerId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        _gameService.UpdatePlayerConnection(gameId, playerId, Context.ConnectionId);
        
        var game = _gameService.GetGame(gameId);
        if (game != null)
        {
            await Clients.Group(gameId).SendAsync("PlayerJoined", game.Players);
            await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
        }

        _logger.LogInformation("Player {PlayerId} joined lobby for game {GameId}", playerId, gameId);
    }

    public async Task LeaveLobby(string gameId, string playerId)
    {
        _gameService.LeaveGame(gameId, playerId);
        
        var game = _gameService.GetGame(gameId);
        if (game != null)
        {
            await Clients.Group(gameId).SendAsync("PlayerLeft", playerId);
            await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
        _logger.LogInformation("Player {PlayerId} left lobby for game {GameId}", playerId, gameId);
    }

    public async Task UpdateMaxPlayers(string gameId, int maxPlayers, string creatorId)
    {
        if (_gameService.UpdateMaxPlayers(gameId, maxPlayers, creatorId))
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                await Clients.Group(gameId).SendAsync("MaxPlayersUpdated", maxPlayers);
                await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
            }
        }
    }

    public async Task UpdateMinPlayers(string gameId, int minPlayers, string creatorId)
    {
        if (_gameService.UpdateMinPlayers(gameId, minPlayers, creatorId))
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                await Clients.Group(gameId).SendAsync("MinPlayersUpdated", minPlayers);
                await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
            }
        }
    }

    public async Task UpdateGameName(string gameId, string gameName, string creatorId)
    {
        if (_gameService.UpdateGameName(gameId, gameName, creatorId))
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                await Clients.Group(gameId).SendAsync("GameNameUpdated", gameName);
                await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
            }
        }
    }

    public async Task RemovePlayer(string gameId, string playerId, string moderatorId)
    {
        if (_gameService.RemovePlayer(gameId, playerId, moderatorId))
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                await Clients.Group(gameId).SendAsync("PlayerRemoved", playerId);
                await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
            }
        }
    }

    public async Task UpdatePlayerName(string gameId, string playerId, string displayName)
    {
        if (_gameService.UpdatePlayerName(gameId, playerId, displayName))
        {
            var game = _gameService.GetGame(gameId);
            if (game != null)
            {
                await Clients.Group(gameId).SendAsync("LobbyUpdated", game);
            }
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Find which game this connection belongs to and mark player as disconnected
        _logger.LogInformation("Connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

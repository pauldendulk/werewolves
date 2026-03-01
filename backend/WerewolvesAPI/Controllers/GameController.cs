using Microsoft.AspNetCore.Mvc;
using WerewolvesAPI.DTOs;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly GameService _gameService;
    private readonly ILogger<GameController> _logger;

    public GameController(GameService gameService, ILogger<GameController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    [HttpPost("create")]
    public ActionResult<CreateGameResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var game = _gameService.CreateGame(request.GameName, request.CreatorName, request.MaxPlayers, request.FrontendBaseUrl);

        var response = new CreateGameResponse
        {
            GameId = game.GameId,
            PlayerId = game.CreatorId,
            JoinLink = game.JoinLink,
            QrCodeBase64 = game.QrCodeUrl
        };

        return Ok(response);
    }

    [HttpPost("{gameId}/join")]
    public ActionResult<JoinGameResponse> JoinGame(string gameId, [FromBody] JoinGameRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var (success, message, player) = _gameService.JoinGame(gameId, request.DisplayName, request.PlayerId);

        if (!success)
        {
            return BadRequest(new JoinGameResponse
            {
                Success = false,
                Message = message
            });
        }

        return Ok(new JoinGameResponse
        {
            PlayerId = player!.PlayerId,
            Success = true,
            Message = message
        });
    }

    [HttpGet("{gameId}")]
    public ActionResult<LobbyStateDto> GetGameState(string gameId, [FromQuery] int? version = null)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null)
        {
            return NotFound(new { message = "Game not found" });
        }

        if (version.HasValue && version.Value == game.Version)
        {
            return NoContent();
        }

        var creatorPlayer = game.Players.FirstOrDefault(p => p.PlayerId == game.CreatorId);
        var lobbyState = new LobbyStateDto
        {
            Game = new GameInfoDto
            {
                GameId = game.GameId,
                GameName = game.GameName,
                CreatorId = game.CreatorId,
                CreatorName = creatorPlayer?.DisplayName ?? "Unknown",
                MinPlayers = game.MinPlayers,
                MaxPlayers = game.MaxPlayers,
                JoinLink = game.JoinLink,
                QrCodeBase64 = game.QrCodeUrl,
                Status = game.Status.ToString(),
                Version = game.Version
            },
            Players = game.Players.Select(p => new PlayerDto
            {
                PlayerId = p.PlayerId,
                DisplayName = p.DisplayName,
                IsCreator = p.IsCreator,
                IsModerator = p.IsModerator,
                Status = p.Status.ToString(),
                JoinedAt = p.JoinedAt
            }).ToList(),
            HasDuplicateNames = _gameService.HasDuplicateNames(gameId)
        };

        return Ok(lobbyState);
    }

    [HttpPost("{gameId}/leave")]
    public ActionResult LeaveGame(string gameId, [FromBody] LeaveGameRequest request)
    {
        if (_gameService.LeaveGame(gameId, request.PlayerId))
        {
            return Ok();
        }

        return NotFound(new { message = "Game or player not found" });
    }

    [HttpPost("{gameId}/remove")]
    public ActionResult RemovePlayer(string gameId, [FromBody] RemovePlayerRequest request)
    {
        if (_gameService.RemovePlayer(gameId, request.PlayerId, request.ModeratorId))
        {
            return Ok();
        }

        return Unauthorized(new { message = "Not authorized to remove player" });
    }

    [HttpPost("{gameId}/settings")]
    public ActionResult UpdateSettings(string gameId, [FromBody] UpdateSettingsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        bool maxUpdated = _gameService.UpdateMaxPlayers(gameId, request.MaxPlayers, request.CreatorId);
        bool minUpdated = _gameService.UpdateMinPlayers(gameId, request.MinPlayers, request.CreatorId);

        if (maxUpdated || minUpdated)
        {
            return Ok();
        }

        return Unauthorized(new { message = "Only the creator can update settings" });
    }

    [HttpPost("{gameId}/name")]
    public ActionResult UpdateGameName(string gameId, [FromBody] UpdateGameNameRequest request)
    {
        if (_gameService.UpdateGameName(gameId, request.GameName, request.CreatorId))
        {
            return Ok();
        }

        return Unauthorized(new { message = "Only the creator can update the game name" });
    }

    [HttpPost("{gameId}/player-name")]
    public ActionResult UpdatePlayerName(string gameId, [FromBody] UpdatePlayerNameRequest request)
    {
        if (_gameService.UpdatePlayerName(gameId, request.PlayerId, request.DisplayName))
        {
            return Ok();
        }

        return NotFound(new { message = "Game or player not found" });
    }
}

public class LeaveGameRequest
{
    public string PlayerId { get; set; } = string.Empty;
}

public class RemovePlayerRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string ModeratorId { get; set; } = string.Empty;
}

public class UpdateSettingsRequest
{
    public string CreatorId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(2, 40)]
    public int MinPlayers { get; set; }

    [System.ComponentModel.DataAnnotations.Range(2, 40)]
    public int MaxPlayers { get; set; }
}

public class UpdateGameNameRequest
{
    public string CreatorId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
}

public class UpdatePlayerNameRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

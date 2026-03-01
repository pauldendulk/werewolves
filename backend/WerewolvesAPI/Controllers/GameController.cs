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
        // Lazy phase advancement on poll
        _gameService.TryAdvancePhaseIfExpired(gameId);

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
        var playerLookup = game.Players.ToDictionary(p => p.PlayerId, p => p.DisplayName);

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
                Version = game.Version,
                DiscussionDurationMinutes = game.DiscussionDurationMinutes,
                NumberOfWerewolves = game.NumberOfWerewolves,
                Phase = game.Phase.ToString(),
                RoundNumber = game.RoundNumber,
                PhaseEndsAt = game.PhaseEndsAt,
                LastEliminatedByNight = game.LastEliminatedByNight,
                LastEliminatedByNightName = game.LastEliminatedByNight != null && playerLookup.TryGetValue(game.LastEliminatedByNight, out var nvName) ? nvName : null,
                LastEliminatedByDay = game.LastEliminatedByDay,
                LastEliminatedByDayName = game.LastEliminatedByDay != null && playerLookup.TryGetValue(game.LastEliminatedByDay, out var dvName) ? dvName : null,
                Winner = game.Winner,
                TiebreakCandidates = game.TiebreakCandidates
            },
            Players = game.Players.Select(p => new PlayerDto
            {
                PlayerId = p.PlayerId,
                DisplayName = p.DisplayName,
                IsCreator = p.IsCreator,
                IsModerator = p.IsModerator,
                IsConnected = p.IsConnected,
                ParticipationStatus = p.ParticipationStatus.ToString(),
                Role = p.Role?.ToString(),
                IsEliminated = p.IsEliminated,
                IsDone = p.IsDone,
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
        bool durationUpdated = _gameService.UpdateDiscussionDuration(gameId, request.DiscussionDurationMinutes, request.CreatorId);
        bool werewolvesUpdated = _gameService.UpdateNumberOfWerewolves(gameId, request.NumberOfWerewolves, request.CreatorId);

        if (maxUpdated || minUpdated || durationUpdated || werewolvesUpdated)
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

    [HttpPost("{gameId}/start")]
    public ActionResult StartGame(string gameId, [FromBody] StartGameRequest request)
    {
        var (success, error) = _gameService.StartGame(gameId, request.CreatorId);
        if (!success)
            return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/done")]
    public ActionResult MarkDone(string gameId, [FromBody] PlayerActionRequest request)
    {
        var (success, error) = _gameService.MarkDone(gameId, request.PlayerId);
        if (!success)
            return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/vote")]
    public ActionResult CastVote(string gameId, [FromBody] VoteRequest request)
    {
        var (success, error) = _gameService.CastVote(gameId, request.VoterId, request.TargetId);
        if (!success)
            return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/force-advance")]
    public ActionResult ForceAdvancePhase(string gameId, [FromBody] PlayerActionRequest request)
    {
        var (success, error) = _gameService.ForceAdvancePhase(gameId, request.PlayerId);
        if (!success)
            return BadRequest(new { message = error });
        return Ok();
    }

    [HttpGet("{gameId}/role")]
    public ActionResult<PlayerRoleDto> GetRole(string gameId, [FromQuery] string playerId)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null)
            return NotFound(new { message = "Game not found" });
        if (game.Status != WerewolvesAPI.Models.GameStatus.InProgress && game.Status != WerewolvesAPI.Models.GameStatus.Ended)
            return BadRequest(new { message = "Game has not started" });

        var (role, fellows) = _gameService.GetPlayerRole(gameId, playerId);
        return Ok(new PlayerRoleDto { Role = role, FellowWerewolves = fellows });
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

    [System.ComponentModel.DataAnnotations.Range(1, 30)]
    public int DiscussionDurationMinutes { get; set; } = 5;

    [System.ComponentModel.DataAnnotations.Range(1, 40)]
    public int NumberOfWerewolves { get; set; } = 1;
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

public class StartGameRequest
{
    public string CreatorId { get; set; } = string.Empty;
}

public class PlayerActionRequest
{
    public string PlayerId { get; set; } = string.Empty;
}

public class VoteRequest
{
    public string VoterId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
}

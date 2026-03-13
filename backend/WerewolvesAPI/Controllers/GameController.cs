using Microsoft.AspNetCore.Mvc;
using WerewolvesAPI.DTOs;
using WerewolvesAPI.Models;
using WerewolvesAPI.Services;

namespace WerewolvesAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly ILogger<GameController> _logger;

    public GameController(IGameService gameService, ILogger<GameController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    [HttpPost("create")]
    public ActionResult<CreateGameResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var game = _gameService.CreateGame(request.GameName, request.CreatorName, request.MaxPlayers, request.FrontendBaseUrl);

        return Ok(new CreateGameResponse
        {
            GameId = game.GameId,
            PlayerId = game.CreatorId,
            JoinLink = game.JoinLink,
            QrCodeBase64 = game.QrCodeUrl
        });
    }

    [HttpPost("{gameId}/join")]
    public ActionResult<JoinGameResponse> JoinGame(string gameId, [FromBody] JoinGameRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (success, message, player) = _gameService.JoinGame(gameId, request.DisplayName, request.PlayerId);

        if (!success)
            return BadRequest(new JoinGameResponse { Success = false, Message = message });

        return Ok(new JoinGameResponse { PlayerId = player!.PlayerId, Success = true, Message = message });
    }

    [HttpGet("{gameId}")]
    public ActionResult<LobbyStateDto> GetGameState(string gameId, [FromQuery] int? version = null)
    {
        _gameService.TryAdvancePhaseIfExpired(gameId);

        var game = _gameService.GetGame(gameId);
        if (game == null) return NotFound(new { message = "Game not found" });

        if (version.HasValue && version.Value == game.Version)
            return NoContent();

        var hasDuplicateNames = _gameService.HasDuplicateNames(gameId);
        return Ok(MapToLobbyStateDto(game, hasDuplicateNames));
    }

    private static LobbyStateDto MapToLobbyStateDto(GameState game, bool hasDuplicateNames)
    {
        return new LobbyStateDto
        {
            Game = new GameInfoDto
            {
                GameId = game.GameId,
                GameName = game.GameName,
                CreatorId = game.CreatorId,
                MinPlayers = game.MinPlayers,
                MaxPlayers = game.MaxPlayers,
                JoinLink = game.JoinLink,
                QrCodeBase64 = game.QrCodeUrl,
                Status = game.Status.ToString(),
                Version = game.Version,
                DiscussionDurationMinutes = game.DiscussionDurationMinutes,
                NumberOfWerewolves = game.NumberOfWerewolves,
                EnabledSkills = game.EnabledSkills.Select(s => s.ToString()).ToList(),
                Phase = game.Phase.ToString(),
                RoundNumber = game.RoundNumber,
                PhaseEndsAt = game.PhaseEndsAt,
                PhaseStartedAt = game.PhaseStartedAt,
                NightDeaths = game.NightDeaths.Select(e => new EliminationEntryDto
                {
                    PlayerId = e.PlayerId,
                    PlayerName = e.PlayerName,
                    Cause = e.Cause.ToString()
                }).ToList(),
                DayDeaths = game.DayDeaths.Select(e => new EliminationEntryDto
                {
                    PlayerId = e.PlayerId,
                    PlayerName = e.PlayerName,
                    Cause = e.Cause.ToString()
                }).ToList(),
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
                Skill = p.Skill == PlayerSkill.None ? null : p.Skill.ToString(),
                IsEliminated = p.IsEliminated,
                IsDone = p.IsDone,
                JoinedAt = p.JoinedAt
            }).ToList(),
            HasDuplicateNames = hasDuplicateNames
        };
    }

    [HttpPost("{gameId}/leave")]
    public ActionResult LeaveGame(string gameId, [FromBody] LeaveGameRequest request)
    {
        if (_gameService.LeaveGame(gameId, request.PlayerId)) return Ok();
        return NotFound(new { message = "Game or player not found" });
    }

    [HttpPost("{gameId}/remove")]
    public ActionResult RemovePlayer(string gameId, [FromBody] RemovePlayerRequest request)
    {
        if (_gameService.RemovePlayer(gameId, request.PlayerId, request.ModeratorId)) return Ok();
        return Unauthorized(new { message = "Not authorized to remove player" });
    }

    [HttpPost("{gameId}/settings")]
    public ActionResult UpdateSettings(string gameId, [FromBody] UpdateSettingsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        bool maxUpdated      = _gameService.UpdateMaxPlayers(gameId, request.MaxPlayers, request.CreatorId);
        bool minUpdated      = _gameService.UpdateMinPlayers(gameId, request.MinPlayers, request.CreatorId);
        bool durationUpdated = _gameService.UpdateDiscussionDuration(gameId, request.DiscussionDurationMinutes, request.CreatorId);
        bool wolvesUpdated   = _gameService.UpdateNumberOfWerewolves(gameId, request.NumberOfWerewolves, request.CreatorId);
        bool skillsUpdated   = false;
        if (request.EnabledSkills != null)
            skillsUpdated = _gameService.UpdateEnabledSkills(gameId, request.EnabledSkills, request.CreatorId);

        if (maxUpdated || minUpdated || durationUpdated || wolvesUpdated || skillsUpdated)
            return Ok();

        return Unauthorized(new { message = "Only the creator can update settings" });
    }

    [HttpPost("{gameId}/name")]
    public ActionResult UpdateGameName(string gameId, [FromBody] UpdateGameNameRequest request)
    {
        if (_gameService.UpdateGameName(gameId, request.GameName, request.CreatorId)) return Ok();
        return Unauthorized(new { message = "Only the creator can update the game name" });
    }

    [HttpPost("{gameId}/player-name")]
    public ActionResult UpdatePlayerName(string gameId, [FromBody] UpdatePlayerNameRequest request)
    {
        if (_gameService.UpdatePlayerName(gameId, request.PlayerId, request.DisplayName)) return Ok();
        return NotFound(new { message = "Game or player not found" });
    }

    [HttpPost("{gameId}/start")]
    public ActionResult StartGame(string gameId, [FromBody] StartGameRequest request)
    {
        var (success, error) = _gameService.StartGame(gameId, request.CreatorId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/done")]
    public ActionResult MarkDone(string gameId, [FromBody] PlayerActionRequest request)
    {
        var (success, error) = _gameService.MarkDone(gameId, request.PlayerId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/vote")]
    public ActionResult CastVote(string gameId, [FromBody] VoteRequest request)
    {
        var (success, error) = _gameService.CastVote(gameId, request.VoterId, request.TargetId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/cupid-action")]
    public ActionResult CupidAction(string gameId, [FromBody] CupidActionRequest request)
    {
        var (success, error) = _gameService.CupidAction(gameId, request.PlayerId, request.Lover1Id, request.Lover2Id);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpGet("{gameId}/seer-action")]
    public ActionResult<SeerActionResponse> SeerAction(string gameId, [FromQuery] string seerId, [FromQuery] string targetId)
    {
        var (success, error, result) = _gameService.SeerAction(gameId, seerId, targetId);
        if (!success) return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("{gameId}/witch-action")]
    public ActionResult WitchAction(string gameId, [FromBody] WitchActionRequest request)
    {
        var (success, error) = _gameService.WitchAction(gameId, request.PlayerId, request.Choice, request.PoisonTargetId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/hunter-action")]
    public ActionResult HunterAction(string gameId, [FromBody] HunterActionRequest request)
    {
        var (success, error) = _gameService.HunterAction(gameId, request.PlayerId, request.TargetId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{gameId}/force-advance")]
    public ActionResult ForceAdvancePhase(string gameId, [FromBody] PlayerActionRequest request)
    {
        var (success, error) = _gameService.ForceAdvancePhase(gameId, request.PlayerId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpGet("{gameId}/role")]
    public ActionResult<PlayerRoleDto> GetRole(string gameId, [FromQuery] string playerId)
    {
        var game = _gameService.GetGame(gameId);
        if (game == null) return NotFound(new { message = "Game not found" });
        if (game.Status != GameStatus.InProgress && game.Status != GameStatus.Ended)
            return BadRequest(new { message = "Game has not started" });

        var (role, skill, fellows, loverName, nightKillTargetName, witchHealUsed, witchPoisonUsed) =
            _gameService.GetPlayerRole(gameId, playerId);

        return Ok(new PlayerRoleDto
        {
            Role = role,
            Skill = skill,
            FellowWerewolves = fellows,
            LoverName = loverName,
            NightKillTargetName = nightKillTargetName,
            WitchHealUsed = witchHealUsed,
            WitchPoisonUsed = witchPoisonUsed
        });
    }
}


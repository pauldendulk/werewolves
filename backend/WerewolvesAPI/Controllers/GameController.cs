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
    private readonly IStripeService _stripeService;
    private readonly ILogger<GameController> _logger;

    public GameController(IGameService gameService, IStripeService stripeService, ILogger<GameController> logger)
    {
        _gameService = gameService;
        _stripeService = stripeService;
        _logger = logger;
    }

    [HttpPost("create")]
    public ActionResult<CreateGameResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var game = _gameService.CreateGame(request.CreatorName, request.MaxPlayers, request.FrontendBaseUrl);

        return Ok(new CreateGameResponse
        {
            TournamentCode = game.TournamentCode,
            PlayerId = game.CreatorId,
            JoinLink = game.JoinLink,
            QrCodeBase64 = game.QrCodeUrl
        });
    }

    [HttpPost("{tournamentCode}/join")]
    public ActionResult<JoinGameResponse> JoinGame(string tournamentCode, [FromBody] JoinGameRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (success, message, player) = _gameService.JoinGame(tournamentCode, request.DisplayName, request.PlayerId);

        if (!success)
            return BadRequest(new JoinGameResponse { Success = false, Message = message });

        return Ok(new JoinGameResponse { PlayerId = player!.PlayerId, Success = true, Message = message });
    }

    [HttpGet("time")]
    public ActionResult GetServerTime()
    {
        return Ok(new { serverTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }

    [HttpGet("{tournamentCode}")]
    public ActionResult<LobbyStateDto> GetGameState(string tournamentCode, [FromQuery] int? version = null)
    {
        _gameService.TryAdvancePhaseIfExpired(tournamentCode);

        var game = _gameService.GetGame(tournamentCode);
        if (game == null) return NotFound(new { message = "Game not found" });

        if (version.HasValue && version.Value == game.Version)
            return NoContent();

        var hasDuplicateNames = _gameService.HasDuplicateNames(tournamentCode);
        return Ok(MapToLobbyStateDto(game, hasDuplicateNames));
    }

    private static LobbyStateDto MapToLobbyStateDto(GameState game, bool hasDuplicateNames)
    {
        return new LobbyStateDto
        {
            Game = new GameInfoDto
            {
                GameId = game.GameId,
                TournamentCode = game.TournamentCode,
                CreatorId = game.CreatorId,
                MinPlayers = game.MinPlayers,
                MaxPlayers = game.MaxPlayers,
                JoinLink = game.JoinLink,
                QrCodeBase64 = game.QrCodeUrl,
                Status = game.Status.ToString(),
                Version = game.Version,
                DiscussionDurationMinutes = game.DiscussionDurationMinutes,
                TiebreakDiscussionDurationSeconds = game.TiebreakDiscussionDurationSeconds,
                NumberOfWerewolves = game.NumberOfWerewolves,
                EnabledSkills = game.EnabledSkills.Select(s => s.ToString()).ToList(),
                Phase = game.Phase.ToString(),
                RoundNumber = game.RoundNumber,
                PhaseEndsAt = game.PhaseEndsAt,
                PhaseStartedAt = game.PhaseStartedAt,
                AudioPlayAt = game.AudioPlayAt,
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
                TiebreakCandidates = game.TiebreakCandidates,
                GameIndex = game.GameIndex,
                IsPremium = game.IsPremium
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
                Score = p.Score,
                TotalScore = p.TotalScore,
                JoinedAt = p.JoinedAt
            }).ToList(),
            HasDuplicateNames = hasDuplicateNames
        };
    }

    [HttpPost("{tournamentCode}/leave")]
    public ActionResult LeaveGame(string tournamentCode, [FromBody] LeaveGameRequest request)
    {
        if (_gameService.LeaveGame(tournamentCode, request.PlayerId)) return Ok();
        return NotFound(new { message = "Game or player not found" });
    }

    [HttpPost("{tournamentCode}/remove")]
    public ActionResult RemovePlayer(string tournamentCode, [FromBody] RemovePlayerRequest request)
    {
        if (_gameService.RemovePlayer(tournamentCode, request.PlayerId, request.ModeratorId)) return Ok();
        return Unauthorized(new { message = "Not authorized to remove player" });
    }

    [HttpPost("{tournamentCode}/settings")]
    public ActionResult UpdateSettings(string tournamentCode, [FromBody] UpdateSettingsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        bool maxUpdated      = _gameService.UpdateMaxPlayers(tournamentCode, request.MaxPlayers, request.CreatorId);
        bool minUpdated      = _gameService.UpdateMinPlayers(tournamentCode, request.MinPlayers, request.CreatorId);
        bool durationUpdated = _gameService.UpdateDiscussionDuration(tournamentCode, request.DiscussionDurationMinutes, request.CreatorId);
        bool tiebreakUpdated = _gameService.UpdateTiebreakDiscussionDuration(tournamentCode, request.TiebreakDiscussionDurationSeconds, request.CreatorId);
        bool wolvesUpdated   = _gameService.UpdateNumberOfWerewolves(tournamentCode, request.NumberOfWerewolves, request.CreatorId);
        bool skillsUpdated   = false;
        if (request.EnabledSkills != null)
            skillsUpdated = _gameService.UpdateEnabledSkills(tournamentCode, request.EnabledSkills, request.CreatorId);

        if (maxUpdated || minUpdated || durationUpdated || tiebreakUpdated || wolvesUpdated || skillsUpdated)
            return Ok();

        return Unauthorized(new { message = "Only the creator can update settings" });
    }

    [HttpPost("{tournamentCode}/player-name")]
    public ActionResult UpdatePlayerName(string tournamentCode, [FromBody] UpdatePlayerNameRequest request)
    {
        if (_gameService.UpdatePlayerName(tournamentCode, request.PlayerId, request.DisplayName)) return Ok();
        return NotFound(new { message = "Game or player not found" });
    }

    [HttpPost("{tournamentCode}/start")]
    public ActionResult StartGame(string tournamentCode, [FromBody] StartGameRequest request)
    {
        var (success, error) = _gameService.StartGame(tournamentCode, request.CreatorId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/done")]
    public ActionResult MarkDone(string tournamentCode, [FromBody] PlayerActionRequest request)
    {
        var (success, error) = _gameService.MarkDone(tournamentCode, request.PlayerId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/vote")]
    public ActionResult CastVote(string tournamentCode, [FromBody] VoteRequest request)
    {
        var (success, error) = _gameService.CastVote(tournamentCode, request.VoterId, request.TargetId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/cupid-action")]
    public ActionResult CupidAction(string tournamentCode, [FromBody] CupidActionRequest request)
    {
        var (success, error) = _gameService.CupidAction(tournamentCode, request.PlayerId, request.Lover1Id, request.Lover2Id);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpGet("{tournamentCode}/seer-action")]
    public ActionResult<SeerActionResponse> SeerAction(string tournamentCode, [FromQuery] string seerId, [FromQuery] string targetId)
    {
        var (success, error, result) = _gameService.SeerAction(tournamentCode, seerId, targetId);
        if (!success) return BadRequest(new { message = error });
        return Ok(result);
    }

    [HttpPost("{tournamentCode}/witch-action")]
    public ActionResult WitchAction(string tournamentCode, [FromBody] WitchActionRequest request)
    {
        var (success, error) = _gameService.WitchAction(tournamentCode, request.PlayerId, request.Choice, request.PoisonTargetId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/hunter-action")]
    public ActionResult HunterAction(string tournamentCode, [FromBody] HunterActionRequest request)
    {
        var (success, error) = _gameService.HunterAction(tournamentCode, request.PlayerId, request.TargetId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/force-advance")]
    public ActionResult ForceAdvancePhase(string tournamentCode, [FromBody] PlayerActionRequest request)
    {
        var (success, error) = _gameService.ForceAdvancePhase(tournamentCode, request.PlayerId);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/unlock")]
    public ActionResult UnlockTournament(string tournamentCode, [FromBody] UnlockTournamentRequest request)
    {
        var (success, error) = _gameService.UnlockTournament(tournamentCode, request.Code);
        if (!success) return BadRequest(new { message = error });
        return Ok();
    }

    [HttpPost("{tournamentCode}/checkout")]
    public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession(
        string tournamentCode, [FromBody] CreateCheckoutSessionRequest request)
    {
        var game = _gameService.GetGame(tournamentCode);
        if (game == null) return NotFound(new { message = "Game not found" });

        var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
            tournamentCode,
            request.SuccessUrl,
            request.CancelUrl);

        return Ok(new CreateCheckoutSessionResponse { CheckoutUrl = checkoutUrl });
    }

    [HttpGet("{tournamentCode}/role")]
    public ActionResult<PlayerRoleDto> GetRole(string tournamentCode, [FromQuery] string playerId)
    {
        var game = _gameService.GetGame(tournamentCode);
        if (game == null) return NotFound(new { message = "Game not found" });
        if (game.Status != GameStatus.InProgress && game.Status != GameStatus.Ended)
            return BadRequest(new { message = "Game has not started" });

        var (role, skill, fellows, loverName, nightKillTargetName, witchHealUsed, witchPoisonUsed) =
            _gameService.GetPlayerRole(tournamentCode, playerId);

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


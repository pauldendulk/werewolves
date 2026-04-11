using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using QRCoder;
using WerewolvesAPI.DTOs;
using WerewolvesAPI.Models;
using WerewolvesAPI.Repositories;

namespace WerewolvesAPI.Services;

public class GameService : IGameService
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    private readonly ConcurrentDictionary<string, object> _phaseLocks = new();
    private static readonly JsonSerializerOptions _jsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    private readonly ILogger<GameService> _logger;
    private readonly IGameRepository _gameRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IPromoCodeRepository _promoCodeRepository;

    private object GetPhaseLock(string gameId) => _phaseLocks.GetOrAdd(gameId, _ => new object());

    public GameService(ILogger<GameService> logger, IGameRepository gameRepository, ITournamentRepository tournamentRepository, IPromoCodeRepository promoCodeRepository)
    {
        _logger = logger;
        _gameRepository = gameRepository;
        _tournamentRepository = tournamentRepository;
        _promoCodeRepository = promoCodeRepository;
    }

    // ── Lobby management ─────────────────────────────────────────────────────

    public GameState CreateGame(string creatorName, int maxPlayers, string baseUrl)
    {
        var tournamentId = Guid.NewGuid();
        var tournamentCode = GenerateTournamentCode();
        var gameId = Guid.NewGuid().ToString("N")[..8];
        var playerId = Guid.NewGuid().ToString();
        var joinLink = $"{baseUrl}/game/{tournamentCode}";

        var game = new GameState
        {
            GameId = gameId,
            TournamentId = tournamentId.ToString(),
            TournamentCode = tournamentCode,
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
        _games[tournamentCode] = game;

        ThrowOnFailure(SaveTournamentAsync(tournamentId, tournamentCode, playerId));
        ThrowOnFailure(UpsertLiveStateAsync(game));
        _logger.LogInformation("Game created: {TournamentCode} by {CreatorName}", tournamentCode, creatorName);
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
            return (false, "Game not found", null);

        if (!string.IsNullOrEmpty(existingPlayerId))
        {
            var existingPlayer = game.Players.FirstOrDefault(p => p.PlayerId == existingPlayerId);
            if (existingPlayer != null)
            {
                if (existingPlayer.ParticipationStatus == ParticipationStatus.Removed)
                    return (false, "You were removed from this game", null);
                existingPlayer.IsConnected = true;
                existingPlayer.ParticipationStatus = ParticipationStatus.Participating;
                // Player rejoining mid-game won't participate until next game
                if (game.Status == GameStatus.InProgress)
                    existingPlayer.IsDone = true;
                RecalculateGameState(game);
                BumpVersion(game);
                return (true, "Rejoined successfully", existingPlayer);
            }
        }

        var activePlayers = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        if (activePlayers >= game.MaxPlayers)
            return (false, $"Game is full ({activePlayers}/{game.MaxPlayers})", null);

        var playerId = Guid.NewGuid().ToString();
        var player = new PlayerState
        {
            PlayerId = playerId,
            DisplayName = displayName,
            IsCreator = false,
            IsModerator = false,
            IsConnected = true
        };

        // Player joining mid-game won't participate until next game
        if (game.Status == GameStatus.InProgress)
            player.IsDone = true;

        game.Players.Add(player);
        RecalculateGameState(game);
        BumpVersion(game);
        _logger.LogInformation("Player joined: {PlayerId} ({DisplayName}) in game {GameId}", playerId, displayName, gameId);
        return (true, null, player);
    }

    public bool LeaveGame(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return false;
        player.ParticipationStatus = ParticipationStatus.Left;
        player.IsConnected = false;
        RecalculateGameState(game);
        BumpVersion(game);
        return true;
    }

    public bool RemovePlayer(string gameId, string playerId, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        var moderator = game.Players.FirstOrDefault(p => p.PlayerId == moderatorId && p.IsModerator);
        if (moderator == null) return false;
        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player != null && !player.IsCreator)
        {
            player.ParticipationStatus = ParticipationStatus.Removed;
            player.IsConnected = false;
            RecalculateGameState(game);
            BumpVersion(game);
            return true;
        }
        return false;
    }

    public bool UpdateMaxPlayers(string gameId, int maxPlayers, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || !game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return false;
        game.MaxPlayers = maxPlayers;
        RecalculateGameState(game);
        BumpVersion(game);
        return true;
    }

    public bool UpdateMinPlayers(string gameId, int minPlayers, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || !game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return false;
        game.MinPlayers = minPlayers;
        RecalculateGameState(game);
        BumpVersion(game);
        return true;
    }

    public bool UpdatePlayerName(string gameId, string playerId, string displayName)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        if (game.Status == GameStatus.InProgress || game.Status == GameStatus.Ended) return false;
        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return false;
        player.DisplayName = displayName;
        BumpVersion(game);
        return true;
    }

    public bool HasDuplicateNames(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        var names = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating)
            .Select(p => p.DisplayName)
            .ToList();
        return names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    public bool UpdateDiscussionDuration(string gameId, int minutes, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || !game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return false;
        game.DiscussionDurationMinutes = minutes;
        BumpVersion(game);
        return true;
    }

    public bool UpdateTiebreakDiscussionDuration(string gameId, int seconds, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || !game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return false;
        game.TiebreakDiscussionDurationSeconds = seconds;
        BumpVersion(game);
        return true;
    }

    public bool UpdateNumberOfWerewolves(string gameId, int count, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || !game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return false;
        var activeCount = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        if (count < 1 || count >= activeCount) return false;
        game.NumberOfWerewolves = count;
        BumpVersion(game);
        return true;
    }

    public bool UpdateEnabledSkills(string gameId, List<string> skillNames, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || !game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return false;
        game.EnabledSkills = skillNames
            .Where(s => Enum.TryParse<PlayerSkill>(s, true, out _))
            .Select(s => Enum.Parse<PlayerSkill>(s, true))
            .Distinct()
            .ToList();
        BumpVersion(game);
        return true;
    }

    // ── Session phase logic ──────────────────────────────────────────────────

    public (bool Success, string? Error) StartGame(string gameId, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (!game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return (false, "Only a moderator can start the game");
        if (game.Status != GameStatus.ReadyToStart) return (false, "Not enough players to start");
        if (game.GameIndex >= 2 && !game.IsTournamentModeUnlocked) return (false, "A tournament pass is required to continue beyond game 1");

        var activePlayers = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        for (int i = 0; i < activePlayers.Count; i++)
            activePlayers[i].Role = i < game.NumberOfWerewolves ? PlayerRole.Werewolf : PlayerRole.Villager;

        var villagers = activePlayers.Where(p => p.Role == PlayerRole.Villager).ToList();
        var skills = game.EnabledSkills.OrderBy(_ => Guid.NewGuid()).ToList();
        for (int i = 0; i < Math.Min(villagers.Count, skills.Count); i++)
            villagers[i].Skill = skills[i];

        foreach (var p in game.Players) p.IsDone = false;

        game.Status = GameStatus.InProgress;
        game.Phase = GamePhase.RoleReveal;
        game.RoundNumber = 1;
        game.PhaseEndsAt = null;
        game.PhaseStartedAt = DateTime.UtcNow;
        game.AudioPlayAt = DateTime.UtcNow.AddMilliseconds(3000);
        game.NightVotes.Clear();
        game.DayVotes.Clear();
        game.TiebreakCandidates.Clear();
        game.DayTiebreakUsed = false;
        game.NightDeaths.Clear();
        game.DayDeaths.Clear();
        game.Winner = null;
        game.Lover1Id = null;
        game.Lover2Id = null;
        game.WitchHealUsed = false;
        game.WitchPoisonUsed = false;
        game.NightKillTargetId = null;
        game.WitchSavedThisNight = false;
        game.WitchPoisonTargetId = null;
        game.HunterMustShoot = false;
        game.HunterEliminatedAtNight = false;
        BumpVersion(game);
        ThrowOnFailure(UpsertLiveStateAsync(game));
        _logger.LogInformation("Game {GameId} started", gameId);
        return (true, null);
    }

    public (bool Success, string? Error) MarkDone(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        var isGameOver = game.Status == GameStatus.Ended && game.Phase == GamePhase.FinalScoresReveal;
        if (game.Status != GameStatus.InProgress && !isGameOver) return (false, "Game is not in progress");

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) return (false, "Player not found");

        player.IsDone = true;
        BumpVersion(game);

        var eligible = GetEligibleForMarkDone(game).ToList();
        if (eligible.Count > 0 && eligible.All(p => p.IsDone))
        {
            lock (GetPhaseLock(gameId))
            {
                var eligibleNow = GetEligibleForMarkDone(game).ToList();
                if (eligibleNow.Count > 0 && eligibleNow.All(p => p.IsDone))
                    AdvancePhase(game);
            }
        }

        return (true, null);
    }

    public (bool Success, string? Error) CastVote(string gameId, string voterId, string targetId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.Status != GameStatus.InProgress) return (false, "Game is not in progress");

        var voter = game.Players.FirstOrDefault(p => p.PlayerId == voterId);
        if (voter == null) return (false, "Voter not found");

        if (string.IsNullOrEmpty(targetId))
            return RetractVote(game, voterId);

        var target = game.Players.FirstOrDefault(p => p.PlayerId == targetId);
        if (target == null) return (false, "Target not found");

        if (game.Phase == GamePhase.Werewolves)
        {
            if (voter.Role != PlayerRole.Werewolf || voter.IsEliminated)
                return (false, "Only alive werewolves can vote at night");
            game.NightVotes[voterId] = targetId;
            BumpVersion(game);

            var aliveWerewolves = game.Players
                .Where(p => p.Role == PlayerRole.Werewolf && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating)
                .ToList();
            if (aliveWerewolves.Count > 0 && aliveWerewolves.All(p => game.NightVotes.ContainsKey(p.PlayerId)))
            {
                lock (GetPhaseLock(gameId))
                {
                    var aliveWolvesNow = game.Players
                        .Where(p => p.Role == PlayerRole.Werewolf && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating)
                        .ToList();
                    if (aliveWolvesNow.Count > 0 && aliveWolvesNow.All(p => game.NightVotes.ContainsKey(p.PlayerId)))
                        AdvancePhase(game);
                }
            }

            return (true, null);
        }
        else if (game.Phase == GamePhase.Discussion || game.Phase == GamePhase.TiebreakDiscussion)
        {
            if (game.Phase == GamePhase.TiebreakDiscussion && !game.TiebreakCandidates.Contains(targetId))
                return (false, "Can only vote for tied candidates in tiebreak");
            game.DayVotes[voterId] = targetId;
        }
        else
        {
            return (false, "Voting is not open in this phase");
        }

        BumpVersion(game);
        return (true, null);
    }

    private (bool Success, string? Error) RetractVote(GameState game, string voterId)
    {
        if (game.Phase == GamePhase.Discussion || game.Phase == GamePhase.TiebreakDiscussion)
            game.DayVotes.TryRemove(voterId, out _);
        else if (game.Phase == GamePhase.Werewolves)
            game.NightVotes.TryRemove(voterId, out _);
        else
            return (false, "Voting is not open in this phase");

        BumpVersion(game);
        return (true, null);
    }

    public (bool Success, string? Error) CupidAction(string gameId, string cupidId, string lover1Id, string lover2Id)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.Phase != GamePhase.Cupid) return (false, "Not the Cupid turn");

        var cupid = game.Players.FirstOrDefault(p => p.PlayerId == cupidId);
        if (cupid == null || cupid.Skill != PlayerSkill.Cupid) return (false, "Not the Cupid");
        if (cupid.IsEliminated) return (false, "Player is eliminated");
        if (lover1Id == lover2Id) return (false, "Lovers must be different players");

        var lover1 = game.Players.FirstOrDefault(p => p.PlayerId == lover1Id && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating);
        var lover2 = game.Players.FirstOrDefault(p => p.PlayerId == lover2Id && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating);
        if (lover1 == null || lover2 == null) return (false, "Invalid lover selection");

        game.Lover1Id = lover1Id;
        game.Lover2Id = lover2Id;
        cupid.IsDone = true;
        BumpVersion(game);

        lock (GetPhaseLock(gameId))
            AdvancePhase(game);

        return (true, null);
    }

    public (bool Success, string? Error, SeerActionResponse? Result) SeerAction(string gameId, string seerId, string targetId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found", null);
        if (game.Phase != GamePhase.Seer) return (false, "Not the Seer turn", null);

        var seer = game.Players.FirstOrDefault(p => p.PlayerId == seerId);
        if (seer == null || seer.Skill != PlayerSkill.Seer) return (false, "Not the Seer", null);
        if (seer.IsEliminated) return (false, "Player is eliminated", null);

        var target = game.Players.FirstOrDefault(p => p.PlayerId == targetId && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating);
        if (target == null) return (false, "Target not found", null);

        return (true, null, new SeerActionResponse
        {
            IsWerewolf = target.Role == PlayerRole.Werewolf,
            Skill = target.Skill == PlayerSkill.None ? null : target.Skill.ToString()
        });
    }

    public (bool Success, string? Error) WitchAction(string gameId, string witchId, string choice, string? poisonTargetId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.Phase != GamePhase.Witch) return (false, "Not the Witch turn");

        var witch = game.Players.FirstOrDefault(p => p.PlayerId == witchId);
        if (witch == null || witch.Skill != PlayerSkill.Witch) return (false, "Not the Witch");
        if (witch.IsEliminated) return (false, "Player is eliminated");

        switch (choice.ToLowerInvariant())
        {
            case "save":
                if (game.WitchHealUsed) return (false, "Heal potion already used");
                if (game.NightKillTargetId == null) return (false, "No one to save tonight");
                game.WitchSavedThisNight = true;
                game.WitchHealUsed = true;
                break;
            case "poison":
                if (game.WitchPoisonUsed) return (false, "Poison potion already used");
                if (string.IsNullOrEmpty(poisonTargetId)) return (false, "Must specify a poison target");
                var poisonTarget = game.Players.FirstOrDefault(p => p.PlayerId == poisonTargetId && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating);
                if (poisonTarget == null) return (false, "Poison target not found or already eliminated");
                game.WitchPoisonTargetId = poisonTargetId;
                game.WitchPoisonUsed = true;
                break;
            case "nothing":
                break;
            default:
                return (false, "Invalid choice. Use 'save', 'poison', or 'nothing'");
        }

        witch.IsDone = true;
        BumpVersion(game);

        lock (GetPhaseLock(gameId))
            AdvancePhase(game);

        return (true, null);
    }

    public (bool Success, string? Error) HunterAction(string gameId, string hunterId, string targetId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.Phase != GamePhase.Hunter) return (false, "Not the Hunter turn");

        var hunter = game.Players.FirstOrDefault(p => p.PlayerId == hunterId);
        if (hunter == null || hunter.Skill != PlayerSkill.Hunter) return (false, "Not the Hunter");
        if (hunterId == targetId) return (false, "Hunter cannot shoot themselves");

        var target = game.Players.FirstOrDefault(p => p.PlayerId == targetId && !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating);
        if (target == null) return (false, "Target not found or already eliminated");

        target.IsEliminated = true;
        var killedIds = new HashSet<string> { targetId };

        var deaths = game.HunterEliminatedAtNight ? game.NightDeaths : game.DayDeaths;
        deaths.Add(new EliminationEntry { PlayerId = targetId, PlayerName = target.DisplayName, Cause = EliminationCause.HunterShot });

        ApplyLoverCascade(game, killedIds, deaths);

        game.HunterMustShoot = false;
        hunter.IsDone = true;
        BumpVersion(game);

        EvaluateWinCondition(game);

        lock (GetPhaseLock(gameId))
            AdvancePhase(game);

        return (true, null);
    }

    public (bool Success, string? Error) ForceAdvancePhase(string gameId, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (!game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return (false, "Only a moderator can force advance");
        if (game.Status != GameStatus.InProgress) return (false, "Game is not in progress");

        lock (GetPhaseLock(gameId))
            AdvancePhase(game);

        return (true, null);
    }

    public (bool Success, string? Error) ExtendDiscussion(string gameId, string moderatorId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (!game.Players.Any(p => p.PlayerId == moderatorId && p.IsModerator)) return (false, "Only a moderator can extend the discussion");
        if (game.Phase != GamePhase.Discussion && game.Phase != GamePhase.TiebreakDiscussion)
            return (false, "Can only extend during Discussion or Tiebreak Discussion");

        // Extend from the current deadline (or now, if it has already passed)
        var baseline = game.PhaseEndsAt.HasValue && game.PhaseEndsAt.Value > DateTime.UtcNow
            ? game.PhaseEndsAt.Value
            : DateTime.UtcNow;
        game.PhaseEndsAt = baseline.AddSeconds(60);
        BumpVersion(game);
        ThrowOnFailure(UpsertLiveStateAsync(game));
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UnlockTournamentAsync(string tournamentCode, string code)
    {
        if (!_games.TryGetValue(tournamentCode, out var game)) return (false, "Game not found");
        if (!await _promoCodeRepository.RedeemAsync(code))
            return (false, "Invalid code");

        game.IsTournamentModeUnlocked = true;
        ThrowOnFailure(UpsertLiveStateAsync(game));
        ThrowOnFailure(UpdateTournamentModeUnlockedAsync(game));
        return (true, null);
    }

    public bool UnlockTournamentMode(string tournamentCode)
    {
        if (!_games.TryGetValue(tournamentCode, out var game)) return false;
        game.IsTournamentModeUnlocked = true;
        ThrowOnFailure(UpsertLiveStateAsync(game));
        ThrowOnFailure(UpdateTournamentModeUnlockedAsync(game));
        return true;
    }

    private async Task UpdateTournamentModeUnlockedAsync(GameState game)
    {
        if (string.IsNullOrEmpty(game.TournamentId)) return;
        var existing = await _tournamentRepository.GetByIdAsync(Guid.Parse(game.TournamentId));
        if (existing == null) return;
        await _tournamentRepository.SaveTournamentAsync(existing with { IsTournamentModeUnlocked = true });
    }

    public void TryAdvancePhaseIfExpired(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return;

        // FinalScoresReveal isn't a regular phase — the timer triggers a full game reset instead.
        if (game.Phase == GamePhase.FinalScoresReveal)
        {
            if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value) return;
            lock (GetPhaseLock(gameId))
            {
                if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value) return;
                ResetForNextGame(game);
            }
            return;
        }

        if (game.Status != GameStatus.InProgress) return;
        if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value) return;

        lock (GetPhaseLock(gameId))
        {
            if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value) return;
            AdvancePhase(game);
        }
    }

    public (string Role, string? Skill, List<string> FellowWerewolves, string? LoverName, string? NightKillTargetName, bool WitchHealUsed, bool WitchPoisonUsed) GetPlayerRole(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return ("Unknown", null, new(), null, null, false, false);

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player?.Role == null)
            return ("Unknown", null, new(), null, null, false, false);

        // Fellow werewolves visible during werewolf phases
        var fellows = new List<string>();
        if (player.Role == PlayerRole.Werewolf &&
            (game.Phase == GamePhase.WerewolvesMeeting || game.Phase == GamePhase.Werewolves || game.Phase == GamePhase.WerewolvesCloseEyes))
        {
            fellows = game.Players
                .Where(p => p.Role == PlayerRole.Werewolf && p.PlayerId != playerId && !p.IsEliminated)
                .Select(p => p.DisplayName)
                .ToList();
        }

        // Lover name (always visible once revealed)
        string? loverName = null;
        if (game.Phase != GamePhase.RoleReveal && game.Phase != GamePhase.Cupid && game.Phase != GamePhase.CupidCloseEyes)
        {
            if (player.PlayerId == game.Lover1Id)
                loverName = game.Players.FirstOrDefault(p => p.PlayerId == game.Lover2Id)?.DisplayName;
            else if (player.PlayerId == game.Lover2Id)
                loverName = game.Players.FirstOrDefault(p => p.PlayerId == game.Lover1Id)?.DisplayName;
        }

        // Night kill target shown to Witch only
        string? nightKillTargetName = null;
        if (player.Skill == PlayerSkill.Witch && game.Phase == GamePhase.Witch)
            nightKillTargetName = game.Players.FirstOrDefault(p => p.PlayerId == game.NightKillTargetId)?.DisplayName;

        return (player.Role.ToString()!, player.Skill == PlayerSkill.None ? null : player.Skill.ToString(), fellows, loverName, nightKillTargetName, game.WitchHealUsed, game.WitchPoisonUsed);
    }

    // ── Private phase engine ─────────────────────────────────────────────────

    private void AdvancePhase(GameState game)
    {
        switch (game.Phase)
        {
            case GamePhase.RoleReveal:
                BeginNightSequence(game);
                break;

            case GamePhase.NightAnnouncement:
                BeginPhase(game, game.RoundNumber == 1 ? GamePhase.WerewolvesMeeting : GamePhase.Werewolves);
                break;

            case GamePhase.WerewolvesMeeting:
                BeginPhase(game, GamePhase.WerewolvesCloseEyes);
                break;

            case GamePhase.WerewolvesCloseEyes:
                if (game.RoundNumber == 1)
                {
                    if (HasLivingSkill(game, PlayerSkill.Cupid))
                        BeginPhase(game, GamePhase.Cupid);
                    else
                        BeginDaySequence(game);
                }
                else
                {
                    game.NightKillTargetId = ResolveNightVote(game);
                    TransitionToNextAfterWerewolves(game);
                }
                break;

            case GamePhase.Cupid:
                BeginPhase(game, GamePhase.CupidCloseEyes);
                break;

            case GamePhase.CupidCloseEyes:
                BeginDaySequence(game);
                break;

            case GamePhase.LoversReveal:
                TransitionToDiscussion(game);
                break;

            case GamePhase.Werewolves:
                BeginPhase(game, GamePhase.WerewolvesCloseEyes);
                break;

            case GamePhase.Seer:
                BeginPhase(game, GamePhase.SeerCloseEyes);
                break;

            case GamePhase.SeerCloseEyes:
                TransitionToNextAfterSeer(game);
                break;

            case GamePhase.Witch:
                BeginPhase(game, GamePhase.WitchCloseEyes);
                break;

            case GamePhase.WitchCloseEyes:
                ResolveNightDeaths(game);
                EvaluateWinCondition(game);
                BeginDaySequence(game);
                break;

            case GamePhase.DayAnnouncement:
                // Round 1 had no wolf-kill cycle. If Cupid was in play, show LoverReveal before discussion.
                if (game.RoundNumber == 1 && game.Players.Any(p => p.Skill == PlayerSkill.Cupid))
                    BeginPhase(game, GamePhase.LoversReveal);
                else if (game.RoundNumber == 1)
                    TransitionToDiscussion(game);
                else
                    BeginPhase(game, GamePhase.NightEliminationReveal);
                break;

            case GamePhase.NightEliminationReveal:
                if (game.HunterMustShoot)
                {
                    game.HunterEliminatedAtNight = true;
                    BeginPhase(game, GamePhase.Hunter);
                    break;
                }
                if (TransitionToFinalScoresRevealIfWon(game)) return;
                TransitionToDiscussion(game);
                break;

            case GamePhase.Hunter:
                if (TransitionToFinalScoresRevealIfWon(game)) return;
                if (game.HunterEliminatedAtNight)
                {
                    TransitionToDiscussion(game);
                }
                else
                {
                    game.RoundNumber++;
                    BeginNightSequence(game);
                }
                break;

            case GamePhase.Discussion:
            {
                var (winnerId, isTie, tiedIds) = ResolveDayVote(AliveVotes(game));
                if (isTie && !game.DayTiebreakUsed)
                {
                    game.TiebreakCandidates = tiedIds;
                    game.DayTiebreakUsed = true;
                    game.DayVotes.Clear();
                    BeginPhase(game, GamePhase.TiebreakDiscussion);
                }
                else
                {
                    FinalizeDayEliminationReveal(game, winnerId);
                }
                break;
            }

            case GamePhase.TiebreakDiscussion:
            {
                var (tbWinner, tbIsTie, _) = ResolveDayVote(AliveVotes(game));
                FinalizeDayEliminationReveal(game, tbIsTie ? null : tbWinner);
                break;
            }

            case GamePhase.DayEliminationReveal:
                if (TransitionToFinalScoresRevealIfWon(game)) return;
                if (game.HunterMustShoot)
                {
                    game.HunterEliminatedAtNight = false;
                    BeginPhase(game, GamePhase.Hunter);
                    break;
                }
                game.RoundNumber++;
                BeginNightSequence(game);
                break;

            case GamePhase.FinalScoresReveal:
                ResetForNextGame(game);
                break;
        }
    }

    private void BeginNightSequence(GameState game)
    {
        game.NightVotes.Clear();
        game.NightDeaths.Clear();
        game.NightKillTargetId = null;
        game.WitchSavedThisNight = false;
        game.WitchPoisonTargetId = null;
        game.HunterMustShoot = false;
        game.DayTiebreakUsed = false;
        game.TiebreakCandidates.Clear();
        BeginPhase(game, GamePhase.NightAnnouncement);
        _logger.LogInformation("Game {GameId} → NightAnnouncement (round {Round})", game.GameId, game.RoundNumber);
    }

    private void BeginDaySequence(GameState game)
    {
        BeginPhase(game, GamePhase.DayAnnouncement);
        _logger.LogInformation("Game {GameId} → DayAnnouncement (round {Round})", game.GameId, game.RoundNumber);
    }

    private void TransitionToNextAfterWerewolves(GameState game)
    {
        if (HasLivingSkill(game, PlayerSkill.Seer))
        {
            BeginPhase(game, GamePhase.Seer);
            return;
        }
        TransitionToNextAfterSeer(game);
    }

    private void TransitionToNextAfterSeer(GameState game)
    {
        if (HasLivingSkill(game, PlayerSkill.Witch))
        {
            BeginPhase(game, GamePhase.Witch);
            return;
        }
        ResolveNightDeaths(game);
        EvaluateWinCondition(game);
        BeginDaySequence(game);
    }

    private void TransitionToDiscussion(GameState game)
    {
        game.DayVotes.Clear();
        game.DayDeaths.Clear();
        game.DayTiebreakUsed = false;
        game.TiebreakCandidates.Clear();
        BeginPhase(game, GamePhase.Discussion);
        _logger.LogInformation("Game {GameId} → Discussion (round {Round})", game.GameId, game.RoundNumber);
    }

    private void FinalizeDayEliminationReveal(GameState game, string? eliminatedId)
    {
        game.DayDeaths.Clear();
        var killedIds = new HashSet<string>();

        if (eliminatedId != null)
        {
            var p = game.Players.First(p => p.PlayerId == eliminatedId);
            p.IsEliminated = true;
            killedIds.Add(eliminatedId);
            game.DayDeaths.Add(new EliminationEntry { PlayerId = eliminatedId, PlayerName = p.DisplayName, Cause = EliminationCause.VillageVote });
            ApplyLoverCascade(game, killedIds, game.DayDeaths);
            CheckHunterTriggered(game, killedIds);
            AwardCorrectVotePoints(game, eliminatedId);
        }

        RecordDayVoteAccuracy(game);

        EvaluateWinCondition(game);
        BeginPhase(game, GamePhase.DayEliminationReveal);
        _logger.LogInformation("Game {GameId} → DayEliminationReveal (eliminated: {Id}, winner: {Winner})", game.GameId, eliminatedId ?? "none", game.Winner ?? "none");
    }

    private static void ResolveNightDeaths(GameState game)
    {
        game.NightDeaths.Clear();
        var killedIds = new HashSet<string>();

        // Primary kill (unless Witch saved)
        if (game.NightKillTargetId != null && !game.WitchSavedThisNight)
        {
            var p = game.Players.FirstOrDefault(q => q.PlayerId == game.NightKillTargetId);
            if (p != null && !p.IsEliminated)
            {
                p.IsEliminated = true;
                killedIds.Add(p.PlayerId);
                game.NightDeaths.Add(new EliminationEntry { PlayerId = p.PlayerId, PlayerName = p.DisplayName, Cause = EliminationCause.WerewolfKill });
            }
        }

        // Witch poison
        if (game.WitchPoisonTargetId != null)
        {
            var p = game.Players.FirstOrDefault(q => q.PlayerId == game.WitchPoisonTargetId);
            if (p != null && !p.IsEliminated && killedIds.Add(p.PlayerId))
            {
                p.IsEliminated = true;
                game.NightDeaths.Add(new EliminationEntry { PlayerId = p.PlayerId, PlayerName = p.DisplayName, Cause = EliminationCause.WitchPoison });
            }
        }

        ApplyLoverCascade(game, killedIds, game.NightDeaths);
        CheckHunterTriggered(game, killedIds);
    }

    private static void ApplyLoverCascade(GameState game, HashSet<string> alreadyKilled, List<EliminationEntry> deaths)
    {
        if (game.Lover1Id == null || game.Lover2Id == null) return;

        string? otherLoverId = null;
        if (alreadyKilled.Contains(game.Lover1Id) && !alreadyKilled.Contains(game.Lover2Id))
            otherLoverId = game.Lover2Id;
        else if (alreadyKilled.Contains(game.Lover2Id) && !alreadyKilled.Contains(game.Lover1Id))
            otherLoverId = game.Lover1Id;

        if (otherLoverId == null) return;

        var loverPlayer = game.Players.FirstOrDefault(p => p.PlayerId == otherLoverId);
        if (loverPlayer == null || loverPlayer.IsEliminated) return;

        loverPlayer.IsEliminated = true;
        alreadyKilled.Add(otherLoverId);
        deaths.Add(new EliminationEntry { PlayerId = otherLoverId, PlayerName = loverPlayer.DisplayName, Cause = EliminationCause.LoverDeath });
    }

    private static void CheckHunterTriggered(GameState game, HashSet<string> killedIds)
    {
        var hunter = game.Players.FirstOrDefault(p =>
            p.Skill == PlayerSkill.Hunter &&
            p.ParticipationStatus == ParticipationStatus.Participating);
        if (hunter != null && killedIds.Contains(hunter.PlayerId))
            game.HunterMustShoot = true;
    }

    private static void EvaluateWinCondition(GameState game)
    {
        var alive = game.Players
            .Where(p => !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating)
            .ToList();

        // Lovers win when only the two of them remain
        if (game.Lover1Id != null && game.Lover2Id != null)
        {
            var loverIds = new HashSet<string> { game.Lover1Id, game.Lover2Id };
            if (alive.Count == 2 && alive.All(p => loverIds.Contains(p.PlayerId)))
            {
                game.Winner = "Lovers";
                return;
            }
        }

        var aliveWerewolves = alive.Count(p => p.Role == PlayerRole.Werewolf);
        var aliveVillagers  = alive.Count(p => p.Role == PlayerRole.Villager);

        if (aliveWerewolves == 0)
            game.Winner = "Villagers";
        else if (aliveVillagers == 0)
            game.Winner = "Werewolves";
    }

    private bool TransitionToFinalScoresRevealIfWon(GameState game)
    {
        if (game.Winner == null) return false;
        AwardTeamWinPoints(game);
        AccumulateTournamentScores(game);
        game.Status = GameStatus.Ended;
        BeginPhase(game, GamePhase.FinalScoresReveal);
        ThrowOnFailure(PersistGameResultsAsync(game));
        return true;
    }

    private static void AccumulateTournamentScores(GameState game)
    {
        foreach (var p in game.Players.Where(p => p.ParticipationStatus == ParticipationStatus.Participating))
            p.TotalScore += p.Score;
    }

    private void ResetForNextGame(GameState game)
    {
        game.GameIndex++;
        game.GameId = Guid.NewGuid().ToString("N")[..8];
        game.Status = GameStatus.WaitingForPlayers; // RecalculateGameState skips non-lobby states
        game.ResetSessionState();
        RecalculateGameState(game);
        BumpVersion(game);
        ThrowOnFailure(UpsertLiveStateAsync(game));
        _logger.LogInformation("Game reset for next round: {TournamentCode} (game {GameIndex})", game.TournamentCode, game.GameIndex);
    }

    public async Task InitializeAsync()
    {
        var stateJsons = await _gameRepository.LoadAllLiveStatesAsync();
        foreach (var json in stateJsons)
        {
            var game = JsonSerializer.Deserialize<GameState>(json, _jsonOptions)!;
            _games[game.TournamentCode] = game;
            // Ensure results are persisted for any game that ended before the server restarted.
            if (game.Phase == GamePhase.FinalScoresReveal)
                await PersistGameResultsAsync(game);
            _logger.LogInformation("Restored game {TournamentCode} from live state (phase {Phase})", game.TournamentCode, game.Phase);
        }
    }

    private Task UpsertLiveStateAsync(GameState game) =>
        _gameRepository.UpsertLiveStateAsync(game.TournamentCode, SerializeState(game));

    private static string SerializeState(GameState game) =>
        JsonSerializer.Serialize(game, _jsonOptions);

    // Faults on the thread pool crash the process — we prefer that over silently swallowed errors.
    private void ThrowOnFailure(Task task) =>
        task.ContinueWith(
            t =>
            {
                var ex = t.Exception!.GetBaseException();
                _logger.LogCritical(ex, "Background task failed — crashing process");
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
            },
            TaskContinuationOptions.OnlyOnFaulted);

    private async Task PersistGameResultsAsync(GameState game)
    {
        var gameRecord = new GameRecord(
            Id: game.GameId,
            TournamentId: string.IsNullOrEmpty(game.TournamentId) ? null : game.TournamentId,
            JoinCode: string.IsNullOrEmpty(game.TournamentCode) ? game.GameId : game.TournamentCode,
            Status: game.Status.ToString(),
            Winner: game.Winner,
            Settings: System.Text.Json.JsonSerializer.Serialize(new
            {
                game.MinPlayers,
                game.MaxPlayers,
                game.DiscussionDurationMinutes,
                game.NumberOfWerewolves,
                EnabledSkills = game.EnabledSkills.Select(s => s.ToString())
            }),
            CreatedAt: game.CreatedAt,
            EndedAt: DateTime.UtcNow);

        await _gameRepository.SaveGameAsync(gameRecord);

        var playerRecords = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating)
            .Select(p => new GamePlayerRecord(
                GameId: game.GameId,
                PlayerId: p.PlayerId,
                DisplayName: p.DisplayName,
                Role: p.Role?.ToString(),
                Skill: p.Skill == PlayerSkill.None ? null : p.Skill.ToString(),
                IsEliminated: p.IsEliminated,
                EliminationCause: null,
                Score: p.Score,
                VotesCast: p.VotesCast,
                VotesCorrect: p.VotesCorrect,
                IsCreator: p.IsCreator,
                IsModerator: p.IsModerator,
                ParticipationStatus: p.ParticipationStatus.ToString(),
                JoinedAt: p.JoinedAt));

        await _gameRepository.SaveGamePlayersAsync(playerRecords);

        if (!string.IsNullOrEmpty(game.TournamentId) && Guid.TryParse(game.TournamentId, out var tournamentId))
            await UpsertTournamentParticipantsAsync(game, tournamentId);

        _logger.LogInformation("Game {GameId} results persisted", game.GameId);
    }

    private async Task UpsertTournamentParticipantsAsync(GameState game, Guid tournamentId)
    {
        foreach (var p in game.Players.Where(p => p.ParticipationStatus == ParticipationStatus.Participating))
        {
            var record = new TournamentParticipantRecord(
                TournamentId: tournamentId,
                PlayerId: p.PlayerId,
                DisplayName: p.DisplayName,
                TotalScore: p.TotalScore,
                JoinedAt: p.JoinedAt);
            await _tournamentRepository.UpsertParticipantAsync(record);
        }
    }

    private static string? ResolveNightVote(GameState game)
    {
        if (!game.NightVotes.Any()) return null;
        var tally = game.NightVotes
            .GroupBy(kv => kv.Value)
            .Select(g => (TargetId: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        var maxVotes = tally[0].Count;
        var top = tally.Where(x => x.Count == maxVotes).ToList();
        return top.Count == 1 ? top[0].TargetId : null;
    }

    private static (string? Winner, bool IsTie, List<string> TiedIds) ResolveDayVote(IEnumerable<KeyValuePair<string, string>> votes)
    {
        var voteList = votes.ToList();
        if (!voteList.Any()) return (null, false, new());
        var tally = voteList
            .GroupBy(kv => kv.Value)
            .Select(g => (TargetId: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        var maxVotes = tally[0].Count;
        var top = tally.Where(x => x.Count == maxVotes).Select(x => x.TargetId).ToList();
        return top.Count > 1 ? (null, true, top) : (top[0], false, new());
    }

    // Only alive, participating players' votes count toward who gets eliminated.
    // Eliminated players can still vote (for scoring), but not for the tally.
    private static IEnumerable<KeyValuePair<string, string>> AliveVotes(GameState game) =>
        game.DayVotes.Where(kv => game.Players.Any(p =>
            p.PlayerId == kv.Key &&
            !p.IsEliminated &&
            p.ParticipationStatus == ParticipationStatus.Participating));

    // Award 1 point to every player who voted for the eliminated player.
    private static void AwardCorrectVotePoints(GameState game, string eliminatedId)
    {
        var playerMap = game.Players.ToDictionary(p => p.PlayerId);
        foreach (var (voterId, targetId) in game.DayVotes)
            if (targetId == eliminatedId && playerMap.TryGetValue(voterId, out var voter))
                voter.Score++;
    }

    // Track how many votes each player cast and how many targeted an actual werewolf.
    // Called after every resolved day vote, including tiebreaks and no-elimination rounds.
    private static void RecordDayVoteAccuracy(GameState game)
    {
        var playerMap = game.Players.ToDictionary(p => p.PlayerId);
        foreach (var (voterId, targetId) in game.DayVotes)
        {
            if (!playerMap.TryGetValue(voterId, out var voter)) continue;
            voter.VotesCast++;
            if (playerMap.TryGetValue(targetId, out var target) && target.Role == PlayerRole.Werewolf)
                voter.VotesCorrect++;
        }
    }

    // Award 8 points to all players on the winning team.
    private static void AwardTeamWinPoints(GameState game)
    {
        const int WinPoints = 8;
        IEnumerable<PlayerState> winners = game.Winner switch
        {
            "Villagers"  => game.Players.Where(p =>
                p.Role == PlayerRole.Villager &&
                p.ParticipationStatus == ParticipationStatus.Participating),
            "Werewolves" => game.Players.Where(p =>
                p.Role == PlayerRole.Werewolf &&
                p.ParticipationStatus == ParticipationStatus.Participating),
            "Lovers"     => game.Players.Where(p =>
                p.ParticipationStatus == ParticipationStatus.Participating &&
                (p.PlayerId == game.Lover1Id || p.PlayerId == game.Lover2Id)),
            _            => Enumerable.Empty<PlayerState>()
        };
        foreach (var p in winners)
            p.Score += WinPoints;
    }

    private static IEnumerable<PlayerState> GetEligibleForMarkDone(GameState game) =>
        PhaseDescriptor.Get(game.Phase).EligibleForDone(game);

    private static bool HasLivingSkill(GameState game, PlayerSkill skill) =>
        game.EnabledSkills.Contains(skill) &&
        game.Players.Any(p =>
            p.Skill == skill &&
            !p.IsEliminated &&
            p.ParticipationStatus == ParticipationStatus.Participating);

    private void BeginPhase(GameState game, GamePhase phase)
    {
        var duration = PhaseDescriptor.Get(phase).Duration(game);
        game.Phase = phase;
        game.PhaseStartedAt = DateTime.UtcNow;
        game.PhaseEndsAt = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
        game.AudioPlayAt = DateTime.UtcNow.AddMilliseconds(3000);
        ResetDone(game);
        BumpVersion(game);
        ThrowOnFailure(UpsertLiveStateAsync(game));
    }

    private static void ResetDone(GameState game)
    {
        foreach (var p in game.Players) p.IsDone = false;
    }

    // ── Lobby state recalculation ────────────────────────────────────────────

    private static void RecalculateGameState(GameState game)
    {
        if (game.Status != GameStatus.WaitingForPlayers && game.Status != GameStatus.ReadyToStart)
            return;
        var activeCount = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        game.Status = activeCount >= game.MinPlayers ? GameStatus.ReadyToStart : GameStatus.WaitingForPlayers;
    }

    private static void BumpVersion(GameState game) => game.Version++;

    private string GenerateTournamentCode()
    {
        const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do
        {
            code = new string(Enumerable.Range(0, 6).Select(_ => Chars[Random.Shared.Next(Chars.Length)]).ToArray());
        } while (_games.ContainsKey(code));
        return code;
    }

    private async Task SaveTournamentAsync(Guid tournamentId, string joinCode, string hostPlayerId)
    {
        var record = new TournamentRecord(
            Id: tournamentId,
            Name: null,
            JoinCode: joinCode,
            HostPlayerId: hostPlayerId,
            CreatedAt: DateTime.UtcNow,
            IsTournamentModeUnlocked: false);
        await _tournamentRepository.SaveTournamentAsync(record);
    }

    private string GenerateQrCode(string text)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 }, false);
        return Convert.ToBase64String(qrCodeBytes);
    }
}

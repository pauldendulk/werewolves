using System.Collections.Concurrent;
using QRCoder;
using WerewolvesAPI.DTOs;
using WerewolvesAPI.Models;

namespace WerewolvesAPI.Services;

public class GameService : IGameService
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    private readonly ConcurrentDictionary<string, object> _phaseLocks = new();
    private readonly ILogger<GameService> _logger;

    private object GetPhaseLock(string gameId) => _phaseLocks.GetOrAdd(gameId, _ => new object());

    public GameService(ILogger<GameService> logger)
    {
        _logger = logger;
    }

    // â”€â”€ Lobby management â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public GameState CreateGame(string creatorName, int maxPlayers, string baseUrl)
    {
        var gameId = Guid.NewGuid().ToString("N")[..8];
        var playerId = Guid.NewGuid().ToString();
        var joinLink = $"{baseUrl}/game/{gameId}";

        var game = new GameState
        {
            GameId = gameId,
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

    public bool UpdateMaxPlayers(string gameId, int maxPlayers, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || game.CreatorId != creatorId) return false;
        game.MaxPlayers = maxPlayers;
        RecalculateGameState(game);
        BumpVersion(game);
        return true;
    }

    public bool UpdateMinPlayers(string gameId, int minPlayers, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || game.CreatorId != creatorId) return false;
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

    public bool UpdateDiscussionDuration(string gameId, int minutes, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || game.CreatorId != creatorId) return false;
        game.DiscussionDurationMinutes = minutes;
        BumpVersion(game);
        return true;
    }

    public bool UpdateNumberOfWerewolves(string gameId, int count, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || game.CreatorId != creatorId) return false;
        var activeCount = game.Players.Count(p => p.ParticipationStatus == ParticipationStatus.Participating);
        if (count < 1 || count >= activeCount) return false;
        game.NumberOfWerewolves = count;
        BumpVersion(game);
        return true;
    }

    public bool UpdateEnabledSkills(string gameId, List<string> skillNames, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game) || game.CreatorId != creatorId) return false;
        game.EnabledSkills = skillNames
            .Where(s => Enum.TryParse<PlayerSkill>(s, true, out _))
            .Select(s => Enum.Parse<PlayerSkill>(s, true))
            .Distinct()
            .ToList();
        BumpVersion(game);
        return true;
    }

    // â”€â”€ Session phase logic â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public (bool Success, string? Error) StartGame(string gameId, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.CreatorId != creatorId) return (false, "Only the creator can start the game");
        if (game.Status != GameStatus.ReadyToStart) return (false, "Not enough players to start");

        var activePlayers = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        // Assign roles
        for (int i = 0; i < activePlayers.Count; i++)
            activePlayers[i].Role = i < game.NumberOfWerewolves ? PlayerRole.Werewolf : PlayerRole.Villager;

        // Assign skills to villagers (shuffle enabled skills, assign one-per-villager)
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
        game.AudioPlayAt = DateTime.UtcNow.AddMilliseconds(2000);
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
        _logger.LogInformation("Game {GameId} started", gameId);
        return (true, null);
    }

    public (bool Success, string? Error) MarkDone(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.Status != GameStatus.InProgress) return (false, "Game is not in progress");

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
        var target = game.Players.FirstOrDefault(p => p.PlayerId == targetId);
        if (target == null) return (false, "Target not found");

        if (game.Phase == GamePhase.WerewolvesTurn)
        {
            if (voter.Role != PlayerRole.Werewolf || voter.IsEliminated)
                return (false, "Only alive werewolves can vote at night");
            game.NightVotes[voterId] = targetId;
        }
        else if (game.Phase == GamePhase.Discussion || game.Phase == GamePhase.TiebreakDiscussion)
        {
            if (voter.IsEliminated)
                return (false, "Eliminated players cannot vote");
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

    public (bool Success, string? Error) CupidAction(string gameId, string cupidId, string lover1Id, string lover2Id)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.Phase != GamePhase.CupidTurn) return (false, "Not the Cupid turn");

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
        if (game.Phase != GamePhase.SeerTurn) return (false, "Not the Seer turn", null);

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
        if (game.Phase != GamePhase.WitchTurn) return (false, "Not the Witch turn");

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
        if (game.Phase != GamePhase.HunterTurn) return (false, "Not the Hunter turn");

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

    public (bool Success, string? Error) ForceAdvancePhase(string gameId, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Game not found");
        if (game.CreatorId != creatorId) return (false, "Only the creator can force advance");
        if (game.Status != GameStatus.InProgress) return (false, "Game is not in progress");

        lock (GetPhaseLock(gameId))
            AdvancePhase(game);

        return (true, null);
    }

    public void TryAdvancePhaseIfExpired(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return;
        if (game.Status != GameStatus.InProgress) return;
        if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value) return;

        lock (GetPhaseLock(gameId))
        {
            if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value) return;
            AdvancePhase(game);
        }
    }

    public (string Role, string Skill, List<string> FellowWerewolves, string? LoverName, string? NightKillTargetName, bool WitchHealUsed, bool WitchPoisonUsed) GetPlayerRole(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return ("Unknown", "None", new(), null, null, false, false);

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player?.Role == null)
            return ("Unknown", "None", new(), null, null, false, false);

        // Fellow werewolves visible during werewolf phases
        var fellows = new List<string>();
        if (player.Role == PlayerRole.Werewolf &&
            (game.Phase == GamePhase.WerewolvesMeeting || game.Phase == GamePhase.WerewolvesTurn))
        {
            fellows = game.Players
                .Where(p => p.Role == PlayerRole.Werewolf && p.PlayerId != playerId && !p.IsEliminated)
                .Select(p => p.DisplayName)
                .ToList();
        }

        // Lover name (always visible once revealed)
        string? loverName = null;
        if (game.Phase != GamePhase.RoleReveal && game.Phase != GamePhase.CupidTurn)
        {
            if (player.PlayerId == game.Lover1Id)
                loverName = game.Players.FirstOrDefault(p => p.PlayerId == game.Lover2Id)?.DisplayName;
            else if (player.PlayerId == game.Lover2Id)
                loverName = game.Players.FirstOrDefault(p => p.PlayerId == game.Lover1Id)?.DisplayName;
        }

        // Night kill target shown to Witch only
        string? nightKillTargetName = null;
        if (player.Skill == PlayerSkill.Witch && game.Phase == GamePhase.WitchTurn)
            nightKillTargetName = game.Players.FirstOrDefault(p => p.PlayerId == game.NightKillTargetId)?.DisplayName;

        return (player.Role.ToString()!, player.Skill.ToString(), fellows, loverName, nightKillTargetName, game.WitchHealUsed, game.WitchPoisonUsed);
    }

    // â”€â”€ Private phase engine â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void AdvancePhase(GameState game)
    {
        switch (game.Phase)
        {
            case GamePhase.RoleReveal:
                TransitionToFirstNightStep(game);
                break;

            case GamePhase.CupidTurn:
                // If Cupid chose lovers show them; otherwise skip to WerewolvesMeeting
                if (game.Lover1Id != null && game.Lover2Id != null)
                    BeginPhase(game, GamePhase.LoverReveal);
                else
                    BeginPhase(game, GamePhase.WerewolvesMeeting);
                break;

            case GamePhase.LoverReveal:
                BeginPhase(game, GamePhase.WerewolvesMeeting);
                break;

            case GamePhase.WerewolvesMeeting:
                // End of first night â€” no kill
                TransitionToDiscussion(game);
                break;

            case GamePhase.WerewolvesTurn:
                game.NightKillTargetId = ResolveNightVote(game);
                TransitionToNextAfterWerewolves(game);
                break;

            case GamePhase.SeerTurn:
                TransitionToNextAfterSeer(game);
                break;

            case GamePhase.WitchTurn:
                ResolveNightDeaths(game);
                EvaluateWinCondition(game);
                BeginPhase(game, GamePhase.NightElimination, TimeSpan.FromSeconds(10));
                break;

            case GamePhase.NightElimination:
                if (game.HunterMustShoot)
                {
                    game.HunterEliminatedAtNight = true;
                    BeginPhase(game, GamePhase.HunterTurn);
                    break;
                }
                if (TransitionToGameOverIfWon(game)) return;
                TransitionToDiscussion(game);
                break;

            case GamePhase.HunterTurn:
                if (TransitionToGameOverIfWon(game)) return;
                if (game.HunterEliminatedAtNight)
                {
                    TransitionToDiscussion(game);
                }
                else
                {
                    game.RoundNumber++;
                    TransitionToFirstNightStep(game);
                }
                break;

            case GamePhase.Discussion:
            {
                var (winnerId, isTie, tiedIds) = ResolveDayVote(game.DayVotes);
                if (isTie && !game.DayTiebreakUsed)
                {
                    game.Phase = GamePhase.TiebreakDiscussion;
                    game.TiebreakCandidates = tiedIds;
                    game.DayTiebreakUsed = true;
                    game.DayVotes.Clear();
                    game.PhaseEndsAt = DateTime.UtcNow.AddSeconds(60);
                    game.PhaseStartedAt = DateTime.UtcNow;
                    game.AudioPlayAt = DateTime.UtcNow.AddMilliseconds(2000);
                    ResetDone(game);
                    BumpVersion(game);
                }
                else
                {
                    FinalizeDayElimination(game, winnerId);
                }
                break;
            }

            case GamePhase.TiebreakDiscussion:
            {
                var (tbWinner, tbIsTie, _) = ResolveDayVote(game.DayVotes);
                FinalizeDayElimination(game, tbIsTie ? null : tbWinner);
                break;
            }

            case GamePhase.DayElimination:
                if (TransitionToGameOverIfWon(game)) return;
                if (game.HunterMustShoot)
                {
                    game.HunterEliminatedAtNight = false;
                    BeginPhase(game, GamePhase.HunterTurn);
                    break;
                }
                game.RoundNumber++;
                TransitionToFirstNightStep(game);
                break;

            case GamePhase.GameOver:
                break;
        }
    }

    private void TransitionToFirstNightStep(GameState game)
    {
        game.NightVotes.Clear();
        game.NightDeaths.Clear();
        game.NightKillTargetId = null;
        game.WitchSavedThisNight = false;
        game.WitchPoisonTargetId = null;
        game.HunterMustShoot = false;
        game.DayTiebreakUsed = false;
        game.TiebreakCandidates.Clear();

        if (game.RoundNumber == 1)
        {
            if (HasLivingSkill(game, PlayerSkill.Cupid))
                BeginPhase(game, GamePhase.CupidTurn);
            else
                BeginPhase(game, GamePhase.WerewolvesMeeting);
        }
        else
        {
            BeginPhase(game, GamePhase.WerewolvesTurn);
        }
        _logger.LogInformation("Game {GameId} â†’ {Phase} (round {Round})", game.GameId, game.Phase, game.RoundNumber);
    }

    private void TransitionToNextAfterWerewolves(GameState game)
    {
        if (HasLivingSkill(game, PlayerSkill.Seer))
        {
            BeginPhase(game, GamePhase.SeerTurn);
            return;
        }
        TransitionToNextAfterSeer(game);
    }

    private void TransitionToNextAfterSeer(GameState game)
    {
        if (HasLivingSkill(game, PlayerSkill.Witch))
        {
            BeginPhase(game, GamePhase.WitchTurn);
            return;
        }
        ResolveNightDeaths(game);
        EvaluateWinCondition(game);
        BeginPhase(game, GamePhase.NightElimination, TimeSpan.FromSeconds(10));
    }

    private void TransitionToDiscussion(GameState game)
    {
        game.DayVotes.Clear();
        game.DayDeaths.Clear();
        game.DayTiebreakUsed = false;
        game.TiebreakCandidates.Clear();
        BeginPhase(game, GamePhase.Discussion, TimeSpan.FromMinutes(game.DiscussionDurationMinutes));
        _logger.LogInformation("Game {GameId} â†’ Discussion (round {Round})", game.GameId, game.RoundNumber);
    }

    private void FinalizeDayElimination(GameState game, string? eliminatedId)
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
        }

        EvaluateWinCondition(game);
        BeginPhase(game, GamePhase.DayElimination, TimeSpan.FromSeconds(10));
        _logger.LogInformation("Game {GameId} â†’ DayElimination (eliminated: {Id}, winner: {Winner})", game.GameId, eliminatedId ?? "none", game.Winner ?? "none");
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

    private static bool TransitionToGameOverIfWon(GameState game)
    {
        if (game.Winner == null) return false;
        game.Phase = GamePhase.GameOver;
        game.PhaseEndsAt = null;
        game.Status = GameStatus.Ended;
        BumpVersion(game);
        return true;
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

    private static (string? Winner, bool IsTie, List<string> TiedIds) ResolveDayVote(ConcurrentDictionary<string, string> votes)
    {
        if (!votes.Any()) return (null, false, new());
        var tally = votes
            .GroupBy(kv => kv.Value)
            .Select(g => (TargetId: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        var maxVotes = tally[0].Count;
        var top = tally.Where(x => x.Count == maxVotes).Select(x => x.TargetId).ToList();
        return top.Count > 1 ? (null, true, top) : (top[0], false, new());
    }

    private static IEnumerable<PlayerState> GetEligibleForMarkDone(GameState game)
    {
        var alive = game.Players.Where(p =>
            p.ParticipationStatus == ParticipationStatus.Participating && !p.IsEliminated);

        return game.Phase switch
        {
            GamePhase.RoleReveal         => alive,
            GamePhase.CupidTurn          => alive.Where(p => p.Skill == PlayerSkill.Cupid),
            GamePhase.LoverReveal        => alive.Where(p => p.PlayerId == game.Lover1Id || p.PlayerId == game.Lover2Id),
            GamePhase.WerewolvesMeeting  => alive.Where(p => p.Role == PlayerRole.Werewolf),
            GamePhase.WerewolvesTurn     => alive.Where(p => p.Role == PlayerRole.Werewolf),
            GamePhase.SeerTurn           => alive.Where(p => p.Skill == PlayerSkill.Seer),
            GamePhase.Discussion         => alive,
            GamePhase.TiebreakDiscussion => alive,
            _                            => Enumerable.Empty<PlayerState>()
        };
    }

    private static bool HasLivingSkill(GameState game, PlayerSkill skill) =>
        game.EnabledSkills.Contains(skill) &&
        game.Players.Any(p =>
            p.Skill == skill &&
            !p.IsEliminated &&
            p.ParticipationStatus == ParticipationStatus.Participating);

    private static void BeginPhase(GameState game, GamePhase phase, TimeSpan? duration = null)
    {
        game.Phase = phase;
        game.PhaseStartedAt = DateTime.UtcNow;
        game.PhaseEndsAt = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
        game.AudioPlayAt = DateTime.UtcNow.AddMilliseconds(2000);
        ResetDone(game);
        BumpVersion(game);
    }

    private static void ResetDone(GameState game)
    {
        foreach (var p in game.Players) p.IsDone = false;
    }

    // â”€â”€ Lobby state recalculation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
        var qrCodeBytes = qrCode.GetGraphic(20, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 }, false);
        return Convert.ToBase64String(qrCodeBytes);
    }
}

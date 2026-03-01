using System.Collections.Concurrent;
using QRCoder;
using WerewolvesAPI.Models;

namespace WerewolvesAPI.Services;

public class GameService
{
    private readonly ConcurrentDictionary<string, GameState> _games = new();
    private readonly ConcurrentDictionary<string, object> _phaseLocks = new();
    private readonly ILogger<GameService> _logger;

    private object GetPhaseLock(string gameId) => _phaseLocks.GetOrAdd(gameId, _ => new object());

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

    // ── Session phase logic ──────────────────────────────────────────────────

    public (bool Success, string? Error) StartGame(string gameId, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Game not found");
        if (game.CreatorId != creatorId)
            return (false, "Only the creator can start the game");
        if (game.Status != GameStatus.ReadyToStart)
            return (false, "Not enough players to start");

        var activePlayers = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        for (int i = 0; i < activePlayers.Count; i++)
            activePlayers[i].Role = i < game.NumberOfWerewolves ? PlayerRole.Werewolf : PlayerRole.Villager;

        foreach (var p in game.Players)
            p.IsDone = false;

        game.Status = GameStatus.InProgress;
        game.Phase = GamePhase.RoleReveal;
        game.RoundNumber = 1;
        game.PhaseEndsAt = null;
        game.NightVotes.Clear();
        game.DayVotes.Clear();
        game.TiebreakCandidates.Clear();
        game.DayTiebreakUsed = false;
        game.LastEliminatedByNight = null;
        game.LastEliminatedByDay = null;
        game.Winner = null;
        BumpVersion(game);
        _logger.LogInformation("Game {GameId} started", gameId);
        return (true, null);
    }

    public (bool Success, string? Error) MarkDone(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Game not found");
        if (game.Status != GameStatus.InProgress)
            return (false, "Game is not in progress");

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null)
            return (false, "Player not found");

        player.IsDone = true;
        BumpVersion(game);

        // Check if all alive players are done (or all eligible players for this phase)
        var eligiblePlayers = game.Players
            .Where(p => p.ParticipationStatus == ParticipationStatus.Participating && !p.IsEliminated);

        if (eligiblePlayers.All(p => p.IsDone))
        {
            lock (GetPhaseLock(gameId))
            {
                if (game.Phase == GamePhase.RoleReveal || game.Phase == GamePhase.Discussion || game.Phase == GamePhase.TiebreakDiscussion)
                    AdvancePhase(game);
            }
        }

        return (true, null);
    }

    public (bool Success, string? Error) CastVote(string gameId, string voterId, string targetId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Game not found");
        if (game.Status != GameStatus.InProgress)
            return (false, "Game is not in progress");

        var voter = game.Players.FirstOrDefault(p => p.PlayerId == voterId);
        if (voter == null)
            return (false, "Voter not found");

        var target = game.Players.FirstOrDefault(p => p.PlayerId == targetId);
        if (target == null)
            return (false, "Target not found");

        if (game.Phase == GamePhase.Night)
        {
            // Only alive werewolves can vote at night
            if (voter.Role != PlayerRole.Werewolf || voter.IsEliminated)
                return (false, "Only alive werewolves can vote at night");
            if (game.RoundNumber == 1)
                return (false, "No kill on the first night");

            game.NightVotes.RemoveAll(v => v.VoterId == voterId);
            game.NightVotes.Add(new Vote { VoterId = voterId, TargetId = targetId });
        }
        else if (game.Phase == GamePhase.Discussion || game.Phase == GamePhase.TiebreakDiscussion)
        {
            if (voter.IsEliminated)
                return (false, "Eliminated players cannot vote");

            if (game.Phase == GamePhase.TiebreakDiscussion && !game.TiebreakCandidates.Contains(targetId))
                return (false, "Can only vote for tied candidates in tiebreak");

            game.DayVotes.RemoveAll(v => v.VoterId == voterId);
            game.DayVotes.Add(new Vote { VoterId = voterId, TargetId = targetId });
        }
        else
        {
            return (false, "Voting is not open in this phase");
        }

        BumpVersion(game);
        return (true, null);
    }

    public (bool Success, string? Error) ForceAdvancePhase(string gameId, string creatorId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Game not found");
        if (game.CreatorId != creatorId)
            return (false, "Only the creator can force advance");
        if (game.Status != GameStatus.InProgress)
            return (false, "Game is not in progress");

        lock (GetPhaseLock(gameId))
            AdvancePhase(game);

        return (true, null);
    }

    public void TryAdvancePhaseIfExpired(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return;
        if (game.Status != GameStatus.InProgress)
            return;
        if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value)
            return;

        lock (GetPhaseLock(gameId))
        {
            // Re-check inside lock to avoid double transition
            if (game.PhaseEndsAt == null || DateTime.UtcNow < game.PhaseEndsAt.Value)
                return;
            AdvancePhase(game);
        }
    }

    public (string Role, List<string> FellowWerewolves) GetPlayerRole(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return ("Unknown", new());

        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player?.Role == null)
            return ("Unknown", new List<string>());

        var fellows = new List<string>();
        if (player.Role == PlayerRole.Werewolf && game.Phase == GamePhase.Night)
        {
            fellows = game.Players
                .Where(p => p.Role == PlayerRole.Werewolf && p.PlayerId != playerId && !p.IsEliminated)
                .Select(p => p.DisplayName)
                .ToList();
        }

        return (player.Role.ToString()!, fellows);
    }

    private void AdvancePhase(GameState game)
    {
        switch (game.Phase)
        {
            case GamePhase.RoleReveal:
                TransitionToNight(game);
                break;

            case GamePhase.Night:
                if (game.RoundNumber == 1)
                {
                    // No kill on first night — go straight to Discussion
                    TransitionToDiscussion(game);
                }
                else
                {
                    var nightKill = ResolveNightVote(game);
                    game.LastEliminatedByNight = nightKill;
                    if (nightKill != null)
                    {
                        var p = game.Players.First(p => p.PlayerId == nightKill);
                        p.IsEliminated = true;
                    }
                    game.Phase = GamePhase.NightElimination;
                    game.PhaseEndsAt = DateTime.UtcNow.AddSeconds(10);
                    EvaluateWinCondition(game);
                    ResetDone(game);
                    BumpVersion(game);
                }
                break;

            case GamePhase.NightElimination:
                if (TransitionToGameOverIfWon(game)) return;
                TransitionToDiscussion(game);
                break;

            case GamePhase.Discussion:
                var (winnerId, isTie, tiedIds) = ResolveDayVote(game, game.DayVotes);
                if (isTie && !game.DayTiebreakUsed)
                {
                    game.Phase = GamePhase.TiebreakDiscussion;
                    game.TiebreakCandidates = tiedIds;
                    game.DayTiebreakUsed = true;
                    game.DayVotes.Clear();
                    game.PhaseEndsAt = DateTime.UtcNow.AddSeconds(60);
                    ResetDone(game);
                    BumpVersion(game);
                }
                else
                {
                    FinalizeDayElimination(game, winnerId);
                }
                break;

            case GamePhase.TiebreakDiscussion:
                var (tbWinner, tbIsTie, _) = ResolveDayVote(game, game.DayVotes);
                FinalizeDayElimination(game, tbIsTie ? null : tbWinner);
                break;

            case GamePhase.DayElimination:
                if (TransitionToGameOverIfWon(game)) return;
                game.RoundNumber++;
                TransitionToNight(game);
                break;

            case GamePhase.GameOver:
                break;
        }
    }

    private void TransitionToNight(GameState game)
    {
        game.Phase = GamePhase.Night;
        game.PhaseEndsAt = DateTime.UtcNow.AddSeconds(30);
        game.NightVotes.Clear();
        game.LastEliminatedByNight = null;
        game.DayTiebreakUsed = false;
        game.TiebreakCandidates.Clear();
        ResetDone(game);
        BumpVersion(game);
        _logger.LogInformation("Game {GameId} → Night (round {Round})", game.GameId, game.RoundNumber);
    }

    private void TransitionToDiscussion(GameState game)
    {
        game.Phase = GamePhase.Discussion;
        game.PhaseEndsAt = DateTime.UtcNow.AddMinutes(game.DiscussionDurationMinutes);
        game.DayVotes.Clear();
        game.LastEliminatedByDay = null;
        game.DayTiebreakUsed = false;
        game.TiebreakCandidates.Clear();
        ResetDone(game);
        BumpVersion(game);
        _logger.LogInformation("Game {GameId} → Discussion (round {Round})", game.GameId, game.RoundNumber);
    }

    private void FinalizeDayElimination(GameState game, string? eliminatedId)
    {
        game.LastEliminatedByDay = eliminatedId;
        if (eliminatedId != null)
        {
            var p = game.Players.First(p => p.PlayerId == eliminatedId);
            p.IsEliminated = true;
        }
        game.Phase = GamePhase.DayElimination;
        game.PhaseEndsAt = DateTime.UtcNow.AddSeconds(10);
        EvaluateWinCondition(game);
        ResetDone(game);
        BumpVersion(game);
        _logger.LogInformation("Game {GameId} → DayElimination (eliminated: {Id}, winner: {Winner})", game.GameId, eliminatedId ?? "none", game.Winner ?? "none");
    }

    /// <summary>Evaluates win condition and sets Winner if met, but does NOT change Phase.</summary>
    private static void EvaluateWinCondition(GameState game)
    {
        var alivePlayers = game.Players.Where(p => !p.IsEliminated && p.ParticipationStatus == ParticipationStatus.Participating).ToList();
        var aliveWerewolves = alivePlayers.Count(p => p.Role == PlayerRole.Werewolf);
        var aliveVillagers = alivePlayers.Count(p => p.Role == PlayerRole.Villager);

        if (aliveWerewolves == 0)
            game.Winner = "Villagers";
        else if (aliveVillagers == 0)
            game.Winner = "Werewolves";
    }

    /// <summary>Transitions to GameOver if Winner is already set, otherwise returns false.</summary>
    private static bool TransitionToGameOverIfWon(GameState game)
    {
        if (game.Winner == null) return false;
        game.Phase = GamePhase.GameOver;
        game.PhaseEndsAt = null;
        game.Status = GameStatus.Ended;
        BumpVersion(game); // Must bump so polling clients see the transition
        return true;
    }

    private static string? ResolveNightVote(GameState game)
    {
        if (!game.NightVotes.Any()) return null;

        var tally = game.NightVotes
            .GroupBy(v => v.TargetId)
            .Select(g => (TargetId: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var maxVotes = tally[0].Count;
        var topVoters = tally.Where(x => x.Count == maxVotes).ToList();

        // Tie among werewolves = no kill
        return topVoters.Count == 1 ? topVoters[0].TargetId : null;
    }

    private static (string? Winner, bool IsTie, List<string> TiedIds) ResolveDayVote(GameState game, List<Vote> votes)
    {
        if (!votes.Any()) return (null, false, new());

        var tally = votes
            .GroupBy(v => v.TargetId)
            .Select(g => (TargetId: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var maxVotes = tally[0].Count;
        var topCandidates = tally.Where(x => x.Count == maxVotes).Select(x => x.TargetId).ToList();

        if (topCandidates.Count > 1)
            return (null, true, topCandidates);

        return (topCandidates[0], false, new());
    }

    private static void ResetDone(GameState game)
    {
        foreach (var p in game.Players)
            p.IsDone = false;
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

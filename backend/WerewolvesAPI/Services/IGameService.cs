using WerewolvesAPI.DTOs;
using WerewolvesAPI.Models;

namespace WerewolvesAPI.Services;

public interface IGameService
{
    Task InitializeAsync();
    GameState CreateGame(string creatorName, int maxPlayers, string baseUrl);
    GameState? GetGame(string tournamentCode);
    (bool Success, string? Message, PlayerState? Player) JoinGame(string tournamentCode, string displayName, string? existingPlayerId = null);
    bool LeaveGame(string tournamentCode, string playerId);
    bool RemovePlayer(string tournamentCode, string playerId, string moderatorId);
    bool UpdateMaxPlayers(string tournamentCode, int maxPlayers, string moderatorId);
    bool UpdateMinPlayers(string tournamentCode, int minPlayers, string moderatorId);
    bool UpdatePlayerName(string tournamentCode, string playerId, string displayName);
    bool HasDuplicateNames(string tournamentCode);
    bool UpdateDiscussionDuration(string tournamentCode, int minutes, string moderatorId);
    bool UpdateTiebreakDiscussionDuration(string tournamentCode, int seconds, string moderatorId);
    bool UpdateNumberOfWerewolves(string tournamentCode, int count, string moderatorId);
    bool UpdateEnabledSkills(string tournamentCode, List<string> skillNames, string moderatorId);
    (bool Success, string? Error) StartGame(string tournamentCode, string moderatorId);
    (bool Success, string? Error) MarkDone(string tournamentCode, string playerId);
    (bool Success, string? Error) CastVote(string tournamentCode, string voterId, string targetId);
    (bool Success, string? Error) CupidAction(string tournamentCode, string cupidId, string lover1Id, string lover2Id);
    (bool Success, string? Error, SeerActionResponse? Result) SeerAction(string tournamentCode, string seerId, string targetId);
    (bool Success, string? Error) WitchAction(string tournamentCode, string witchId, string choice, string? poisonTargetId);
    (bool Success, string? Error) HunterAction(string tournamentCode, string hunterId, string targetId);
    (bool Success, string? Error) ForceAdvancePhase(string tournamentCode, string moderatorId);
    (bool Success, string? Error) ExtendDiscussion(string tournamentCode, string moderatorId);
    void TryAdvancePhaseIfExpired(string tournamentCode);
    Task<(bool Success, string? Error)> UnlockTournamentAsync(string tournamentCode, string code);
    bool SetPremium(string tournamentCode);
    (string Role, string? Skill, List<string> FellowWerewolves, string? LoverName, string? NightKillTargetName, bool WitchHealUsed, bool WitchPoisonUsed) GetPlayerRole(string tournamentCode, string playerId);
}

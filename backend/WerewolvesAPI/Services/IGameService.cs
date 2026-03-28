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
    bool UpdateMaxPlayers(string tournamentCode, int maxPlayers, string creatorId);
    bool UpdateMinPlayers(string tournamentCode, int minPlayers, string creatorId);
    bool UpdatePlayerName(string tournamentCode, string playerId, string displayName);
    bool HasDuplicateNames(string tournamentCode);
    bool UpdateDiscussionDuration(string tournamentCode, int minutes, string creatorId);
    bool UpdateNumberOfWerewolves(string tournamentCode, int count, string creatorId);
    bool UpdateEnabledSkills(string tournamentCode, List<string> skillNames, string creatorId);
    (bool Success, string? Error) StartGame(string tournamentCode, string creatorId);
    (bool Success, string? Error) MarkDone(string tournamentCode, string playerId);
    (bool Success, string? Error) CastVote(string tournamentCode, string voterId, string targetId);
    (bool Success, string? Error) CupidAction(string tournamentCode, string cupidId, string lover1Id, string lover2Id);
    (bool Success, string? Error, SeerActionResponse? Result) SeerAction(string tournamentCode, string seerId, string targetId);
    (bool Success, string? Error) WitchAction(string tournamentCode, string witchId, string choice, string? poisonTargetId);
    (bool Success, string? Error) HunterAction(string tournamentCode, string hunterId, string targetId);
    (bool Success, string? Error) ForceAdvancePhase(string tournamentCode, string creatorId);
    void TryAdvancePhaseIfExpired(string tournamentCode);
    (bool Success, string? Error) UnlockTournament(string tournamentCode, string code);
    (string Role, string Skill, List<string> FellowWerewolves, string? LoverName, string? NightKillTargetName, bool WitchHealUsed, bool WitchPoisonUsed) GetPlayerRole(string tournamentCode, string playerId);
}

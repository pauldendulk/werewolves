using WerewolvesAPI.DTOs;
using WerewolvesAPI.Models;

namespace WerewolvesAPI.Services;

public interface IGameService
{
    GameState CreateGame(string gameName, string creatorName, int maxPlayers, string baseUrl);
    GameState? GetGame(string gameId);
    (bool Success, string? Message, PlayerState? Player) JoinGame(string gameId, string displayName, string? existingPlayerId = null);
    bool LeaveGame(string gameId, string playerId);
    bool RemovePlayer(string gameId, string playerId, string moderatorId);
    bool UpdateMaxPlayers(string gameId, int maxPlayers, string creatorId);
    bool UpdateMinPlayers(string gameId, int minPlayers, string creatorId);
    bool UpdateGameName(string gameId, string gameName, string creatorId);
    bool UpdatePlayerName(string gameId, string playerId, string displayName);
    bool HasDuplicateNames(string gameId);
    bool UpdateDiscussionDuration(string gameId, int minutes, string creatorId);
    bool UpdateNumberOfWerewolves(string gameId, int count, string creatorId);
    bool UpdateEnabledSkills(string gameId, List<string> skillNames, string creatorId);
    (bool Success, string? Error) StartGame(string gameId, string creatorId);
    (bool Success, string? Error) MarkDone(string gameId, string playerId);
    (bool Success, string? Error) CastVote(string gameId, string voterId, string targetId);
    (bool Success, string? Error) CupidAction(string gameId, string cupidId, string lover1Id, string lover2Id);
    (bool Success, string? Error, SeerActionResponse? Result) SeerAction(string gameId, string seerId, string targetId);
    (bool Success, string? Error) WitchAction(string gameId, string witchId, string choice, string? poisonTargetId);
    (bool Success, string? Error) HunterAction(string gameId, string hunterId, string targetId);
    (bool Success, string? Error) ForceAdvancePhase(string gameId, string creatorId);
    void TryAdvancePhaseIfExpired(string gameId);
    (string Role, string Skill, List<string> FellowWerewolves, string? LoverName, string? NightKillTargetName, bool WitchHealUsed, bool WitchPoisonUsed) GetPlayerRole(string gameId, string playerId);
}

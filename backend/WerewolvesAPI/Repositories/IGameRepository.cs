namespace WerewolvesAPI.Repositories;

public record GameRecord(
    string Id,
    string? TournamentId,
    string JoinCode,
    string Status,
    string? Winner,
    string Settings,
    DateTime CreatedAt,
    DateTime? EndedAt);

public record GamePlayerRecord(
    string GameId,
    string PlayerId,
    string DisplayName,
    string? Role,
    string? Skill,
    bool IsEliminated,
    string? EliminationCause,
    int Score,
    int VotesCast,
    int VotesCorrect,
    bool IsCreator,
    bool IsModerator,
    string ParticipationStatus,
    DateTime JoinedAt);

public interface IGameRepository
{
    Task SaveGameAsync(GameRecord game);
    Task SaveGamePlayersAsync(IEnumerable<GamePlayerRecord> players);
    Task UpsertLiveStateAsync(string tournamentCode, string stateJson);
    Task<IEnumerable<string>> LoadAllLiveStatesAsync();
}

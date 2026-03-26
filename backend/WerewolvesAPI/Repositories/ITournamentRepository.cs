namespace WerewolvesAPI.Repositories;

public record TournamentRecord(
    Guid Id,
    string? Name,
    string? JoinCode,
    string HostPlayerId,
    DateTime CreatedAt,
    bool IsPremium);

public record TournamentParticipantRecord(
    Guid TournamentId,
    string PlayerId,
    string DisplayName,
    int TotalScore,
    DateTime JoinedAt);

public interface ITournamentRepository
{
    Task<TournamentRecord?> GetByIdAsync(Guid tournamentId);
    Task SaveTournamentAsync(TournamentRecord tournament);
    Task UpsertParticipantAsync(TournamentParticipantRecord participant);
    Task AddToParticipantScoreAsync(Guid tournamentId, string playerId, int points);
    Task<IEnumerable<TournamentParticipantRecord>> GetParticipantsAsync(Guid tournamentId);
}

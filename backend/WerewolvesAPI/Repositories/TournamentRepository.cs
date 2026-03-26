using Dapper;
using Npgsql;

namespace WerewolvesAPI.Repositories;

public class TournamentRepository(string connectionString) : ITournamentRepository
{
    private NpgsqlConnection Open() => new(connectionString);

    public async Task<TournamentRecord?> GetByIdAsync(Guid tournamentId)
    {
        using var conn = Open();
        return await conn.QuerySingleOrDefaultAsync<TournamentRecord>(
            "SELECT id, name, host_player_id, created_at, is_premium FROM tournaments WHERE id = @tournamentId",
            new { tournamentId });
    }

    public async Task SaveTournamentAsync(TournamentRecord tournament)
    {
        using var conn = Open();
        await conn.ExecuteAsync("""
            INSERT INTO tournaments (id, name, join_code, host_player_id, created_at, is_premium)
            VALUES (@Id, @Name, @JoinCode, @HostPlayerId, @CreatedAt, @IsPremium)
            ON CONFLICT (id) DO UPDATE SET
                name       = EXCLUDED.name,
                join_code  = COALESCE(tournaments.join_code, EXCLUDED.join_code),
                is_premium = EXCLUDED.is_premium
            """, tournament);
    }

    public async Task UpsertParticipantAsync(TournamentParticipantRecord participant)
    {
        using var conn = Open();
        await conn.ExecuteAsync("""
            INSERT INTO tournament_participants (tournament_id, player_id, display_name, total_score, joined_at)
            VALUES (@TournamentId, @PlayerId, @DisplayName, @TotalScore, @JoinedAt)
            ON CONFLICT (tournament_id, player_id) DO UPDATE SET
                display_name = EXCLUDED.display_name
            """, participant);
    }

    public async Task AddToParticipantScoreAsync(Guid tournamentId, string playerId, int points)
    {
        using var conn = Open();
        await conn.ExecuteAsync("""
            UPDATE tournament_participants
            SET total_score = total_score + @points
            WHERE tournament_id = @tournamentId AND player_id = @playerId
            """, new { tournamentId, playerId, points });
    }

    public async Task<IEnumerable<TournamentParticipantRecord>> GetParticipantsAsync(Guid tournamentId)
    {
        using var conn = Open();
        return await conn.QueryAsync<TournamentParticipantRecord>(
            "SELECT tournament_id, player_id, display_name, total_score, joined_at FROM tournament_participants WHERE tournament_id = @tournamentId ORDER BY total_score DESC",
            new { tournamentId });
    }
}

using Dapper;
using Npgsql;

namespace WerewolvesAPI.Repositories;

public class GameRepository(string connectionString) : IGameRepository
{
    private NpgsqlConnection Open() => new(connectionString);

    public async Task SaveGameAsync(GameRecord game)
    {
        using var conn = Open();
        await conn.ExecuteAsync("""
            INSERT INTO games (id, tournament_id, join_code, status, winner, settings, created_at, ended_at)
            VALUES (@Id, @TournamentId::uuid, @JoinCode, @Status, @Winner, @Settings::jsonb, @CreatedAt, @EndedAt)
            ON CONFLICT (id) DO UPDATE SET
                status     = EXCLUDED.status,
                winner     = EXCLUDED.winner,
                ended_at   = EXCLUDED.ended_at
            """, game);
    }

    public async Task SaveGamePlayersAsync(IEnumerable<GamePlayerRecord> players)
    {
        using var conn = Open();
        await conn.ExecuteAsync("""
            INSERT INTO game_players
                (game_id, player_id, display_name, role, skill, is_eliminated, elimination_cause,
                 score, votes_cast, votes_correct, is_creator, is_moderator, participation_status, joined_at)
            VALUES
                (@GameId, @PlayerId, @DisplayName, @Role, @Skill, @IsEliminated, @EliminationCause,
                 @Score, @VotesCast, @VotesCorrect, @IsCreator, @IsModerator, @ParticipationStatus, @JoinedAt)
            ON CONFLICT (game_id, player_id) DO UPDATE SET
                role                 = EXCLUDED.role,
                skill                = EXCLUDED.skill,
                is_eliminated        = EXCLUDED.is_eliminated,
                elimination_cause    = EXCLUDED.elimination_cause,
                score                = EXCLUDED.score,
                votes_cast           = EXCLUDED.votes_cast,
                votes_correct        = EXCLUDED.votes_correct,
                participation_status = EXCLUDED.participation_status
            """, players);
    }

    public async Task UpsertLiveStateAsync(string tournamentCode, string stateJson)
    {
        using var conn = Open();
        await conn.ExecuteAsync("""
            INSERT INTO game_live_state (tournament_code, state, saved_at)
            VALUES (@TournamentCode, @State::jsonb, NOW())
            ON CONFLICT (tournament_code) DO UPDATE SET
                state    = EXCLUDED.state,
                saved_at = NOW()
            """, new { TournamentCode = tournamentCode, State = stateJson });
    }

    public async Task<IEnumerable<string>> LoadAllLiveStatesAsync()
    {
        using var conn = Open();
        return await conn.QueryAsync<string>("SELECT state FROM game_live_state");
    }
}

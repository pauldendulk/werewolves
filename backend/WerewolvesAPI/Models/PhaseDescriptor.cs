namespace WerewolvesAPI.Models;

public record PhaseDescriptor(
    GamePhase Phase,
    Func<GameState, TimeSpan?> Duration,
    Func<GameState, IEnumerable<PlayerState>> EligibleForDone
)
{
    private static IEnumerable<PlayerState> Alive(GameState game) =>
        game.Players.Where(p => p.ParticipationStatus == ParticipationStatus.Participating && !p.IsEliminated);

    public static readonly IReadOnlyDictionary<GamePhase, PhaseDescriptor> All =
        new PhaseDescriptor[]
        {
            new(GamePhase.RoleReveal,
                _ => null,
                Alive),

            new(GamePhase.NightAnnouncement,
                _ => TimeSpan.FromSeconds(8),
                _ => []),

            new(GamePhase.WerewolvesMeeting,
                _ => null,
                game => Alive(game).Where(p => p.Role == PlayerRole.Werewolf)),

            new(GamePhase.CupidTurn,
                _ => null,
                game => Alive(game).Where(p => p.Skill == PlayerSkill.Cupid)),

            new(GamePhase.LoverReveal,
                _ => TimeSpan.FromSeconds(20),
                game => Alive(game).Where(p => p.PlayerId == game.Lover1Id || p.PlayerId == game.Lover2Id)),

            new(GamePhase.WerewolvesTurn,
                _ => null,
                game => Alive(game).Where(p => p.Role == PlayerRole.Werewolf)),

            new(GamePhase.SeerTurn,
                _ => null,
                game => Alive(game).Where(p => p.Skill == PlayerSkill.Seer)),

            new(GamePhase.WitchTurn,
                _ => null,
                _ => []),

            new(GamePhase.DayAnnouncement,
                _ => TimeSpan.FromSeconds(8),
                _ => []),

            new(GamePhase.HunterTurn,
                _ => null,
                _ => []),

            new(GamePhase.NightEliminationReveal,
                _ => TimeSpan.FromSeconds(10),
                _ => []),

            new(GamePhase.Discussion,
                game => TimeSpan.FromMinutes(game.DiscussionDurationMinutes),
                Alive),

            new(GamePhase.TiebreakDiscussion,
                game => TimeSpan.FromSeconds(game.TiebreakDiscussionDurationSeconds),
                Alive),

            new(GamePhase.DayEliminationReveal,
                _ => TimeSpan.FromSeconds(10),
                _ => []),

            new(GamePhase.FinalScoresReveal,
                _ => TimeSpan.FromMinutes(1),
                game => game.Players.Where(p => p.ParticipationStatus == ParticipationStatus.Participating)),
        }.ToDictionary(d => d.Phase);

    public static PhaseDescriptor Get(GamePhase phase) => All[phase];
}

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
                _ => TimeSpan.FromSeconds(12),
                _ => []),

            new(GamePhase.WerewolvesMeeting,
                _ => null,
                game => Alive(game).Where(p => p.Role == PlayerRole.Werewolf)),

            new(GamePhase.WerewolvesCloseEyes,
                _ => TimeSpan.FromSeconds(6),
                _ => []),

            new(GamePhase.Cupid,
                _ => null,
                game => Alive(game).Where(p => p.Skill == PlayerSkill.Cupid)),

            new(GamePhase.CupidCloseEyes,
                _ => TimeSpan.FromSeconds(6),
                _ => []),

            new(GamePhase.LoversReveal,
                _ => TimeSpan.FromSeconds(20),
                game => Alive(game).Where(p => p.PlayerId == game.Lover1Id || p.PlayerId == game.Lover2Id)),

            new(GamePhase.Werewolves,
                _ => null,
                game => Alive(game).Where(p => p.Role == PlayerRole.Werewolf)),

            new(GamePhase.Seer,
                _ => null,
                game => Alive(game).Where(p => p.Skill == PlayerSkill.Seer)),

            new(GamePhase.SeerCloseEyes,
                _ => TimeSpan.FromSeconds(6),
                _ => []),

            new(GamePhase.Witch,
                _ => null,
                _ => []),

            new(GamePhase.WitchCloseEyes,
                _ => TimeSpan.FromSeconds(6),
                _ => []),

            new(GamePhase.DayAnnouncement,
                _ => TimeSpan.FromSeconds(12),
                _ => []),

            new(GamePhase.Hunter,
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

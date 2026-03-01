namespace WerewolvesAPI.Models;

public class GameState
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public int MinPlayers { get; set; } = 3;
    public int MaxPlayers { get; set; } = 20;
    public string JoinLink { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public GameStatus Status { get; set; } = GameStatus.WaitingForPlayers;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PlayerState> Players { get; set; } = new();
    public int Version { get; set; } = 1;
    public int DiscussionDurationMinutes { get; set; } = 5;
    public int NumberOfWerewolves { get; set; } = 1;

    // Session state
    public GamePhase Phase { get; set; } = GamePhase.RoleReveal;
    public int RoundNumber { get; set; } = 0;
    public DateTime? PhaseEndsAt { get; set; }
    public List<Vote> NightVotes { get; set; } = new();
    public List<Vote> DayVotes { get; set; } = new();
    public List<string> TiebreakCandidates { get; set; } = new();
    public bool DayTiebreakUsed { get; set; }
    public string? LastEliminatedByNight { get; set; }
    public string? LastEliminatedByDay { get; set; }
    public string? Winner { get; set; }
}

public enum GameStatus
{
    WaitingForPlayers,
    ReadyToStart,
    InProgress,
    Ended
}

public enum GamePhase
{
    RoleReveal,
    Night,
    NightElimination,
    Discussion,
    TiebreakDiscussion,
    DayElimination,
    GameOver
}

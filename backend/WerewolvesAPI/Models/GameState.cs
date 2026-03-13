using System.Collections.Concurrent;

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
    public List<PlayerSkill> EnabledSkills { get; set; } = new() { PlayerSkill.Seer, PlayerSkill.Cupid, PlayerSkill.Witch, PlayerSkill.Hunter };

    // Session state
    public GamePhase Phase { get; set; } = GamePhase.RoleReveal;
    public int RoundNumber { get; set; } = 0;
    public DateTime? PhaseEndsAt { get; set; }
    public DateTime? PhaseStartedAt { get; set; }
    public ConcurrentDictionary<string, string> NightVotes { get; set; } = new();
    public ConcurrentDictionary<string, string> DayVotes { get; set; } = new();
    public List<string> TiebreakCandidates { get; set; } = new();
    public bool DayTiebreakUsed { get; set; }
    public string? Winner { get; set; }

    // Skill state
    public string? Lover1Id { get; set; }
    public string? Lover2Id { get; set; }
    public bool WitchHealUsed { get; set; }
    public bool WitchPoisonUsed { get; set; }
    public string? NightKillTargetId { get; set; }
    public bool WitchSavedThisNight { get; set; }
    public string? WitchPoisonTargetId { get; set; }
    public bool HunterMustShoot { get; set; }
    public bool HunterEliminatedAtNight { get; set; }
    public List<EliminationEntry> NightDeaths { get; set; } = new();
    public List<EliminationEntry> DayDeaths { get; set; } = new();
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
    CupidTurn,
    LoverReveal,
    WerewolvesMeeting,
    SeerTurn,
    WerewolvesTurn,
    WitchTurn,
    NightElimination,
    HunterTurn,
    Discussion,
    TiebreakDiscussion,
    DayElimination,
    GameOver
}

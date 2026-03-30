using System.Collections.Concurrent;

namespace WerewolvesAPI.Models;

public class GameState
{
    public string GameId { get; set; } = string.Empty;
    public string TournamentId { get; set; } = string.Empty;
    public string TournamentCode { get; set; } = string.Empty;
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
    public int TiebreakDiscussionDurationSeconds { get; set; } = 60;
    public int NumberOfWerewolves { get; set; } = 1;
    public List<PlayerSkill> EnabledSkills { get; set; } = new() { PlayerSkill.Seer };

    // Session state
    public GamePhase Phase { get; set; } = GamePhase.RoleReveal;
    public int RoundNumber { get; set; } = 0;
    public DateTime? PhaseEndsAt { get; set; }
    public DateTime? PhaseStartedAt { get; set; }
    public DateTime? AudioPlayAt { get; set; }
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

    public int GameIndex { get; set; } = 1;
    public bool IsPremium { get; set; } = false;

    // Add new per-game fields here alongside their property initializers above.
    public void ResetSessionState()
    {
        Phase = GamePhase.RoleReveal;
        RoundNumber = 0;
        PhaseEndsAt = null;
        PhaseStartedAt = null;
        AudioPlayAt = null;
        NightVotes = new();
        DayVotes = new();
        TiebreakCandidates = new();
        DayTiebreakUsed = false;
        Winner = null;

        // Skill state
        Lover1Id = null;
        Lover2Id = null;
        WitchHealUsed = false;
        WitchPoisonUsed = false;
        NightKillTargetId = null;
        WitchSavedThisNight = false;
        WitchPoisonTargetId = null;
        HunterMustShoot = false;
        HunterEliminatedAtNight = false;
        NightDeaths = new();
        DayDeaths = new();

        foreach (var p in Players)
            p.ResetForNewGame();
    }
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
    NightEliminationReveal,
    HunterTurn,
    Discussion,
    TiebreakDiscussion,
    DayEliminationReveal,
    FinalScoresReveal
}

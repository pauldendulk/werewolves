namespace WerewolvesAPI.DTOs;

public class LobbyStateDto
{
    public GameInfoDto Game { get; set; } = new();
    public List<PlayerDto> Players { get; set; } = new();
    public bool HasDuplicateNames { get; set; }
}

public class GameInfoDto
{
    public string GameId { get; set; } = string.Empty;
    public string TournamentCode { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public int MinPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public string JoinLink { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public int DiscussionDurationMinutes { get; set; }
    public int NumberOfWerewolves { get; set; }
    public List<string> EnabledSkills { get; set; } = new();
    // Session state
    public string Phase { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public DateTime? PhaseEndsAt { get; set; }
    public DateTime? PhaseStartedAt { get; set; }
    public DateTime? AudioPlayAt { get; set; }
    public List<EliminationEntryDto> NightDeaths { get; set; } = new();
    public List<EliminationEntryDto> DayDeaths { get; set; } = new();
    public string? Winner { get; set; }
    public List<string> TiebreakCandidates { get; set; } = new();
}

public class PlayerDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCreator { get; set; }
    public bool IsModerator { get; set; }
    public bool IsConnected { get; set; }
    public string ParticipationStatus { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Skill { get; set; }
    public bool IsEliminated { get; set; }
    public bool IsDone { get; set; }
    public int Score { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class PlayerRoleDto
{
    public string Role { get; set; } = string.Empty;
    public string? Skill { get; set; }
    public List<string> FellowWerewolves { get; set; } = new();
    public string? LoverName { get; set; }
    public string? NightKillTargetName { get; set; }
    public bool WitchHealUsed { get; set; }
    public bool WitchPoisonUsed { get; set; }
}


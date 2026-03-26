namespace WerewolvesAPI.Models;

public class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCreator { get; set; }
    public bool IsModerator { get; set; }
    public bool IsConnected { get; set; } = true;
    public ParticipationStatus ParticipationStatus { get; set; } = ParticipationStatus.Participating;
    public PlayerRole? Role { get; set; }
    public PlayerSkill Skill { get; set; } = PlayerSkill.None;
    public bool IsEliminated { get; set; }
    public bool IsDone { get; set; }
    public int Score { get; set; } = 0;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public void ResetForNewGame()
    {
        Role = null;
        Skill = PlayerSkill.None;
        IsEliminated = false;
        IsDone = false;
        Score = 0;
    }
}

public enum ParticipationStatus
{
    Participating,
    Left,
    Removed
}

public enum PlayerRole
{
    Villager,
    Werewolf
}

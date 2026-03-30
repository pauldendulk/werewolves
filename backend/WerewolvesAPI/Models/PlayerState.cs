namespace WerewolvesAPI.Models;

public class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Identifies the player who created this tournament session.
    /// This is a permanent, informational flag shown as the "HOST" badge in the lobby.
    /// It does not grant any permissions by itself.
    /// </summary>
    public bool IsCreator { get; set; }

    /// <summary>
    /// Grants moderation rights: starting the game, changing settings, removing players,
    /// and force-advancing phases. The creator always starts as a moderator, but this flag
    /// can in principle be granted to other players independently of who created the session.
    /// </summary>
    public bool IsModerator { get; set; }
    public bool IsConnected { get; set; } = true;
    public ParticipationStatus ParticipationStatus { get; set; } = ParticipationStatus.Participating;
    public PlayerRole? Role { get; set; }
    public PlayerSkill Skill { get; set; } = PlayerSkill.None;
    public bool IsEliminated { get; set; }
    public bool IsDone { get; set; }
    public int Score { get; set; } = 0;
    public int TotalScore { get; set; } = 0;
    public int VotesCast { get; set; } = 0;
    public int VotesCorrect { get; set; } = 0;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public void ResetForNewGame()
    {
        Role = null;
        Skill = PlayerSkill.None;
        IsEliminated = false;
        IsDone = false;
        Score = 0;
        VotesCast = 0;
        VotesCorrect = 0;
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

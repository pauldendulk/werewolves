namespace WerewolvesAPI.Models;

public class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCreator { get; set; }
    public bool IsModerator { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Connected;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public enum PlayerStatus
{
    Connected,
    Disconnected,
    Left,
    Removed
}

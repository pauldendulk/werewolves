namespace WerewolvesAPI.Models;

public class GameState
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public string CreatorName { get; set; } = string.Empty;
    public int MinPlayers { get; set; } = 4;
    public int MaxPlayers { get; set; } = 20;
    public string JoinLink { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public GameStatus Status { get; set; } = GameStatus.Lobby;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PlayerState> Players { get; set; } = new();
}

public enum GameStatus
{
    Lobby,
    InProgress,
    Ended
}

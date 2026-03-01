namespace WerewolvesAPI.DTOs;

public class RemovePlayerRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string ModeratorId { get; set; } = string.Empty;
}

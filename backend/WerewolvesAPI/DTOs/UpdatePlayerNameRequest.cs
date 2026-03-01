namespace WerewolvesAPI.DTOs;

public class UpdatePlayerNameRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

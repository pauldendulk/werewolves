namespace WerewolvesAPI.DTOs;

public class CreateGameResponse
{
    public string TournamentCode { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string JoinLink { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
}

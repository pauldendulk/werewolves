using System.ComponentModel.DataAnnotations;

namespace WerewolvesAPI.DTOs;

public class CreateGameRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string GameName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string CreatorName { get; set; } = string.Empty;
    
    [Range(2, 40)]
    public int MaxPlayers { get; set; } = 20;

    [Required]
    public string FrontendBaseUrl { get; set; } = string.Empty;
}

public class CreateGameResponse
{
    public string GameId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string JoinLink { get; set; } = string.Empty;
    public string QrCodeBase64 { get; set; } = string.Empty;
}

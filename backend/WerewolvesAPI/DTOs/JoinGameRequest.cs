using System.ComponentModel.DataAnnotations;

namespace WerewolvesAPI.DTOs;

public class JoinGameRequest
{
    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string DisplayName { get; set; } = string.Empty;
    
    public string? PlayerId { get; set; }
}

public class JoinGameResponse
{
    public string PlayerId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
}

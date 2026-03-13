namespace WerewolvesAPI.DTOs;

public class WitchActionRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string Choice { get; set; } = string.Empty; // "save", "nothing", "poison"
    public string? PoisonTargetId { get; set; }
}

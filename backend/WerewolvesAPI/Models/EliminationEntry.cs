namespace WerewolvesAPI.Models;

public class EliminationEntry
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public EliminationCause Cause { get; set; }
}

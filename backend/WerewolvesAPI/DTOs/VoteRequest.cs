namespace WerewolvesAPI.DTOs;

public class VoteRequest
{
    public string VoterId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
}

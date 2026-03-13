using System.ComponentModel.DataAnnotations;

namespace WerewolvesAPI.DTOs;

public class UpdateSettingsRequest
{
    public string CreatorId { get; set; } = string.Empty;

    [Range(2, 40)]
    public int MinPlayers { get; set; }

    [Range(2, 40)]
    public int MaxPlayers { get; set; }

    [Range(1, 30)]
    public int DiscussionDurationMinutes { get; set; } = 5;

    [Range(1, 40)]
    public int NumberOfWerewolves { get; set; } = 1;

    public List<string>? EnabledSkills { get; set; }
}

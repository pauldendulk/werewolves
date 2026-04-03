namespace NarrationStudio.Models;

public record NarrationEntry(string Key, string Description, string Text, int Version, string? Style, bool ForCreatorOnly);

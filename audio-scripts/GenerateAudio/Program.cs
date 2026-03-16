// Generates MP3 narration files from narration.json using Azure Cognitive Services TTS.
//
// Prerequisites:
//   Set environment variables:
//     AZURE_TTS_KEY    — your Azure Speech resource key
//     AZURE_TTS_REGION — your Azure region (e.g. eastus), defaults to eastus
//
// Usage (from audio-scripts/GenerateAudio/):
//   dotnet run
//
// Output:
//   ../../frontend/werewolves-app/src/assets/audio/{lang}/{key}.mp3

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

// Load .env file if present (one KEY=VALUE per line, # for comments)
var envFile = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
        var parts = line.Split('=', 2);
        if (parts.Length == 2)
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}

var narrationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "narration.json"));
var outputBase   = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend", "werewolves-app", "public", "assets", "audio"));

var apiKey = Environment.GetEnvironmentVariable("AZURE_TTS_KEY")
    ?? throw new InvalidOperationException("AZURE_TTS_KEY environment variable is not set.");
var region = Environment.GetEnvironmentVariable("AZURE_TTS_REGION") ?? "westeurope";

Console.WriteLine($"Reading {narrationPath}");
var json      = await File.ReadAllTextAsync(narrationPath);
var narration = JsonNode.Parse(json)!;

var voice           = narration["voice"]?             .GetValue<string>() ?? "en-US-AriaNeural";
var defaultStyle    = narration["defaultStyle"]?       .GetValue<string>() ?? "whispering";
var defaultDegree   = narration["defaultStyledegree"]? .GetValue<double>() ?? 1.5;

var entries = narration["entries"]!.AsObject();
var endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("User-Agent", "GenerateAudio/1.0");
int generated = 0, skipped = 0;

// Optional key filter: dotnet run -- <key> (e.g. dotnet run -- lover-reveal)
var filterKey = args.Length > 0 ? args[0] : null;
if (filterKey != null) Console.WriteLine($"Filtering to key: {filterKey}");

foreach (var (lang, textsNode) in entries)
{
    var outputDir = Path.Combine(outputBase, lang);
    Directory.CreateDirectory(outputDir);

    foreach (var (key, textNode) in textsNode!.AsObject())
    {
        if (filterKey != null && key != filterKey) continue;
        var text       = textNode!.GetValue<string>();
        var outputPath = Path.Combine(outputDir, $"{key}.mp3");

        Console.Write($"  {lang}/{key}.mp3 ... ");

        var ssml = $"""
            <speak version='1.0'
                   xmlns='http://www.w3.org/2001/10/synthesis'
                   xmlns:mstts='http://www.w3.org/2001/mstts'
                   xml:lang='{lang}'>
              <voice name='{voice}'>
                <mstts:express-as style='{defaultStyle}' styledegree='{defaultDegree}'>
                  {WebUtility.HtmlEncode(text)}
                </mstts:express-as>
              </voice>
            </speak>
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(outputPath, bytes);

        Console.WriteLine($"OK ({bytes.Length / 1024} KB)");
        generated++;
    }
}

Console.WriteLine($"\nDone. Generated {generated} files, skipped {skipped}.");

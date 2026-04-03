// Generates MP3 narration files from narration.json using Azure Cognitive Services TTS.
//
// Prerequisites:
//   Set environment variables:
//     AZURE_TTS_KEY    — your Azure Speech resource key
//     AZURE_TTS_REGION — your Azure region (e.g. westeurope), defaults to westeurope
//
// Usage (from audio-scripts/GenerateAudio/):
//   dotnet run                   — regenerate only entries whose version changed
//   dotnet run -- lover-reveal   — force-regenerate a single key regardless of version
//
// Output:
//   ../../frontend/werewolves-app/public/assets/audio/{lang}/{key}.mp3
// Version tracking:
//   ../generated.json  — records the last-generated version per lang/key

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

var narrationPath  = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "narration.json"));
var generatedPath  = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "generated.json"));
var outputBase     = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend", "werewolves-app", "public", "assets", "audio"));

var apiKey = Environment.GetEnvironmentVariable("AZURE_TTS_KEY")
    ?? throw new InvalidOperationException("AZURE_TTS_KEY environment variable is not set.");
var region = Environment.GetEnvironmentVariable("AZURE_TTS_REGION") ?? "westeurope";

// Load narration.json
Console.WriteLine($"Reading {narrationPath}");
var json      = await File.ReadAllTextAsync(narrationPath);
var narration = JsonNode.Parse(json)!;

var voice         = narration["voice"]?            .GetValue<string>() ?? "en-US-AriaNeural";
var defaultStyle  = narration["defaultStyle"]?     .GetValue<string>() ?? "whispering";
var defaultDegree = narration["defaultStyledegree"]?.GetValue<double>() ?? 1.5;
var entries       = narration["entries"]!.AsObject();

// Load generated.json sidecar (tracks last-generated version per lang/key)
var generatedJson = File.Exists(generatedPath)
    ? JsonNode.Parse(await File.ReadAllTextAsync(generatedPath))!.AsObject()
    : new JsonObject();

// Optional key filter: forces regeneration of one key regardless of version
var filterKey = args.Length > 0 ? args[0] : null;
if (filterKey != null) Console.WriteLine($"Force-regenerating key: {filterKey}");

var endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
using var http = new HttpClient();
http.DefaultRequestHeaders.Add("User-Agent", "GenerateAudio/1.0");
int generated = 0, skipped = 0;

static string PrepareTextForTts(string text)
{
    text = text.Replace("&", "&amp;");
    text = Regex.Replace(text, @"([.!?])(?!\s*<break)(?=\s|$)", "$1 <break time=\"1000ms\" />");
    text = Regex.Replace(text, @",(?!\s*<break)", ", <break time=\"1000ms\" />");
    return $"<prosody rate=\"-10%\">{text}</prosody>";
}

static int GeneratedVersion(JsonObject generatedLang, string key)
{
    if (!generatedLang.ContainsKey(key)) return -1;
    var node = generatedLang[key];
    if (node is JsonObject obj) return obj["version"]?.GetValue<int>() ?? -1;
    return node?.GetValue<int>() ?? -1;
}

foreach (var (lang, textsNode) in entries)
{
    var outputDir = Path.Combine(outputBase, lang);
    Directory.CreateDirectory(outputDir);

    if (!generatedJson.ContainsKey(lang))
        generatedJson[lang] = new JsonObject();
    var generatedLang = generatedJson[lang]!.AsObject();

    foreach (var (key, entryNode) in textsNode!.AsObject())
    {
        var entry   = entryNode!.AsObject();
        var text    = entry["text"]!.GetValue<string>();
        var version = entry["version"]!.GetValue<int>();
        var style   = entry["style"]?.GetValue<string>() ?? defaultStyle;

        // Skip if version unchanged and no explicit filter for this key
        if (filterKey == null)
        {
            var lastVersion = GeneratedVersion(generatedLang, key);
            if (lastVersion == version)
            {
                Console.WriteLine($"  {lang}/{key}.mp3 ... skipped (v{version} up to date)");
                skipped++;
                continue;
            }
        }
        else if (key != filterKey) continue;

        var outputPath = Path.Combine(outputDir, $"{key}.mp3");
        Console.Write($"  {lang}/{key}.mp3 (v{version}) ... ");

        var ssml = $"""
            <speak version='1.0'
                   xmlns='http://www.w3.org/2001/10/synthesis'
                   xmlns:mstts='http://www.w3.org/2001/mstts'
                   xml:lang='{lang}'>
              <voice name='{voice}'>
                <mstts:express-as style='{style}' styledegree='{defaultDegree}'>
                  {PrepareTextForTts(text)}
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

        generatedLang[key] = new JsonObject { ["version"] = version };

        Console.WriteLine($"OK ({bytes.Length / 1024} KB)");
        generated++;
    }
}

// Save updated sidecar to audio-scripts/ and copy to frontend assets
var sidecarOptions = new JsonSerializerOptions { WriteIndented = true };
var sidecarJson = generatedJson.ToJsonString(sidecarOptions);
await File.WriteAllTextAsync(generatedPath, sidecarJson);
var frontendGeneratedPath = Path.Combine(outputBase, "generated.json");
Directory.CreateDirectory(outputBase);
await File.WriteAllTextAsync(frontendGeneratedPath, sidecarJson);
Console.WriteLine($"Wrote generated.json → {generatedPath}");
Console.WriteLine($"Wrote generated.json → {frontendGeneratedPath}");

Console.WriteLine($"\nDone. Generated {generated} files, skipped {skipped}.");

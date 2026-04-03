using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NarrationStudio.Models;

namespace NarrationStudio.Services;

public class TtsService
{
    private readonly HttpClient _http = new();

    public TtsService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "NarrationStudio/1.0");
    }

    public async Task<byte[]> SynthesizeAsync(
        string apiKey,
        string region,
        string voice,
        string style,
        decimal styleDegree,
        string language,
        string text,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
        var ssml = BuildSsml(language, voice, style, styleDegree, text);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
        request.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string BuildSsml(string language, string voice, string style, decimal styleDegree, string text) =>
        $"""
        <speak version='1.0'
               xmlns='http://www.w3.org/2001/10/synthesis'
               xmlns:mstts='http://www.w3.org/2001/mstts'
               xml:lang='{language}'>
          <voice name='{voice}'>
            <mstts:express-as style='{style}' styledegree='{styleDegree.ToString("F1", CultureInfo.InvariantCulture)}'>
              {PrepareTextForSsml(text)}
            </mstts:express-as>
          </voice>
        </speak>
        """;

    private static string PrepareTextForSsml(string text)
    {
        text = text.Replace("&", "&amp;");
        text = Regex.Replace(text, @",(?!\s*<break)", ", <break time=\"1000ms\" />");
        return $"<prosody rate=\"-10%\">{text}</prosody>";
    }

    public async Task<List<VoiceInfo>> FetchVoicesAsync(
        string apiKey,
        string region,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/voices/list";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dtos = JsonSerializer.Deserialize<AzureVoiceDto[]>(json, options) ?? [];

        return dtos
            .Select(d => new VoiceInfo(
                d.ShortName,
                d.DisplayName ?? d.ShortName,
                d.Locale,
                d.LocaleName ?? d.Locale,
                d.StyleList ?? []))
            .ToList();
    }

    private record AzureVoiceDto(
        string ShortName,
        string? DisplayName,
        string Locale,
        string? LocaleName,
        [property: JsonPropertyName("StyleList")] string[]? StyleList);
}

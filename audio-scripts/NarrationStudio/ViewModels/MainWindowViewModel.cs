using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using NAudio.Wave;
using NarrationStudio.Models;
using NarrationStudio.Services;

namespace NarrationStudio.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly string[] KnownVoices =
    [
        "en-US-AriaNeural",
        "en-US-GuyNeural",
        "en-US-JennyNeural",
        "nl-NL-FennaNeural",
        "nl-NL-MaartenNeural",
        "de-DE-ConradNeural",
        "de-DE-KatjaNeural",
        "fr-FR-DeniseNeural",
        "fr-FR-HenriNeural",
        "es-ES-ElviraNeural",
        "pt-BR-FranciscaNeural",
    ];

    private static readonly string[] KnownStyles =
    [
        "whispering",
        "narration",
        "narration-professional",
        "narration-relaxed",
        "newscast",
        "newscast-casual",
        "newscast-formal",
        "customerservice",
        "chat",
        "cheerful",
        "empathetic",
        "angry",
        "sad",
        "excited",
        "friendly",
        "terrified",
        "shouting",
        "unfriendly",
        "hopeful",
        "lyrical",
        "poetry-reading",
        "sports-commentary",
        "documentary-narration",
        "gentle",
    ];

    private readonly TtsService _ttsService = new();
    private List<VoiceInfo> _allVoices = [];
    private readonly Dictionary<string, string[]> _voiceStyleMap = new();
    private JsonNode? _narrationRoot;
    private string? _tempAudioFile;
    private WaveOutEvent? _waveOut;
    private Mp3FileReader? _currentReader;

    private string _narrationJsonPath = string.Empty;
    private string _apiKey = string.Empty;
    private string _region = "westeurope";
    private LocaleOption? _selectedLanguage;
    private NarrationEntry? _selectedEntry;
    private string _editableDescription = string.Empty;
    private string _editableText = string.Empty;
    private string _selectedVoice = "en-US-AriaNeural";
    private string _selectedStyle = "narration";
    private decimal? _styleDegree = 1.5m;
    private string _status = "Ready";
    private bool _isBusy;

    public MainWindowViewModel()
    {
        Voices = new ObservableCollection<string>(KnownVoices);
        Styles = new ObservableCollection<string>(KnownStyles);
        Languages = new ObservableCollection<LocaleOption>();
        Entries = new ObservableCollection<NarrationEntry>();

        LoadEnvFile();
        LoadNarrationJson();
    }

    public string NarrationJsonPath
    {
        get => _narrationJsonPath;
        private set => Set(ref _narrationJsonPath, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => Set(ref _apiKey, value);
    }

    public string Region
    {
        get => _region;
        set => Set(ref _region, value);
    }

    public ObservableCollection<string> Voices { get; }
    public ObservableCollection<string> Styles { get; }
    public ObservableCollection<LocaleOption> Languages { get; }
    public ObservableCollection<NarrationEntry> Entries { get; }

    public LocaleOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!Set(ref _selectedLanguage, value)) return;
            RefreshEntries();
            RefreshVoicesForLocale();
        }
    }

    public NarrationEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!Set(ref _selectedEntry, value)) return;
            EditableDescription = value?.Description ?? string.Empty;
            EditableText        = value?.Text         ?? string.Empty;
            if (value?.Style is { } entryStyle)
                SelectedStyle = entryStyle;
        }
    }

    public string EditableDescription
    {
        get => _editableDescription;
        set => Set(ref _editableDescription, value);
    }

    public string EditableText
    {
        get => _editableText;
        set => Set(ref _editableText, value);
    }

    public string SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            if (!Set(ref _selectedVoice, value)) return;
            UpdateStylesForVoice(value);
        }
    }

    public string SelectedStyle
    {
        get => _selectedStyle;
        set => Set(ref _selectedStyle, value);
    }

    public decimal? StyleDegree
    {
        get => _styleDegree;
        set => Set(ref _styleDegree, value);
    }

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!Set(ref _isBusy, value)) return;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !_isBusy;

    public void Reload()
    {
        Languages.Clear();
        Entries.Clear();
        _narrationRoot = null;
        NarrationJsonPath = string.Empty;
        LoadNarrationJson();
    }

    public async Task PlayAsync()
    {
        if (SelectedEntry == null)
        {
            Status = "Select an entry first.";
            return;
        }

        // If the MP3 already exists in the assets folder, play it without touching Azure.
        var existingFile = ResolveAssetPath(_selectedLanguage?.Locale ?? "en-US", SelectedEntry.Key);
        if (File.Exists(existingFile))
        {
            StopPlayback();
            StartPlayback(existingFile);
            Status = $"Playing existing asset '{SelectedEntry.Key}'";
            return;
        }

        // No existing file — need to synthesise via Azure.
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Status = $"No audio file found for '{SelectedEntry.Key}' ({_selectedLanguage?.Locale}). Enter an Azure TTS API key to generate it.";
            return;
        }

        IsBusy = true;
        Status = "Synthesising…";

        try
        {
            var bytes = await _ttsService.SynthesizeAsync(
                ApiKey, Region, SelectedVoice, SelectedStyle,
                _styleDegree ?? 1.5m,
                _selectedLanguage?.Locale ?? "en-US",
                SelectedEntry.Text);

            StopPlayback();
            _tempAudioFile = WriteTempFile(bytes);
            StartPlayback(_tempAudioFile);

            Status = $"Playing synthesised '{SelectedEntry.Key}'  ({bytes.Length / 1024} KB) — press Save to keep it";
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 400)
        {
            Status = "Azure rejected the request (400) — this style may not be supported by the selected voice.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task GenerateAsync()
    {
        if (SelectedEntry == null)
        {
            Status = "Select an entry first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Status = "Enter your Azure TTS API key to generate audio.";
            return;
        }

        IsBusy = true;
        Status = "Synthesising…";

        try
        {
            var bytes = await _ttsService.SynthesizeAsync(
                ApiKey, Region, SelectedVoice, SelectedStyle,
                _styleDegree ?? 1.5m,
                _selectedLanguage?.Locale ?? "en-US",
                SelectedEntry.Text);

            StopPlayback();
            _tempAudioFile = WriteTempFile(bytes);
            StartPlayback(_tempAudioFile);

            Status = $"Playing synthesised '{SelectedEntry.Key}'  ({bytes.Length / 1024} KB) — press Save to keep it";
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 400)
        {
            Status = "Azure rejected the request (400) — this style may not be supported by the selected voice.";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAsync()
    {
        if (SelectedEntry == null || _selectedLanguage?.Locale == null)
        {
            Status = "Select an entry first.";
            return;
        }
        if (_tempAudioFile == null || !File.Exists(_tempAudioFile))
        {
            Status = "Press Generate to synthesise audio first, then Save.";
            return;
        }

        var outputBase = ResolveOutputBase();
        var assetsParent = Path.GetDirectoryName(outputBase);
        if (assetsParent == null || !Directory.Exists(assetsParent))
        {
            Status = $"Could not locate frontend public/assets folder. Expected: {outputBase}";
            return;
        }

        var outputDir = Path.Combine(outputBase, _selectedLanguage!.Locale);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"{SelectedEntry.Key}.mp3");
        File.Copy(_tempAudioFile, outputPath, overwrite: true);

        Status = $"Saved → {outputPath}";

        await Task.CompletedTask;
    }

    public Task SaveEntryAsync()
    {
        if (SelectedEntry == null || _selectedLanguage == null)
        {
            Status = "Select an entry first.";
            return Task.CompletedTask;
        }

        if (_narrationRoot == null || string.IsNullOrEmpty(NarrationJsonPath))
        {
            Status = "narration.json is not loaded.";
            return Task.CompletedTask;
        }

        var entryNode = _narrationRoot["entries"]?[_selectedLanguage.Locale]?[SelectedEntry.Key]?.AsObject();
        if (entryNode == null)
        {
            Status = $"Could not find entry '{SelectedEntry.Key}' in narration.json.";
            return Task.CompletedTask;
        }

        entryNode["description"] = EditableDescription;
        entryNode["text"]        = EditableText;
        entryNode["style"]       = SelectedStyle;

        // Bump version when text changes
        if (EditableText != SelectedEntry.Text)
            entryNode["version"] = (SelectedEntry.Version + 1);

        File.WriteAllText(NarrationJsonPath, _narrationRoot.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        // Refresh the in-memory entry and reselect it so the ListBox stays on the same row
        var updated = SelectedEntry with { Description = EditableDescription, Text = EditableText, Style = SelectedStyle, Version = entryNode["version"]?.GetValue<int>() ?? SelectedEntry.Version };
        var idx = Entries.IndexOf(SelectedEntry);
        if (idx >= 0)
            Entries[idx] = updated;
        SelectedEntry = updated;

        Status = $"Saved '{updated.Key}'.";
        return Task.CompletedTask;
    }

    public void Cleanup()
    {
        StopPlayback();
        if (_tempAudioFile != null && File.Exists(_tempAudioFile))
        {
            try { File.Delete(_tempAudioFile); } catch { /* best-effort */ }
        }
    }

    private void LoadEnvFile()
    {
        // Look next to executable first, then in the audio-scripts parent folder
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            // Shared .env in the sibling GenerateAudio project
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GenerateAudio", ".env")),
        };

        var envFile = Array.Find(candidates, File.Exists);
        if (envFile == null) return;

        foreach (var line in File.ReadAllLines(envFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            var (k, v) = (parts[0].Trim(), parts[1].Trim());
            if (k == "AZURE_TTS_KEY") _apiKey = v;
            else if (k == "AZURE_TTS_REGION") _region = v;
        }
    }

    private void LoadNarrationJson()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "narration.json"));

        if (!File.Exists(path))
        {
            Status = $"narration.json not found at: {path}";
            return;
        }

        NarrationJsonPath = path;
        var json = File.ReadAllText(path);
        _narrationRoot = JsonNode.Parse(json);

        SelectedVoice  = _narrationRoot?["voice"]?.GetValue<string>()              ?? "en-US-AriaNeural";
        SelectedStyle  = _narrationRoot?["defaultStyle"]?.GetValue<string>()       ?? "whispering";
        _styleDegree   = (decimal?)_narrationRoot?["defaultStyledegree"]?.GetValue<double>() ?? 1.5m;
        OnPropertyChanged(nameof(StyleDegree));

        // Seed Languages with the locales found in narration.json as LocaleOptions.
        // LoadVoicesAsync will later replace this with the full Azure list.
        if (_narrationRoot?["entries"]?.AsObject() is { } entries)
        {
            foreach (var (lang, _) in entries)
                Languages.Add(new LocaleOption(lang, lang));

            SelectedLanguage = Languages.FirstOrDefault();
        }

        Status = $"Loaded {Languages.Count} language(s) — {Entries.Count} entries";
    }

    private void RefreshEntries()
    {
        Entries.Clear();
        if (_narrationRoot == null || _selectedLanguage == null) return;

        var langEntries = _narrationRoot["entries"]?[_selectedLanguage.Locale]?.AsObject();
        if (langEntries == null) return;

        foreach (var (key, entryNode) in langEntries)
        {
            var description    = entryNode?["description"]?.GetValue<string>()    ?? string.Empty;
            var text           = entryNode?["text"]?.GetValue<string>()           ?? string.Empty;
            var version        = entryNode?["version"]?.GetValue<int>()           ?? 1;
            var style          = entryNode?["style"]?.GetValue<string>();
            var forCreatorOnly = entryNode?["forCreatorOnly"]?.GetValue<bool>()   ?? false;
            Entries.Add(new NarrationEntry(key, description, text, version, style, forCreatorOnly));
        }

        SelectedEntry = Entries.FirstOrDefault();
    }

    private void RefreshVoicesForLocale()
    {
        if (_selectedLanguage == null) return;

        var filtered = _allVoices
            .Where(v => v.Locale == _selectedLanguage.Locale)
            .Select(v => v.ShortName)
            .ToList();

        var voicesToShow = filtered.Count > 0 ? filtered : KnownVoices.ToList();

        Voices.Clear();
        foreach (var v in voicesToShow) Voices.Add(v);

        if (!Voices.Contains(SelectedVoice))
            SelectedVoice = Voices.FirstOrDefault() ?? string.Empty;
    }

    private void UpdateStylesForVoice(string shortName)
    {
        if (!_voiceStyleMap.TryGetValue(shortName, out var styles) || styles.Length == 0)
            return;

        var current = SelectedStyle;
        Styles.Clear();
        foreach (var s in styles) Styles.Add(s);
        SelectedStyle = Styles.Contains(current) ? current : Styles[0];
    }

    public async Task LoadVoicesAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Status = "No API key — using built-in voice list. Enter a key to load all Azure voices.";
            return;
        }

        IsBusy = true;
        Status = "Loading voices from Azure…";

        try
        {
            _allVoices = await _ttsService.FetchVoicesAsync(ApiKey, Region);

            _voiceStyleMap.Clear();
            foreach (var v in _allVoices)
                _voiceStyleMap[v.ShortName] = v.StyleList;

            // Replace Languages with the full Azure locale list
            var currentLocale = _selectedLanguage?.Locale;
            Languages.Clear();
            var locales = _allVoices
                .GroupBy(v => v.Locale)
                .Select(g => new LocaleOption(g.Key, g.First().LocaleName))
                .OrderBy(l => l.Locale)
                .ToList();
            foreach (var l in locales) Languages.Add(l);

            // Restore the previously selected locale (or default to first)
            SelectedLanguage = Languages.FirstOrDefault(l => l.Locale == currentLocale)
                               ?? Languages.FirstOrDefault();

            Status = $"Loaded {_allVoices.Count} voices across {Languages.Count} locales";
        }
        catch (Exception ex)
        {
            Status = $"Failed to load voices: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"narration-studio-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private void StartPlayback(string filePath)
    {
        _currentReader = new Mp3FileReader(filePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_currentReader);
        _waveOut.Play();
    }

    private void StopPlayback()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _currentReader?.Dispose();
        _currentReader = null;
    }

    private static string ResolveOutputBase() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "frontend", "werewolves-app", "public", "assets", "audio"));

    private static string ResolveAssetPath(string language, string key) =>
        Path.Combine(ResolveOutputBase(), language, $"{key}.mp3");

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

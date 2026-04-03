# Audio narration

The app plays pre-generated MP3 narration clips at each game phase transition. All clips are produced once with Azure Cognitive Services Text-to-Speech and committed to the repository. There is no runtime TTS call from the app.

---

## File layout

```
audio-scripts/
  narration.json        — source of truth: text, voice settings, version per clip
  generated.json        — sidecar: records the last-generated version per lang/key
  GenerateAudio/        — .NET 9 CLI tool that calls the Azure TTS API

frontend/werewolves-app/public/assets/audio/
  en-US/
    role-reveal.mp3
    werewolves-meeting.mp3
    … (one file per key)
```

---

## narration.json

Defines every clip. Top-level fields control the Azure voice:

| Field | Description |
|---|---|
| `voice` | Azure Neural voice name (e.g. `en-US-AriaNeural`) |
| `defaultStyle` | Speaking style applied to all clips (e.g. `whispering`) |
| `defaultStyledegree` | Emphasis of the style (float, e.g. `1.5`) |

Under `entries`, clips are grouped by BCP-47 language tag (`en-US`, etc.). Each clip is an object:

```json
"role-reveal": { "version": 1, "text": "Everyone may now look at their role cards…" }
```

| Field | Description |
|---|---|
| `key` | Filename stem and the `AudioKey` constant in the frontend |
| `version` | Integer. Increment whenever the text changes to force regeneration |
| `text` | The spoken text sent to Azure TTS |

---

## generated.json

Tracks which version of each clip was last successfully generated:

```json
{
  "en-US": {
    "role-reveal": 1,
    "lover-reveal": 2
  }
}
```

The CLI tool compares `narration.json` versions against this file to decide which clips to regenerate. This file is committed so developers don't regenerate clips that are already up to date.

---

## GenerateAudio CLI tool

Location: `audio-scripts/GenerateAudio/` (.NET 9 console app, no NuGet dependencies beyond the SDK).

### Prerequisites

| Environment variable | Description |
|---|---|
| `AZURE_TTS_KEY` | Azure Speech resource API key (required) |
| `AZURE_TTS_REGION` | Azure region, defaults to `westeurope` |

A `.env` file in the tool's base directory is loaded automatically if present (one `KEY=VALUE` per line).

### Usage

```powershell
cd audio-scripts/GenerateAudio

# Regenerate only clips whose version changed since last run
dotnet run

# Force-regenerate a single clip regardless of version
dotnet run -- lover-reveal
```

### What the tool does

1. Reads `narration.json` for text and voice settings.
2. Reads `generated.json` to find the last-generated version per clip.
3. For each clip where the version in `narration.json` is higher than the version in `generated.json` (or all clips if a key filter is given), it:
   - Builds an SSML document wrapping the text in `<mstts:express-as>` with the configured style and degree.
   - POSTs to `https://{region}.tts.speech.microsoft.com/cognitiveservices/v1` with the `audio-24khz-48kbitrate-mono-mp3` output format.
   - Writes the response bytes to `frontend/werewolves-app/public/assets/audio/{lang}/{key}.mp3`.
   - Updates `generated.json` with the new version number.
4. Saves the updated `generated.json`.

Output files are committed to the repository and served as static assets by the Angular app. No build step processes them further.

---

## Frontend integration

### AudioKey constants (`src/app/models/audio-keys.ts`)

Maps human-readable names to the string keys that match MP3 filenames:

```ts
export const AudioKey = {
  RoleReveal: 'role-reveal',
  WerewolvesTurn: 'werewolves-turn',
  // …
} as const;
```

Adding a new clip requires: adding an entry here, adding it to `narration.json`, and running the CLI tool.

### AudioService (`src/app/services/audio.service.ts`)

A root-level Angular service. On construction it preloads all clips into `HTMLAudioElement` instances from `assets/audio/en-US/{key}.mp3`. The language is currently hard-coded as `en-US`.

| Method | Description |
|---|---|
| `unlock()` | Plays a silent audio element to satisfy browser autoplay policy; call on any user gesture before the first clip |
| `play(key)` | Plays a preloaded clip; returns a `Promise<void>` that resolves when playback ends (or on error) |
| `schedulePlay(key, playAt)` | Schedules `play()` at a specific `Date`; plays immediately if the date is in the past |

### Triggering audio

`AudioService` is injected into the `SessionComponent`, which calls `schedulePlay()` whenever the game state changes. Each phase transition carries a `playAt` timestamp from the server, so all players hear the clip at the same wall-clock time regardless of poll latency.

`unlock()` is called from the `LobbyComponent` on the first user interactions (joining or starting a game).

---

## Adding or updating a clip

1. Edit `narration.json`: add the new entry or update the `text` field and increment `version`.
2. If adding a new key, add the matching constant to `audio-keys.ts`.
3. Run the CLI tool:
   ```powershell
   cd audio-scripts/GenerateAudio
   dotnet run        # or: dotnet run -- <key>
   ```
4. Commit the new/updated `.mp3` file(s) and the updated `generated.json`.

---

## Adding a new language

1. Add a new language block under `entries` in `narration.json` (e.g. `"nl-NL": { … }`).
2. Run the CLI tool; it will create `frontend/…/public/assets/audio/nl-NL/`.
3. Update the hard-coded `LANGUAGE` constant in `AudioService` or make it configurable.
4. Commit all generated files.

---

## When to Regenerate

Only regenerate audio files when:

- Narration text changes (script edits in `narration.json`)
- Voice or synthesis settings change
- A new language is added

Do **not** regenerate on every build — the files are stable build artifacts that rarely change.

---

## Setup: Azure AI Speech

The generator uses Azure AI Speech (Cognitive Services).

1. Sign in to [portal.azure.com](https://portal.azure.com)
2. Create a resource → search **Speech** → select **Speech** (Azure AI services)
3. Fill in:
    - **Region**: `West Europe`
    - **Pricing tier**: `Free F0` (5 hours/month) or `Standard S0`
4. After deployment, go to the resource → **Keys and Endpoint**
5. Copy **KEY 1**

---

## Generating MP3 Files

```powershell
# Copy and fill in the env file
Copy-Item audio-scripts/GenerateAudio/.env.example `
          audio-scripts/GenerateAudio/bin/Debug/net9.0/.env
# Edit .env: replace 'your-key-here' with your Azure Speech key

# Run the generator
cd audio-scripts/GenerateAudio
dotnet run
```

Files are written to `frontend/werewolves-app/public/assets/audio/en-US/`. Commit the generated files.

---

## Narration Manifest

`audio-scripts/narration.json` defines:

- The narration text for each phase
- The Azure AI Speech voice name and style
- The output filename for each clip

Edit this file to change scripts or voice settings, then regenerate and commit.

# Audio Narration

## How It Works

Phase narration is delivered as pre-generated MP3 files, not synthesised at runtime. This avoids latency and ensures consistent quality across devices. Files are committed to the repository and served as static assets.

- **Location**: `frontend/werewolves-app/public/assets/audio/en-US/`
- **Service**: `AudioService` in the frontend plays the correct track at each phase transition
- **Manifest**: Track definitions and TTS settings live in `audio-scripts/narration.json`

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

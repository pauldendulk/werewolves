---
name: audio
description: How audio translatables work — narration.json, AudioKey constants, NarrationStudio, and how to write descriptions for audio translatables.
---

# Audio translatables

Audio translatables are spoken TTS clips played at game phase transitions. They live in `audio-scripts/narration.json` and are pre-generated as MP3s via Azure Cognitive Services — no runtime TTS call from the app.

For full pipeline details (GenerateAudio CLI, AudioService, file layout) see `docs-src/docs/audio.md`.

---

## narration.json structure

```json
"<key>": { "version": 1, "description": "...", "text": "..." }
```

- `key` — matches the `AudioKey` constant and the MP3 filename stem
- `version` — increment when `text` changes to force regeneration
- `description` — human-readable context (see below)
- `text` — spoken text sent to Azure TTS; may contain SSML inline tags (e.g. `<break time="500ms" />`)

Entries are grouped by BCP-47 locale under `entries` (e.g. `entries.en-US`).

---

## AudioKey constants

`src/app/models/audio-keys.ts` maps TypeScript names to key strings. Add a new entry here whenever you add a clip to `narration.json`.

---

## NarrationStudio

Desktop tool at `audio-scripts/NarrationStudio`. Use it to:
- Browse all translatables and their descriptions
- Edit description and text, then save back to `narration.json`
- Preview different voices/styles/degrees via Azure TTS
- Save generated audio directly to `public/assets/audio/<locale>/`

---

## Adding or updating a clip

1. Edit `narration.json` — add or update `text`, increment `version`.
2. If new key: add to `audio-keys.ts`.
3. Run the CLI: `cd audio-scripts/GenerateAudio; dotnet run` (or `dotnet run -- <key>`).
4. Commit the updated `.mp3` and `generated.json`.

---

## Writing the `description` field

One sentence. Include:

- **Phase** — the game phase it plays in (e.g. `RoleReveal`, `WerewolvesTurn`, `Discussion`)
- **Position** — `start` (default, omit if obvious), `end`, or `triggered by <event>` (e.g. "triggered when the Hunter is eliminated")
- **Audience** — if not everyone hears it (e.g. "Werewolves only", "Cupid only")

Technical detail is fine for code-driven triggers.

**Examples:**
- `"RoleReveal phase, start — all players peek at their secret role card"`
- `"WerewolvesTurn phase — werewolves pick a victim; others wait with eyes closed"`
- `"HunterTurn phase — triggered when the Hunter is eliminated; they shoot one player before dying"`
- `"NightEliminationReveal phase, end — no one was killed in the night"`

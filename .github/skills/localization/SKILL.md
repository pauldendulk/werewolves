---
name: localization
description: Localization concepts for this project — what a translatable is and how the two kinds (audio and UI) relate.
---

# Localization

## Terminology

A **translatable** is a keyed text value that varies by language. Its key is a stable identifier used in code; its value is the translated text for a given locale.

---

## Two kinds of translatables

| Kind | Status | Details |
|---|---|---|
| **Audio** | ✅ Implemented | Spoken TTS clips. See the `audio` skill. |
| **UI** | ⬜ Not yet built | On-screen text (buttons, labels, headings). No i18n framework added yet. |

The two systems are intentionally separate — audio translatables carry voice/style/version metadata that UI text does not need.

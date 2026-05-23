# Six Ages 2: Lights Going Out — Accessibility Mod

Accessibility mod for [Six Ages 2: Lights Going Out](https://www.sixages.com/) that adds screen reader support (Tolk / NVDA) and keyboard navigation so blind and visually impaired players can play the game.

## Features

- Screen reader announcements via Tolk (NVDA, JAWS, Narrator)
- Keyboard navigation through menus, dialogs, advisors, clan management
- Spoken descriptions of stat panels, advisors, magic screen, war screen, lore, tutorials, and more
- Works alongside (but does not require) other Six Ages 2 mods

## Installation

1. Download the latest ZIP from the [Releases](../../releases) page.
2. Extract it into your Six Ages 2 game folder (default: `C:\Program Files (x86)\alphatest\Six Ages 2 Lights Going Out`), merging folders and replacing files when prompted.
3. Start the game. The mod loads automatically on next launch.
4. Press `F1` in-game for a list of accessibility hotkeys.

The release ZIP bundles BepInEx (the mod loader) and the native Tolk / NVDA DLLs, so no separate setup is required.

## Building from source

Requirements:

- Windows
- .NET SDK with `dotnet` CLI
- PowerShell
- A local copy of Six Ages 2: Lights Going Out (needed for the game DLLs)

The project references game assemblies in a `lib\` folder that is intentionally not committed to this repository (the DLLs are game content and not redistributable). Copy the following files from your installed game into a new `lib\` folder at the repo root before building:

- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll`
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.UI.dll`
- `UnityEngine.UIModule.dll`
- `UnityEngine.IMGUIModule.dll`
- `UnityEngine.InputModule.dll`
- `Unity.TextMeshPro.dll`
- `BepInEx.dll`
- `0Harmony.dll`

These live in `<GameDir>\Six Ages 2 Lights Going Out_Data\Managed\` and `<GameDir>\BepInEx\core\` respectively.

Then build and package:

```powershell
scripts\Build-Mod.ps1
scripts\Package-AccessibilityOnly.ps1
```

`Package-AccessibilityOnly.ps1` produces a release ZIP under `dist\` ready to upload.

## Repository layout

- `src\SixAgesAccessibility\` — mod source
- `scripts\` — build, deploy, packaging
- `docs\` — modding notes, game API reference, accessibility patterns (English)
- `docs-de\` — selected German user-facing docs
- `templates\` — generic mod templates reused across accessibility projects

## Compatibility

- Game version: Six Ages 2: Lights Going Out (alpha test build)
- Mod loader: BepInEx 5.4.23.5 (x86)
- Architecture: 32-bit (game is x86)

## Author / Contact

Developed by HappyStarfish in collaboration with a blind player. Issues and feedback welcome via the GitHub issue tracker.

---

## Auf Deutsch

Eine Mod für „Six Ages 2: Lights Going Out", die das Spiel für blinde Spielerinnen und Spieler zugänglich macht — Screenreader-Ausgabe (Tolk / NVDA) und Tastatursteuerung.

Installation: aktuellste ZIP von der Releases-Seite herunterladen, in den Spielordner entpacken, Dateien überschreiben. Beim nächsten Spielstart lädt die Mod automatisch. F1 listet die Tasten.

Die Mod läuft im englischen Originaltext. Eine separate deutsche Übersetzungsmod existiert als eigenständiges Projekt und ist hier nicht enthalten.

# Six Ages 2: Lights Going Out — Accessibility Mod

Accessibility mod for [Six Ages 2: Lights Going Out](https://www.sixages.com/) that adds screen reader support (Tolk / NVDA) and keyboard navigation so blind and visually impaired players can play the game.

## Installation

1. Download the latest ZIP from the [Releases](../../releases) page.
2. Extract it into your Six Ages 2 game folder, merging folders and replacing files when prompted.
3. Start the game. The mod loads automatically on next launch.
4. Press `Shift+F1` in-game for a list of accessibility hotkeys.

The release ZIP bundles BepInEx (the mod loader) and the native Tolk / NVDA DLLs, so no separate setup is required.

## Building from source

Requirements: Windows, .NET SDK with `dotnet` CLI, PowerShell, and a local copy of Six Ages 2 (for the game DLLs).

The project references game assemblies in a `lib\` folder that is intentionally not committed (the DLLs are game content and not redistributable). Copy these files into a new `lib\` folder at the repo root before building:

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

The Unity DLLs live in `<GameDir>\Six Ages 2 Lights Going Out_Data\Managed\`, the BepInEx DLLs in `<GameDir>\BepInEx\core\`.

Then build and package:

```powershell
scripts\Build-Mod.ps1
scripts\Package-AccessibilityOnly.ps1 -GameDir <path-to-your-game-folder>
```

`Package-AccessibilityOnly.ps1` produces a release ZIP under `dist\` ready to upload.

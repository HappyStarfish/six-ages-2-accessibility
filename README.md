# Six Ages 2: Lights Going Out — Accessibility Mod

> **Status: stable beta** — well tested, daily-driven by a blind player. Rough edges and bugs are still possible; please report what breaks.
>
> The sibling project [Six Ages 1: Ride Like the Wind](https://github.com/HappyStarfish/six-ages-1-accessibility) exists too but is still in unstable alpha.

Accessibility mod for [Six Ages 2: Lights Going Out](https://www.sixages.com/) that adds screen reader support (Tolk / NVDA) and keyboard navigation so blind and visually impaired players can play the game.

## Installation

1. Download the latest ZIP from the [Releases](../../releases) page.
2. Extract it into your Six Ages 2 game folder, merging folders and replacing files when prompted.
3. Start the game. The mod loads automatically on next launch.
4. Press `Shift+F1` in-game for a list of accessibility hotkeys.

The release ZIP bundles BepInEx (the mod loader) and the native Tolk / NVDA DLLs, so no separate setup is required.

## How the mod works

Six Ages 2: Lights Going Out is a storytelling strategy game set in mythic-age Glorantha. You guide a small bronze-age clan across generations: each in-game year runs through five seasons plus a year-end Sacred Time. You don't move armies on a map turn by turn — you weigh advisors, read a scene, pick a response, and let the consequences unfold. The mod turns that loop into a screen-reader experience: the active screen speaks, the keys around it do everything the mouse would.

The game divides naturally into four kinds of screen, and each one wants the keyboard a little differently.

### Anywhere you are

- **Arrows / Tab** move the focus, **Shift + Tab** moves it backwards.
- **D** describes whatever is focused — longer text, the part the screen reader wouldn't otherwise read aloud.
- **Escape** backs out of dialogs and overlays.
- **Shift + F1** lists the mod's keys for the screen you're currently on. If you forget anything below, this is the answer.
- **H** repeats the current tutorial card when one is shown.
- **F1** opens the game's own help.

### Management screens

The day-to-day of running the clan: Clan, Magic, Map, Relations, War, Wealth, Lore, Saga and Controls. You step between them as often as you read a single one.

- **Ctrl + 1..9** jumps straight to a screen by its index.
- **Ctrl + Tab** / **Ctrl + Shift + Tab** cycles through them in order.
- **F2** reads the stat panel — press once for the season, again for time, resources, reputation. Each press steps one further.
- **F3** reads advisor advice — first press all advisors, then one per press as you cycle.
- **Shift + F3** reads the focused advisor's full dossier (skills, deity, location, health).
- **F4** reads the clan's current concerns — the issues that would otherwise only show as dashboard icons.
- **S** advances to the next season once you're done.

### Dialogs

A dialog overlays the management screen when the clan has to decide something specific — who to send on a raid, which advisor to reorganise, which spirit to bargain with. The work happens in a list: each row is one piece of the decision, and most dialogs need several rows filled in before they can be committed.

That is why the mod splits the two actions cleanly:

- **Space** selects the focused row — it ticks the choice you want for *that* line. You keep arrowing and selecting until the dialog has everything it needs.
- **Enter** commits the whole dialog and closes it. Pressing Enter on a row that isn't selected yet still selects it first, so the muscle memory "Enter just commits" keeps working when there's only one decision to make.
- **D** reads the row's full description, **F3** the advisors' opinion on the choice in front of you.

### Scenes

The story half of the game. A scene is a short narrative beat — a vignette, a vision, a council meeting — with two to six response buttons at the bottom. The mod reads the caption and body automatically as soon as the scene loads.

- **Arrow keys** move between the response buttons; **Enter** picks the one you're on. This is where you spend most of a session.
- **1..9** picks a response directly by its number, **Y** / **N** for yes/no scenes.
- **F5** rereads the scene text, **F6** lists the response options again.
- **F2** and **F3** still work — the season and advisor readouts are useful mid-scene too.

### Sacred Time

At the end of each year the clan looks back and forward. The screen has two zones: a multi-paragraph forecast for the coming year, and an allocation grid where you spend your magic reserve across the clan's defenses for the year ahead.

- **Tab** switches between the forecast zone and the allocation zone.
- In the forecast: **Arrow up / down** reads one paragraph at a time. **F5** rereads from the top.
- In the allocation: **Up / Down** moves between lines, **Right / Left** raises and lowers magic on the focused line. The reserve is announced as it changes so you always know how much is left.
- **G** opens the Saga (the clan's history book) without leaving Sacred Time.
- **Enter** moves on into the new year once the allocation is set.

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

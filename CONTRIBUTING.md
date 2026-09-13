# Contributing to Open Nest UIKit

Thanks for your interest in improving Open Nest UIKit! This document explains how to
report issues, propose changes, and set up a development environment.

## Code of Conduct

- Be respectful and constructive.
- Keep discussions focused on the project.
- Assume good faith; ask questions instead of making assumptions.

## How to Report an Issue

Please include:

1. **What you expected** vs **what happened**.
2. **Your setup**: loader and version (BepInEx `6.0.0-be.xxx` / MelonLoader `0.7.3`),
   game version, and whether the game is running with the IL2CPP build.
3. **Steps to reproduce** — the exact UI path (`ESC` → clipboard → which page → which widget).
4. **Logs**: `<Game>/BepInEx/LogOutput.log` (or `<Game>/MelonLoader/Latest.log`) and
   `<Game>/OpenNestUIKitLogs/test.log` if you used the test harness.
5. **Screenshots** for anything visual. A screenshot next to the game's own settings page
   is worth ten descriptions of "it looks off".

For UI fidelity reports, the most useful thing you can do is **measure the game's own
widget** and tell us the numbers (see *Probing the native UI* below).

## Development Setup

### Prerequisites

- Windows, **.NET 6 SDK**
- Iron Nest: Heavy Turret Simulator, **launched at least once** with your loader installed
  so the loader generates its interop assemblies
- BepInEx 6 (IL2CPP) and/or MelonLoader 0.7.3

### Building

```powershell
git clone https://github.com/1499501762/Open-Nest-UIKit.git
cd Open-Nest-UIKit

# BepInEx build -> deploy into <Game>/BepInEx/plugins/
dotnet build src\OpenNestUIKit\OpenNestUIKit.csproj -c Release `
  -p:GameDir="C:\path\to\game" -p:DeployToGame=true

# MelonLoader build -> deploy into <Game>/Mods/ + <Game>/UserLibs/
dotnet build src\OpenNestUIKit.MelonMod\OpenNestUIKit.MelonMod.csproj -c Release `
  -p:GameDir="C:\path\to\game" -p:ClientGame="C:\path\to\game" -p:DeployToMods=true
```

Close the game before deploying: the plugin DLL is locked while the process runs.

### Probing the native UI

The game's own settings page is the specification. The test harness mod can open it,
dump it and screenshot it:

```powershell
# open the game's ESC menu, then click the game's own Settings button
-onuktest-run=nativeopen:keep;wait:1500;clickgame:OpenSettingsBtn;wait:2500;pagedump:Settings;shot:native_settings;report
```

- `pagespec` / `pagedump:<node>` print every rect, anchor, colour, 9-slice and
  `CanvasRenderer` tint (in the game's **local units**)
- `shot:<name>` writes a PNG into `<Game>/OpenNestUIKitLogs/shots/`
- `pageprobe` prints the same measurements for **our** page, so the two can be diffed

> Read `pagedump` output from the deepest node down, not from a shallow summary: the
> by-name summary is depth-limited and once made a widget's real children invisible.

## Testing

Before opening a PR, please run through this checklist on the platforms you have:

| # | Environment | Verify |
|---|---|---|
| 1 | BepInEx 6, native | Game starts, `ESC` list shows the UIKit entry, page opens, all widgets render |
| 2 | BepInEx 6 + MelonLoader bridge | MelonLoader build of the mod loads and its pages open in the same process |
| 3 | MelonLoader 0.7.3, native | Same as #1 with the MelonLoader build |
| 4 | Other mods installed | No click-through, scroll-through or input stealing; the game's own UI still works |

For each run, check the log ends with no `Exception`/`IL2CPP` errors, and that geometry
probes report the expected numbers (`pageprobe`) when you changed anything visual.

## Implementation Notes

Please keep these rules in mind — they are the reason this mod looks native:

- **Measure, don't guess.** Widget specs live in `src/OpenNestUIKit/Native/NativeWidgets.cs`
  and carry the native evidence in a comment. If you cannot point at a `pagedump` line or a
  screenshot for a number, it does not belong in the code yet.
- **`CanvasRenderer` colour can differ from `Image.color`.** The real rendered colour
  (and any MonoBehaviour tint) must be read from the renderer, not the component's field.
- **Slight deviations need a reason.** Where a native value does not reproduce the native
  *look* (e.g. the slider knob), the measured in-game pixel value wins — and the reason is
  written down in `docs/UI_KIT.md`.
- **Local units, then `localScale`.** Native coordinates are authored in the game's local
  units and scaled by the page block's own `localScale` (0.3591). Do not bake screen pixels.
- **Never trample the game.** Touch only *foreign* raycasters/input modules, capture every
  state you change, and restore it. Canvas sorting order must not take the game's cursor layer.
- **Loader parity.** BepInEx and MelonLoader builds compile the same sources. Anything
  loader-specific goes through reflection; both targets must build.
- **Soft dependencies stay soft.** `OpenNestUIKit.API.dll` must not reference Unity or the
  mod assembly. Third-party mods must load (and do nothing) when the host is missing.
- **Update the docs.** Behaviour or geometry changes get an entry in `docs/UI_KIT.md` (and
  `docs/UI_KIT_TEST.md` for new probe commands).

Keep changes small and focused. One feature or fix per pull request, please.

## Commit Messages

We follow [Conventional Commits](https://www.conventionalcommits.org/):

| Prefix | Use for |
|---|---|
| `feat:` | New feature or widget |
| `fix:` | Bug fix |
| `docs:` | Documentation only |
| `refactor:` | Code change that neither fixes a bug nor adds a feature |
| `perf:` | Performance improvement |
| `test:` | Test harness or scripted test changes |
| `chore:` | Build scripts, tooling, packaging |

Example: `fix(native): match the checkbox checkmark to the game's own toggle`

## Pull Requests

- **Keep it focused.** Describe *what* changed and *how it was verified* (which loader, which
  probe output, which screenshots).
- **Do not commit** build output (`bin/`, `obj/`), game files, interop assemblies
  (`interop/`, `Il2CppAssemblies/`), extracted game assets, logs or screenshots. See
  `.gitignore` — everything in that list stays out of the repository.
- **Do not commit** the game's own sprites or any extracted game asset. Slice definitions
  reference assets the user extracts locally; they are not redistributed.
- If your change alters geometry, include the `pageprobe`/`pagedump` output before/after.

## Licensing

By contributing, you agree that your contributions are licensed under the
**GNU Affero General Public License v3.0** (see [LICENSE](LICENSE)) — the same license as
the rest of the project.

# Open Nest UIKit `0.0.1-Alpha-1` — Release Notes

**First public release (pre-release).** An alpha: the native page, the widget set, the
third-party contract, input isolation and dual-loader packaging are implemented and
verified in-game, but the public API may still change between alpha builds.

Native-looking UI library mod for **Iron Nest: Heavy Turret Simulator** (Unity 6 / IL2CPP).
Works with **BepInEx 6** and **MelonLoader 0.7.3**.

## Highlights

### A real menu inside the game's own ESC clipboard
- Pages are drawn with the game's own UI: the page block is copied from the game's settings
  screen — `871.7 × 1012.5` local units at `localScale 0.3591`, a non-scrolling **72-unit
  header strip**, and an `871.7 × 844.2` content viewport that scrolls.
- The mod injects its own row into the game's ESC list, mirroring the game's button
  template (structure, tint target, font, metrics).
- Rubber-band (**elastic**) scrolling with overscroll and recovery, exactly like the
  game's own `ScrollRect`; drag-to-scroll and the wheel are both supported.

### Native widget set
Title, sub-title + rule, tabs, slider, checkbox, dropdown, keybind, text field, scroller,
scrollbar and primary/secondary buttons — each copied from the game's settings page, sprite,
9-slice `pixelsPerUnitMultiplier`, colour **and** `CanvasRenderer` tint included. Where the
native values alone could not reproduce the native *look*, the measured in-game pixel values
were used instead (documented in `docs/UI_KIT.md`).

### Input isolation (no click-through)
- The library hit-tests the pointer itself, so pages work even when the game's
  `EventSystem` is inactive (the clipboard canvas is `WorldSpace`).
- The game's `BaseInputModule`s are disabled and **re-disabled every frame** (the game
  writes its own state back), foreign raycasters are suppressed, and a Harmony prefix blocks
  the world interaction entry point (`LookAtTarget.OnClickDown`) while a page is open.
- Everything is captured and restored on close; an escape level stack stops `ESC` from
  opening the game's pause menu from the same key press that closed a page.

### Third-party contract — `OpenNestUIKit.API.dll`
- Plain .NET, no Unity dependency: `UiKitProviderBase` + `UiKitHost.Register(...)` with
  rows for slider, toggle, dropdown, tabs, keybind, text, button, nav, label, header,
  separator, progress and list, plus two-column layouts and page sizing.
- **Soft dependency**: a mod that references only the API assembly still loads when this
  mod is absent (`UiKitHost.IsHostAvailable` is `false`, calls do nothing).
- Host controls: open/close/toggle/refresh, current page, text-input focus, and a chat
  overlay for other mods.

### Localisation, tooling and the test harness
- All user-visible strings go through `UiKitLoc` (zh/en) with an external language file.
- Slice/atlas tooling (`tools/slice_tool.py`, `scripts/slice-tool.ps1`) for the game's
  9-slice sprites, with hot-reloadable slice definitions.
- A separate test mod (`src/OpenNestUIKit.Test/`) drives the library from a script:
  scripted clicks/drags/wheel/keys, geometry probes, native-page dumps and screenshots —
  so fidelity claims are backed by measured numbers.

## Installation

```
BepInEx    : OpenNestUIKit.dll + OpenNestUIKit.API.dll  ->  <Game>/BepInEx/plugins/
MelonLoader: OpenNestUIKit.MelonMod.dll                 ->  <Game>/Mods/
             OpenNestUIKit.API.dll                      ->  <Game>/UserLibs/
```

Launch the game once with your loader installed before installing this mod (interop
assemblies are generated on first launch). Do not install both loader builds at once.

## Verified

- **BepInEx 6 (IL2CPP)** and **MelonLoader 0.7.3**, including the MelonLoader build running
  inside a BepInEx process through the MelonLoader bridge.
- Geometry probes: content viewport `871.7 × 844.2`, header `72`, scroll offset clamps to
  the content limit, overshoot returns to the limit after a bounce.
- Widget probes: knob travel reaches both track ends, fill edges align with the track,
  scrollbar handle reaches both ends, checkbox check matches the game's own toggle
  (`SGCheckMark`, white, `27.9 × 27.9`).
- Input probes: `game click block installed`, blocked-click counter increments while a page
  is open, suppression and interaction lock both released on close.

## Known limits

- **Only two of four loader combinations are verified.** Runs on BepInEx-with-bridge and
  native MelonLoader have been tested; the other two combinations have no test environment
  on the development machine and are not claimed.
- The public API is alpha: breaking changes bump `UiKitHost.ApiVersion`.
- Page layout is authored for the game's current UI scale; a game update that changes the
  native settings layout can shift the metrics.
- `docs/API.md` (written API documentation) and a sample mod are not written yet.

## License

AGPL-3.0. Not affiliated with the developers of Iron Nest: Heavy Turret Simulator.
Part of the Open Nest mod family, but an independent mod — it installs on its own and the
other Open Nest mods work without it.

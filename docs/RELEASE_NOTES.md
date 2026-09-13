# Open Nest UIKit — Release Notes

## `0.0.1-Alpha-4` — flat industrial look, key/value rows, two-pane pages and working CJK input

Verified **in-game** on the BepInEx 6 end (scripted runs + screenshots + numeric probes). This release is
mostly about how a page *looks* and how text gets *into* it.

### Flat industrial theme

The theme moved away from cards and rounded fills to solid fills plus **procedural 1px lines** (no new
textures): `ModStyle.Outline` for window/frame borders, and `TopRule` / `BottomRule` / `LeftRule` for
separators that consume **zero layout height**. Rows now share the content background (no per-row card),
selection is an amber tint plus the left accent bar, tabs are flat (amber text + bold + 2px underline, no
fill), section titles are a 2px amber bar with letter-spaced text, and buttons carry a style-coloured 1px
outline (`primary` = amber, `danger` = red, `secondary` = border grey).

### New rows and layout

| Item | What it gives you |
|---|---|
| `Info(label, value)` | a read-only **key/value row** (76px label column, value left-aligned, single line) — for detail pages that are mostly "name: value" |
| `SelectableList(..., hints, ...)` | the scrolling single-selection list with per-row hover hints |
| `List` / `SelectableList` with `height < 0` | **grow to fill** — one of the two pieces a two-pane page needs |
| two-pane declarative pages | a fixed-width left column plus a growing right column, each scrolling independently |

Also fixed: a list declared inside a `Grow` host no longer produces a **second (outer) scrollbar** — when the
host itself is height-auto the content fills the viewport (`UiList.fillHost`), instead of deriving content
height from children whose own height depends on the viewport.

### CJK / IME input

Four separate defects, all fixed (all observable in `widgetprobe` / `chatprobe`):

1. **Partial pinyin used to leak in.** The OS "result" string is not always converted text, so a native
   commit is only accepted when it contains CJK (otherwise the composition channel owns it).
2. **The IME could not be switched to Chinese at all.** Unity only sets `Input.imeCompositionMode = On`
   when a real input field takes focus; a canvas-drawn box with no focused `InputField` left it `Off`, so
   the OS never associated an IME context. The router now sets the mode explicitly (through reflection —
   the interop assembly does not expose the enum) and, as a fallback, creates/associates an IME context for
   the window after focus (delayed ~0.4s so it does not race the engine).
3. **Ghost text after submit.** `GCS_RESULTSTR` is a *held value*, not an event: clearing the "last native
   string" on focus made the next focus replay the previous word. It is now kept for the whole session, and
   a fresh-commit flag distinguishes "same value, new commit" from "same value, still held".
4. **The first candidate of a session was inserted twice** when the native and composition channels both
   delivered the same commit a few frames apart. Appends are now deduplicated by *content* (the box tail
   already ends with the text and no newer typing activity happened since), so a genuinely repeated word
   still types twice.

The chat overlay also stopped re-focusing and re-filling itself right after sending (a short cooldown after
`UiTextRouter.Submit` plus clearing the draft when the box is re-expanded).

### Debug mode and the test mod

- Host/footer debug text is **debug-only** now (`OpenNestModMenu.cfg` → `Debug`, `LogLevel == Debug`, or the
  test harness).
- **`mockcfg`** writes a mock config file (`BepInEx\config\open.nest.uikit.test.cfg`) covering every
  auto-generated control: bool, int+range, float+step, enum (including Chinese values), keybind, text, empty
  text, read-only, section headings and no-type-inference keys — the fastest way to check that the settings
  renderer still handles everything.
- New offline test commands: `imedecide:<native>|<cached>|<cur>` (replays the input-decision rule) and
  `imefake:<text>` / `imecomp:<text>` (inject a native commit or a composition string).

### Build status

Five projects (mod × 2 loaders, contract, test × 2 shells) compile with **0 errors / 0 warnings**; the
scripted in-game runs pass, and Chinese input was verified end-to-end (native submit logged, no ghost text,
no duplicate candidate, no auto re-focus after send).

## `0.0.1-Alpha-3` — a bigger third-party widget set, dialogs, scrolling and localisation

Everything below was verified **in-game** (scripted clicks + screenshots, BepInEx 6 end): the new
rows render as native widgets, the foldout expands/collapses and the list keeps its selection
highlight.

### New rows

| Row | What it gives you |
|---|---|
| `Stepper(key, label, value, min, max, step, onChanged)` | the native `− value +` numeric row (game-faithful, for precise small ranges) |
| `Foldout(key, label, expanded, onToggle, body)` | collapsible group; `body` is only invoked while expanded, and the host rebuilds the page after a toggle (you only store the state). Rendered with a **procedural triangle arrow** — see "font/atlases below" |
| `SelectableList(key, height, items, selected, onSelected)` | scrolling list with exactly one highlighted row: "pick one, then act" without a button per row |
| `Text(..., placeholder, maxLength)` | grey placeholder when empty + hard character limit enforced through the IME/keyboard pipeline |

### Row modifiers and naming parity

- `Hint(string)` / `Hint(Func<string>)` — static or **dynamic** hover text for a row
  (`() => "now " + value`), plus `MarkSelected()` for hand-built lists.
- `Bool(key, label, value, onChanged)`, `Number(...)`, `Action(...)` are now provided as aliases of
  `Toggle` / `Slider` / `Button`, named exactly as in `OpenNestModMenu.API` — a settings page can be
  written once for both mods.

### Host controls

| API | Behaviour |
|---|---|
| `UiKitHost.Confirm(title, body, onResult)` | native modal confirm dialog (mask + title/body + OK/Cancel); ESC cancels without closing the menu; if the menu is closed the call warns and answers `false` instead of hanging |
| `UiKitHost.CanShowDialog` | whether a host provides the dialog |
| `UiKitHost.ScrollToKey(key)` / `ScrollToTop()` | bring a declared row into view (no-op when it is already fully visible), or jump back to the top |
| `UiKitHost.PageChanged` | `(oldId, newId)` on open / navigate / back / close — the clean hook for lazy data instead of doing work in `BuildMenu` |

### Localisation and reset

- **`UiKitLang.T(zh, en)`** (+ `IsChinese`, `Changed`) lets third-party pages follow the game
  language; the host pushes the current language. (Named `UiKitLang` because the host assembly has
  an internal `UiKitLoc` of its own — a same-named contract type would collide.)
- **`IUiKitDefaults.ResetToDefaults()`** — implementing it makes the host append a "Reset to
  defaults" row to your root page; clicking it asks for confirmation first, and the host refreshes
  afterwards. Same name and meaning as `OpenNestModMenu.API.IModMenuProvider.ResetToDefaults`.

### Fixed along the way

- **The foldout arrow is drawn, not typed.** `▾` (U+25BE) / `▸` (U+25B8) have **no glyph in the
  game font** — the first attempt rendered as a gold tofu box on screen. The arrow is now a
  procedurally generated triangle sprite (rotated for open/closed), the same approach the slider
  knob already uses: when a font or atlas cannot give the native shape, draw it.
- **Hover tooltips can no longer break row interaction.** The generic "row hot zone" that backs
  dynamic hints is only added to **read-only** rows; interactive widgets register their own
  row-level zone *before* their sub-controls, so `<`/`>`/`+`/`-`/tabs keep receiving clicks
  (zones registered later win hit-testing).
- `UiActionRow`'s whole-row hot zone now declares its owner (it was anonymous, which made the
  renderer treat the row as "no zone" and add a duplicate one).

### Build status

Four projects (mod × 2 loaders, sample × 2 shells) compile with **0 errors / 0 warnings** on the
BepInEx end; the scripted in-game run reported `PASS=11 FAIL=0` and the follow-up interaction run
passed every step (foldout toggle, list selection, stepper ±, confirm dialog).

### Installation

```
BepInEx    : OpenNestUIKit.dll + OpenNestUIKit.API.dll  ->  <Game>/BepInEx/plugins/
MelonLoader: OpenNestUIKit.MelonMod.dll                 ->  <Game>/Mods/
             OpenNestUIKit.API.dll                      ->  <Game>/UserLibs/
```

### Known limits

- **Only two of four loader combinations are verified** (BepInEx-with-bridge and native
  MelonLoader); the other two have no test environment here.
- The public API is alpha: breaking changes bump `UiKitHost.ApiVersion`.
- `Confirm` requires the menu to be open (documented above).
- `Foldout`/`SelectableList` state is yours to keep — the host only renders and rebuilds.

---

## `0.0.1-Alpha-2` — API reference + sample mod

A documentation-and-example release: **no behaviour change** in the shipped binaries (the only
difference from `0.0.1-Alpha-1` is the version string they report).

### What's new

- **`docs/API.md` — the third-party contract reference.** Every type a mod can use, written
  against the actual sources: provider identity, the page/row model, all 14 row verbs with their
  exact signatures and caveats, the host-control surface (`OpenMenu` / `Refresh` /
  `IsTextInputFocused` / chat overlay), the native-ESC entry opt-in (`IUiKitNativeEntry`),
  lifecycle/threading rules, and the packaging rule that matters most — **reference
  `OpenNestUIKit.API.dll` but do not ship it**.
- **`samples/` — a complete, buildable sample mod.** One shared provider file (plain .NET, no
  Unity) plus a BepInEx 6 shell and a MelonLoader shell, each with its own `.csproj` and deploy
  switch. Both shells compile from the same source, which is the point: the provider does not
  care which loader runs it.
- **Corrected provider page ids in the README.** Provider pages are namespaced by the host as
  `provider:<Id>[:<pageId>]` — the old README example passed a page id that would only have
  worked by accident.
- The samples are built with `Private="false"` on the API reference, so a sample build produces
  exactly one dll — no duplicate `OpenNestUIKit.API.dll` in `plugins/`.

### What the sample demonstrates

| Page | Shows |
|---|---|
| root | labels, sub-page entries, a toggle that triggers `UiKitHost.Refresh()` |
| General | slider, dropdown, text field, action button, and a row that only exists while the feature is off |
| Keys | key binding plus a live "current binding" line |
| Live data | an embedded scrolling `List` rebuilt on demand, guarded by `IsTextInputFocused` |
| Two columns | `Columns` layout: list on the left, details on the right |
| About | host diagnostics (`IsHostAvailable`, `HostVersion`, `ApiVersion`, `ProviderCount`, `CurrentPageId`) |

### Build status

`docs/API.md` and the sample were written against the contract sources, then verified by
building them: **both sample shells and both loader builds of the mod compile with 0 errors /
0 warnings**, and a fresh clone of the tag builds the same way.

### Installation

```
BepInEx    : OpenNestUIKit.dll + OpenNestUIKit.API.dll  ->  <Game>/BepInEx/plugins/
MelonLoader: OpenNestUIKit.MelonMod.dll                 ->  <Game>/Mods/
             OpenNestUIKit.API.dll                      ->  <Game>/UserLibs/
```

Launch the game once with your loader installed before installing this mod. Do not install both
loader builds at once.

### Known limits

- **Only two of four loader combinations are verified** (BepInEx-with-bridge and native
  MelonLoader); the other two have no test environment here.
- The public API is alpha: breaking changes bump `UiKitHost.ApiVersion`.
- The sample targets the mod's own repository layout (it references `src/OpenNestUIKit.API` as a
  project). Third-party mods should reference the shipped assembly instead — see `docs/API.md` §10.
- No automated test runs the sample in-game; it is verified to compile and to use the contract
  exactly as documented.

---

## `0.0.1-Alpha-1` — first public release

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
  *(Both landed in `0.0.1-Alpha-2`.)*

## License

AGPL-3.0. Not affiliated with the developers of Iron Nest: Heavy Turret Simulator.
Part of the Open Nest mod family, but an independent mod — it installs on its own and the
other Open Nest mods work without it.

# Open Nest UIKit

**Native-looking UI library mod for [Iron Nest: Heavy Turret Simulator](https://store.steampowered.com/app/2950790)** (Unity 6 / IL2CPP).

A standalone library that gives other mods two things the game does not expose:

- **A real menu inside the game's own ESC clipboard** — pages drawn with the game's own
  widgets (titles, tabs, sliders, toggles, dropdowns, keybinds, text fields, scrollers,
  buttons) so they look and behave like the settings screen that ships with the game.
- **A declarative UI contract for third parties** — `OpenNestUIKit.API.dll` lets any mod
  register pages and native menu entries without referencing this mod's assembly.

Works with **BepInEx 6 (IL2CPP)** and **MelonLoader 0.7.3**, including when MelonLoader
mods run inside a BepInEx process through the `BepInEx.MelonLoader.Loader` bridge.

> **Status: `0.0.1-Alpha-3` — public alpha (pre-release).**
> The native page, the widget set, the third-party contract, input isolation and the
> dual-loader packaging are implemented and verified in-game on both loaders. The public
> contract may still change between alpha builds.

---

## What it does today

### Native page injection (the reason this mod exists)

The game's ESC menu and clipboard panel are the only UI surfaces the player already
knows, but they are closed to mods: the clipboard canvas is `WorldSpace`, the game's
`EventSystem` is often inactive, and the buttons are instantiated on demand. This mod:

1. **Injects rows into the game's own ESC list** — mirroring the game's button template
   (structure, tint target, font, metrics) instead of re-drawing something similar.
2. **Owns the pointer while a page is open** — its own hit-testing router, so pages work
   even with no active `EventSystem`.
3. **Isolates input** — the game's `BaseInputModule`s are disabled (and re-disabled every
   frame, because the game writes its own state back), a Harmony prefix blocks the world
   interaction entry point (`LookAtTarget.OnClickDown`), and everything is restored on close.
4. **Blocks ESC from leaking** — an escape level stack keeps the game's pause menu from
   opening from the same key press that closed a page.

### Native widget set

Every widget is copied from the game's own `Settings` page (`pagespec` / `pagedump`
evidence, coordinates in the game's **local units**, then scaled by the page block's own
`localScale = 0.3591`):

| Widget | Native source |
|---|---|
| Big title | `Title Settings` (17 local × `localScale 5.1476` ⇒ 86.7 local, black 0.902, centred) |
| Sub-title + rule | `HeadlineUGUI` (large text + 4-unit `RawImage` underline) |
| Tabs | `TabsCtn` / `TabButtonUGUI` (selected = dark rounded plate + cyan label + diamond) |
| Slider | `SliderConsoleUGUI` (15-unit track, black fill, **round 30×30 knob**, right-aligned value) |
| Checkbox | `ToggleConsoleUGUI` (label left, 20×20 plate at the right, **green `SGCheckMark`**) |
| Dropdown | `DropdownUGUIWithLabel` (label above a dark rounded plate + `SGDownArrow`) |
| Keybind | `InputBindingConsoleUGUI` (label + key plate; blue listening state) |
| Text field | `TextfieldConsoleUGUI` (label + plate; own key pipeline + native IME) |
| Scroller | `OptionsButtonConsoleUGUI` (`◀ value ▶`, rotated `SGDownArrow`s) |
| Scrollbar | `Scrollbar Vertical` (20-wide track, `Sliding Area`, brown-tinted handle) |
| Buttons | `SGButtonPrimaryUGUI` / `ButtonSecondaryUGUI` |

The page block itself is copied too: `871.7 × 1012.5` local at `localScale 0.3591`, with a
non-scrolling 72-unit header strip (`TabsCtn`) and an `871.7 × 844.2` content viewport
(`ContentCtn`, top edge 72 below the block top) that scrolls with a rubber-band
(`Elastic`) bounce, exactly like the game's own `ScrollRect`.

### Third-party pages (`OpenNestUIKit.API.dll`)

A mod declares pages and rows, and the library renders them natively:

```csharp
using OpenNestUIKit.API;

public sealed class MyModUi : UiKitProviderBase
{
    public override string Id => "mymod";
    public override string DisplayName => "My Mod";

    public override void BuildMenu(IUiMenuTree menu)
    {
        menu.Root.Nav("Settings", "settings");
        menu.Root.Toggle("mymod.enabled", "Enable feature", MyConfig.Enabled, v => MyConfig.Enabled = v);

        var page = menu.Page("settings", "Settings");
        page.Slider("mymod.volume", "Volume", 60, 0, 100, 1, v => MyConfig.Volume = (float)v);
        page.Choice("mymod.mode", "Mode", new[] { "Fast", "Native" }, 1, i => MyConfig.Mode = i);
        page.Text("mymod.name", "Player name", MyConfig.Name, s => MyConfig.Name = s);
        page.KeyBind("mymod.hotkey", "Hotkey", "F7", k => MyConfig.Hotkey = k);
        page.Tabs("mymod.tab", new[] { "General", "Debug" }, 0, i => MyConfig.Tab = i);
        page.Columns(280f, left: l => l.Label("list side"), right: r => r.Label("details side"));
        page.Separator();
        page.Button("Apply", "Apply", () => MyConfig.Save());
    }
}
```

```csharp
// Register once at startup — works whether or not Open Nest UIKit is installed.
UiKitHost.Register(new MyModUi());

// Later, from a keybind: the host namespaces provider pages as "provider:<your Id>[:<pageId>]".
UiKitHost.OpenMenu("provider:mymod");
```

Rows available today: `Header`, `Label`, `Separator`, `Button`, `Nav`, `Toggle`, `Slider`,
`Stepper`, `Choice` (dropdown), `Tabs`, `Text` (placeholder + max length), `KeyBind`, `Progress`,
`Foldout` (collapsible group), `SelectableList`, `Columns` (two-column layout), `List` (embedded
scroller), plus `Hint(text)` / `Hint(Func<string>)` row hints, page sizing (`Size`), density
(`SetCompact`) and the ModMenu-compatible aliases (`Bool` / `Number` / `Action`).

`UiKitHost` is the host-side contract: `Register`/`Unregister` providers, `OpenMenu`/
`CloseMenu`/`ToggleMenu`/`Refresh`, `IsMenuOpen`/`CurrentPageId`/`IsTextInputFocused`,
`PageChanged`, `Confirm` (native modal), `ScrollToKey`/`ScrollToTop`, the chat overlay
(`SetChat`/`FocusChat`/`CloseChat`) and `UiKitLang` for bilingual pages. It is a **soft dependency**
— a mod that references only `OpenNestUIKit.API.dll` still loads when this mod is absent,
`IsHostAvailable` is `false`, and the calls do nothing. Every provider also gets a row in the
game's own ESC list (`IUiKitNativeEntry` changes or disables it), and implementing the optional
`IUiKitDefaults` adds a confirmed "Reset to defaults" row to your root page.

The contract is versioned (`UiKitHost.ApiVersion`); a breaking change bumps it.

📖 **Full contract reference: [docs/API.md](docs/API.md).** A complete, buildable example mod
(both loaders, one shared provider): **[samples/](samples)**.

### In-game test & automation harness

`src/OpenNestUIKit.Test/` is a separate mod that drives the library from a script —
clicking zones, injecting pointer/wheel events, dumping page geometry, taking screenshots —
so "does it look native?" is answered with measured numbers instead of impressions:

```powershell
-onuktest-run=wait:52000;gallery;wait:2500;pageprobe;wait:400;report
```

Commands include `gallery`, `pageprobe`, `pagespec`, `pagedump`, `zones`, `tap`/`move`/
`drag`/`dragpick`/`vdrag`/`scroll`, `key`, `shot`, `clickgame`, `nativeopen`, `report`.
Reports land in `<Game>/OpenNestUIKitLogs/test.log`.

---

## Requirements

- Iron Nest: Heavy Turret Simulator (IL2CPP build)
- **BepInEx 6.0.0-be.785+** (IL2CPP) **or** **MelonLoader 0.7.3**
- That's it — no extra dependencies

## Installation

```
BepInEx    : OpenNestUIKit.dll + OpenNestUIKit.API.dll  ->  <Game>/BepInEx/plugins/
MelonLoader: OpenNestUIKit.MelonMod.dll                 ->  <Game>/Mods/
             OpenNestUIKit.API.dll                      ->  <Game>/UserLibs/
```

Don't forget to launch the game once with the loader installed so it generates its
interop assemblies — both build targets need them.

## Releases

Prebuilt packages are published on the [Releases page](../../releases):

| Package | Contents |
|---|---|
| `OpenNestUIKit-<ver>-BepInEx.zip` | `BepInEx/plugins/OpenNestUIKit.dll` + `OpenNestUIKit.API.dll` |
| `OpenNestUIKit-<ver>-MelonLoader.zip` | `Mods/OpenNestUIKit.MelonMod.dll` + `UserLibs/OpenNestUIKit.API.dll` |

## Building from source

```powershell
git clone https://github.com/1499501762/Open-Nest-UIKit.git
cd Open-Nest-UIKit

# BepInEx build (add -p:DeployToGame=true to copy into <Game>/BepInEx/plugins/)
dotnet build src\OpenNestUIKit\OpenNestUIKit.csproj -c Release -p:GameDir="C:\path\to\game"

# MelonLoader build (add -p:DeployToMods=true to copy into Mods/ + UserLibs/)
dotnet build src\OpenNestUIKit.MelonMod\OpenNestUIKit.MelonMod.csproj -c Release -p:GameDir="C:\path\to\game" -p:ClientGame="C:\path\to\game"
```

Close the game before deploying — the plugin DLL is locked while it runs.

### Layout

| Path | What |
|---|---|
| `src/OpenNestUIKit.API/` | **Third-party contract** — plain .NET, no Unity dependency. Reference this from your mod to add pages. |
| `src/OpenNestUIKit/` | The mod itself: BepInEx shell, `Core/`, `Menu/`, `Layout/`, `Widgets/`, `Theme/`, `Native/`, `Pages/`, `Vendor/` (vendored UI/logging base with its own namespace). |
| `src/OpenNestUIKit.MelonMod/` | MelonLoader entry point; compiles the same `src/OpenNestUIKit/**` sources. |
| `src/OpenNestUIKit.Test/`, `src/OpenNestUIKit.Test.MelonMod/` | The in-game test harness mod (dev tool, not shipped in release packages). |
| `samples/` | A complete sample third-party mod: shared provider + BepInEx and MelonLoader shells. |
| `docs/API.md` | **Third-party contract reference** — every provider/host type, row verb and lifecycle rule. |
| `docs/UI_KIT.md` | Design notes: native-page geometry, widget specs, input isolation, measured evidence, update log. |
| `docs/UI_KIT_SLICE.md` | Slice/atlas tooling: how this mod consumes the game's 9-slice sprites. |
| `docs/UI_KIT_TEST.md` | The test harness: every CLI command, and what it proves. |
| `tools/slice_tool.py`, `scripts/slice-tool.ps1` | Offline 9-slice inspector/editor for the game's UI atlas (bring your own extracted sprites). |

## Design highlights

| Concern | How this mod handles it |
|---|---|
| **Native or nothing** | Widgets are copied from the game's own settings page — sprite, 9-slice `pixelsPerUnitMultiplier`, colour **and** the `CanvasRenderer` tint (the real colour often lives there, not in `Image.color`). Where the native value cannot reproduce the native *look* (the slider knob renders much darker in-game than its own colour suggests), the measured in-game pixel values win, and the reason is written down. |
| **Evidence over guesses** | Every risky claim in `docs/UI_KIT.md` carries a measured log line or a probe command. Conclusions are re-checked with a fresh dump when they disagree with what the user sees — the depth-limited dump that once "proved" the slider had no knob is recorded as the counter-example. |
| **Own input path** | The clipboard canvas is `WorldSpace` and the game's `EventSystem` is frequently inactive, so the library hit-tests the pointer itself. Zone order is the z-order: the wheel/drag area is registered first (bottom), rows after it. |
| **No trampling the game** | Only *foreign* raycasters/input modules are touched, canvas sorting order never takes the game's cursor layer, and every state is captured and restored. The escape level stack self-heals if a close path is missed. |
| **Both loaders, one source** | BepInEx and MelonLoader builds compile the *same* sources; only the entry shell differs. Everything loader-specific goes through reflection. |
| **Independent** | No reference to any other mod's assembly. The UI/logging base it reuses is **vendored source** in `src/OpenNestUIKit/Vendor/` (own namespace), so the mod can be installed on its own. |

Full design notes: [docs/UI_KIT.md](docs/UI_KIT.md).

## Roadmap

| # | Item | Status |
|---|---|---|
| U1 | Native page block geometry (871.7×1012.5 @ 0.3591) + non-scrolling header | ✅ |
| U2 | Native widget set (title / sub-title / tabs / slider / checkbox / dropdown / keybind / input / scroller / buttons / scrollbar) | ✅ |
| U3 | Input isolation (own pointer router, module suppression, world-click prefix, escape levels) | ✅ |
| U4 | Third-party contract (`OpenNestUIKit.API`: pages, native entries, chat overlay, refresh) | ✅ |
| U5 | Localisation keys (zh/en) + external language file | ✅ |
| U6 | Slice/atlas tooling + hot-reloaded slice definitions | ✅ |
| U7 | In-game test harness with scripted input and geometry probes | ✅ |
| U8 | Rubber-band scrolling, squared-up handle travel, native handle colours | ✅ |
| U9 | Public API documentation (`docs/API.md`) and sample mod | ✅ |
| U11 | `Stepper` / `Foldout` / `SelectableList` rows, dynamic `Hint`, `Text` placeholder + max length | ✅ |
| U12 | Host controls: native `Confirm` dialog, `ScrollToKey` / `ScrollToTop`, `PageChanged`, `UiKitLang`, `IUiKitDefaults` | ✅ |
| U13 | Naming parity with `OpenNestModMenu.API` (`Bool` / `Number` / `Action` aliases) | ✅ |
| U10 | Cross-loader test matrix (BepInEx without bridge, MelonLoader build inside BepInEx) | ⏳ |

**Test matrix (honest scope):** verified in-game on **G-side** (BepInEx 6 + bridge) and
**D-side** (native MelonLoader 0.7.3). The two remaining combinations have no test
environment on the development machine and are **not** claimed as verified.

## Contributing

Issues, ideas and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

**AGPL-3.0** — see [LICENSE](LICENSE).

This project is not affiliated with the developers of Iron Nest: Heavy Turret Simulator.
It is part of the Open Nest mod family alongside
[Open Nest Co-op](https://github.com/1499501762/OPEN_NEST_CO-OP),
[Open Nest Mod Menu](https://github.com/1499501762/Open-Nest-Mod-Menu) and
[Open Nest Core](https://github.com/1499501762/Open-Nest-Core), but is an independent
mod: it can be installed on its own, and the other mods work without it.

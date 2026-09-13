# Third-party API — `OpenNestUIKit.API`

Reference **`OpenNestUIKit.API.dll`** from your mod to give it a page inside the game's own
menu — drawn with the game's native widget set — without linking to the mod itself.

**It is a soft dependency.** The API assembly only holds the contract and a registry, so:

- if Open Nest UIKit is **not** installed, `UiKitHost.Register(...)` still succeeds — your
  provider just sits in memory, `UiKitHost.IsHostAvailable` stays `false`, and nothing else
  in your mod is affected;
- your mod never needs a compile-time reference to the mod assembly;
- the API has **no Unity or game assembly dependency** — the same DLL works for BepInEx and
  MelonLoader mods.

---

## 0. Implementation status

| Contract piece | Status |
|---|---|
| `UiKitHost` registry (`Register` / `Unregister` / `Unregister(id)` / `Clear`, `ProviderCount`, `Providers`, `Changed`, `IsHostAvailable`, `HostVersion`, `ApiVersion`) | ✅ implemented — registrations are collected and listed |
| `IUiKitProvider` identity + `BuildMenu(IUiMenuTree)` | ✅ implemented — called on menu open and on `Refresh()` |
| `IUiKitNativeEntry` (`ShowInNativeMenu` / `NativeOrder` / `NativeTitle` / `NativeTitleEn`) | ✅ implemented — a row is injected into the game's ESC list for every provider by default; opt out or re-title/order it here |
| Page model (`UiPageDef`, `UiRow`, `UiRowKind`, `UiMenuTree`) | ✅ implemented |
| Row family (`Header` / `Label` / `Separator` / `Button` / `Nav` / `Toggle` / `Slider` / `Choice` / `Tabs` / `Text` / `KeyBind` / `Progress` / `Columns` / `List` / `Add`) | ✅ rendered — every row renders as the game's own widget (text fields include the in-game IME/keyboard pipeline, key rows capture the pressed key) |
| Page sizing (`Size`) and density (`SetCompact`) | ✅ implemented |
| Host controls (`OpenMenu` / `CloseMenu` / `ToggleMenu` / `Refresh` / `IsMenuOpen` / `CanControlMenu` / `CurrentPageId` / `IsTextInputFocused`) | ✅ implemented (`CanControlMenu` is `false` until the host is loaded) |
| Chat overlay (`SetChat` / `ClearChat` / `FocusChat` / `CloseChat` / `CanShowChat` / `IsChatTyping`) | ✅ implemented — one floating panel per host, driven by your line/en send callbacks |

`UiKitHost.ApiVersion` is currently **1**. Nothing in this document is a placeholder — if a
piece behaves differently from what is written here, that is a bug.

---

## 1. Quick start

Two files are enough: a provider (pure .NET — the same file compiles for both loaders) and a
loader entry point.

**`MyModMenu.cs`** — the page itself:

```csharp
using System.Collections.Generic;
using OpenNestUIKit.API;

public sealed class MyModMenu : UiKitProviderBase
{
    public override string Id          => "mymod";        // stable + unique (assembly name is fine)
    public override string DisplayName => "My Mod";       // shown in the menu list and the ESC entry
    public override string Version     => "1.0.0";
    public override string Author      => "you";

    private readonly MyConfig _cfg;

    public MyModMenu(MyConfig cfg) { _cfg = cfg; }

    public override void BuildMenu(IUiMenuTree menu)
    {
        // Root is the page shown when the player picks "My Mod"; put Nav entries in it.
        menu.Root.Label("My Mod " + Version);
        menu.Root.Nav("General", "general", "gameplay options");
        menu.Root.Nav("Keys",    "keys");
        menu.Root.Toggle("mymod.enabled", "Enable mod", _cfg.Enabled, v => _cfg.Enabled = v);

        var general = menu.Page("general", "General");
        general.Header("Gameplay");
        general.Slider("mymod.range", "Range (m)", _cfg.Range, 10, 500, 5, v => _cfg.Range = (float)v);
        general.Choice("mymod.mode", "Mode", new[] { "Fast", "Accurate" }, _cfg.ModeIndex, i => _cfg.ModeIndex = i);
        general.Text("mymod.name", "Callsign", _cfg.Callsign, v => _cfg.Callsign = v);
        general.Separator();
        general.Button("Reset defaults", "Reset", () => _cfg.Reset());

        var keys = menu.Page("keys", "Keys");
        keys.Label("Click the box, then press a key (ESC cancels).");
        keys.KeyBind("mymod.hotkey", "Open my menu", _cfg.Hotkey, v => _cfg.Hotkey = v);
    }
}
```

**BepInEx entry point**:

```csharp
using BepInEx;
using BepInEx.Unity.IL2CPP;

[BepInPlugin("you.mymod", "My Mod", "1.0.0")]
public sealed class MyPlugin : BasePlugin
{
    public override void Load()
    {
        var cfg  = MyConfig.Load();
        var menu = new MyModMenu(cfg);

        // Works whether or not Open Nest UIKit is installed.
        UiKitHost.Register(menu);

        // Optional: open our page from a hotkey. Provider pages are namespaced
        // as "provider:<Id>" by the host, so that is the id to open.
        // UiKitHost.OpenMenu("provider:" + menu.Id);
    }
}
```

For MelonLoader, use `MelonMod`/`MelonMod` entry instead of `BasePlugin` — the **provider file
does not change**. A complete, buildable pair of shells lives in [`samples/`](../samples).

---

## 2. Provider identity

```csharp
public interface IUiKitProvider
{
    string Id { get; }            // stable, unique, case-insensitive (registering the same Id replaces)
    string DisplayName { get; }   // menu label + default native-entry title
    string Version { get; }       // optional ("" is fine)
    string Author { get; }        // optional
    void BuildMenu(IUiMenuTree menu);
}
```

`UiKitProviderBase` implements the interface with safe defaults for `Version`, `Author` and
`BuildMenu` — derive from it and override `Id`, `DisplayName` and `BuildMenu`.

Rules that matter:

- **`Id` must be non-empty** — an empty id is ignored by `Register`.
- `Id` is the namespace for everything you declare (see §3) and for your native entry.
- If `Id` throws, `Register` ignores the provider. Keep identity properties trivial.
- Re-registering a different instance with the same `Id` **replaces** the old one; registering
  the exact same instance twice is a no-op.

---

## 3. Pages, ids and navigation

```csharp
public interface IUiMenuTree
{
    UiPageDef Root { get; }                        // implicit root page (titled with DisplayName)
    UiPageDef Page(string id, string title);       // declare / fetch a page by id
    IReadOnlyList<UiPageDef> Pages { get; }        // Root + every declared page
}

public sealed class UiPageDef      // builder: one method per row kind, chainable
{
    UiPageDef(string id, string title);
    string Id { get; }  string Title { get; set; }
    IReadOnlyList<UiRow> Rows { get; }

    UiPageDef Size(float w, float h);              // preferred window size (0 = host default)
    UiPageDef SetCompact(bool compact = true);     // tighter density for narrow panels
    UiPageDef Add(UiRow row);                      // escape hatch for the row model
    // …row verbs, see §4
}
```

**How the host namespaces your pages.** You declare short ids (`"general"`); the host prefixes
them so two mods can both have a `general` page:

| You declare | Page id in the host |
|---|---|
| `menu.Root` | `provider:<your Id>` |
| `menu.Page("general", …)` | `provider:<your Id>:general` |

`UiKitHost.OpenMenu("provider:<your Id>")` opens your root page;
`UiKitHost.CurrentPageId` returns the namespaced id. An unknown id falls back to the menu home
page (a warning is logged).

**Navigation.** `Nav(label, pageId)` pushes a page; the menu keeps a page stack and shows a back
row, so sub-pages behave like the game's own multi-level menus.

**Auto-listing.** The host adds a `Nav` row to your root page for every page you declared but
did **not** reference with `Nav` in `Root` — so a provider that declares pages and no root rows
still gets a usable list, and a hand-ordered root never shows duplicates.

**Persistence between opens.** The host calls `BuildMenu` again every time the menu is built;
your provider instance survives, so fields in it are your state. Read your config inside
`BuildMenu` if something else can change it.

---

## 4. Row reference

All verbs are chainable and return the same `UiPageDef`.

| Verb | Renders as |
|---|---|
| `Header(string text)` | group title |
| `Label(string text)` | plain text |
| `Separator()` | divider line |
| `Button(string label, string buttonText, Action onClick)` | label + button |
| `Nav(string label, string pageId, string hint = null)` | submenu entry |
| `Tabs(string key, IReadOnlyList<string> tabs, int index, Action<int> onChanged)` | tab bar |
| `Toggle(string key, string label, bool value, Action<bool> onChanged)` | native checkbox |
| `Slider(string key, string label, double value, double min, double max, double step, Action<double> onChanged)` | native slider |
| `Choice(string key, string label, IReadOnlyList<string> choices, int selected, Action<int> onChanged)` | dropdown (or `◀ value ▶` scroller in narrow pages) |
| `Text(string key, string label, string value, Action<string> onChanged)` | text field (in-game keyboard/IME) |
| `KeyBind(string key, string label, string current, Action<string> onChanged)` | key capture box |
| `Progress(string label, double value01)` | read-only progress bar (clamped to 0..1) |
| `Columns(float leftWidth, Action<UiPageDef> left, Action<UiPageDef> right, float gap = 12f)` | two columns, see §5 |
| `List(string key, float height, Action<UiPageDef> build)` | scrolling sub-list, see §5 |

Details worth knowing:

- **`key`** is your own handle for the setting (it is not used for storage — the host never
  writes anywhere). It is useful for diagnostics and for correlated rows.
- **Write-back goes through your callback only.** The host reports the user's action; you decide
  where it lands (config file, memory, network). There is no automatic config persistence.
- `Slider` accepts a fractional `step`; `step <= 0` means "host default" (`(max - min) / 100`).
- `Choice` writes back the chosen **string**, and calls you with its **index**.
- `Toggle` parses `"true" / "1" / "on" / "yes"` (anything else is `false`).
- `Progress` is read-only; update it by calling `UiKitHost.Refresh()` after your state changes.
- Numbers cross the contract as invariant-culture strings, so `step 0.05` and `1.5` behave the
  same on every locale.

**Row model escape hatch.** For anything not covered by a verb, build a `UiRow` and `Add` it:

```csharp
public sealed class UiRow
{
    public UiRowKind Kind { get; set; }      // Header Label Separator Button Nav Toggle
                                             // Slider Choice Text KeyBind Progress Tabs Columns List
    public string Key, Label, Value, Hint;
    public bool ReadOnly;
    public string PageId;                    // Nav
    public IReadOnlyList<string> Choices;    // Choice / Tabs
    public double Min, Max, Step;            // Slider / Progress
    public Action OnClick;                   // Button
    public Func<string, bool> Write;         // Toggle/Slider/Choice/Text/KeyBind: return false to reject
    public float LeftWidth, ColumnGap;       // Columns
    public IReadOnlyList<UiRow> LeftRows, RightRows;   // Columns
    public float ListHeight;                 // List
    public IReadOnlyList<UiRow> ListRows;    // List
}
```

---

## 5. Layout containers

**`Columns`** — a fixed-width left column plus a right column that takes the rest; each side is
declared with the same verbs as a page (nesting containers inside a column is not supported):

```csharp
page.Columns(440f,
    left:  l => { l.List("mymod.list", 420f, list => { /* one Button per item */ }); },
    right: r => { r.Header("Details"); r.Label(...); r.Button("Apply", "Apply", Apply); });
```

**`List`** — a fixed-height area with its own scrollbar and rubber-band scrolling. Use it for
regions whose row count is unknown (mod lists, room lists, logs) so the whole page does not grow
and scroll instead.

---

## 6. Host controls

```csharp
UiKitHost.ApiVersion          // 1 — contract revision, see §10
UiKitHost.IsHostAvailable     // false until the mod is loaded (and after it unloads)
UiKitHost.HostVersion         // host mod version, "" when unavailable
UiKitHost.ProviderCount       // registered providers
UiKitHost.Providers           // snapshot array of IUiKitProvider
UiKitHost.Changed             // event: registry changed / host (un)available

UiKitHost.Register(provider);          UiKitHost.Unregister(provider);
UiKitHost.Unregister("mymod");         // by id

UiKitHost.CanControlMenu      // false when the host is not loaded
UiKitHost.IsMenuOpen
UiKitHost.OpenMenu(pageId = null);     // null = home page
UiKitHost.CloseMenu();
UiKitHost.ToggleMenu(pageId = null);
UiKitHost.Refresh();                   // rebuild the current page in place
UiKitHost.CurrentPageId                // "" when the menu is closed
UiKitHost.IsTextInputFocused           // true while a text field in the menu has focus
```

Semantics:

- **Every call is safe when the host is missing** — the calls do nothing rather than throw.
  Use `IsHostAvailable` (or `CanControlMenu`) if you want to log a hint for the player.
- `Refresh()` rebuilds the menu **in place**: the page stack and scroll position stay, but
  `BuildMenu` runs again. Call it after data your page displays changed (a list of players, a
  lobby, a chat log, a toggle flipped from elsewhere).
- ⚠️ **Do not `Refresh()` while `IsTextInputFocused` is true** — the rebuild discards what the
  user has typed into a field. Typical guard:

  ```csharp
  void OnLobbyChanged() { if (!UiKitHost.IsTextInputFocused) UiKitHost.Refresh(); }
  ```

- `Changed` fires when providers register/unregister and when the host comes online — a good
  place to log "connected to UIKit 0.0.1-Alpha-1".

---

## 7. Native ESC entry

By default the host injects **one row per provider** into the game's own ESC list, labelled with
`DisplayName`, loading your root page. Implement `IUiKitNativeEntry` to change that:

```csharp
public sealed class MyModMenu : UiKitProviderBase, IUiKitNativeEntry
{
    public bool   ShowInNativeMenu => false;      // false = only reachable inside the UIKit menu
    public int    NativeOrder      => 50;         // smaller comes first (default: 100 + registration order)
    public string NativeTitle      => "我的模组";  // shown when the game language is Chinese
    public string NativeTitleEn    => "My Mod";   // shown otherwise (empty = fall back to NativeTitle)
}
```

Injecting is **entirely the host's job** — you never touch the game's own UI. That keeps multiple
providers from fighting over slots and keeps the native look in one place. Note the game's ESC
panel is already full (8 rows fill the panel), so a new row compresses the layout: only ask for
one if your page is worth a top-level slot.

---

## 8. Chat overlay

If your mod has a conversation (voice/text chat, lobbies, notifications), hand it to the host
and let it render a floating panel that exists **while the menu is closed**:

```csharp
UiKitHost.SetChat(
    lines:  () => myChat.RecentLines,          // newest last; polled every ~0.4s
    onSend: text => myChat.Send(text),
    title:  "Squad",                            // optional
    hint:   () => "Enter to chat");             // optional collapsed-state hint

UiKitHost.FocusChat();        // expand + focus the input (optionally pre-fill a draft)
UiKitHost.CloseChat();        // collapse, keep history
UiKitHost.ClearChat();        // unregister (when the player leaves the session)

UiKitHost.CanShowChat;        // host provides the overlay
UiKitHost.IsChatTyping;       // the overlay currently has the input focused
```

- The overlay is a **separate canvas** that stays visible when the menu closes, and it does not
  steal mouse input while collapsed.
- The host rebuilds the panel only when your line list changes, so returning a cached list is
  fine.
- `Enter` opens/sends, `ESC` collapses; the host reuses the same text-input pipeline as the menu.

---

## 9. Lifecycle, threading and errors

| Rule | Why |
|---|---|
| Register at any time, including before the host loads | Registrations are just an in-memory list; the host picks them up when it starts |
| Keep `Id`/`DisplayName`/`Version`/`Author` trivial | They are read during collection; a throw makes the host skip your provider |
| Keep `BuildMenu` cheap, and only touch your own state | It runs on the main thread whenever the menu opens or `Refresh()` is called |
| Never assume a page is visible | `BuildMenu` is called for collection even if the player never opens the page |
| Exceptions are contained, not punished | The host catches throwables from your callbacks so one bad provider cannot break the menu — but the row will silently do nothing |
| Update rows through `Refresh()`, not by mutating a built page | The host re-reads your declarations on rebuild |
| Unregister on unload (`Unregister(provider)`) | Otherwise your page stays listed until the host restarts |

---

## 10. Packaging and versioning

- **Reference the API, do not ship it.** The API assembly is distributed by Open Nest UIKit
  (`BepInEx/plugins/OpenNestUIKit.API.dll`, and `UserLibs/OpenNestUIKit.API.dll` on MelonLoader).
  A second copy in `plugins/` is a duplicate-assembly hazard:

  ```xml
  <ProjectReference Include="..\OpenNestUIKit.API\OpenNestUIKit.API.csproj" Private="false" />
  <!-- or, with a prebuilt dll: -->
  <Reference Include="OpenNestUIKit.API"><HintPath>lib\OpenNestUIKit.API.dll</HintPath><Private>false</Private></Reference>
  ```

- **Versioning.** `UiKitHost.ApiVersion` bumps only on a breaking change to the contract; the
  mod's own version moves independently. Compare `ApiVersion` if you want to be defensive.
- **Isolation.** The API assembly is plain .NET: it has no dependency on Unity, BepInEx,
  MelonLoader or the mod. A single build of your provider works on both loaders.
- **Missing host.** `Register` succeeds, `IsHostAvailable` is `false`, all controls are no-ops.
  Your mod must keep working — that is the whole point of the soft dependency.

---

## 11. Samples and further reading

- [`samples/`](../samples) — a complete provider plus BepInEx and MelonLoader entry points,
  buildable against the API in this repository.
- `docs/UI_KIT.md` (in the mod repository) — how the native page/widgets are built, measured and
  verified: geometry, colours, input isolation, scrolling.
- `docs/UI_KIT_TEST.md` — the in-game test harness used to prove the above (scripted clicks,
  geometry probes, screenshots).

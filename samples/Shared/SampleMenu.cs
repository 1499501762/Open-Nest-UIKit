using System;
using System.Collections.Generic;
using OpenNestUIKit.API;

namespace OpenNestUIKit.Sample;

/// <summary>
/// A complete third-party menu: **declare pages and rows, the host renders them natively.**
///
/// This file is deliberately plain .NET — it references only <c>OpenNestUIKit.API</c>, so the same
/// source compiles for BepInEx and MelonLoader and keeps working when Open Nest UIKit is absent
/// (<see cref="UiKitHost"/> calls become no-ops, <see cref="UiKitHost.IsHostAvailable"/> is false).
///
/// What it demonstrates, page by page:
/// <list type="bullet">
/// <item>root page: labels, sub-page entries, a toggle;</item>
/// <item><c>general</c>: slider / dropdown / text field / action button, and content that changes
/// with state (the warning row only exists while the feature is off);</item>
/// <item><c>keys</c>: key binding + a label that always shows the current binding;</item>
/// <item><c>live</c>: an embedded scrolling list rebuilt on demand with <see cref="UiKitHost.Refresh"/>,
/// guarded by <see cref="UiKitHost.IsTextInputFocused"/>;</item>
/// <item><c>columns</c>: a two-column layout (list on the left, details on the right);</item>
/// <item><c>about</c>: host/contract diagnostics — useful while integrating.</item>
/// </list>
/// </summary>
public sealed class SampleMenu : UiKitProviderBase, IUiKitNativeEntry
{
    /// <summary>Stable provider id. Host page ids are namespaced with it: <c>provider:sample.mod</c>.</summary>
    public const string ModId = "sample.mod";

    public override string Id => ModId;
    public override string DisplayName => "Sample Mod";
    public override string Version => "1.0.0";
    public override string Author => "OpenNestUIKit";

    // ---- IUiKitNativeEntry: what goes into the game's own ESC list --------------------------
    // The host injects the row; a third-party mod never touches the game's UI itself.
    public bool ShowInNativeMenu => true;
    public int NativeOrder => 150;              // smaller comes first (default is 100 + register order)
    public string NativeTitle => "示例模组";     // shown on a Chinese game language
    public string NativeTitleEn => "Sample Mod"; // otherwise (empty = fall back to NativeTitle)

    private readonly SampleConfig _cfg;
    private readonly List<string> _players = new List<string> { "Host", "Player 2", "Player 3" };
    private int _selected;

    public SampleMenu(SampleConfig cfg) { _cfg = cfg; }

    public override void BuildMenu(IUiMenuTree menu)
    {
        // ---------------- root page (the page the ESC entry opens) ----------------
        menu.Root.Label("Sample Mod " + Version + " — pages are declared, not drawn.");
        menu.Root.Toggle("sample.enabled", "Enable sample feature", _cfg.Enabled, v =>
        {
            _cfg.Enabled = v;
            _cfg.Save();
            UiKitHost.Refresh();               // keep dependent rows in sync
        });
        menu.Root.Nav("General", "general", "toggle / slider / dropdown / text");
        menu.Root.Nav("Keys", "keys", "key binding");
        menu.Root.Nav("Live data", "live", "scrolling list + Refresh");
        menu.Root.Nav("Two columns", "columns", "list on the left, details on the right");
        menu.Root.Nav("About", "about", "host + contract diagnostics");

        // ---------------- general ----------------
        var general = menu.Page("general", "General");
        general.Header("Gameplay");
        general.Slider("sample.range", "Range (m)", _cfg.Range, 10, 500, 5, v =>
        {
            _cfg.Range = v;
            _cfg.Save();
        });
        general.Choice("sample.mode", "Mode", new[] { "Fast", "Accurate", "Native" }, _cfg.ModeIndex, i =>
        {
            _cfg.ModeIndex = i;
            _cfg.Save();
        });
        general.Text("sample.callsign", "Callsign", _cfg.Callsign, v =>
        {
            _cfg.Callsign = v;
            _cfg.Save();
        });
        if (!_cfg.Enabled)
        {
            // Content can depend on state: BuildMenu runs again on every open/refresh.
            general.Separator();
            general.Label("Feature is disabled — enable it on the previous page.");
        }
        general.Separator();
        general.Header("Actions");
        general.Button("Restore defaults", "Reset", () =>
        {
            _cfg.Reset();
            UiKitHost.Refresh();
        });
        general.Nav("Show the current binding", "keys");

        // ---------------- keys ----------------
        var keys = menu.Page("keys", "Keys");
        keys.Label("Click the box on the right, then press a key (ESC cancels).");
        keys.KeyBind("sample.hotkey", "Open this menu", _cfg.Hotkey, v =>
        {
            _cfg.Hotkey = v;
            _cfg.Save();
            UiKitHost.Refresh();               // repaint the line below with the new binding
        });
        keys.Separator();
        keys.Label("Current binding: " + (_cfg.Hotkey.Length == 0 ? "(none)" : _cfg.Hotkey));
        keys.Label("Binding is stored by the mod itself (here: in memory).");

        // ---------------- live data (embedded scrolling list) ----------------
        var live = menu.Page("live", "Live data");
        live.Label("Players seen by the host: " + _players.Count + ". The list is rebuilt by UiKitHost.Refresh().");
        live.List("sample.players", 300f, list =>
        {
            for (int i = 0; i < _players.Count; i++)
            {
                int index = i;
                bool self = index == _selected;
                list.Button((self ? "● " : "○ ") + _players[index], "Select", () =>
                {
                    _selected = index;
                    UiKitHost.Refresh();
                });
            }
        });
        live.Separator();
        live.Button("Add a player", "Add", () => { _players.Add("Player " + (_players.Count + 1)); RefreshLive(); });
        live.Button("Remove the last player", "Remove", () =>
        {
            if (_players.Count > 1) _players.RemoveAt(_players.Count - 1);
            if (_selected >= _players.Count) _selected = _players.Count - 1;
            RefreshLive();
        });
        if (_players.Count > 0)
            live.Label("Selected: " + _players[Math.Max(0, Math.Min(_players.Count - 1, _selected))]);

        // ---------------- two columns ----------------
        var columns = menu.Page("columns", "Two columns");
        columns.Columns(280f,
            left: l =>
            {
                l.Header("Entries");
                l.Label("The left column has a fixed width; the right column takes the rest.");
            },
            right: r =>
            {
                r.Header("Details");
                r.Label("Callsign : " + _cfg.Callsign);
                r.Label("Range    : " + _cfg.Range.ToString("0.##") + " m");
                r.Label("Mode     : " + ModeName(_cfg.ModeIndex));
                r.Separator();
                r.Button("Open the general page", "Open", () => UiKitHost.OpenMenu("provider:" + ModId + ":general"));
            });

        // ---------------- about / diagnostics ----------------
        var about = menu.Page("about", "About");
        about.Header("This mod");
        about.Label("Id      : " + Id);
        about.Label("Version : " + Version + " (sample)");
        about.Separator();
        about.Header("Host");
        about.Label("Available   : " + UiKitHost.IsHostAvailable);
        about.Label("HostVersion : " + (UiKitHost.HostVersion.Length == 0 ? "(none)" : UiKitHost.HostVersion));
        about.Label("ApiVersion  : " + UiKitHost.ApiVersion);
        about.Label("Providers   : " + UiKitHost.ProviderCount);
        about.Label("CurrentPage : " + (UiKitHost.CurrentPageId.Length == 0 ? "(menu closed)" : UiKitHost.CurrentPageId));
        about.Progress("Sample progress", 0.42);
    }

    /// <summary>Rebuild the page, but never while the player is typing in a text field.</summary>
    private static void RefreshLive()
    {
        if (!UiKitHost.IsTextInputFocused) UiKitHost.Refresh();
    }

    private static string ModeName(int index)
    {
        string[] modes = { "Fast", "Accurate", "Native" };
        return (index >= 0 && index < modes.Length) ? modes[index] : "?";
    }
}

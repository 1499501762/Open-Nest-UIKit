using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if MELONLOADER
using TMPro = Il2CppTMPro;
#else
using TMPro;
#endif

namespace OpenNestUIKit.Widgets;

/// <summary>
/// **悬浮聊天层**（默认：左侧中部的独立小面板）——把原模组 `CoopUIManager` 那套“独立聊天浮窗”搬回 UIKit。
///
/// 为什么要有它：聊天不应该只藏在菜单的某个页签里。原模组的行为是——
/// · 联机中，**左中常驻一个半透明浮窗**显示最近几条聊天（鼠标穿透，不挡游戏操作）；
/// · **回车唤入**：按回车展开输入行（并唤起输入法）；
/// · **回车发送 / ESC 收起**；发送后自动收起回“只看历史”的状态。
///
/// 实现要点：
/// - 它是**自己的画布**（`OpenNestUIKit_HUD`，层级 32760 —— 比菜单低一档），因为菜单画布在关菜单时会整体隐藏，
///   而浮窗要在**菜单关着的时候也在**；
/// - 文本/输入走本库组件（聊天输入直接用 <see cref="UiTextInput"/> → 走 <see cref="UiTextRouter"/> 的输入管线，
///   中文输入法与输入框完全同一套）；
/// - 数据由第三方提供：`lines()` 每次拉最近 N 条（宿主每 0.4s 拉一次，内容变了才重建），`onSend(text)` 发送。
/// </summary>
public static class UiChatOverlay
{
    private const int MaxLines = 40;          // 最多拉多少条（只渲染最后 ~8 条可见）
    private const float PanelW = 340f, PanelH = 190f;
    private const float Margin = 16f;

    private static string _id;
    private static string _title = "Chat";
    private static Func<IReadOnlyList<string>> _lines;
    private static Action<string> _onSend;
    private static Func<string> _hint;

    private static Canvas _canvas;
    private static RectTransform _root, _panel, _inputRow;
    private static Image _panelBg;
    private static UiList _list;
    private static UiText _hintText;
    private static UiTextInput _input;
    private static Image _inputBg;

    private static bool _expanded;
    private static float _pollT;
    private static string _lastSig = "";
    private static float _focusAt;
    private static string _lastSent = "";      // 诊断：上次通过悬浮层真正发出去的文本
    private static int _openCount, _closeCount; // 诊断：展开/收起次数
    private static string _lastClose = "-";     // 诊断：最近一次收起的原因

    /// <summary>是否已注册悬浮层。</summary>
    public static bool Has => _canvas != null && _lines != null;

    /// <summary>是否展开（正在打字）。</summary>
    public static bool Focused => _expanded;

    /// <summary>注册/更新悬浮聊天层（第三方在进入会话时调一次，离开时 <see cref="Clear"/>）。</summary>
    public static void Set(string id, string title, Func<IReadOnlyList<string>> lines, Action<string> onSend, Func<string> hint = null)
    {
        _id = id ?? "chat";
        _title = string.IsNullOrEmpty(title) ? "Chat" : title;
        _lines = lines;
        _onSend = onSend;
        _hint = hint;
        try
        {
            EnsureBuilt();
            RefreshLines(force: true);
            SetExpanded(false);
        }
        catch (Exception ex) { CoopLog.Warn("uikit.chat", () => "overlay set failed: " + ex.Message); }
    }

    /// <summary>注销（离开会话/关闭联机时调）。</summary>
    public static void Clear()
    {
        _lines = null; _onSend = null; _hint = null; _id = null;
        _expanded = false;
        _lastSig = "";
        try
        {
            if (Focused) UiTextRouter.SetFocused(null);
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas.gameObject);
        }
        catch { }
        _canvas = null; _root = null; _panel = null; _panelBg = null; _inputRow = null;
        _list = null; _hintText = null; _input = null; _inputBg = null;
    }

    /// <summary>展开输入并聚焦（回车唤入 / 点浮窗）。</summary>
    public static void Focus(string draft = null)
    {
        if (!Has) return;
        try
        {
            SetExpanded(true);
            if (draft != null && _input != null) _input.Value = draft;
            _input?.Focus();
            _focusAt = Time.realtimeSinceStartup;
        }
        catch (Exception ex) { CoopLog.Warn("uikit.chat", () => "focus failed: " + ex.Message); }
    }

    /// <summary>收起输入（保留历史）。</summary>
    public static void Close()
    {
        try
        {
            if (Focused) UiTextRouter.SetFocused(null);
            SetExpanded(false);
        }
        catch { }
    }

    /// <summary>每帧（<c>UiKitBehaviour.Update</c> 调用）。</summary>
    public static void Tick(float dt)
    {
        if (!Has) return;
        try
        {
            // ① 历史内容变化 → 重建（节流 0.4s；内容 signature 变了才重建）
            _pollT += dt;
            if (_pollT >= 0.4f) { _pollT = 0f; RefreshLines(force: false); }

            // ② 展开态与输入框焦点保持一致（输入框被 ESC/提交失焦 → 自动收起）
            if (_expanded && !ReferenceEquals(UiTextRouter.Focused, _input) && Time.realtimeSinceStartup - _focusAt > 0.2f)
            {
                _lastClose = "输入框失焦（Focused=" + (UiTextRouter.Focused != null ? UiTextRouter.Focused.Label : "null") + "）";
                SetExpanded(false);
            }

            // ③ **回车唤入**：菜单没开、没人正在打字时，按回车展开聊天
            //    ⚠️ 回车判定走 `UiTextRouter.EnterThisFrame`（物理上升沿 + 单帧脉冲 + 文本通道 \r 三通道，
            //       比只看 wasPressedThisFrame 稳）；`ConsumedEnter` = 同一次回车已经被输入框提交用掉了，
            //       不能再用它重新展开（否则刚收起的聊天框会立刻弹回来）。
            if (!_expanded && !UiTextRouter.Typing && !UiTextRouter.Composing
                && !Menu.UiMenuWindow.IsOpen && !UiTextRouter.ConsumedEnter && UiTextRouter.EnterThisFrame)
            {
                Focus();
            }
        }
        catch { }
    }

    // ---------------- 内部：构建与状态 ----------------

    private static void SetExpanded(bool on)
    {
        if (on != _expanded) { if (on) _openCount++; else _closeCount++; }
        _expanded = on;
        try
        {
            if (_panelBg != null)
            {
                var c = _panelBg.color;
                _panelBg.color = new Color(c.r, c.g, c.b, on ? 0.94f : 0.34f);   // 收起时更透（不挡视野）
            }
            if (_inputRow != null && _inputRow.gameObject.activeSelf != on) _inputRow.gameObject.SetActive(on);
            if (_hintText != null) _hintText.Visible = !on;
            if (_list != null) _list.Rect.sizeDelta = new Vector2(PanelW - 16f, on ? PanelH - 74f : PanelH - 30f);
            if (_inputBg != null) _inputBg.color = on ? new Color(0.086f, 0.114f, 0.153f, 1f) : Theme.UiTheme.InputBg;
        }
        catch { }
    }

    private static void EnsureBuilt()
    {
        if (_canvas != null) return;

        _canvas = UI.UiKit.CreateCanvas("OpenNestUIKit_HUD", 32760);
        if (_canvas == null) throw new InvalidOperationException("HUD canvas 创建失败");
        _canvas.gameObject.SetActive(true);

        _root = _canvas.GetComponent<RectTransform>();
        _panel = UI.UiKit.MakeRect("chat", _root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
        try
        {
            _panel.pivot = new Vector2(0f, 0.5f);
            _panel.anchoredPosition = new Vector2(Margin, 40f);
            _panel.sizeDelta = new Vector2(PanelW, PanelH);
        }
        catch { }
        _panelBg = _panel.gameObject.AddComponent<Image>();
        _panelBg.color = new Color(0.043f, 0.055f, 0.071f, 0.34f);
        _panelBg.raycastTarget = false;

        // 标题
        var title = UiText.Create(_panel, _title, UiTextKind.Header, 0f, TMPro.TextAlignmentOptions.MidlineLeft);
        try
        {
            title.Rect.anchorMin = new Vector2(0f, 1f);
            title.Rect.anchorMax = new Vector2(1f, 1f);
            title.Rect.pivot = new Vector2(0f, 1f);
            title.Rect.offsetMin = new Vector2(10f, -24f);
            title.Rect.offsetMax = new Vector2(-10f, 0f);
        }
        catch { }
        title.Color = Theme.UiTheme.Accent;

        // 历史列表（自己的滚动条；鼠标穿透：raycastTarget 关掉）
        _list = UiList.Create(_panel, PanelH - 30f, "chat-list");
        try
        {
            _list.Rect.anchorMin = new Vector2(0f, 1f);
            _list.Rect.anchorMax = new Vector2(1f, 1f);
            _list.Rect.pivot = new Vector2(0.5f, 1f);
            _list.Rect.offsetMin = new Vector2(8f, -(PanelH - 30f) - 24f);
            _list.Rect.offsetMax = new Vector2(-8f, -24f);
            _list.Rect.sizeDelta = new Vector2(-16f, PanelH - 30f);
        }
        catch { }

        // 收起时的提示行（回车打开聊天 / 鼠标点击浮窗）
        _hintText = UiText.Create(_panel, "", UiTextKind.Note, 0f, TMPro.TextAlignmentOptions.MidlineLeft);
        try
        {
            _hintText.Rect.anchorMin = new Vector2(0f, 0f);
            _hintText.Rect.anchorMax = new Vector2(1f, 0f);
            _hintText.Rect.pivot = new Vector2(0f, 0f);
            _hintText.Rect.offsetMin = new Vector2(10f, 4f);
            _hintText.Rect.offsetMax = new Vector2(-10f, 24f);
        }
        catch { }
        _hintText.Color = Theme.UiTheme.TextDim;

        // 输入行（展开才显示；用本库输入框 → 走 UiTextRouter 的物理键/IME 管线）
        var inputRow = UiTextInput.Create(_panel, "", "", v => { }, onSubmit: OnSubmit, height: 30f, zoneName: "chat-input");
        _input = inputRow;
        _inputRow = inputRow.Rect;
        try
        {
            _inputRow.anchorMin = new Vector2(0f, 0f);
            _inputRow.anchorMax = new Vector2(1f, 0f);
            _inputRow.pivot = new Vector2(0.5f, 0f);
            _inputRow.offsetMin = new Vector2(8f, 6f);
            _inputRow.offsetMax = new Vector2(-8f, 6f + 30f);
            _inputRow.sizeDelta = new Vector2(-16f, 30f);
        }
        catch { }
        _inputBg = _panelBg;

        // 点击浮窗 = 展开并聚焦（自管指针热区，覆盖整个浮窗）
        Native.UiPointerRouter.Add(new Native.UiHotZone
        {
            Name = "chat-open",
            Rect = _panel,
            OnClick = () => { if (!_expanded) Focus(); },
        });
    }

    private static void OnSubmit(string text)
    {
        try
        {
            string msg = (text ?? "").Trim();
            _lastSent = msg;
            if (msg.Length > 0) _onSend?.Invoke(msg);
        }
        catch (Exception ex) { CoopLog.Warn("uikit.chat", () => "send failed: " + ex.Message); }
        try { if (_input != null) _input.Value = ""; } catch { }
        Close();
        RefreshLines(force: true);
    }

    private static void RefreshLines(bool force)
    {
        if (_list == null || _lines == null) return;
        try
        {
            IReadOnlyList<string> all = null;
            try { all = _lines(); } catch { }
            if (all == null) return;
            int from = Mathf.Max(0, all.Count - MaxLines);

            var sig = new System.Text.StringBuilder();
            sig.Append(all.Count).Append('|');
            for (int i = from; i < all.Count; i++) sig.Append(all[i]).Append('\n');
            string s = sig.ToString();
            if (!force && s == _lastSig) return;
            _lastSig = s;

            // 历史：每行一条只读文本（重建后自动滚到底部）
            _list.ClearRows();
            for (int i = from; i < all.Count; i++)
            {
                var t = UiText.Create(_list.Flow.Rect, all[i] ?? "", UiTextKind.Note);
                _list.Add(t, Layout.UiSize.Auto);
            }
            _list.ApplyLayout();
            try { _list.ScrollBy(float.MaxValue); } catch { }      // 滚到底（最新在下面）
            if (_hintText != null && _hint == null)
                _hintText.Value = UiKitLoc.T("回车 打开聊天", "Enter to chat");
            else if (_hintText != null)
            {
                try { _hintText.Value = _hint(); } catch { }
            }
        }
        catch (Exception ex) { CoopLog.Warn("uikit.chat", () => "refresh failed: " + ex.Message); }
    }

    /// <summary>宿主专用：与 <c>UiKitHost.SetChatControls</c> 对接的签名包装。</summary>
    public static void SetFromHost(string title, Func<IReadOnlyList<string>> lines, Action<string> onSend, Func<string> hint)
        => Set("chat", title, lines, onSend, hint);

    /// <summary>诊断串（测试模组用）。</summary>
    public static string Probe()
    {
        int n = 0;
        try { n = _list != null ? _list.Count : 0; } catch { }
        return $"悬浮聊天层：注册={(Has ? "是" : "否")} id='{_id}' 展开={_expanded} 行数={n}"
             + $"｜输入框聚焦={(_input != null && _input.Focused ? "是" : "否")} 菜单开={Menu.UiMenuWindow.IsOpen}"
             + $" 上次发送='{_lastSent}' 输入框文本='{(_input != null ? _input.Value : "-")}'"
             + $" 开{_openCount}/关{_closeCount} 最近关：{_lastClose}";
    }
}

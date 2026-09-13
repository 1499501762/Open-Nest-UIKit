using System;
using UnityEngine;
using UnityEngine.UI;
#if MELONLOADER
using TMPro = Il2CppTMPro;
#else
using TMPro;
#endif

namespace OpenNestUIKit.Widgets;

/// <summary>
/// 子菜单入口行（`标签 + 说明 + ›`）：整行可点 → 由调用方 Push 到目标页面。
/// 这是"多级菜单"在界面上的表现（表现与"跳到另一个菜单"一致，不需要重建窗口）。
/// </summary>
public sealed class UiNavRow : UiWidget
{
    private Image _bg;
    private UiText _label, _hint;

    private UiNavRow(RectTransform rt, Image bg, UiText label, UiText hint) : base(rt)
    {
        _bg = bg; _label = label; _hint = hint;
    }

    /// <summary>创建子菜单入口行。<paramref name="zoneName"/> 可覆盖热区名（默认 <c>nav:标签</c>），自动化脚本用它做语言无关寻址。</summary>
    public static UiNavRow Create(Transform parent, string label, string hint, Action onClick, float height = 0f, string zoneName = null)
    {
        var rt = NewRect("nav", parent);
        float h = height > 0f ? height : Theme.UiTheme.RowH + 6f;
        Theme.UiTheme.SetRect(rt, 0f, 0f, 300f, h);

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = Theme.UiTheme.RowBg;

        var txt = UiText.Create(rt, label, UiTextKind.Body, 0f, TextAlignmentOptions.Left);
        try
        {
            txt.Rect.anchorMin = new Vector2(0f, 0f);
            txt.Rect.anchorMax = new Vector2(1f, 1f);
            txt.Rect.pivot = new Vector2(0f, 0.5f);
            txt.Rect.offsetMin = new Vector2(10f, 0f);
            txt.Rect.offsetMax = new Vector2(-24f, 0f);
        }
        catch { }
        txt.Color = Theme.UiTheme.TextPrimary;

        UiText hintTxt = null;
        if (!string.IsNullOrEmpty(hint))
        {
            hintTxt = UiText.Create(rt, hint, UiTextKind.Note, 0f, TextAlignmentOptions.MidlineRight);
            try
            {
                hintTxt.Rect.anchorMin = new Vector2(1f, 0f);
                hintTxt.Rect.anchorMax = new Vector2(1f, 1f);
                hintTxt.Rect.pivot = new Vector2(1f, 0.5f);
                hintTxt.Rect.offsetMin = new Vector2(-220f, 0f);
                hintTxt.Rect.offsetMax = new Vector2(-24f, 0f);
                hintTxt.Rect.sizeDelta = new Vector2(196f, 0f);
            }
            catch { }
        }

        var arrow = UiText.Create(rt, ">", UiTextKind.Title, 0f, TextAlignmentOptions.Center);
        try
        {
            arrow.Rect.anchorMin = new Vector2(1f, 0f);
            arrow.Rect.anchorMax = new Vector2(1f, 1f);
            arrow.Rect.pivot = new Vector2(1f, 0.5f);
            arrow.Rect.offsetMin = new Vector2(-22f, 0f);
            arrow.Rect.offsetMax = new Vector2(-6f, 0f);
            arrow.Rect.sizeDelta = new Vector2(16f, 0f);
        }
        catch { }
        arrow.Color = Theme.UiTheme.Accent;

        var row = new UiNavRow(rt, bg, txt, hintTxt);
        Native.UiPointerRouter.Add(new Native.UiHotZone
        {
            Name = !string.IsNullOrEmpty(zoneName) ? zoneName : "nav:" + label,
            Rect = rt,
            Owner = row,
            Bg = bg,
            Tint = true,
            BaseColor = Theme.UiTheme.RowBg,
            HoverColor = Theme.UiTheme.RowHover,
            PressColor = Theme.UiTheme.RowSelected,
            OnClick = () => { try { onClick?.Invoke(); } catch (Exception ex) { CoopLog.Warn("uikit.widget", () => "nav click failed: " + ex.Message); } },
        });

        Layout.UiMeasure.Register(rt, _ => h, _ => 300f);
        return row;
    }

    /// <summary>改文案。</summary>
    public void SetText(string label, string hint = null)
    {
        if (_label != null) _label.Value = label;
        if (_hint != null && hint != null) _hint.Value = hint;
    }

    public override void Destroy()
    {
        try { Layout.UiMeasure.Unregister(_rt); } catch { }
        base.Destroy();
    }
}

/// <summary>只读信息行（`标签 … 值`）。</summary>
public sealed class UiInfoRow : UiWidget
{
    private UiText _label, _value;

    private UiInfoRow(RectTransform rt, UiText label, UiText value) : base(rt) { _label = label; _value = value; }

    /// <summary>创建信息行。</summary>
    public static UiInfoRow Create(Transform parent, string label, string value, float height = 0f)
    {
        var rt = NewRect("info", parent);
        float h = height > 0f ? height : Theme.UiTheme.RowH;
        Theme.UiTheme.SetRect(rt, 0f, 0f, 300f, h);

        var l = UiText.Create(rt, label, UiTextKind.Body, 0f, TextAlignmentOptions.Left);
        try
        {
            l.Rect.anchorMin = new Vector2(0f, 0f);
            l.Rect.anchorMax = new Vector2(0.5f, 1f);
            l.Rect.pivot = new Vector2(0f, 0.5f);
            l.Rect.offsetMin = new Vector2(0f, 0f);
            l.Rect.offsetMax = new Vector2(0f, 0f);
        }
        catch { }

        var v = UiText.Create(rt, value, UiTextKind.Value, 0f, TextAlignmentOptions.MidlineRight);
        try
        {
            v.Rect.anchorMin = new Vector2(0.5f, 0f);
            v.Rect.anchorMax = new Vector2(1f, 1f);
            v.Rect.pivot = new Vector2(1f, 0.5f);
            v.Rect.offsetMin = new Vector2(0f, 0f);
            v.Rect.offsetMax = new Vector2(0f, 0f);
        }
        catch { }

        Layout.UiMeasure.Register(rt, _ => h, _ => 300f);
        return new UiInfoRow(rt, l, v);
    }

    /// <summary>改值。</summary>
    public void SetValue(string s) { if (_value != null) _value.Value = s; }

    public override void Destroy()
    {
        try { Layout.UiMeasure.Unregister(_rt); } catch { }
        base.Destroy();
    }
}

/// <summary>动作行（`标签 + [按钮]`）。</summary>
public sealed class UiActionRow : UiWidget
{
    private UiButton _button;
    private UiText _label;

    private UiActionRow(RectTransform rt, UiText label, UiButton button) : base(rt) { _label = label; _button = button; }

    /// <summary>
    /// 创建动作行。
    ///
    /// ⚠ 2026-09-13：**整行可点**（不只是右侧按钮）—— 原模组的列表/详情栏都是“点行即选中/执行”
    /// （用户反馈“交互还是差点”）。做法：先登记一个整行热区（*后登记的热区命中优先* →
    /// 按钮自己的热区后建，所以按钮区域仍然归按钮，其余区域归整行）。
    /// </summary>
    public static UiActionRow Create(Transform parent, string label, string buttonText, Action onClick, float height = 0f)
    {
        var rt = NewRect("action", parent);
        float h = height > 0f ? height : Theme.UiTheme.RowH + 4f;
        Theme.UiTheme.SetRect(rt, 0f, 0f, 300f, h);

        // 整行背景（悬停/按下反馈）+ 整行热区（先登记，优先级低于右侧按钮）
        var rowBg = rt.gameObject.AddComponent<Image>();
        rowBg.color = Theme.UiTheme.RowBg;
        rowBg.raycastTarget = false;
        Native.UiPointerRouter.Add(new Native.UiHotZone
        {
            Name = "row:" + StripTags(label),
            Rect = rt,
            Bg = rowBg,
            Tint = true,
            BaseColor = Theme.UiTheme.RowBg,
            HoverColor = Theme.UiTheme.RowHover,
            PressColor = Theme.UiTheme.RowSelected,
            OnClick = () => { try { onClick?.Invoke(); } catch (Exception ex) { CoopLog.Warn("uikit.widget", () => "row click failed: " + ex.Message); } },
        });

        var l = UiText.Create(rt, label, UiTextKind.Body, 0f, TextAlignmentOptions.Left);
        try
        {
            l.Rect.anchorMin = new Vector2(0f, 0f);
            l.Rect.anchorMax = new Vector2(1f, 1f);
            l.Rect.pivot = new Vector2(0f, 0.5f);
            l.Rect.offsetMin = new Vector2(0f, 0f);
            l.Rect.offsetMax = new Vector2(-130f, 0f);
            l.SetSingleLine(true);      // 长模组名不换行（行高固定，折行会溢出到下一行）
        }
        catch { }

        var btn = UiButton.Create(rt, buttonText, onClick, UiButtonStyle.Secondary, 120f, Theme.UiTheme.ButtonH);
        try
        {
            btn.Rect.anchorMin = btn.Rect.anchorMax = new Vector2(1f, 0.5f);
            btn.Rect.pivot = new Vector2(1f, 0.5f);
            btn.Rect.anchoredPosition = Vector2.zero;
        }
        catch { }

        Layout.UiMeasure.Register(rt, _ => h, _ => 300f);
        return new UiActionRow(rt, l, btn);
    }

    public override void Destroy()
    {
        try { Layout.UiMeasure.Unregister(_rt); } catch { }
        base.Destroy();
    }

    /// <summary>去掉 TMP 富文本标签（热区名用：自动化脚本要的是纯文本，不能带 `&lt;color=…&gt;`）。</summary>
    private static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s)) return "-";
        var sb = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}

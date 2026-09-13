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
/// 窗口（标题栏 + 内容区 + 关闭按钮 + 淡入/缩放过渡）。菜单窗口（<see cref="Menu.UiMenuWindow"/>）用它做骨架。
/// </summary>
public sealed class UiWindow : UiWidget
{
    /// <summary>标题栏容器。</summary>
    public RectTransform Header { get; private set; }

    /// <summary>内容区容器。</summary>
    public RectTransform Content { get; private set; }

    /// <summary>内容流。</summary>
    public Layout.UiFlow Flow { get; private set; }

    /// <summary>标题文字。</summary>
    public UiText Title { get; private set; }

    /// <summary>关闭按钮（可空）。</summary>
    public UiButton CloseButton { get; private set; }

    /// <summary>用于淡入淡出的 CanvasGroup。</summary>
    public CanvasGroup Group { get; private set; }

    /// <summary>底板 Image（改色用）。</summary>
    public Image Fill { get; private set; }

    private UiWindow(RectTransform rt, RectTransform header, RectTransform content, Layout.UiFlow flow,
        UiText title, UiButton close, CanvasGroup group, Image fill) : base(rt)
    {
        Header = header; Content = content; Flow = flow; Title = title; CloseButton = close; Group = group; Fill = fill;
    }

    /// <summary>创建窗口（默认居中，尺寸走 <see cref="Theme.UiTheme"/>）。</summary>
    public static UiWindow Create(Transform parent, string title, Action onClose,
        float width = 0f, float height = 0f)
    {
        float w = width > 0f ? width : Theme.UiTheme.WindowW;
        float h = height > 0f ? height : Theme.UiTheme.WindowH;

        var rt = NewRect("window", parent);
        try
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }
        catch { }

        var fill = UiSurface.Build(rt, Theme.UiTheme.WindowBg, UiSurface.PanelSprite);

        var group = rt.gameObject.AddComponent<CanvasGroup>();
        try
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        catch { }

        // 标题栏
        var header = NewRect("header", rt);
        try
        {
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -Theme.UiTheme.HeaderH);
            header.offsetMax = Vector2.zero;
            var sd = header.sizeDelta; sd.x = 0f; sd.y = Theme.UiTheme.HeaderH; header.sizeDelta = sd;
            header.anchoredPosition = Vector2.zero;
        }
        catch { }
        var headerBg = header.gameObject.AddComponent<Image>();
        headerBg.color = Theme.UiTheme.HeaderBg;
        headerBg.raycastTarget = false;

        var titleTxt = UiText.Create(header, title, UiTextKind.Title, 0f, TextAlignmentOptions.Left);
        try
        {
            titleTxt.Rect.anchorMin = new Vector2(0f, 0f);
            titleTxt.Rect.anchorMax = new Vector2(1f, 1f);
            titleTxt.Rect.pivot = new Vector2(0f, 0.5f);
            titleTxt.Rect.offsetMin = new Vector2(Theme.UiTheme.Pad, 0f);
            titleTxt.Rect.offsetMax = new Vector2(-56f, 0f);
        }
        catch { }

        UiButton close = null;
        if (onClose != null)
        {
            // ⚠ 符号别用字库外的字形（`✕` U+2715 实测在游戏字体里会渲染成方块）→ 用 ASCII `X`
            close = UiButton.Create(header, "X", onClose, UiButtonStyle.Secondary, 34f, 30f, zoneName: "btn:close");
            try
            {
                close.Rect.anchorMin = close.Rect.anchorMax = new Vector2(1f, 0.5f);
                close.Rect.pivot = new Vector2(1f, 0.5f);
                close.Rect.anchoredPosition = new Vector2(-12f, 0f);
            }
            catch { }
        }

        // 内容区
        var content = NewRect("content", rt);
        try
        {
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, -Theme.UiTheme.HeaderH);
        }
        catch { }

        var flow = new Layout.UiFlow(content)
        {
            Axis = Layout.UiAxis.Vertical,
            Padding = new Layout.UiPadding(),
            Gap = Theme.UiTheme.GapSm,
            CrossStretch = true,
            AutoHeight = false,
        };

        return new UiWindow(rt, header, content, flow, titleTxt, close, group, fill);
    }

    /// <summary>改标题。</summary>
    public void SetTitle(string s) { if (Title != null) Title.Value = s; }

    /// <summary>打开过渡（淡入 + 轻微放大）。</summary>
    public void PlayOpen()
    {
        try
        {
            if (Group != null) Theme.UiKitTween.Fade(Group, 0f, 1f, Theme.UiTheme.OpenDur);
            if (_rt != null) Theme.UiKitTween.Scale(_rt, 0.97f, 1f, Theme.UiTheme.OpenDur);
        }
        catch { }
    }

    /// <summary>关闭过渡（淡出）。</summary>
    public void PlayClose(Action done)
    {
        try
        {
            if (Group != null) Theme.UiKitTween.Fade(Group, Group.alpha, 0f, Theme.UiTheme.CloseDur, done);
            else done?.Invoke();
        }
        catch { done?.Invoke(); }
    }
}

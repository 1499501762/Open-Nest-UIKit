using UnityEngine;
using UnityEngine.UI;
#if MELONLOADER
using TMPro = Il2CppTMPro;
#else
using TMPro;
#endif

namespace OpenNestUIKit.Theme;

/// <summary>
/// 视觉标准（调色板 / 尺寸 / 字号 / 过渡时长）：集中一处，界面代码只谈布局与状态。
///
/// 配色取向 = **深色工业面板 + 琥珀强调色**（与游戏仪表盘指针一致，也和 OpenNestModMenu 的取向一致，
/// 但本库有自己的一份常量，不共享静态状态）。
/// </summary>
public static class UiTheme
{
    // ---------------- 调色板 ----------------

    public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.66f);
    public static readonly Color WindowBg = new Color(0.055f, 0.070f, 0.094f, 0.985f);
    public static readonly Color HeaderBg = new Color(0.086f, 0.110f, 0.145f, 1f);
    public static readonly Color FooterBg = new Color(0.047f, 0.063f, 0.082f, 1f);
    public static readonly Color ContentBg = new Color(0.071f, 0.086f, 0.110f, 1f);
    public static readonly Color CardBg = new Color(0.098f, 0.118f, 0.149f, 1f);
    public static readonly Color Border = new Color(0.165f, 0.196f, 0.239f, 1f);

    public static readonly Color Accent = new Color(0.878f, 0.635f, 0.235f, 1f);
    public static readonly Color AccentDim = new Color(0.560f, 0.400f, 0.150f, 1f);

    public static readonly Color RowBg = new Color(0.086f, 0.106f, 0.133f, 1f);
    public static readonly Color RowAlt = new Color(0.078f, 0.096f, 0.122f, 1f);
    public static readonly Color RowHover = new Color(0.145f, 0.180f, 0.227f, 1f);
    public static readonly Color RowSelected = new Color(0.130f, 0.196f, 0.286f, 1f);

    public static readonly Color ButtonBg = new Color(0.118f, 0.145f, 0.184f, 1f);
    public static readonly Color ButtonPrimary = new Color(0.361f, 0.290f, 0.145f, 1f);
    public static readonly Color ButtonDanger = new Color(0.310f, 0.145f, 0.145f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.098f, 0.110f, 0.129f, 1f);

    public static readonly Color TrackBg = new Color(0.055f, 0.067f, 0.086f, 1f);
    public static readonly Color InputBg = new Color(0.043f, 0.055f, 0.071f, 1f);

    public static readonly Color TextPrimary = new Color(0.902f, 0.918f, 0.941f, 1f);
    public static readonly Color TextSecondary = new Color(0.624f, 0.667f, 0.722f, 1f);
    public static readonly Color TextDim = new Color(0.427f, 0.470f, 0.529f, 1f);
    public static readonly Color Ok = new Color(0.498f, 0.816f, 0.541f, 1f);
    public static readonly Color Warn = new Color(0.878f, 0.635f, 0.235f, 1f);
    public static readonly Color Err = new Color(0.878f, 0.424f, 0.424f, 1f);

    // ---------------- 尺寸（参考分辨率 1920×1080） ----------------
    // ⚠ 2026-09-13：这些改成 **可变字段**，因为两个模组的页面需要“原模组那种紧凑密度”
    //   （Coop 原来是 500 宽的紧凑单栏：行高 20~30、间隙 4~8，而我们默认是 36/6）。
    //   由 `PushDensity(compact)` / `PopDensity()` 成对包住一次 `page.Build()`（见 UiMenuWindow.ShowCurrentPage）。
    //   行高都是**构建时就量下**的，所以构建期间改值即生效。

    public const float ReferenceW = 1920f, ReferenceH = 1080f;
    public static float WindowW = 1180f;
    public static float WindowH = 720f;
    public static float HeaderH = 54f;
    public static float FooterH = 36f;
    public static float Pad = 18f;
    public static float Gap = 12f;
    public static float GapSm = 8f;
    public static float ContentH => WindowH - HeaderH - FooterH;
    public const float ScrollBarW = 8f;

    public static float RowH = 36f;
    public static float RowGap = 6f;
    public static float ControlH = 28f;
    public static float ButtonH = 32f;
    public static float ToggleW = 52f, ToggleH = 26f;
    public const float StepperBtnW = 28f;
    public static float SliderLabelW = 52f;
    public static float TabH = 34f;

    // ---------------- 字号 ----------------

    public static int FontTitle = 22;
    public static int FontSubtitle = 13;
    public static int FontBody = 15;
    public static int FontSmall = 12;

    /// <summary>
    /// 9-slice 装饰层的 <c>Image.pixelsPerUnitMultiplier</c>（**照抄游戏原生用法**）。
    /// 实机实测（`uiscale` 命令）：游戏自己用 `UI Box Castile` 做按钮/面板底的 Image
    /// （尺寸 250x38 / 178x40 / 186x64）**全都是 `pixelsPerUnitMultiplier = 2.5`**，
    /// 即素材边框按 `1/2.5` 渲染 —— `Castile` 的上边框 34px → 实际只有 13.6px，才能塞进 38px 高的按钮。
    /// 我们之前把它硬写成 1 → 边框按 1:1 渲染 → **比原生粗 2.5 倍**（“背景倍率太大”的根因）。
    /// 注：它是 `Image` 的字段，赋值后需要素材重新切片；本模组在给 `Image.sprite` 之后立即设它。
    /// </summary>
    public const float SpritePpuMul = 2.5f;
    public static int FontRow = 15, FontValue = 14, FontButton = 14, FontHeader = 13;

    // ---------------- 密度（紧凑 / 默认） ----------------

    private struct Metrics
    {
        public float RowH, RowGap, Pad, Gap, GapSm, ControlH, ButtonH, TabH, SliderLabelW;
        public int FontTitle, FontSubtitle, FontBody, FontSmall, FontRow, FontValue, FontButton, FontHeader;
    }

    private static Metrics _saved;
    private static bool _pushed;

    /// <summary>
    /// 切到“紧凑密度”（原模组窄面板那种观感：行高 26、内边距 12、间隙 6、字号小 1~2 档）。
    /// 必须与 <see cref="PopDensity"/> 成对使用（把页面构建包在中间）。
    /// </summary>
    public static void PushDensity(bool compact)
    {
        _saved = new Metrics
        {
            RowH = RowH, RowGap = RowGap, Pad = Pad, Gap = Gap, GapSm = GapSm,
            ControlH = ControlH, ButtonH = ButtonH, TabH = TabH, SliderLabelW = SliderLabelW,
            FontTitle = FontTitle, FontSubtitle = FontSubtitle, FontBody = FontBody, FontSmall = FontSmall,
            FontRow = FontRow, FontValue = FontValue, FontButton = FontButton, FontHeader = FontHeader,
        };
        _pushed = true;

        if (compact)
        {
            RowH = 26f; RowGap = 6f; Pad = 12f; Gap = 8f; GapSm = 6f;
            ControlH = 24f; ButtonH = 26f; TabH = 30f; SliderLabelW = 110f;
            FontTitle = 18; FontSubtitle = 12; FontBody = 14; FontSmall = 12;
            FontRow = 14; FontValue = 13; FontButton = 13; FontHeader = 12;
        }
    }

    /// <summary>恢复 <see cref="PushDensity"/> 之前的密度。</summary>
    public static void PopDensity()
    {
        if (!_pushed) return;
        _pushed = false;
        RowH = _saved.RowH; RowGap = _saved.RowGap; Pad = _saved.Pad; Gap = _saved.Gap; GapSm = _saved.GapSm;
        ControlH = _saved.ControlH; ButtonH = _saved.ButtonH; TabH = _saved.TabH; SliderLabelW = _saved.SliderLabelW;
        FontTitle = _saved.FontTitle; FontSubtitle = _saved.FontSubtitle; FontBody = _saved.FontBody; FontSmall = _saved.FontSmall;
        FontRow = _saved.FontRow; FontValue = _saved.FontValue; FontButton = _saved.FontButton; FontHeader = _saved.FontHeader;
    }

    // ---------------- 过渡 ----------------

    /// <summary>打开/关闭过渡时长（秒）。</summary>
    public const float OpenDur = 0.12f, CloseDur = 0.08f;
    /// <summary>关闭过渡后延迟销毁/隐藏的宽限（避免关一半又被打开时闪烁）。</summary>
    public const float CloseGrace = 0.02f;

    // ---------------- 工具 ----------------

    /// <summary>颜色 → TMP 富文本用 #RRGGBB（不用 ColorUtility，避免两端 interop 差异）。</summary>
    public static string Hex(Color c)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    /// <summary>悬停色（比底色亮一档；自管指针命中时手动套用）。</summary>
    public static Color Hover(Color c)
        => new Color(Mathf.Min(c.r * 1.32f + 0.04f, 1f), Mathf.Min(c.g * 1.32f + 0.04f, 1f), Mathf.Min(c.b * 1.32f + 0.04f, 1f), c.a);

    /// <summary>按下色（比底色暗一档）。</summary>
    public static Color Pressed(Color c)
        => new Color(c.r * 0.74f, c.g * 0.74f, c.b * 0.74f, c.a);

    /// <summary>带透明度的同色（做描边/分隔线用）。</summary>
    public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    /// <summary>按钮观感：悬停提亮、按下压暗（UGUI ColorTint 的倍乘色）。</summary>
    public static void StyleButton(Button b, Color bg)
    {
        if (b == null) return;
        try
        {
            var img = b.targetGraphic as Image;
            if (img != null) img.color = bg;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.20f, 1.20f, 1.20f, 1f);
            cb.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            cb.selectedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
        }
        catch { }
    }

    /// <summary>按"左上角锚点 + 负 y"摆位（配合 <see cref="UiKit.Place"/> 建的控件做动态纵向排版）。</summary>
    public static void SetTopLeft(RectTransform rt, float x, float y)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    /// <summary>左上角对齐 + 指定尺寸（动态布局排列时大量使用）。</summary>
    public static void SetRect(RectTransform rt, float x, float y, float w, float h)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>给 Rectangle 加 1px 描边（顶部/底部/左右各一条细 Image，四角不拉伸）。</summary>
    public static RectTransform MakeBorder(Transform parent, float x, float y, float w, float h, Color color)
    {
        var rt = UI.UiKit.MakeRect("border", parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, Vector2.zero, new Vector2(0f, 1f));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        SetRect(rt, x, y, w, h);
        return rt;
    }
}

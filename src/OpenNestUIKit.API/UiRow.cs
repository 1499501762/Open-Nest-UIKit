using System.Collections.Generic;

namespace OpenNestUIKit.API;

/// <summary>页面里的行种类（宿主按此渲染成真实控件）。</summary>
public enum UiRowKind
{
    /// <summary>分组标题（不可交互）。</summary>
    Header = 0,
    /// <summary>普通说明文字。</summary>
    Label = 1,
    /// <summary>分隔线。</summary>
    Separator = 2,
    /// <summary>动作按钮（点一下就执行）。</summary>
    Button = 3,
    /// <summary>子菜单入口（点击 Push 到另一个页面）。</summary>
    Nav = 4,
    /// <summary>布尔开关。</summary>
    Toggle = 5,
    /// <summary>数值（滑条 / 步进）。</summary>
    Slider = 6,
    /// <summary>枚举（循环选择 / 下拉）。</summary>
    Choice = 7,
    /// <summary>文本输入。</summary>
    Text = 8,
    /// <summary>快捷键绑定。</summary>
    KeyBind = 9,
    /// <summary>只读进度条（0..1）。</summary>
    Progress = 10,
    /// <summary>页签栏（一行多列；<see cref="UiRow.Choices"/> = 页签名，<see cref="UiRow.Value"/> = 当前索引）。
    /// 宿主渲染成可点的页签，点哪列就把索引写回 <see cref="UiRow.Write"/>。</summary>
    Tabs = 11,

    /// <summary>
    /// **两栏容器**（左边固定宽 + 右边占剩余）：<see cref="UiRow.LeftRows"/> / <see cref="UiRow.RightRows"/> 各是一组子行，
    /// 子行种类与本枚举完全相同（可嵌套，但不要再套 Columns）。
    ///
    /// 用途：把原模组的“左边列表 + 右边详情”布局搬回来（ModMenu 的模组列表 / 详情栏）。
    /// </summary>
    Columns = 12,

    /// <summary>
    /// **内嵌可滚动列表**（固定高度，自带滚动条）：<see cref="UiRow.ListRows"/> 是列表里的行（通常是 Button），
    /// <see cref="UiRow.ListHeight"/> 是列表高度。
    ///
    /// 用途：模组列表 / 房间列表这类“行数不确定、应该自己滞动而不是把整页撑长”的区域。
    /// </summary>
    List = 13,
}

/// <summary>
/// 声明式行模型：第三方用「动词方法」描述界面（见 <see cref="UiPageDef"/>），
/// 宿主把它渲染成真实控件（<c>Widgets/UiRow.cs</c>）。**不含任何 Unity 类型**，双端通用。
///
/// 写回约定（与 <c>OpenNestModMenu.API.IModMenuPage</c> 一致）：<c>Write</c> 直接回调给第三方，
/// 由第三方自己决定往哪写（配置文件/内存），宿主只负责把用户操作转成 <c>Write</c> 调用。
/// </summary>
public sealed class UiRow
{
    public UiRowKind Kind { get; set; }
    /// <summary>稳定键（第三方自己用来定位设置项；可空）。</summary>
    public string Key { get; set; }
    /// <summary>显示标签。</summary>
    public string Label { get; set; }
    /// <summary>当前值（字符串形式；Toggle = "true"/"false"）。</summary>
    public string Value { get; set; }
    /// <summary>副说明（灰色小字，可空）。</summary>
    public string Hint { get; set; }
    /// <summary>只读（界面不可改）。</summary>
    public bool ReadOnly { get; set; }
    /// <summary>子菜单目标页 id（仅 <see cref="UiRowKind.Nav"/>）。</summary>
    public string PageId { get; set; }
    /// <summary>选项列表（仅 <see cref="UiRowKind.Choice"/>）。</summary>
    public IReadOnlyList<string> Choices { get; set; }
    /// <summary>数值范围（仅 <see cref="UiRowKind.Slider"/> 与 <see cref="UiRowKind.Progress"/>）。</summary>
    public double Min { get; set; }
    public double Max { get; set; } = 1;
    public double Step { get; set; } = 1;
    /// <summary>动作按钮回调（仅 <see cref="UiRowKind.Button"/>）。</summary>
    public System.Action OnClick { get; set; }
    /// <summary>写回回调（Toggle/Slider/Choice/Text/KeyBind）。返回是否接受。</summary>
    public System.Func<string, bool> Write { get; set; }

    // ---------------- 两栏容器（仅 <see cref="UiRowKind.Columns"/>） ----------------

    /// <summary>左栏宽度（像素；<see cref="UiRowKind.Columns"/> 用）。</summary>
    public float LeftWidth { get; set; } = 440f;

    /// <summary>两栏之间的水平间隙（像素）。</summary>
    public float ColumnGap { get; set; } = 12f;

    /// <summary>左栏子行。</summary>
    public IReadOnlyList<UiRow> LeftRows { get; set; }

    /// <summary>右栏子行。</summary>
    public IReadOnlyList<UiRow> RightRows { get; set; }

    // ---------------- 内嵌列表（仅 <see cref="UiRowKind.List"/>） ----------------

    /// <summary>列表高度（像素；0 = 取一个默认值）。</summary>
    public float ListHeight { get; set; }

    /// <summary>列表里的行。</summary>
    public IReadOnlyList<UiRow> ListRows { get; set; }
}

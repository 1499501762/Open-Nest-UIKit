using System;
using OpenNestUIKit.API;

namespace OpenNestUIKit.Pages;

/// <summary>
/// **第三方模组示例**：演示"别的模组怎么把自己的菜单页挂进原生 UI 库"。
///
/// 关键点（也是给第三方看的模板）：
/// - 只引用 `OpenNestUIKit.API`（**纯 .NET，无 Unity 依赖**）→ 同一份代码双端都能编译；
/// - 未安装本库时 <see cref="UiKitHost.Register"/> 只是进内存注册表（`IsHostAvailable == false`），
///   不影响模组自身加载（**软依赖**）；
/// - 界面用"动词方法"声明（Header/Label/Nav/Toggle/Slider/Choice/Text/KeyBind/Button/Progress），
///   渲染由本库负责；写回通过回调交回给第三方（第三方自己决定往哪写）。
/// </summary>
internal sealed class DemoProvider : UiKitProviderBase, IUiKitNativeEntry
{
    public override string Id => "demo.provider";
    public override string DisplayName => "示例模组";
    public override string Version => "1.0.0";
    public override string Author => "OpenNestUIKit";

    // 原生 ESC 列表本来就已经排满（实测原生 8 行占满 400 高面板），每多一行都要等比压缩节距。
    // 示例页只演示"怎么声明菜单"，没必要占原生格位 → 不注入原生入口（只在本库菜单里出现）。
    public bool ShowInNativeMenu => false;
    public int NativeOrder => 0;
    public string NativeTitle => DisplayName;
    public string NativeTitleEn => DisplayName;

    // 示例设置（真实模组里应读写自己的配置文件）
    private bool _enabled = true;
    private double _scale = 1.0;
    private int _mode = 1;
    private string _name = "";
    private string _key = "f8";

    public override void BuildMenu(IUiMenuTree menu)
    {
        // ---- 根页：子菜单入口 + 少量常用项 ----
        menu.Root.Header("示例模组");
        menu.Root.Label("这是一个用声明式契约注册的页面（不依赖 Unity）。");
        menu.Root.Nav("常规设置", "general", "开关 / 数值 / 枚举");
        menu.Root.Nav("快捷键", "keys", "按键绑定");
        menu.Root.Nav("只读信息", "info", "进度条与信息展示");
        menu.Root.Toggle("demo.enabled", "启用示例模组", _enabled, v =>
        {
            _enabled = v;
            Menu.UiMenuWindow.SetStatus("demo.enabled = " + v);
        });

        // ---- 子页 1：常规 ----
        var general = menu.Page("general", "常规设置");
        general.Header("数值");
        general.Slider("demo.scale", "缩放", _scale, 0.5, 2.0, 0.05, v =>
        {
            _scale = v;
            Menu.UiMenuWindow.SetStatus("demo.scale = " + v.ToString("0.00"));
        });
        general.Choice("demo.mode", "模式", new[] { "简单", "标准", "专家" }, _mode, i =>
        {
            _mode = i;
            Menu.UiMenuWindow.SetStatus("demo.mode = " + i);
        });
        general.Text("demo.name", "名称", _name, v =>
        {
            _name = v;
            Menu.UiMenuWindow.SetStatus("demo.name = '" + v + "'");
        });
        general.Separator();
        general.Header("动作");
        general.Button("立即执行一次", "执行", () => Menu.UiMenuWindow.SetStatus("demo: action executed"));
        general.Nav("查看只读信息", "info");

        // ---- 子页 2：快捷键 ----
        var keys = menu.Page("keys", "快捷键");
        keys.Label("点击右侧的键位框，然后按任意键完成绑定（ESC 取消）。");
        keys.KeyBind("demo.key", "打开面板", _key, v =>
        {
            _key = v;
            Menu.UiMenuWindow.SetStatus("demo.key = " + v);
        });
        keys.Separator();
        keys.Label("绑定结果由模组自己保存（本示例只存在内存里）。");

        // ---- 子页 3：只读信息 ----
        var info = menu.Page("info", "只读信息");
        info.Header("状态");
        info.Label("宿主：" + (UiKitHost.IsHostAvailable ? UiKitHost.HostVersion : "（未安装 OpenNestUIKit）"));
        info.Label("契约 API 版本：" + UiKitHost.ApiVersion);
        info.Progress("示例进度", 0.42);
        info.Separator();
        info.Nav("返回常规设置", "general");
    }
}

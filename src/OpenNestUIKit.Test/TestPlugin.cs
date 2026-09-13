using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace OpenNestUIKit.Test;

/// <summary>
/// BepInEx 入口壳（MelonLoader 版见 <c>TestMelonEntry.cs</c>，本文件在 ML 工程里被排除）。
/// </summary>
[BepInPlugin(TestInfo.Guid, TestInfo.Name, TestInfo.Version)]
public class TestPlugin : BasePlugin
{
    public override void Load()
    {
        TestRuntime.Initialize(m => Log.LogInfo(m));
        try { TestRuntime.Startup(); }
        catch (System.Exception ex) { Log.LogError("OpenNestUIKit.Test 初始化失败：" + ex); }
    }

    public override bool Unload()
    {
        try { TestRuntime.Shutdown(); } catch { }
        return true;
    }
}

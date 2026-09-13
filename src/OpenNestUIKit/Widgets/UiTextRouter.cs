using System;
using UnityEngine;
#if MELONLOADER
using TMPro = Il2CppTMPro;
#else
using TMPro;
#endif

namespace OpenNestUIKit.Widgets;

/// <summary>
/// 文本输入总管线 —— **照搬 OpenNestCoop 自带输入框那套已经实机验证过的机制**
/// （`CoopInputBox` + `CoopUIManager.PollInput/ActivateImeAnchor/ReadImeCommitted`，见 `docs/UI_KIT.md` §8.2）。
///
/// 为什么不再走 `TMP_InputField` 收键：
///   - 任务场景里游戏 `EventSystem` 常未激活；就算激活，我们为了防指针穿透还要停用它的输入模块，
///     结果 **TMP_InputField 收不到键盘**（用户报“输入框没有实现”）。而“为了打字把模块放行”又会
///     让点击有穿透风险 —— 原模组早就绕开了这条路。
///   - 原模组实测结论：`TMP_InputField` 在 IL2CPP 下 **`.text` 恒空、OnTextInput 把 CJK 破坏成 U+FFFD**，
///     真正能拿到中文的通道是 **OS 输入法本身**（`ImmGetCompositionStringW`）。
///
/// 所以本管线 = 四通道合一：
///   ① **物理键轮询**（`Keyboard.current`，字母/数字/常用符号 + Shift）：英文/数字/符号，可靠、无 native 风险；
///   ② **隐藏 IME 锚点**（一个屏幕内、全透明、不可点的 `TMP_InputField`）：只为**唤起系统输入法**
///      （配合 `Keyboard.SetIMEEnabled(true)` + `SetIMECursorPosition`）；
///   ③ **原生 Win32 IME 读取**：`GCS_RESULTSTR` = 用户刚提交（确认）的中文，`GCS_COMPSTR` = 正在组合的拼音
///      —— 后者用于**组合中屏蔽物理键**（否则拼音字母/空格选字/数字选字会漏进输入框）；
///   ④ **Harmony patch `Keyboard.OnTextInput`**（`PostTextInput`）：转发 CJK 与 `\b`/`\r`，
///      作为 ③ 的兜底通道（原模组同款）。
///
/// 界面侧只做两件事：<see cref="Focused"/> 是谁、以及把 <see cref="UiTextInput"/> 的文本渲染出来。
/// </summary>
public static class UiTextRouter
{
    // ---------------- 状态 ----------------

    /// <summary>当前聚焦的输入框（null = 没有任何输入框在打字）。</summary>
    public static UiTextInput Focused { get; private set; }

    /// <summary>是否正在打字（有输入框聚焦）。</summary>
    public static bool Typing => Focused != null;

    private static UnityEngine.InputSystem.Keyboard _kb;
    private static float _backspaceAt = -1f, _lastRepeatAt;
    private static float _focusAt;
    private static string _lastNative = "";      // 最近一次原生 IME 提交（去重用）
    private static string _compositionLast = "";  // 上次组合文本（组合确认时追加；补 OnTextInput 收不到确认字符）
    private static bool _composing;
    private static int _diag;

    // ---- 首字母缓冲 / 快模式（照搬原模组 CoopUIManager.ProcessKeyChar/ProcessPendingChar）----
    // 为什么需要：打拼音时**第一个字母所在的帧，系统 IME 还没开始组合**（GCS_COMPSTR 还是空），
    // 直接 Append 就会把 'n'（nihao 的第一个字母）先写进输入框 → 用户看到的“串拼音进去”。
    // 做法：先缓一帧（约 2 帧）看组合是否起来 —— 起来了就丢弃（是拼音首键）；没起来就补回去（是英文）。
    private static char _pendingChar;
    private static int _pendingFrames;
    private static bool _fastMode;

    /// <summary>本帧的 ESC 已被输入管线吃掉（结束打字）→ 窗口不要再把它当成“返回/关闭”。</summary>
    public static bool ConsumedEsc { get; private set; }

    /// <summary>本帧的回车已被输入管线吃掉（提交/发送）→ 悬浮聊天层不要再拿同一个回车去“唤入”。
    ///
    /// ⚠️ 2026-09-13（用户：“输入回车后没有效果”）：`UiTextRouter.Tick` 与 `UiChatOverlay.Tick`
    /// 在同一帧里都会看回车 —— 路由器先提交并**收起**聊天框，紧接着聊天层又把**同一次按键**当成
    /// “回车唤入”重新展开 → 看上去就像回车什么也没干（刚发的字还消失了）。用这个标记隔开。</summary>
    public static bool ConsumedEnter { get; private set; }

    /// <summary>单框最大长度（照搬原模组：40 字符）。</summary>
    public const int MaxLen = 40;

    // ---------------- 聚焦 / 失焦 ----------------

    /// <summary>切换聚焦到某个输入框（<paramref name="box"/> = null 表示收起输入态）。</summary>
    public static void SetFocused(UiTextInput box)
    {
        if (ReferenceEquals(Focused, box))
        {
            box?.Refresh();
            return;
        }
        var prev = Focused;
        Focused = box;
        try { prev?.OnRouterBlur(); } catch { }
        try { box?.OnRouterFocus(); } catch { }

        if (box != null)
        {
            _focusAt = Time.realtimeSinceStartup;
            _lastNative = "";
            _compositionLast = "";
            _backspaceAt = -1f;
            _pendingChar = (char)0;
            _pendingFrames = 0;
            _fastMode = false;
            // 临时放行拦截层（激活 EventSystem + 启用输入模块 + 压住外部射线器）：
            // 隐藏 IME 锚点必须真的“聚焦”才能收到输入法组合。
            try { Native.UiInputGuard.SetTextCapture(true); } catch { }
            ActivateImeAnchor();
        }
        else
        {
            DeactivateImeAnchor();
            try { Native.UiInputGuard.SetTextCapture(false); } catch { }
        }
    }

    /// <summary>失焦全部（菜单关闭 / 切页时调用）。</summary>
    public static void BlurAll()
    {
        if (Focused != null) SetFocused(null);
    }

    // ---------------- 每帧驱动 ----------------

    /// <summary>本帧的回车（三种通道任一）——悬浮聊天层“回车唤入”与输入框“回车提交”共用同一份判定。
    ///
    /// ⚠️ 2026-09-13（用户：“回车聊天框还是没生效 / 输入回车后没有效果”）：以前只看
    /// `kb.enterKey.wasPressedThisFrame` —— 单帧脉冲，只要那一帧没读到就整次丢掉；
    /// 而且当输入法锚点（TMP_InputField）或系统 IME 在用时，回车常常**只从文本通道
    ///（Keyboard.OnTextInput 的 `\r`）过来**，那条路以前是被我们**故意丢掉**的。
    /// 现在三通道取并：① 物理键**上升沿**（比 wasPressedThisFrame 稳）② wasPressedThisFrame ③ 文本通道 `\r`。</summary>
    public static bool EnterThisFrame { get; private set; }

    /// <summary>本帧的 ESC（物理键上升沿）。</summary>
    public static bool EscThisFrame { get; private set; }

    /// <summary>当前是否在输入法组合中（拼音候选窗开着）。</summary>
    public static bool Composing => _composing;

    private static bool _enterDown, _escDown;
    private static int _textEnterFrame = -1;      // 文本通道收到 \r 的帧号

    /// <summary>每帧读一次按键状态（**无论有没有聚焦都读** —— 悬浮聊天层要靠它“回车唤入”）。</summary>
    private static void ReadFrameKeys()
    {
        EnterThisFrame = false;
        EscThisFrame = false;
        try
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null) _kb = kb;
            kb = _kb;
            if (kb != null)
            {
                bool down = false, escDown = false;
                try { down = kb.enterKey.isPressed || kb.numpadEnterKey.isPressed; } catch { }
                try { escDown = kb.escapeKey.isPressed; } catch { }
                if (down && !_enterDown) EnterThisFrame = true;              // ① 上升沿
                if (escDown && !_escDown) EscThisFrame = true;
                _enterDown = down;
                _escDown = escDown;
                bool pressed = false;
                try { pressed = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame; } catch { }
                if (pressed) EnterThisFrame = true;                          // ② 单帧脉冲（兼容）
            }
            if (_textEnterFrame == Time.frameCount) EnterThisFrame = true;    // ③ 文本通道 \r（当帧有效）
        }
        catch { }
    }

    /// <summary>每帧（<c>UiKitBehaviour.Update</c> 调用）。</summary>
    public static void Tick(float dt)
    {
        ConsumedEsc = false;
        ConsumedEnter = false;
        ReadFrameKeys();
        try
        {
            var box = Focused;
            if (box == null)
            {
                // 没打字时：仍要盯住锚点（万一被游戏抢焦点 → 输入法会失效）
                Diag = "无聚焦";
                return;
            }
            if (!box.Alive) { Diag = "框已销毁"; SetFocused(null); return; }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null) _kb = kb;
            kb = _kb;
            if (kb == null) { Diag = "键盘不可用"; return; }

            // ① 组合中（拼音候选窗打开）：**跳过所有物理键** —— 字母/数字/空格/退格/回车都是输入法在用的
            //    （用原生 GCS_COMPSTR 判定，比 Unity 的 compositionString 更及时 → 首字母不会漏进框）。
            _composing = IsComposing();

            // 回车/ESC 本帧状态（由 ReadFrameKeys 统一给出：物理上升沿 / 单帧脉冲 / 文本通道 \r）
            bool enter = EnterThisFrame, esc = EscThisFrame;
            if (enter)
            {
                // 诊断：这一次回车到底走到哪一步（“回车没反应”就靠它定位）
                float since = Time.realtimeSinceStartup - _focusAt;
                LastEnter = _composing ? "组合中→跳过"
                    : (since <= 0.2f ? $"刚聚焦 0.2s 内→忽略({since:0.###}s)"
                    : "走提交");
                _lastEnterFrame = Time.frameCount;
            }

            if (_composing) { Diag = "组合中（跳过物理键，含回车）"; _pendingChar = (char)0; _pendingFrames = 0; _fastMode = false; PollNativeIme(box); return; }

            // ② 处理上一帧缓冲的首字母（组合未起来 → 确认是英文，补进去）
            ProcessPendingChar(box);

            // ② 退格（长按自动重复）
            HandleBackspaceRepeat(kb, box);

            // ③ 回车提交 / ESC 失焦
            if (enter && Time.realtimeSinceStartup - _focusAt > 0.2f)
            {
                LastEnter = "已提交";
                Diag = "回车提交";
                ConsumedEnter = true;
                Submit(box);
                return;
            }
            if (esc) { Diag = "ESC 失焦"; ConsumedEsc = true; SetFocused(null); return; }

            // ④ 原生 IME 提交（真汉字）—— 主通道
            if (PollNativeIme(box)) { Diag = "原生IME提交"; return; }

            // ⑤ 物理键（英文/数字/符号）——先缓冲再看（避免拼音首字母漏进输入框）
            bool shift = false;
            try { shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed; } catch { }
            if (TryReadChar(kb, shift, out char ch)) { Diag = "物理键字符"; ProcessKeyChar(box, ch); }
            else Diag = "空闲";
        }
        catch (Exception ex) { Diag = "异常: " + ex.Message; }
    }

    /// <summary>诊断：上一次 Tick 停在哪一步（排障“回车没反应 / 打字没反应”时一眼看出是不是被“组合中”挡了）。</summary>
    public static string Diag { get; private set; } = "-";

    /// <summary>诊断：最近一次“回车按下”时本管线的决策（组合中跳过 / 刚聚焦忽略 / 走提交 / 已提交）。</summary>
    public static string LastEnter { get; private set; } = "-";
    private static int _lastEnterFrame = -1;

    /// <summary>按键字符路由：快模式下直接追加；否则先缓冲一个字符（拼音首键不闪烁）。</summary>
    private static void ProcessKeyChar(UiTextInput box, char c)
    {
        if (_fastMode) { Append(box, c); return; }
        if (_pendingChar != 0) Append(box, _pendingChar);   // 缓冲中又来新键 → 组合没起来，上一个是英文
        _pendingChar = c;
        _pendingFrames = 0;
    }

    /// <summary>处理待定首字符：组合起来 → 丢弃（拼音首键）；否则缓冲 2 帧后追加并进入快模式。</summary>
    private static void ProcessPendingChar(UiTextInput box)
    {
        if (_pendingChar == 0) return;
        if (_pendingFrames++ < 2) return;
        char c = _pendingChar;
        _pendingChar = (char)0;
        _fastMode = true;                                    // 后续英文不再延迟
        Append(box, c);
    }

    /// <summary>取一帧里按下的第一个可打印字符（新输入系统 `Key` 枚举顺序：A=15..Z=40、Digit1=41..Digit0=50）。</summary>
    private static bool TryReadChar(UnityEngine.InputSystem.Keyboard kb, bool shift, out char ch)
    {
        ch = '\0';
        try
        {
            var keys = kb.allKeys;
            if (keys == null) return false;
            for (int i = 0; i < keys.Count; i++)
            {
                var kc = keys[i];
                if (kc == null) continue;
                bool pressed;
                try { pressed = kc.wasPressedThisFrame; } catch { continue; }
                if (!pressed) continue;
                int k;
                try { k = (int)kc.keyCode; } catch { continue; }

                if (k >= 15 && k <= 40) { char c = (char)('a' + (k - 15)); ch = shift ? char.ToUpperInvariant(c) : c; return true; }
                if (k >= 41 && k <= 49) { ch = (char)('1' + (k - 41)); return true; }
                if (k == 50) { ch = '0'; return true; }
                switch (k)
                {
                    case 1: ch = ' '; return true;
                    case 7: ch = shift ? '<' : ','; return true;
                    case 8: ch = shift ? '>' : '.'; return true;
                    case 9: ch = shift ? '?' : '/'; return true;
                    case 13: ch = shift ? '_' : '-'; return true;
                    case 14: ch = shift ? '+' : '='; return true;
                    case 5: ch = shift ? '"' : '\''; return true;
                    case 6: ch = shift ? ':' : ';'; return true;
                    case 11: ch = shift ? '{' : '['; return true;
                    case 12: ch = shift ? '}' : ']'; return true;
                    case 10: ch = shift ? '|' : '\\'; return true;
                    case 4: ch = shift ? '~' : '`'; return true;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>退格长按自动重复（首次立即删一次，按住 0.45s 后每 0.06s 一次）。</summary>
    private static void HandleBackspaceRepeat(UnityEngine.InputSystem.Keyboard kb, UiTextInput box)
    {
        bool bs = false;
        try { bs = kb.backspaceKey.isPressed; } catch { }
        float now = Time.realtimeSinceStartup;
        if (bs)
        {
            if (_backspaceAt < 0f) { _backspaceAt = now; _lastRepeatAt = now; Backspace(box); }
            else if (now - _backspaceAt > 0.45f && now - _lastRepeatAt > 0.06f) { _lastRepeatAt = now; Backspace(box); }
        }
        else _backspaceAt = -1f;
    }

    // ---------------- 文本进出（界面只经这两个入口 + 提交） ----------------

    /// <summary>追加文本（物理键 / CJK / 组合确认统一入口）。</summary>
    public static void Append(UiTextInput box, string s)
    {
        if (box == null || string.IsNullOrEmpty(s)) return;
        string cur = box.Value ?? "";
        if (cur.Length + s.Length > MaxLen) s = s.Substring(0, Math.Max(0, MaxLen - cur.Length));
        if (s.Length == 0) return;
        box.SetValueInternal(cur + s, notify: true);
    }

    /// <summary>追加一个字符。</summary>
    public static void Append(UiTextInput box, char c)
    {
        if (c == '\0' || c == '\b' || c == '\r' || c == '\n') return;
        Append(box, c.ToString());
    }

    /// <summary>删除末尾一个字符。</summary>
    public static void Backspace(UiTextInput box)
    {
        if (box == null) return;
        string cur = box.Value ?? "";
        if (cur.Length == 0) return;
        box.SetValueInternal(cur.Substring(0, cur.Length - 1), notify: true);
    }

    /// <summary>提交（回车）。</summary>
    public static void Submit(UiTextInput box)
    {
        if (box == null) return;
        try { box.RaiseSubmit(); } catch { }
        SetFocused(null);
    }

    /// <summary>
    /// Harmony patch `Keyboard.OnTextInput` 的入口（见 <see cref="Install"/>）：
    /// 只转发 **CJK 与 `\b`/`\r`**（英文/数字走物理键，避免双通道重复）。
    /// ⚠️ 退格不在这里处理（物理键已统一处理，否则一次退格删两个字符）；
    /// 回车也不提交（IME 空格确认候选词时 OS 会发 `\r`，会误触发）。
    /// </summary>
    public static void OnImeText(char c)
    {
        try
        {
            var box = Focused;
            // 回车：组合中 = 输入法在确认候选词（丢掉）；**没组合 = 用户真的按了回车** → 走提交通道
            // （2026-09-13：以前一律丢掉，导致“用输入法时回车没反应”。这里只标记，由 Tick 统一提交。）
            if (c == '\r' || c == '\n')
            {
                if (!IsComposing()) _textEnterFrame = Time.frameCount;
                return;
            }
            if (box == null) return;
            if (c == '\b') return;
            if (c <= 0x2E7F) return;                 // 只收 CJK（英文走物理键）
            if (c == '\uFFFD') return;               // IL2CPP 交互层把 CJK 破坏成的替换码（原模组 SanitizeIme 同款）
            Append(box, c);
            if ((++_diag % 20) == 1) CoopLog.Debug("uikit.widget", () => $"OnTextInput CJK '{c}' U+{(int)c:X4}");
        }
        catch { }
    }

    // ---------------- 原生 Win32 IME（真汉字） ----------------

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern int ImmGetCompositionStringW(IntPtr himc, int dwIndex, IntPtr buf, int bufLen);
    [System.Runtime.InteropServices.DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr himc);

    private const int GCS_RESULTSTR = 0x0800;   // 刚提交（确认）的字符串
    private const int GCS_COMPSTR = 0x0008;     // 正在组合的字符串

    /// <summary>读 OS 输入法字符串；无/失败返回空串。</summary>
    private static string ReadImeString(int dwIndex)
    {
        try
        {
            IntPtr hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) return "";
            IntPtr himc = ImmGetContext(hwnd);
            if (himc == IntPtr.Zero) return "";
            try
            {
                int len = ImmGetCompositionStringW(himc, dwIndex, IntPtr.Zero, 0);
                if (len <= 0) return "";
                IntPtr buf = System.Runtime.InteropServices.Marshal.AllocHGlobal(len + 4);
                try
                {
                    int n = ImmGetCompositionStringW(himc, dwIndex, buf, len + 4);
                    if (n <= 0) return "";
                    return System.Runtime.InteropServices.Marshal.PtrToStringUni(buf, n / 2) ?? "";
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buf); }
            }
            finally { ImmReleaseContext(hwnd, himc); }
        }
        catch { return ""; }
    }

    /// <summary>正在组合（拼音候选窗开着）。</summary>
    private static bool IsComposing()
    {
        try
        {
            // ① 原生组合串（最及时：首键按下当帧就能判到）
            if (ReadImeString(GCS_COMPSTR).Length > 0) return true;
            // ② Unity 侧的组合串（兜底）
            if (_anchor != null)
            {
                try { if ((_anchor.compositionString?.Length ?? 0) > 0) return true; } catch { }
                try { if (_anchor.compositionLength > 0) return true; } catch { }
            }
        }
        catch { }
        return false;
    }

    /// <summary>清理 IME 文本（去掉 U+FFFD 与空字符）。</summary>
    private static string SanitizeIme(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '\uFFFD' || ch == '\0') continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 读一帧的原生 IME 状态：① 提交串（GCS_RESULTSTR）→ 追加；② 组合确认（compositionString 消失那一刻）→ 追加。
    /// 返回 true = 本帧已消费（调用方跳过物理键，避免同一拍重复）。
    /// </summary>
    private static bool PollNativeIme(UiTextInput box)
    {
        bool handled = false;
        try
        {
            string native = SanitizeIme(ReadImeCommitted());
            if (!string.IsNullOrEmpty(native) && native != _lastNative)
            {
                _lastNative = native;
                Append(box, native);
                if ((++_diag % 10) == 1) CoopLog.Debug("uikit.widget", () => $"原生中文提交 added='{native}'");
                handled = true;
            }

            // 组合文本 → 组合结束（变空）时把最后一段组合追加进去（补 OnTextInput 收不到确认字符的通道）
            string comp = "";
            try { comp = _anchor?.compositionString ?? ""; } catch { comp = ""; }
            if (comp.Length > 0)
            {
                _compositionLast = comp;
            }
            else if (_compositionLast.Length > 0)
            {
                string cached = SanitizeIme(_compositionLast);
                _compositionLast = "";
                string cur = box.Value ?? "";
                if (cached.Length > 0 && !cur.EndsWith(cached, StringComparison.Ordinal) && cur != _lastNative)
                {
                    Append(box, cached);
                    handled = true;
                }
            }
        }
        catch { }
        return handled;
    }

    private static string ReadImeCommitted() => ReadImeString(GCS_RESULTSTR);

    // ---------------- 隐藏 IME 锚点 ----------------

    private static TMP_InputField _anchor;

    /// <summary>
    /// 建隐藏 IME 锚点（只在第一次需要时建）。
    /// ⚠️ 必须与真实输入框**同构**（主对象 Image + Text + Placeholder + textViewport）：
    ///    原模组踩过坑 —— 简化版（无 placeholder/viewport + 离屏 4×4）能唤起输入法，但 TMP_InputField
    ///    无法真正处理 IME 组合 → 提交字符既不进 `.text` 也不触发 OnTextInput（中文全丢）。
    /// </summary>
    private static void EnsureAnchor()
    {
        if (_anchor != null) return;
        try
        {
            var canvas = Menu.UiMenuWindow.CanvasRoot;
            if (canvas == null) return;

            var go = new GameObject("ImeAnchor");
            go.transform.SetParent(canvas, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -64f);     // 屏幕内（左上），全透明不可见
            rt.sizeDelta = new Vector2(300f, 28f);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;

            var txt = MakeAnchorText(go.transform, "Text");
            var ph = MakeAnchorText(go.transform, "Placeholder");

            var field = go.AddComponent<TMP_InputField>();
            if (field == null) { UnityEngine.Object.Destroy(go); return; }
            field.textComponent = txt;
            field.placeholder = ph;
            field.textViewport = txt.rectTransform;
            field.characterLimit = 0;      // 不限（我们自己在 Append 里限长）
            try { field.customCaretColor = true; field.caretColor = new Color(0f, 0f, 0f, 0f); } catch { }
            try { field.selectionColor = new Color(0f, 0f, 0f, 0f); } catch { }
            try { field.lineType = TMP_InputField.LineType.SingleLine; } catch { }
            _anchor = field;
            CoopLog.Debug("uikit.widget", () => "IME 锚点已建立（隐藏 TMP_InputField，用于唤起系统输入法）");
        }
        catch (Exception ex) { CoopLog.Warn("uikit.widget", () => "建 IME 锚点失败：" + ex.Message); }
    }

    private static TMP_Text MakeAnchorText(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 0f);
        rt.offsetMax = new Vector2(-8f, 0f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = 15;
        t.color = new Color(0f, 0f, 0f, 0f);      // 透明：只为 IME，不显示
        t.alignment = TextAlignmentOptions.Left;
        t.raycastTarget = false;
        try { UI.UiKit.EnsureFont(t); } catch { }
        return t;
    }

    /// <summary>激活锚点 + 唤起输入法 + 把候选窗定位到当前输入框旁（Unity 官方推荐 SetIMECursorPosition）。</summary>
    private static void ActivateImeAnchor()
    {
        try
        {
            EnsureAnchor();
            if (_anchor != null)
            {
                try
                {
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null && es.currentSelectedGameObject != _anchor.gameObject)
                        es.SetSelectedGameObject(_anchor.gameObject);
                }
                catch { }
                if (!_anchor.isFocused)
                {
                    try { _anchor.ActivateInputField(); } catch { }
                }
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                try { kb.SetIMEEnabled(true); } catch { }
                try { kb.SetIMECursorPosition(ImeCursorPos()); } catch { }
            }
        }
        catch (Exception ex) { CoopLog.Warn("uikit.widget", () => "激活 IME 失败：" + ex.Message); }
    }

    private static void DeactivateImeAnchor()
    {
        try
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null) { try { kb.SetIMEEnabled(false); } catch { } }
            if (_anchor != null)
            {
                if (_anchor.isFocused) { try { _anchor.DeactivateInputField(); } catch { } }
                try { _anchor.text = ""; } catch { }
            }
        }
        catch { }
    }

    /// <summary>候选窗位置 = 当前输入框的屏幕坐标（拿不到时给屏幕中偏上）。</summary>
    private static Vector2 ImeCursorPos()
    {
        try
        {
            var f = Focused;
            if (f != null && f.Rect != null)
            {
                var rt = f.Rect;
                var world = rt.TransformPoint(new Vector3(rt.rect.xMin, rt.rect.yMax, 0f));
                var cam = Native.UiPointerRouter.CameraFor(rt);
                return RectTransformUtility.WorldToScreenPoint(cam, world);
            }
        }
        catch { }
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
    }

    // ---------------- Harmony patch（Keyboard.OnTextInput） ----------------

    private static bool _installed;

    /// <summary>
    /// 装 Harmony patch（`Keyboard.OnTextInput` postfix → <see cref="PostTextInput"/>）。
    /// 由 <c>UiKitRuntime.Startup</c> 调一次；失败只告警（其余三通道仍能工作）。
    /// </summary>
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        try
        {
            Native.HarmonyReflect.PatchByTypeName(
                "UnityEngine.InputSystem.Keyboard", "OnTextInput",
                prefixName: null, postfixName: nameof(PostTextInput),
                label: "keyboard.OnTextInput → UiTextRouter",
                callbacksOn: typeof(UiTextRouter));
        }
        catch (Exception ex) { CoopLog.Warn("uikit.widget", () => "patch OnTextInput 失败：" + ex.Message); }
    }

    /// <summary>Harmony postfix：`Keyboard.OnTextInput(char)`。</summary>
    public static void PostTextInput(object __instance, char __0)
    {
        try
        {
            if (__instance == null) return;
            if (__instance != (object)UnityEngine.InputSystem.Keyboard.current) return;
            OnImeText(__0);
        }
        catch { }
    }

    /// <summary>诊断串（测试模组 `widgetprobe` 用）。</summary>
    public static string Probe()
    {
        string box = Focused != null ? "'" + Focused.Label + "'" : "-";
        string anchor = _anchor == null ? "无" : (_anchor.isFocused ? "已聚焦" : "未聚焦");
        bool kb = false; try { kb = UnityEngine.InputSystem.Keyboard.current != null; } catch { }
        return $"输入管线：聚焦={box}（文本='{(Focused != null ? Focused.Value : "")}'）｜组合中={_composing}"
             + $"｜上一帧停='{Diag}'｜最近回车='{LastEnter}'（{(_lastEnterFrame >= 0 ? (Time.frameCount - _lastEnterFrame).ToString() + " 帧前" : "没按过")}）"
             + $"｜回车本帧={(ConsumedEnter ? "被输入框吃掉" : "-")}"
             + $"｜IME 锚点={anchor}｜键盘={kb}｜最近原生提交='{_lastNative}'"
             + $"｜patch={( _installed ? "已尝试" : "未装")}";
    }
}

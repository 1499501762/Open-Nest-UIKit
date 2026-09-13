# 发布手册（PUBLISH）

本仓库是 Open Nest 模组家族中 UIKit 的**独立公开仓库**，与 `Open-Nest-Coop` 主仓库分离：
只包含 UIKit 的源码、文档与工具，不引用其他模组的程序集（UI/日志基类以 `Vendor/` 源码形式内置）。

- 仓库地址：`https://github.com/1499501762/Open-Nest-UIKit`
- 许可证：AGPL-3.0（见 `LICENSE`）
- 首个版本：`0.0.1-Alpha-1`（**Pre-release**）
- 版本号两处，必须同步改：
  - `src/OpenNestUIKit/UiKitInfo.cs` → `public const string Version`
  - 5 个 `.csproj` 的 `<Version>`（`src/OpenNestUIKit{,.API,.MelonMod,.Test,.Test.MelonMod}`）

---

## 一、首次发布：创建空仓库并推送（已执行，本节留作复现）

✅ **2026-09-13 已完成发布**，事实与命令见文末「发布记录」。首次发布时本地仓库已初始化并完成首个提交与 tag
`v0.0.1-Alpha-1`，远端由以下任一路径创建：

### 路径 A：先在网页创建空仓库（无需安装工具）

1. 打开 <https://github.com/new>
   - Owner: `1499501762`
   - Repository name: `Open-Nest-UIKit`
   - Visibility: **Public**
   - **不要**勾选 Add a README / .gitignore / license（保持空仓库，否则推送冲突）
2. 推送：

```powershell
cd d:\Dev\Open-Nest-UIKit
git remote add origin https://github.com/1499501762/Open-Nest-UIKit.git
git push -u origin main
git push origin v0.0.1-Alpha-1
```

### 路径 B：安装 GitHub CLI 后一条命令建仓

```powershell
winget install GitHub.GitHubCLI     # 安装 gh（装完需重开终端）
gh auth login                       # 浏览器登录 1499501762
cd d:\Dev\Open-Nest-UIKit
gh repo create 1499501762/Open-Nest-UIKit --public --source . --push
git push origin v0.0.1-Alpha-1
```

---

## 二、打包发布产物

```powershell
cd <本仓库目录>
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 `
  -GameDirG "<装了 BepInEx 6 的游戏目录>" `
  -GameDirD "<装了 MelonLoader 0.7.3 的游戏目录>"
```

产物在 `release/`（**不进版本库**，`release/*.zip` 已在 `.gitignore`）：

| 文件 | 内容 |
|---|---|
| `OpenNestUIKit-0.0.1-Alpha-1-BepInEx.zip` | `BepInEx/plugins/OpenNestUIKit.dll` + `OpenNestUIKit.API.dll` + `README.txt` |
| `OpenNestUIKit-0.0.1-Alpha-1-MelonLoader.zip` | `Mods/OpenNestUIKit.MelonMod.dll` + `UserLibs/OpenNestUIKit.API.dll` + `README.txt` |

> 打包前**关掉游戏**，否则插件 dll 被占用、构建部署会失败。

---

## 三、创建 Release（Pre-release）

**首选 `gh`（本机已装，见下）**——它按 UTF-8 处理正文，不会有编码坑：

```powershell
gh release create v0.0.1-Alpha-2 --prerelease --title "0.0.1-Alpha-2" `
  --notes-file docs\RELEASE_NOTES.md `
  release\OpenNestUIKit-0.0.1-Alpha-2-BepInEx.zip `
  release\OpenNestUIKit-0.0.1-Alpha-2-MelonLoader.zip
```

改正文 / 换资产：

```powershell
gh release edit v0.0.1-Alpha-2 --notes-file docs\RELEASE_NOTES.md      # 正文（UTF-8）
gh release upload v0.0.1-Alpha-2 --clobber release\*.zip               # 覆盖同名资产
```

> 本机 gh：`C:\Program Files\GitHub CLI\gh.exe`（2.100.0，已登录 `1499501762`，scopes `gist, read:org, repo, workflow`）。
> 若当前终端里 `gh` 命令找不到 → 那是 PATH 未刷新，开新终端或直接用全路径。

### 网页操作（备选）

1. 打开 <https://github.com/1499501762/Open-Nest-UIKit/releases/new>
2. **Choose a tag** → 选 `v0.0.1-Alpha-2`（若列表里没有，选 "Create new tag on publish" 并填 `v0.0.1-Alpha-2`）
3. Release title: `0.0.1-Alpha-2`
4. 说明正文：粘贴 `docs/RELEASE_NOTES.md` 的内容
5. ✅ **勾选 "Set as a pre-release"**（用户要求：Pre-release）
6. 上传 `release/` 下两个 zip
7. Publish release

### 或走 REST（无 gh 时用）

`POST /repos/<owner>/<repo>/releases`（先只传 ASCII 字段）→ `PATCH /releases/<id>` 写正文 → `POST uploads.github.com/.../assets?name=`。
⚠️ 正文一定要用 `[IO.File]::ReadAllText(path, [Text.Encoding]::UTF8)` 读，见 §七。

---

## 四、后续版本 Checklist

1. 改版本号（`UiKitInfo.cs` + 5 个 csproj）
2. 更新 `docs/RELEASE_NOTES.md`（新版本段落或另建文件）与 `README.md` 的 Status 行
3. 双端构建验证（见 `README.md` → Building from source），确认 `Build succeeded`
4. 提交（Conventional Commits，如 `chore(release): 0.0.1-Alpha-2`）→ `git tag v0.0.1-Alpha-2` → 推送
5. `scripts/package.ps1 -Version 0.0.1-Alpha-2` 打包 → 建 Release（仍是 Pre-release）

---

## 五、从主仓库同步源码

UIKit 的源码同时存在于主仓库（私有）`src\OpenNestUIKit*`（双仓维护）。
同步方式：把 `src\OpenNestUIKit{,.API,.MelonMod,.Test,.Test.MelonMod}`、`docs\UI_KIT*.md`、
`tools\slice_tool.py`、`scripts\slice-tool.ps1` 覆盖过来即可；两侧都不引用对方仓库的工程，
复制后各自独立可编译。

⚠️ 复制后**不要**把主仓库的 `bin/`、`obj/`、`BepInEx/`、`interop/` 带过来（`.gitignore` 已挡，
但 `robocopy /XD bin obj` 更省事）。

⚠️ **复制后必须重新脱敏**（公开树不得含本机路径与闭源模组名）：

```powershell
git grep -n -I -E 'D:\\Dev|G:\\Steam|D:\\Steam|C:\\Users\\' -- .
git grep -n -I -E 'decompiled|ilspy|dnSpy|IronNestCoop' -- .
```

（闭源模组名的完整清单在私有仓库的记忆文件里，公开仓**不重复列举**——名字本身也不要进公开树。）

命中就改写成通用描述（`<游戏目录>` / `<本仓库目录>` / “官方联机 UI 的做法”）：
比如源码注释里写了某个闭源模组的类型名，就改成“官方联机 UI 用的也是这一档”。

---

## 六、发布记录

### `0.0.1-Alpha-2`（U9：契约文档 + 示例模组，2026-09-13）

| 项 | 值 |
|---|---|
| commit | `322842d`（`a4158ca` 文档/示例提交 + `322842d` gitignore 修复） |
| tag | `v0.0.1-Alpha-2`（注释标签） |
| Release | id `387840362`，**Pre-release ✅**，正文 = `docs/RELEASE_NOTES.md`（8 539 字符） |
| 资产 | `OpenNestUIKit-0.0.1-Alpha-2-BepInEx.zip`（168.8 KB）、`OpenNestUIKit-0.0.1-Alpha-2-MelonLoader.zip`（168.6 KB） |
| 新增 | `docs/API.md`（英文契约参考）+ `samples/`（共享 provider + BepInEx/MelonLoader 双壳，共 7 文件） |
| 版本号 | 7 处从 `0.0.1-Alpha-1` → `0.0.1-Alpha-2`（`UiKitInfo`/`TestInfo` + 5 个 csproj） |
| 克隆校验 | 102 文件、7 个样例文件；mod 双端 + 样例双壳 **四个工程 0 错**（ML 端仅 0Harmony 引用告警） |

⚠️ **本次的坑（务必记住）**：`.gitignore` 里**未加斜杠**的 `BepInEx/` 会连 `samples/BepInEx/` 一起忽略 —— 推送后才发现样例的 BepInEx 壳（2 文件）没进仓库、也没进 tag。
修法：把“安装到游戏的目录”全部**锚定到仓库根**（`/BepInEx/`、`/Mods/`、`/UserLibs/`、`/interop/`、`/Il2CppAssemblies/`、`/ref/`、`/model/`），因为只有部署时它们才会出现在根目录。
补救：`git tag -f -a <tag>` 重指到修复提交后 `git push --force origin <tag>`（Release 按标签名跟随，资产不受影响；仅限刚发布、无人消费时这么做）。
**发布前必查**：`git ls-files <新增目录>` 的数量对不对（这次 `samples` 应为 7，实际只有 5）。

⚠️ **同一天补修的两处编码问题**（详见 §七）：Release 正文与包内 `README.txt` 都曾出现乱码（`鈥?` / `鍘熺敓`），
已用 `gh release edit --notes-file` 与 `gh release upload --clobber` 重新上传；现在两版的正文与资产均为正常 UTF-8。

### `0.0.1-Alpha-1`（首个公开版本，2026-09-13）

| 项 | 值 |
|---|---|
| 仓库 | <https://github.com/1499501762/Open-Nest-UIKit>（Public，AGPL-3.0，默认分支 `main`） |
| commit | `1abcd5a`（首个提交，94 个文件，仅此一次提交，无历史泄露面） |
| tag | `v0.0.1-Alpha-1` |
| Release | id `387838334`，**Pre-release ✅**，正文 = `docs/RELEASE_NOTES.md`（5 188 字符） |
| 资产 | `OpenNestUIKit-0.0.1-Alpha-1-BepInEx.zip`（168.8 KB）、`OpenNestUIKit-0.0.1-Alpha-1-MelonLoader.zip`（168.6 KB） |
| topics | `bepinex, bepinex-plugin, il2cpp, iron-nest, melonloader, ui, ui-library, unity-mod` |
| 发布前脱敏 | 全库扫描本机路径/用户名 = 0；闭源模组名 = 0 |
| 克隆校验 | `git clone --branch v0.0.1-Alpha-1` → 94 文件 → 双端 `dotnet build` **0 错 0 警 / 0 错 1 警** ✅ |

**本机发布手法**（首发那次本机还没有 `gh`，走的是 REST API；现在两种都可用）：

1. token 从凭据存储取，不落盘不回显：`"protocol=https`nhost=github.com`n`n" | git credential fill`
   → 取其中 `password=` 行作 `Authorization: Bearer <token>`。
2. 建仓：`POST https://api.github.com/user/repos`（body 只有 ASCII 字段）。
3. 推代码/标签：`git push -u origin main` + `git push origin v0.0.1-Alpha-1`（凭据由 credential store 提供）。
4. 建 Pre-release：`POST /repos/<owner>/<repo>/releases`（先只传 `tag_name/name/draft/prerelease`）。
5. 正文：`PATCH /repos/<owner>/<repo>/releases/<id>`，body 用 `System.Web.Script.Serialization.JavaScriptSerializer` 序列化后
   **写入 UTF-8 文件**再 `-InFile` 上传。
6. 资产：`POST https://uploads.github.com/repos/<owner>/<repo>/releases/<id>/assets?name=<文件名>`，`-InFile <zip>`，`Content-Type: application/zip`。
7. 校验：`GET /releases/tags/<tag>` 回读 `prerelease/draft/assets`，并 `git ls-remote` 对 commit。

⚠️ **三条坑（都踩过）**：

- PowerShell 脚本文件**必须是纯 ASCII**（本机 PS 5.1 按 ANSI 读无 BOM 文件，中文注释里的字节能拼出引号 → 解析报错）。
- `$ErrorActionPreference='Stop'` + `git push ... 2>&1` 会把 git 的 **stderr 进度输出**当成致命错误直接中断脚本；
  git 调用处改成 `Continue` 并检查 `$LASTEXITCODE`。
- `ConvertTo-Json` 序列化从文件读来的长字符串会膨胀（实测 5 KB → 454 KB，被 GitHub 以
  “body is too long (maximum is 125000 characters)” 拒绝）；用 `JavaScriptSerializer` 或手写转义。

---

## 七、编码坑（每次发布前过一遍）

本机是 **Windows PowerShell 5.1**（不是 pwsh 7），它默认按 **ANSI/GBK** 解释“无 BOM 的 UTF-8 文件”—— 两个坑都因此产生：

| 现象 | 原因 | 正确做法 |
|---|---|---|
| Release 正文里 `—` 变成 `鈥?` | `Get-Content -Raw` 把 UTF-8 的 `E2 80 94` 当 GBK 解码 | 读**数据文件**一律 `[IO.File]::ReadAllText($p, [Text.Encoding]::UTF8)`，或直接用 `gh ... --notes-file`（gh 自己按 UTF-8 读） |
| 发布包里 `README.txt` 中文变 `鍘熺敓椋庢牸` | `.ps1` 脚本**无 BOM** → PS 5.1 按 ANSI 解析脚本，脚本里的中文字符串被错解后再写成 UTF-8 | 含非 ASCII 的 `.ps1` **必须存成 UTF-8 with BOM**：`[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($true)))` |

**发布前自检（3 条命令）**：

```powershell
# 1. 脚本都有 BOM
foreach ($f in 'scripts\package.ps1','scripts\slice-tool.ps1') {
  $b=[IO.File]::ReadAllBytes($f); '{0}: BOM={1}' -f $f, ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) }
# 2. 包内 README 中文正常（解压 zip 里的 README.txt，应含“原生风格 UI 库模组”且不含“鍘熺敓”）
# 3. Release 正文正常（含真破折号、不含乱码）
gh release view v0.0.1-Alpha-2 --repo 1499501762/Open-Nest-UIKit --json body
```

> 提示：上游（私有仓库）的 `scripts/slice-tool.ps1` 也没有 BOM；同步过来时要么保留公开仓这份带 BOM 的，要么给上游也补上。

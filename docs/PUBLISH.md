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

## 一、首次发布：创建空仓库并推送

本地仓库已经初始化并完成首个提交与 tag `v0.0.1-Alpha-1`，只差远端。

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

### 网页操作（推荐）

1. 打开 <https://github.com/1499501762/Open-Nest-UIKit/releases/new>
2. **Choose a tag** → 选 `v0.0.1-Alpha-1`（若列表里没有，选 "Create new tag on publish" 并填 `v0.0.1-Alpha-1`）
3. Release title: `0.0.1-Alpha-1`
4. 说明正文：粘贴 `docs/RELEASE_NOTES.md` 的内容
5. ✅ **勾选 "Set as a pre-release"**（用户要求：Pre-release）
6. 上传 `release/` 下两个 zip
7. Publish release

### 或使用 gh

```powershell
cd d:\Dev\Open-Nest-UIKit
gh release create v0.0.1-Alpha-1 --prerelease --title "0.0.1-Alpha-1" `
  --notes-file docs/RELEASE_NOTES.md `
  release\OpenNestUIKit-0.0.1-Alpha-1-BepInEx.zip `
  release\OpenNestUIKit-0.0.1-Alpha-1-MelonLoader.zip
```

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
git grep -n -I -E 'D:\\Dev|G:\\Steam|D:\\Steam|C:\\Users\\|Open Nest co-op' -- .
git grep -n -I -E 'Synchrony|IronNestFCS|decompiled|IronNestCoop' -- .
```

命中就改写成通用描述（`<游戏目录>` / `<本仓库目录>` / “官方联机 UI”），例如源码注释里
“抄 `Synchrony.MultiplayerMenu` 的做法” → “官方联机 UI 用的也是这一档”。

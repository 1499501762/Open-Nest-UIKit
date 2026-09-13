# ============================================================
# Open Nest UIKit 打包脚本
# 本地打包到 release/（两个加载器各一个 zip，不含加载器本身）：
#   OpenNestUIKit-<ver>-BepInEx.zip       (BepInEx\plugins\OpenNestUIKit.dll + OpenNestUIKit.API.dll)
#   OpenNestUIKit-<ver>-MelonLoader.zip   (Mods\OpenNestUIKit.MelonMod.dll + UserLibs\OpenNestUIKit.API.dll)
# 用法: powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
#   -Version 0.0.1-Alpha-1
#   -GameDirG "<游戏目录>"   (BepInEx 端构建用，需已装 BepInEx 6 IL2CPP)
#   -GameDirD "<游戏目录>"   (MelonLoader 端构建用，需已装 MelonLoader 0.7.3)
# ============================================================
param(
    [string]$GameDirG = "C:\steam\steamapps\common\Iron Nest Heavy Turret Simulator",
    [string]$GameDirD = "C:\steam\steamapps\common\Iron Nest Heavy Turret Simulator",
    [string]$Version = "0.0.1-Alpha-1"
)
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"   # Compress-Archive 处理大文件时进度条会崩，禁用
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$OutDir = Join-Path $Root "release"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

# ---------- 1. 构建 ----------
Write-Host "== 构建 BepInEx 版 =="
dotnet build "src\OpenNestUIKit\OpenNestUIKit.csproj" -c Release -p:GameDir="$GameDirG" 2>&1 | Select-Object -Last 2
Write-Host "== 构建 MelonLoader 版 =="
dotnet build "src\OpenNestUIKit.MelonMod\OpenNestUIKit.MelonMod.csproj" -c Release -p:GameDir="$GameDirD" -p:ClientGame="$GameDirD" 2>&1 | Select-Object -Last 2

$bepinBin = Join-Path $Root "src\OpenNestUIKit\bin\Release\net6.0"
$mlBin    = Join-Path $Root "src\OpenNestUIKit.MelonMod\bin\Release\net6.0"
foreach ($f in @("$bepinBin\OpenNestUIKit.dll", "$mlBin\OpenNestUIKit.MelonMod.dll", "$mlBin\OpenNestUIKit.API.dll")) {
    if (-not (Test-Path $f)) { throw "构建产物缺失: $f" }
}

# ---------- 2. staging ----------
$stage = Join-Path $env:TEMP "uikit_pkg_$PID"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

function Copy-Into($pkgDir, $src, $relDest) {
    $dest = Join-Path $pkgDir $relDest
    New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
    Copy-Item $src $dest -Force
}
function New-Package($pkgName, $pkgDir) {
    $zip = Join-Path $OutDir "$pkgName.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path "$pkgDir\*" -DestinationPath $zip -CompressionLevel Optimal
    Write-Host ("打包完成: {0}  ({1:N1} KB)" -f $zip, ((Get-Item $zip).Length / 1KB))
}

$readme = @"
Open Nest UIKit v$Version
=========================
Native-looking UI library mod for Iron Nest: Heavy Turret Simulator.
Iron Nest 原生风格 UI 库模组（把页面注入游戏自带的 ESC 剪贴板，控件外观照抄游戏设置页）。

--- 中文 ---
【安装 - BepInEx 版】
1. 需已安装 BepInEx 6 (IL2CPP)，并启动过一次游戏（生成 interop 程序集）。
2. 解压本包，把 BepInEx\plugins\ 下两个 dll 复制到 <游戏目录>\BepInEx\plugins\
      OpenNestUIKit.dll       —— 模组本体
      OpenNestUIKit.API.dll   —— 第三方契约程序集（其他模组引用它加页面）
3. 启动游戏，按 ESC 出现 Open Nest UIKit 入口即成功。

【安装 - MelonLoader 版】
1. 需已安装 MelonLoader 0.7.3，并启动过一次游戏。
2. 解压本包：Mods\OpenNestUIKit.MelonMod.dll → <游戏目录>\Mods\
              UserLibs\OpenNestUIKit.API.dll  → <游戏目录>\UserLibs\
3. 启动游戏，按 ESC 出现 Open Nest UIKit 入口即成功。

【说明】
- 本包不含模组加载器；两个加载器版本不要同时装（会重复注入）。
- 日志: <游戏目录>\OpenNestUIKitLogs\（uikit.log / test.log）。

--- English ---
[Install - BepInEx build]
1. Requires BepInEx 6 (IL2CPP) installed and the game launched once (to generate interop assemblies).
2. Extract; copy both dlls under BepInEx\plugins\ to <GameDir>\BepInEx\plugins\
      OpenNestUIKit.dll       - the mod
      OpenNestUIKit.API.dll   - third-party contract assembly (other mods reference it to add pages)
3. Launch the game; press ESC and the Open Nest UIKit entry appears.

[Install - MelonLoader build]
1. Requires MelonLoader 0.7.3 installed and the game launched once.
2. Extract: Mods\OpenNestUIKit.MelonMod.dll -> <GameDir>\Mods\
            UserLibs\OpenNestUIKit.API.dll  -> <GameDir>\UserLibs\
3. Launch the game; press ESC and the Open Nest UIKit entry appears.

[Notes]
- The mod loader is NOT included. Do not install both builds at once.
- Logs: <GameDir>\OpenNestUIKitLogs\ (uikit.log / test.log)
"@

# ---------- 3. BepInEx 包 ----------
$p1 = Join-Path $stage "OpenNestUIKit-$Version-BepInEx"
New-Item -ItemType Directory -Path $p1 -Force | Out-Null
Copy-Into $p1 (Join-Path $bepinBin "OpenNestUIKit.dll")      "BepInEx\plugins\OpenNestUIKit.dll"
Copy-Into $p1 (Join-Path $bepinBin "OpenNestUIKit.API.dll")  "BepInEx\plugins\OpenNestUIKit.API.dll"
Set-Content -Path (Join-Path $p1 "README.txt") -Value $readme -Encoding UTF8

# ---------- 4. MelonLoader 包 ----------
$p2 = Join-Path $stage "OpenNestUIKit-$Version-MelonLoader"
New-Item -ItemType Directory -Path $p2 -Force | Out-Null
Copy-Into $p2 (Join-Path $mlBin "OpenNestUIKit.MelonMod.dll") "Mods\OpenNestUIKit.MelonMod.dll"
Copy-Into $p2 (Join-Path $mlBin "OpenNestUIKit.API.dll")      "UserLibs\OpenNestUIKit.API.dll"
Set-Content -Path (Join-Path $p2 "README.txt") -Value $readme -Encoding UTF8

New-Package "OpenNestUIKit-$Version-BepInEx"     $p1
New-Package "OpenNestUIKit-$Version-MelonLoader" $p2
Remove-Item $stage -Recurse -Force
Write-Host "全部完成 -> $OutDir"

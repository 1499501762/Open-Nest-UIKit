#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
九宫格切片工具（OpenNestUIKit 切片真相的产出工具）—— 独立离线，不需要开游戏。

用途：把游戏原生 UI sprite 的**九宫格切割线**（border）人工定准，导出成
`OpenNestUIKit.slices.ini`；库（`src/OpenNestUIKit/Theme/UiSlice.cs`）读该文件并**无条件采用**
其中的值（不再自动缩放、不再退回纯色）。

为什么需要它：九宫格只有在 `border 合计 + 余量 ≤ 控件尺寸` 时才不被 Unity 的
`Image.GetAdjustedBorders` 整体压缩；尺寸不匹配时（例如 128px 的 `UI Box Castile`
纵向边框 34+16=50px 塞进 32px 高的按钮）会"看起来像图案被压实/平铺"。
自动规则只能兜底，**正确的切线要人来看**。

------------------------------------------------------------------
用法 1：图形界面（推荐）

    python tools/slice_tool.py --gui
    python tools/slice_tool.py --gui --dir ref/ui_sprites_menu

    · 左侧选图（可按名字过滤；● 已定义 / ○ 未定义；只看已定义或只看未定义）
    · 中间是原图，4 条切线可用鼠标拖动（或方向键微调）
    · 右侧改 L/T/R/B、scale、mode、center；预览 3 档尺寸（含"放得下"提示）
    · 「保存」写入 ini（库 2 秒内自动热重载）；「删除此条定义 / Del」剔除不需要的素材（如非组件 UI）
    · **光浏览不会写进定义**：只有真的改了字段才会把那张图加进定义（避免把非组件素材顺手存进去）
    · 预览 PNG / 部署到游戏按钮同下

用法 2：命令行（批量/脚本/可由我直接调用）

    # 列出图与作者原值
    python tools/slice_tool.py --dir ref/ui_sprites_menu --list

    # 生成"作者原值模板" ini（拿着它逐条改，或丢进 GUI）
    python tools/slice_tool.py --dir ref/ui_sprites_menu --template --out ref/ui_slices_menu.ini

    # 改单条（可一次多条）：--set "名字=L,T,R,B[,scale[,mode[,center]]]"
    python tools/slice_tool.py --ini ref/ui_slices_menu.ini \
        --set "UI Box line=8,8,8,8" \
        --set "UI Box Castile=10,14,10,10,1,sliced,1"

    # 剔除不需要的素材（名字写全 / 按正则批量）
    python tools/slice_tool.py --ini ref/ui_slices.ini --remove "Castle Stamp"
    python tools/slice_tool.py --ini ref/ui_slices.ini --remove-match "Icon|Logo|Arrow|Dot|key art"

    # 导出某张图的 9 宫格预览 PNG（多个尺寸拼一张图）
    python tools/slice_tool.py --dir ref/ui_sprites_menu --ini ref/ui_slices_menu.ini \
        --preview "UI Box Castile" --sizes 120x32,240x56,420x160 --out ref/preview_castile.png

    # 自检（无界面，验证解析/渲染/压缩无回归）
    python tools/slice_tool.py --selftest

    # 部署前体检（素材是否存在 / border 是否越界 / 小尺寸会不会被压缩）
    python tools/slice_tool.py --check
    python tools/slice_tool.py --check --deploy      # 体检通过再部署

    # 直接把 ini 交到游戏配置目录（BepInEx\config 与 MelonLoader 的 UserData 都认）
    python tools/slice_tool.py --ini ref/ui_slices.ini --deploy

ini 格式（与游戏内库完全一致，可手改）：
    [UI Box Castile]
    border=18,34,18,16
    scale=1
    mode=sliced          # sliced | tiled | simple | none
    center=1             # sliced/tiled 时是否绘制中心

依赖：Pillow（预览渲染必需）；tkinter（仅 --gui 需要，Python 自带）。
"""

import argparse
import os
import re
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:
    print("需要 Pillow：pip install Pillow")
    raise

try:
    import tkinter as tk
    from tkinter import ttk, filedialog, messagebox
    HAS_TK = True
except ImportError:
    HAS_TK = False

MANIFEST_NAME = "ui_sprites_manifest.txt"

# 相对路径以**仓库根**为基准（工具可能在任意 cwd 下被调用，例如 scripts/）
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def repo_path(p):
    return p if (not p or os.path.isabs(p)) else os.path.join(REPO_ROOT, p)

HEADER = (
    "# OpenNestUIKit 九宫格切片定义（**切片真相**：本文件里有定义的图，库无条件采用，不再自动缩放/退纯色）\n"
    "#\n"
    "# ⚠ 本文件**只给 OpenNestUIKit 模组用**（我们自己的菜单/注入项），\n"
    "#   **不会修改游戏原生 UI**：游戏原有 Sprite 的 border 不会被改，也不会被换成我们的副本。\n"
    "#\n"
    "# 字段（每个 [图片名] 段下均可写）：\n"
    "#   border = 左,上,右,下   基于该图原图尺寸的九宫格切割线（像素）\n"
    "#   scale  = 1             把 border 再除以它后烤进副本（>1 = 更细的边框；1 = 原样）\n"
    "#   mode   = sliced|tiled|simple|none   （none = 不使用这张图，退回纯色）\n"
    "#   center = 1|0           sliced 时是否绘制中心（中心透明的装饰框保持 1，底层纯色会兜住）\n"
    "#   tag    = 用途标记（哪种组件的背景）：panel|button|button.primary|button.danger|button.secondary\n"
    "#            |dialog|separator|input|tab|row|frame|other；库按 tag 优先为对应控件挑素材\n"
    "#\n"
    "# 由 tools/slice_tool.py 生成/编辑（可手改，游戏内 2 秒热重载）。\n\n"
)

# 组件背景的用途标记（工具下拉预置；也可自由填写）
TAGS = ("", "panel", "button", "button.primary", "button.danger", "button.secondary",
        "dialog", "separator", "input", "tab", "row", "frame", "other")


# ---------------------------------------------------------------- 数据层

def load_manifest(directory):
    """读 ui_sprites_manifest.txt → {name: {'border': (l,t,r,b), 'rect': (x,y,w,h), 'status': str}}"""
    out = {}
    path = os.path.join(directory, MANIFEST_NAME)
    if not os.path.exists(path):
        return out
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t")
            if len(parts) < 2:
                continue
            name = parts[0].strip()
            status = parts[1].strip()
            border = _parse_tuple(line, "border")
            rect = _parse_tuple(line, "rect")
            out[name] = {"status": status, "border": border, "rect": rect}
    return out


def _parse_tuple(line, key):
    m = re.search(key + r"=\(([-\d.,\s]+)\)", line)
    if not m:
        return None
    vals = [v.strip() for v in m.group(1).split(",")]
    try:
        return tuple(float(v) for v in vals[:4])
    except ValueError:
        return None


def load_ini(path):
    """读 ini → {name: entry dict}。与 C# 端 UiSliceStore 的解析保持一致。"""
    out = {}
    if not path or not os.path.exists(path):
        return out
    cur = None
    with open(path, "r", encoding="utf-8") as f:
        for raw in f:
            line = raw.strip()
            if not line or line[0] in "#;":
                continue
            if line.startswith("[") and line.endswith("]"):
                cur = line[1:-1].strip()
                out.setdefault(cur, entry())
                continue
            if "=" not in line or cur is None:
                continue
            k, v = line.split("=", 1)
            k, v = k.strip().lower(), v.strip()
            e = out.setdefault(cur, entry())
            if k == "border":
                parts = [p.strip() for p in v.split(",")]
                if len(parts) >= 4:
                    try:
                        e["border"] = tuple(float(p) for p in parts[:4])
                    except ValueError:
                        pass
            elif k == "scale":
                e["scale"] = _f(v, e["scale"])
            elif k == "mode":
                e["mode"] = v.lower() if v.lower() in ("sliced", "tiled", "simple", "none") else "sliced"
            elif k == "center":
                e["center"] = 1 if v in ("1", "true", "True", "yes") else 0
            elif k == "tag":
                e["tag"] = "" if v.lower() == "none" else v.strip()
    return out


def entry(border=(0, 0, 0, 0), scale=1.0, mode="sliced", center=1, tag=""):
    return {"border": tuple(float(b) for b in border), "scale": float(scale), "mode": mode,
            "center": int(center), "tag": (tag or "").strip()}


def _f(s, fallback):
    try:
        return float(s)
    except (TypeError, ValueError):
        return fallback


def dump_ini(store):
    """{name: entry} → ini 文本（与 C# UiSliceStore.Snapshot 的输出格式一致）。"""
    def num(x):
        return ("%g" % float(x))
    lines = [HEADER]
    for name in sorted(store.keys(), key=str.lower):
        e = store[name]
        b = e["border"]
        lines.append("[%s]\n" % name)
        lines.append("border=%s,%s,%s,%s\n" % (num(b[0]), num(b[1]), num(b[2]), num(b[3])))
        lines.append("scale=%s\n" % num(e["scale"]))
        lines.append("mode=%s\n" % e["mode"])
        lines.append("center=%d\n" % e["center"])
        if e.get("tag"):
            lines.append("tag=%s\n" % e["tag"])
        lines.append("\n")
    return "".join(lines)


def baked_border(e):
    """border ÷ scale（向下取整）—— 与 C# UiSlice.BakedBorder 一致。"""
    s = e["scale"] if e["scale"] and e["scale"] > 0 else 1.0
    return tuple(int(float(v) // s) for v in e["border"])


# ---------------------------------------------------------------- 渲染（与 Unity uGUI 对齐）

def _resize(img, w, h):
    w, h = max(1, int(w)), max(1, int(h))
    return img.resize((w, h), Image.NEAREST if (w <= 4 or h <= 4) else Image.BILINEAR)


def _tile(img, w, h):
    w, h = max(1, int(w)), max(1, int(h))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(0, h, max(1, img.height)):
        for x in range(0, w, max(1, img.width)):
            out.paste(img, (x, y))
    return out


def _adjusted_border(sw, sh, border, dw, dh):
    """
    复刻 Unity `Image.GetAdjustedBorders`：控件比 border 还小时，按**最小比例**等比缩四周，
    这正是"图案被整体压实"的来源 —— 预览里如实画出，便于判断某尺寸能不能用这套切线。
    """
    l, t, r, b = border
    l = min(l, sw); t = min(t, sh); r = min(sw - l, r); b = min(sh - t, b)
    sx = dw / (l + r) if (l + r) > 0 else 1.0
    sy = dh / (t + b) if (t + b) > 0 else 1.0
    k = min(1.0, sx, sy)
    if k < 1.0:
        l, r, t, b = l * k, r * k, t * k, b * k
    return l, t, r, b


def _clamp_border(border, sw, sh, dw, dh):
    """
    把（可能被压缩过的）border 收进合理范围：保证**源图与目标图**都留得下 ≥1px 的中间段。
    控件比 border 还小时 Unity 会把中间段压成 0，预览里那一片就不画（不报错）。
    """
    l, t, r, b = (int(round(v)) for v in border)
    l = max(0, min(l, sw - 1, dw - 1))
    t = max(0, min(t, sh - 1, dh - 1))
    r = max(0, min(r, sw - l - 1, dw - l - 1))
    b = max(0, min(b, sh - t - 1, dh - t - 1))
    return l, t, r, b


def render_slice(src_path, e, dw, dh, checker=True):
    """
    按 Unity 九宫格规则把 sprite 画进 (dw, dh)：
      corners 固定 1:1；edges 单向拉伸（tiled 模式则平铺）；center 双向拉伸（center=0 则不画）；
      border 放不下时按 GetAdjustedBorders 压缩。
    退化情形（border=0 / 控件比 border 还小 → 中间段或整条边被压成 0 像素）会被**跳过而不是报错**，
    与 Unity 里"零面积的那一片就不画"一致。
    """
    dw, dh = max(1, int(dw)), max(1, int(dh))
    img = Image.open(src_path).convert("RGBA")
    sw, sh = img.size
    mode = e["mode"]
    if mode == "none":
        out = Image.new("RGBA", (dw, dh), (0, 0, 0, 0))
    elif mode == "simple":
        out = _resize(img, dw, dh)
    else:
        out = Image.new("RGBA", (dw, dh), (0, 0, 0, 0))
        l, t, r, b = _clamp_border(_adjusted_border(sw, sh, baked_border(e), dw, dh), sw, sh, dw, dh)
        f = _tile if mode == "tiled" else _resize

        def blit(x0, y0, x1, y1, dx, dy, tw, th):
            """裁源图 (x0,y0,x1,y1) → 缩放/平铺到 tw×th → 贴到 (dx,dy)；任一边退化就跳过。"""
            if x1 <= x0 or y1 <= y0 or tw <= 0 or th <= 0:
                return
            piece = f(img.crop((x0, y0, x1, y1)), tw, th)
            out.alpha_composite(piece, (int(dx), int(dy)))

        mw, mh = sw - l - r, sh - t - b                     # 源图中间段
        tw, th = dw - l - r, dh - t - b                     # 目标中间段
        # 四角（1:1）
        blit(0, 0, l, t, 0, 0, l, t)
        blit(sw - r, 0, sw, t, dw - r, 0, r, t)
        blit(0, sh - b, l, sh, 0, dh - b, l, b)
        blit(sw - r, sh - b, sw, sh, dw - r, dh - b, r, b)
        # 四边
        blit(l, 0, sw - r, t, l, 0, tw, t)
        blit(l, sh - b, sw - r, sh, l, dh - b, tw, b)
        blit(0, t, l, sh - b, 0, t, l, th)
        blit(sw - r, t, sw, sh - b, dw - r, t, r, th)
        # 中心
        if e["center"] and mw > 0 and mh > 0:
            blit(l, t, sw - r, sh - b, l, t, tw, th)

    if not checker:
        return out
    bg = Image.new("RGBA", (dw, dh), (34, 38, 46, 255))
    d = ImageDraw.Draw(bg)
    for y in range(0, dh, 8):
        for x in range(0, dw, 8):
            if ((x // 8) + (y // 8)) % 2 == 0:
                d.rectangle([x, y, x + 7, y + 7], fill=(44, 49, 58, 255))
    bg.alpha_composite(out)
    return bg


MARK_COLORS = ((255, 107, 107), (255, 209, 102), (107, 255, 158), (107, 182, 255))


def _line_px(d, x0, y0, x1, y1, color, dashed):
    """画一条线（支持虚线）。坐标会被钳到图内，否则画在图外看不见。"""
    if dashed:
        if y0 == y1:
            for x in range(int(min(x0, x1)), int(max(x0, x1)) + 1, 6):
                d.line([x, y0, min(x + 3, max(x0, x1)), y0], fill=color, width=1)
        else:
            for y in range(int(min(y0, y1)), int(max(y0, y1)) + 1, 6):
                d.line([x0, y, x0, min(y + 3, max(y0, y1))], fill=color, width=1)
    else:
        d.line([x0, y0, x1, y1], fill=color, width=1)


def mark_border(img, l, t, r, b, dashed=False):
    """
    把九宫格切线**画在预览图上**（与画布同色：红=左 黄=上 绿=右 蓝=下）。
    实线 = Unity 实际生效的位置（可能被压缩过）；虚线 = 你设的原始切线（被压缩时用来对照）。
    """
    w, h = img.size
    d = ImageDraw.Draw(img)
    l = max(0, min(int(l), w - 1))
    t = max(0, min(int(t), h - 1))
    r = max(0, min(int(r), w - 1))
    b = max(0, min(int(b), h - 1))
    for (x0, y0, x1, y1), c in zip(((l, 0, l, h - 1), (0, t, w - 1, t),
                                    (w - 1 - r, 0, w - 1 - r, h - 1), (0, h - 1 - b, w - 1, h - 1 - b)),
                                   MARK_COLORS):
        _line_px(d, x0, y0, x1, y1, c + (255,), dashed)
    return img


def _preview_tile(src_path, e, w, h, src_size=None, checker=True):
    """
    单个尺寸的预览：渲染 + 画上切线标注。
    返回 (图, 原始 border, 实际生效 border)。两者不同 = Unity 会压缩（观感与切线位置不一致的原因）。
    """
    im = render_slice(src_path, e, w, h, checker=checker)
    sw, sh = src_size or Image.open(src_path).size
    raw = baked_border(e)
    adj = tuple(int(round(v)) for v in _clamp_border(_adjusted_border(sw, sh, raw, w, h), sw, sh, w, h))
    if e["mode"] in ("sliced", "tiled"):
        if adj != raw:
            mark_border(im, *raw, dashed=True)
        mark_border(im, *adj)
    return im, raw, adj


def preview_sheet(src_path, e, sizes, out_path, label=None):
    """多档尺寸的预览拼成一张竖排 PNG（含切线标注；实线=实际生效，虚线=你设的切线）。"""
    pads, gap, cap_h = 14, 12, 16
    tiles = [_preview_tile(src_path, e, w, h) for w, h in sizes]
    width = pads * 2 + max(im.width for im, _r, _a in tiles)
    height = pads * 2 + 18 + sum(im.height + cap_h for im, _r, _a in tiles) + gap * (len(tiles) - 1)
    sheet = Image.new("RGBA", (width, height), (18, 20, 26, 255))
    d = ImageDraw.Draw(sheet)
    d.text((pads, 2), (label or "") + "  " + ("[border=%s scale=%s mode=%s center=%d]"
                                              % (baked_border(e), e["scale"], e["mode"], e["center"])),
           fill=(210, 190, 140, 255))
    y = pads + 18
    for (im, raw, adj), (w, h) in zip(tiles, sizes):
        sheet.alpha_composite(im, (pads, y))
        d.rectangle([pads - 1, y - 1, pads + im.width, y + im.height], outline=(90, 100, 120, 255))
        note = "%dx%d" % (w, h)
        if e["mode"] in ("sliced", "tiled"):
            note += "  solid=effective %s / dashed=your border %s" % (adj, raw)
            if adj != raw:
                note += "  [COMPRESSED]"
        d.text((pads, y + im.height + 2), note, fill=(170, 180, 200, 255))
        y += im.height + cap_h + gap
    sheet.convert("RGB").save(out_path)
    return out_path


def parse_sizes(text):
    sizes = []
    for part in (text or "").split(","):
        part = part.strip().lower().replace("×", "x")
        if "x" in part:
            a, b = part.split("x", 1)
            try:
                sizes.append((int(float(a)), int(float(b))))
            except ValueError:
                pass
    return sizes or [(120, 32), (240, 56), (420, 160)]


# ---------------------------------------------------------------- 收集素材

def collect(directory, pattern=None):
    """返回 [(name, png_path)]，名字优先取 manifest 原名（PNG 文件名做过非法字符替换）。"""
    manifest = load_manifest(directory)
    out = []
    for fn in sorted(os.listdir(directory), key=str.lower):
        if not fn.lower().endswith(".png"):
            continue
        base = fn[:-4]
        name = None
        for mname in manifest.keys():
            if re.sub(r'[\\/:*?"<>|]', "_", mname).strip() == base:
                name = mname
                break
        if name is None:
            name = base
        if pattern and not re.search(pattern, name, re.IGNORECASE):
            continue
        out.append((name, os.path.join(directory, fn)))
    return out


def default_ini_path(game_dir=None):
    if game_dir:
        return os.path.join(game_dir, "BepInEx", "config", "OpenNestUIKit.slices.ini")
    return os.path.join("ref", "ui_slices.ini")


def game_config_paths(game_dir):
    """库可能读取切片定义的三个位置（对应 UiKitPaths.SlicesFile 的形态判定）：
    BepInEx 端 = BepInEx\\config；MelonLoader 原生端 = UserData；
    桥环境（BepInEx + MelonLoader.Loader）= MLLoader\\UserData。"""
    return [os.path.join(game_dir, "BepInEx", "config", "OpenNestUIKit.slices.ini"),
            os.path.join(game_dir, "UserData", "OpenNestUIKit.slices.ini"),
            os.path.join(game_dir, "MLLoader", "UserData", "OpenNestUIKit.slices.ini")]


def find_game_dirs(env_file="scripts/env.ps1"):
    """从 scripts/env.ps1 读 $GameDir / $ClientGame（工具自己不去猜路径）。"""
    dirs = []
    if not os.path.exists(env_file):
        return dirs
    with open(env_file, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            m = re.match(r'\s*\$(GameDir|ClientGame)\s*=\s*"([^"]+)"', line)
            if m and os.path.isdir(m.group(2)):
                dirs.append(m.group(2))
    return dirs


def deploy(ini_path, game_dirs):
    """把 ini 拷到游戏配置目录（存在 BepInEx/UserData 的那个端）。"""
    written = []
    for gd in game_dirs:
        for dst in game_config_paths(gd):
            parent = os.path.dirname(dst)
            if not os.path.isdir(parent):
                continue
            with open(ini_path, "r", encoding="utf-8") as f:
                data = f.read()
            with open(dst, "w", encoding="utf-8") as f:
                f.write(data)
            written.append(dst)
    return written


def check_defs(directory, store, sizes):
    """
    部署前体检：逐条校验定义与素材是否自洽 ——
    ① 素材目录里有没有这张图（名字拼错会被抓出来）；② border 有没有超出图尺寸（不可能切出来）；
    ③ 在给定尺寸下会不会被 Unity 压缩（提醒，不算错）。有 ①② 时返回 1。
    """
    items = dict(collect(directory))
    hard = 0
    print("校验 %d 条定义（素材目录 %s）\n" % (len(store), directory))
    for name in sorted(store.keys(), key=str.lower):
        e = store[name]
        head = "%-32s tag=%-16s" % (name[:32], e.get("tag") or "-")
        if name not in items:
            print("[!!] %s 素材目录里找不到这张图（名字拼错了？）" % head)
            hard += 1
            continue
        sw, sh = Image.open(items[name]).size
        l, t, r, b = baked_border(e)
        info = "图%dx%d border=%s scale=%g mode=%s center=%d" % (sw, sh, (l, t, r, b), e["scale"], e["mode"], e["center"])
        msgs = []
        if l + r > sw:
            msgs.append("左右 %d+%d > 宽 %d" % (l, r, sw))
        if t + b > sh:
            msgs.append("上下 %d+%d > 高 %d" % (t, b, sh))
        sq = [] if e["mode"] in ("none", "simple") else \
            ["%dx%d" % (w, h) for w, h in sizes if ((l + r) + 8 > w or (t + b) + 8 > h)]
        if msgs:
            print("[!!] %s %s  <- 越界：%s（不可能切出来，请重切）" % (head, info, "；".join(msgs)))
            hard += 1
        else:
            print("[OK] %s %s%s" % (head, info, ("  （%s 会被压缩）" % ", ".join(sq)) if sq else ""))
    print("\n共 %d 条：越界/找不到 = %d" % (len(store), hard))
    return 1 if hard else 0


def selftest(directory, ini_path):
    """无界面自检：确认 ini 读写、九宫格渲染、border 压缩与"放得下"判定都符合预期（含退化情形不崩）。"""
    ok = fail = 0

    def check(label, cond, extra=""):
        nonlocal ok, fail
        if cond:
            ok += 1
            print("PASS  %s %s" % (label, extra))
        else:
            fail += 1
            print("FAIL  %s %s" % (label, extra))

    text = "# c\n[a]\nborder=8,9,10,11\nscale=2\nmode=tiled\ncenter=0\n\n[b]\nborder=1,2,3,4\n"
    store = load_ini_from_text(text)
    check("ini 解析 a.border", store["a"]["border"] == (8, 9, 10, 11), str(store["a"]["border"]))
    check("ini 解析 a.scale/mode/center", (store["a"]["scale"], store["a"]["mode"], store["a"]["center"]) == (2.0, "tiled", 0))
    check("ini 缺省字段补全 b", store["b"]["mode"] == "sliced" and store["b"]["scale"] == 1.0 and store["b"]["center"] == 1)
    check("ini 往返稳定", load_ini_from_text(dump_ini(store))["a"]["border"] == (8, 9, 10, 11))
    check("tag 解析", load_ini_from_text("[x]\nborder=1,2,3,4\ntag=button.primary\n")["x"]["tag"] == "button.primary")
    check("tag=none 视为未标记", load_ini_from_text("[x]\nborder=1,2,3,4\ntag=none\n")["x"]["tag"] == "")
    check("tag 能写回并读回", load_ini_from_text(dump_ini({"a": entry(border=(1, 2, 3, 4), tag="panel")}))["a"]["tag"] == "panel")
    check("未标记不写 tag 行", "tag=" not in dump_ini({"a": entry(border=(1, 2, 3, 4))}))
    check("border÷scale 向下取整", baked_border(store["a"]) == (4, 4, 5, 5), str(baked_border(store["a"])))

    items = collect(directory)
    check("素材收集到 PNG", len(items) >= 1, "%d 张" % len(items))
    check("manifest 作者 border 可读", load_manifest(directory).get("UI Box line", {}).get("border") == (14.0, 14.0, 14.0, 13.0))

    hit = [(n, p) for n, p in items if n == "UI Box line"] or items[:1]
    name, path = hit[0]
    e = entry(border=(14, 14, 14, 13))
    for w, h in ((120, 32), (240, 56)):
        im = render_slice(path, e, w, h, checker=False)
        check("渲染尺寸 %dx%d" % (w, h), im.size == (w, h), str(im.size))
    check("小尺寸 border 会被压缩", _adjusted_border(128, 128, (14, 14, 14, 13), 120, 24)[1] < 14)
    check("大尺寸 border 不压缩", _adjusted_border(128, 128, (14, 14, 14, 13), 420, 160)[1] == 14)
    check("放不下判定 False", _fits(path, e, 24, 24) is False)
    check("放得下判定 True", _fits(path, e, 240, 56) is True)
    e2 = entry(border=(14, 14, 14, 13), mode="none")
    check("mode=none 全透明", render_slice(path, e2, 60, 24, checker=False).getbbox() is None)

    # 退化情形（曾经直接抛 ValueError）：控件比 border 还小 / border=0 / 极小尺寸 / 超小控件
    for label, ent, w, h in (("border 大于控件", e, 120, 32),
                              ("border 远大于控件", e, 12, 8),
                              ("border=0", entry(border=(0, 0, 0, 0)), 120, 32),
                              ("1x1 控件", e, 1, 1),
                              ("height=1", e, 120, 1),
                              ("tiled 小控件", entry(border=(14, 14, 14, 13), mode="tiled"), 20, 10)):
        try:
            im = render_slice(path, ent, w, h, checker=False)
            check("渲染不崩：%s" % label, im.size == (w, h), str(im.size))
        except Exception as ex:
            check("渲染不崩：%s" % label, False, repr(ex))

    # GUI 数学自检（无窗口：Tk 只在内存里建，不 mainloop）——
    # 回归拦住过的真 bug：画布四条切线漏加居中偏移 → "线和图对不上"
    if HAS_TK:
        try:
            import tkinter as _tk
            _root = _tk.Tk()
            _root.withdraw()
            _items = collect(directory)
            _gui = SliceToolGUI(_root, _items, {}, os.path.join(os.path.dirname(ini_path) or ".", "_selftest.ini"))
            _i = next((k for k, (n, _p) in enumerate(_items) if n == "UI Box line"), 0)
            _gui.load(_i)
            for _k, _v in zip(("L", "T", "R", "B"), (14, 14, 14, 13)):
                _gui.vars[_k].set(str(_v))
            _gui.on_field()
            _z, _ox, _oy = _gui.zoom, _gui.off[0], _gui.off[1]
            _w, _h = _gui.img.size
            _c = [_gui.canvas.coords(x) for x in _gui.canvas.find_all()]
            _got2 = (round((_c[2][0] - _ox) / _z), round((_c[3][1] - _oy) / _z),
                     round(_w - (_c[4][0] - _ox) / _z), round(_h - (_c[5][1] - _oy) / _z))
            check("GUI：画布四条切线坐标与 border 一致", _got2 == (14, 14, 14, 13), str(_got2))
            # 反向：按“视觉位置拖拽”应写回同一数值
            _gui.load(_i)
            _gui.selected_edge = 0
            _gui._set_from_pointer(0, _ox + 14 * _z, _oy + 5)
            check("GUI：拖左线到 x=14 处 → 数值回到 14", baked_border(_gui.entry_view())[0] == 14,
                  str(baked_border(_gui.entry_view())))
            # 预览：压缩时画虚线、正常时只画实线
            _e = entry(border=(48, 48, 48, 48))
            _p = [p for n, p in _items if n == "SGRounded"]
            if _p:
                _im, _raw, _adj = _preview_tile(_p[0], _e, 120, 32)
                check("GUI：预览在压缩时仍能画出实/虚线", _adj != _raw and _im.size == (120, 32))
                _im2, _r2, _a2 = _preview_tile(_p[0], _e, 420, 160)
                check("GUI：预览无压缩时线在 48 处", _a2 == _r2 and _im2.convert("RGB").load()[48, 4] == MARK_COLORS[0])
            _root.destroy()
        except Exception as ex:
            check("GUI 数学自检可运行", False, repr(ex))
    else:
        print("（无 tkinter，跳过 GUI 数学自检）")

    print("\n自检：PASS=%d FAIL=%d  素材=%s  ini=%s" % (ok, fail, directory, ini_path))
    return 0 if fail == 0 else 1


def load_ini_from_text(text):
    """与 load_ini 同样的解析，但直接吃文本（自检/测试用）。"""
    import tempfile
    fd, tmp = tempfile.mkstemp(suffix=".ini")
    os.close(fd)
    try:
        with open(tmp, "w", encoding="utf-8") as f:
            f.write(text)
        return load_ini(tmp)
    finally:
        try:
            os.remove(tmp)
        except OSError:
            pass


# ---------------------------------------------------------------- GUI

class SliceToolGUI:
    ZOOM_MAX = 4
    PREVIEW_W = 460
    PREVIEW_H = 300

    def __init__(self, root, items, store, ini_path):
        self.root = root
        self.items = items
        self.store = store
        self.ini_path = ini_path
        self.idx = 0
        self.selected_edge = 0          # 0=L 1=T 2=R 3=B
        self.zoom = 2
        self.img = None
        self.preview_busy = False

        root.title("OpenNestUIKit 切片工具")
        # 按屏幕自适应，避免小屏下右侧预览区被挤掉
        sw_screen = root.winfo_screenwidth()
        sh_screen = root.winfo_screenheight()
        root.geometry("%dx%d+%d+%d" % (min(1500, sw_screen - 60), min(900, sh_screen - 110), 20, 20))

        left = ttk.Frame(root, padding=6)
        left.pack(side="left", fill="y")
        ttk.Label(left, text="素材（双击进 9 宫格预览）").pack(anchor="w")
        self.filter_var = tk.StringVar()
        ent = ttk.Entry(left, textvariable=self.filter_var, width=28)
        ent.pack(fill="x", pady=(0, 4))
        ent.bind("<KeyRelease>", lambda _e: self.refresh_list())
        self.show_mode = tk.StringVar(value="all")
        fr = ttk.Frame(left)
        fr.pack(fill="x", pady=(0, 4))
        for txt, val in (("全部", "all"), ("● 已定义", "defined"), ("○ 未定义", "undefined")):
            ttk.Radiobutton(fr, text=txt, value=val, variable=self.show_mode,
                            command=self.refresh_list).pack(side="left")
        self.tag_filter = tk.StringVar(value="全部 tag")
        self.tag_filter_combo = ttk.Combobox(left, textvariable=self.tag_filter, values=["全部 tag"],
                                             state="readonly")
        self.tag_filter_combo.pack(fill="x", pady=(0, 4))
        self.tag_filter_combo.bind("<<ComboboxSelected>>", lambda _e: self.refresh_list())
        ttk.Label(left, text="● 已定义　○ 未定义（浏览不会自动加进定义）",
                  foreground="#888", wraplength=240).pack(anchor="w", pady=(0, 4))
        self.listbox = tk.Listbox(left, width=32, height=40, exportselection=False)
        self.listbox.pack(fill="y", expand=True)
        self.listbox.bind("<<ListboxSelect>>", self.on_pick_list)
        ttk.Button(left, text="保存 ini（库会热重载）", command=self.save).pack(fill="x", pady=(6, 0))
        ttk.Button(left, text="保存并部署到游戏", command=self.save_and_deploy).pack(fill="x", pady=(2, 0))
        ttk.Button(left, text="删除此条定义（Del）", command=self.delete_current).pack(fill="x", pady=(2, 0))
        ttk.Button(left, text="导出预览 PNG", command=self.export_preview).pack(fill="x", pady=(2, 0))
        ttk.Button(left, text="重置为作者值", command=self.reset_author).pack(fill="x", pady=(2, 0))
        self.count_lbl = ttk.Label(left, text="", foreground="#8c8")
        self.count_lbl.pack(anchor="w", pady=(4, 0))
        ttk.Label(left, text="ini：" + ini_path, wraplength=240, foreground="#888").pack(anchor="w", pady=(6, 0))

        mid = ttk.Frame(root, padding=6)
        mid.pack(side="left", fill="both", expand=True)
        self.canvas = tk.Canvas(mid, width=760, height=620, bg="#1c1f26", highlightthickness=0)
        self.canvas.pack(fill="both", expand=True)
        self.canvas.bind("<Button-1>", self.on_canvas_click)
        self.canvas.bind("<B1-Motion>", self.on_canvas_drag)
        self.canvas.bind("<Configure>", self.on_canvas_resize)
        self.info = ttk.Label(mid, text="", foreground="#aaa")
        self.info.pack(anchor="w", pady=(4, 0))

        right = ttk.Frame(root, padding=6)
        right.pack(side="left", fill="y")
        self.vars = {k: tk.StringVar() for k in ("L", "T", "R", "B", "scale", "mode", "center", "tag")}
        row = 0
        for key in ("L", "T", "R", "B"):
            ttk.Label(right, text={"L": "左 L", "T": "上 T", "R": "右 R", "B": "下 B"}[key]).grid(row=row, column=0, sticky="w")
            sp = ttk.Spinbox(right, from_=0, to=512, width=8, textvariable=self.vars[key], command=self.on_field)
            sp.grid(row=row, column=1, sticky="w")
            sp.bind("<Return>", lambda _e: self.on_field())
            row += 1
        ttk.Label(right, text="scale（border ÷ 它）").grid(row=row, column=0, sticky="w")
        ttk.Spinbox(right, from_=1, to=8, width=8, textvariable=self.vars["scale"], command=self.on_field).grid(row=row, column=1, sticky="w")
        row += 1
        ttk.Label(right, text="mode").grid(row=row, column=0, sticky="w")
        ttk.Combobox(right, values=["sliced", "tiled", "simple", "none"], width=8, textvariable=self.vars["mode"],
                     state="readonly").grid(row=row, column=1, sticky="w")
        self.vars["mode"].trace_add("write", lambda *_a: self.on_field())
        row += 1
        ttk.Checkbutton(right, text="绘制中心 center", variable=self.vars["center"], onvalue="1", offvalue="0",
                        command=self.on_field).grid(row=row, column=0, columnspan=2, sticky="w")
        row += 1
        ttk.Label(right, text="用途标记 tag").grid(row=row, column=0, sticky="w")
        self.tag_combo = ttk.Combobox(right, values=list(TAGS), width=18, textvariable=self.vars["tag"])
        self.tag_combo.grid(row=row, column=1, sticky="w")
        self.tag_combo.bind("<Return>", lambda _e: self.on_field())
        self.tag_combo.bind("<<ComboboxSelected>>", lambda _e: self.on_field())
        row += 1
        ttk.Label(right, text="（这张图是哪种组件的背景，库按它优先挑素材）",
                  foreground="#888", wraplength=440, justify="left").grid(row=row, column=0, columnspan=2, sticky="w")
        row += 1
        ttk.Label(right, text="预览尺寸").grid(row=row, column=0, sticky="w")
        self.size_var = tk.StringVar(value="120x32,240x56,420x160")
        sz = ttk.Entry(right, textvariable=self.size_var, width=20)
        sz.grid(row=row, column=1, sticky="w")
        sz.bind("<Return>", lambda _e: self.refresh_preview())
        row += 1
        ttk.Button(right, text="按当前切线给尺寸", command=self.sizes_from_border).grid(
            row=row, column=0, columnspan=2, sticky="we", pady=(2, 0))
        row += 1
        self.prev_canvas = tk.Canvas(right, width=self.PREVIEW_W, height=self.PREVIEW_H,
                                     bg="#14161c", highlightthickness=0)
        self.prev_canvas.grid(row=row, column=0, columnspan=2, pady=(6, 0))
        row += 1
        self.prev_note = ttk.Label(right, text="", foreground="#bbb", wraplength=440, justify="left")
        self.prev_note.grid(row=row, column=0, columnspan=2, sticky="w")
        row += 1
        ttk.Label(right, text="← → 换图　↑ ↓ 选切线　Shift+方向 微调 1px　Ctrl+S 保存",
                  foreground="#888", wraplength=420).grid(row=row, column=0, columnspan=2, sticky="w", pady=(8, 0))

        root.bind("<Left>", lambda _e: self.step(-1))
        root.bind("<Right>", lambda _e: self.step(1))
        root.bind("<Up>", lambda _e: self.pick_edge(-1))
        root.bind("<Down>", lambda _e: self.pick_edge(1))
        root.bind("<Shift-Left>", lambda _e: self.nudge(-1))
        root.bind("<Shift-Right>", lambda _e: self.nudge(1))
        root.bind("<Shift-Up>", lambda _e: self.nudge(-1))
        root.bind("<Shift-Down>", lambda _e: self.nudge(1))
        root.bind("<Control-s>", lambda _e: self.save())
        # 删除：只绑在"非输入框"的控件上，避免在过滤框里按退格误删
        self.listbox.bind("<Delete>", lambda _e: self.delete_current())
        self.listbox.bind("<BackSpace>", lambda _e: self.delete_current())
        self.canvas.bind("<Delete>", lambda _e: self.delete_current())
        self.canvas.bind("<BackSpace>", lambda _e: self.delete_current())

        self.refresh_list()
        if self.items:
            self.listbox.selection_set(0)
            self.load(0)

    # ---- 列表 ----
    def filtered(self):
        pat = self.filter_var.get().strip()
        mode = self.show_mode.get()
        tf = self.tag_filter.get() if hasattr(self, "tag_filter") else "全部 tag"
        out = []
        for i, (n, _p) in enumerate(self.items):
            if pat and not re.search(pat, n, re.IGNORECASE):
                continue
            defined = n in self.store
            if mode == "defined" and not defined:
                continue
            if mode == "undefined" and defined:
                continue
            if tf not in ("", "全部 tag"):
                tag = (self.store.get(n, {}) or {}).get("tag") or ""
                if tf == "(未标记)":
                    if tag:
                        continue
                elif tag != tf:
                    continue
            out.append(i)
        return out

    def refresh_list(self):
        cur = getattr(self, "idx", None)
        self.visible = self.filtered()
        self.listbox.delete(0, "end")
        for i in self.visible:
            name = self.items[i][0]
            tag = (self.store.get(name, {}) or {}).get("tag") or ""
            self.listbox.insert("end", ("● " if name in self.store else "○ ") + name + (("  [" + tag + "]") if tag else ""))
        if cur in self.visible:
            pos = self.visible.index(cur)
            self.listbox.selection_clear(0, "end")
            self.listbox.selection_set(pos)
            self.listbox.see(pos)
        # 下拉里列出文件里出现过的 tag（含手写的自定义 tag）
        if hasattr(self, "tag_combo"):
            seen = []
            for e in self.store.values():
                t = (e or {}).get("tag") or ""
                if t and t not in seen:
                    seen.append(t)
            seen.sort()
            vals = list(TAGS) + [t for t in seen if t not in TAGS]
            self.tag_combo.config(values=vals)
            self.tag_filter_combo.config(values=["全部 tag", "(未标记)"] + seen)
        if hasattr(self, "count_lbl"):
            tagged = sum(1 for e in self.store.values() if (e or {}).get("tag"))
            self.count_lbl.config(text="已定义 %d 条（已标记 %d）/ 素材 %d 张" % (len(self.store), tagged, len(self.items)))

    def on_pick_list(self, _e=None):
        sel = self.listbox.curselection()
        if not sel:
            return
        self.load(self.visible[sel[0]])

    def step(self, d):
        if not self.visible:
            return
        cur = self.idx
        pos = self.visible.index(cur) if cur in self.visible else 0
        pos = max(0, min(len(self.visible) - 1, pos + d))
        self.listbox.selection_clear(0, "end")
        self.listbox.selection_set(pos)
        self.listbox.see(pos)
        self.load(self.visible[pos])

    # ---- 载入/字段 ----
    def entry_view(self):
        """当前应显示的定义：有定义用定义，没有则用**作者原值**（纯展示，不写入 store）。"""
        if self.name in self.store:
            return self.store[self.name]
        man = load_manifest(os.path.dirname(self.path))
        return entry(border=(man.get(self.name, {}).get("border") or (0, 0, 0, 0)))

    def cur_entry(self):
        """要改的条目：**用户真的动了字段才把这条加进定义**（光浏览不写文件）。"""
        if self.name not in self.store:
            self.store[self.name] = self.entry_view()
        return self.store[self.name]

    def delete_current(self):
        """删除当前图的切片定义（回到"未定义"= 库走自动选材；素材本身不动）。"""
        name = getattr(self, "name", None)
        if not name:
            return
        if name in self.store:
            del self.store[name]
            print("removed:", name)
        self.refresh_list()
        self.load(self.idx)

    def load(self, idx):
        self.idx = idx
        self.name, self.path = self.items[idx]
        e = self.entry_view()
        self.img = Image.open(self.path).convert("RGBA")
        # 字段 set 会触发 mode 的 trace → 载入期间锁住 on_field（否则会被当成"用户在编辑"）
        self._loading = True
        try:
            b = e["border"]
            self.vars["L"].set("%g" % b[0])
            self.vars["T"].set("%g" % b[1])
            self.vars["R"].set("%g" % b[2])
            self.vars["B"].set("%g" % b[3])
            self.vars["scale"].set("%g" % e["scale"])
            self.vars["center"].set(str(e["center"]))
            self.vars["tag"].set(e.get("tag") or "")
            self.vars["mode"].set(e["mode"])
        finally:
            self._loading = False
        self.redraw()
        self.refresh_preview()

    def on_field(self):
        if getattr(self, "_loading", False):
            return
        e = self.cur_entry()
        try:
            b = [float(self.vars[k].get()) for k in ("L", "T", "R", "B")]
        except ValueError:
            return
        b = self._clamp(b)
        for k, v in zip(("L", "T", "R", "B"), b):
            self.vars[k].set("%g" % v)
        e["border"] = tuple(b)
        try:
            e["scale"] = max(0.01, float(self.vars["scale"].get()))
            e["mode"] = self.vars["mode"].get() or "sliced"
            e["center"] = int(self.vars["center"].get() or 0)
            e["tag"] = self.vars["tag"].get().strip()
        except ValueError:
            pass
        self.redraw()
        self.refresh_preview()
        self.refresh_list()

    def _clamp(self, border):
        """把 border 收进图尺寸内：单边 ≤ 图宽/高，且 左+右 ≤ 宽、上+下 ≤ 高（避免切出无效值）。"""
        if self.img is None:
            return border
        w, h = self.img.size
        l = max(0.0, min(border[0], w))
        t = max(0.0, min(border[1], h))
        r = max(0.0, min(border[2], w - l))
        b = max(0.0, min(border[3], h - t))
        return l, t, r, b

    def current_border(self):
        return baked_border(self.entry_view())

    # ---- 画布 ----
    def canvas_size(self):
        """取画布**实际**尺寸（布局前 winfo 可能为 1，回退到请求尺寸 760×620）。"""
        cw = max(self.canvas.winfo_width(), self.canvas.winfo_reqwidth())
        ch = max(self.canvas.winfo_height(), self.canvas.winfo_reqheight())
        return max(200, cw), max(200, ch)

    def on_canvas_resize(self, ev):
        if (ev.width, ev.height) != getattr(self, "_csize", None):
            self._csize = (ev.width, ev.height)
            self.redraw()

    def redraw(self):
        c = self.canvas
        c.delete("all")
        if self.img is None:
            return
        w, h = self.img.size
        cw, ch = self.canvas_size()
        self.zoom = max(1, min(self.ZOOM_MAX, cw // max(1, w), ch // max(1, h)))
        z = self.zoom
        self.off = ((cw - w * z) // 2, (ch - h * z) // 2)
        self.tkimg = _photo(self.img.resize((w * z, h * z), Image.NEAREST))
        c.create_image(self.off[0], self.off[1], anchor="nw", image=self.tkimg)
        c.create_rectangle(self.off[0], self.off[1], self.off[0] + w * z, self.off[1] + h * z, outline="#666")
        b = self.current_border()
        colors = ["#ff6b6b", "#ffd166", "#6bff9e", "#6bb6ff"]
        # ⚠ 四条线都必须加上图片居中偏移 off（曾经左/上两条漏了，导致线画到图外 → "线和图对不上"）
        ox, oy = self.off
        lines = [(ox + b[0] * z, oy, ox + b[0] * z, oy + h * z),
                 (ox, oy + b[1] * z, ox + w * z, oy + b[1] * z),
                 (ox + (w - b[2]) * z, oy, ox + (w - b[2]) * z, oy + h * z),
                 (ox, oy + (h - b[3]) * z, ox + w * z, oy + (h - b[3]) * z)]
        for i, (x0, y0, x1, y1) in enumerate(lines):
            c.create_line(x0, y0, x1, y1, fill=colors[i], width=3 if i == self.selected_edge else 1)
        self.info.config(text="%s  %dx%d  缩放 x%d   border(烤后)=%s   [选中：%s]   %s"
                              % (self.name, w, h, z, b, "LTRB"[self.selected_edge],
                                 "已定义" if self.name in self.store else "未定义（显示作者原值）"))

    def _pick_edge(self, mx, my):
        if self.img is None:
            return 0
        w, h = self.img.size
        z = self.zoom
        b = self.current_border()
        cands = [abs(mx - (self.off[0] + b[0] * z)),
                 abs(my - (self.off[1] + b[1] * z)),
                 abs(mx - (self.off[0] + (w - b[2]) * z)),
                 abs(my - (self.off[1] + (h - b[3]) * z))]
        return cands.index(min(cands))

    def on_canvas_click(self, ev):
        self.selected_edge = self._pick_edge(ev.x, ev.y)
        self.redraw()

    def on_canvas_drag(self, ev):
        self._set_from_pointer(self.selected_edge, ev.x, ev.y)

    def _set_from_pointer(self, edge, mx, my):
        if self.img is None:
            return
        w, h = self.img.size
        z = self.zoom
        s = self.entry_view()["scale"] or 1.0
        if edge == 0:
            v = (mx - self.off[0]) / z * s
        elif edge == 1:
            v = (my - self.off[1]) / z * s
        elif edge == 2:
            v = (w - (mx - self.off[0]) / z) * s
        else:
            v = (h - (my - self.off[1]) / z) * s
        key = "LTRB"[edge]
        self.vars[key].set("%g" % max(0, round(v)))
        self.on_field()

    def pick_edge(self, d):
        self.selected_edge = (self.selected_edge + d) % 4
        self.redraw()

    def nudge(self, d):
        key = "LTRB"[self.selected_edge]
        e = self.entry_view()
        s = e["scale"] or 1.0
        raw = e["border"][self.selected_edge]
        step = 1.0 * s
        self.vars[key].set("%g" % max(0, round(raw + d * step)))
        self.on_field()

    # ---- 预览 ----
    def refresh_preview(self):
        if self.img is None:
            return
        e = self.entry_view()          # 只读：浏览不应把这张图加进定义
        sw, sh = self.img.size
        sizes = parse_sizes(self.size_var.get())
        tiles = [_preview_tile(self.path, e, w, h, (sw, sh)) for w, h in sizes]

        # 竖排拼图：每档一行，都画出来（不因宽度被截掉）；超面板时整体等比缩小
        pads, gap, cap_h = 8, 10, 4
        sheet_h = pads * 2 + sum(im.height + cap_h for im, _r, _a in tiles) + gap * (len(tiles) - 1)
        sheet_w = max(im.width for im, _r, _a in tiles) + pads * 2
        sheet = Image.new("RGBA", (max(1, sheet_w), max(1, sheet_h)), (18, 20, 26, 255))
        d = ImageDraw.Draw(sheet)
        y = pads
        any_compress = False
        for (im, raw, adj), (w, h) in zip(tiles, sizes):
            sheet.alpha_composite(im, (pads, y))
            d.rectangle([pads - 1, y - 1, pads + im.width, y + im.height], outline=(90, 100, 120, 255))
            d.text((pads, y + im.height + 1), "%dx%d" % (w, h), fill=(170, 180, 200, 255))
            if adj != raw:
                any_compress = True
            y += im.height + cap_h + gap
        f = min(1.0, self.PREVIEW_W / sheet.width, self.PREVIEW_H / sheet.height)
        if f < 1.0:
            sheet = sheet.resize((max(1, int(sheet.width * f)), max(1, int(sheet.height * f))), Image.NEAREST)
        self.prev_photo = _photo(sheet)
        self.prev_canvas.delete("all")
        self.prev_canvas.create_image(0, 0, anchor="nw", image=self.prev_photo)

        notes = []
        bleed = self._over_border()
        if bleed:
            notes.append("⚠ " + bleed)
        for (im, raw, adj), (w, h) in zip(tiles, sizes):
            if e["mode"] in ("none", "simple"):
                notes.append("%dx%d 原图拉伸" % (w, h))
            elif adj == raw:
                notes.append("%dx%d ✅ 无压缩（切线生效）" % (w, h))
            else:
                diff = "、".join("%s %d→%d" % (n, a, b)
                                for n, a, b in zip(("左", "上", "右", "下"), raw, adj) if a != b)
                notes.append("%dx%d ⚠ 压缩×%.2f（%s）" % (w, h, self._compress_k(sw, sh, raw, w, h), diff))
        if any_compress:
            notes.insert(0, "实线=实际生效　虚线=你设的切线（差异即被压缩）")
        self.prev_note.config(text="\n".join(notes))

    @staticmethod
    def _compress_k(sw, sh, border, dw, dh):
        l, t, r, b = border
        k = 1.0
        if l + r > 0:
            k = min(k, dw / (l + r))
        if t + b > 0:
            k = min(k, dh / (t + b))
        return min(1.0, k)

    def sizes_from_border(self):
        """按当前切线给一组合适的预览尺寸：正好放得下 / 宽裕一点 / 大面板。"""
        l, t, r, b = baked_border(self.entry_view())
        self.size_var.set("%dx%d,%dx%d,%dx%d" % (
            l + r + 40, t + b + 16, l + r + 120, t + b + 60, l + r + 320, t + b + 160))
        self.refresh_preview()

    def _over_border(self):
        """border 是否超出图尺寸（手工改 ini 或旧版本可能留下这种无效值）。"""
        if self.img is None:
            return ""
        w, h = self.img.size
        l, t, r, b = baked_border(self.entry_view())
        msg = []
        if l + r > w:
            msg.append("左右 %d+%d > 宽 %d" % (l, r, w))
        if t + b > h:
            msg.append("上下 %d+%d > 高 %d" % (t, b, h))
        return ("border 超出图尺寸（%s），会被压得很难看" % "；".join(msg)) if msg else ""

    def export_preview(self):
        e = self.entry_view()
        out = filedialog.asksaveasfilename(defaultextension=".png", initialfile=self.name + "_preview.png",
                                           filetypes=[("PNG", "*.png")])
        if not out:
            return
        preview_sheet(self.path, e, parse_sizes(self.size_var.get()), out, label=self.name)
        messagebox.showinfo("已导出", out)

    def reset_author(self):
        """把当前图恢复为作者原值（保留一个条目，方便继续微调）。想彻底不要就用"删除此条定义"。"""
        man = load_manifest(os.path.dirname(self.path))
        border = (man.get(self.name, {}).get("border") or (0, 0, 0, 0))
        self.store[self.name] = entry(border=border)
        self.load(self.idx)
        self.refresh_list()

    def save(self):
        with open(self.ini_path, "w", encoding="utf-8") as f:
            f.write(dump_ini(self.store))
        print("saved:", self.ini_path, len(self.store), "entries")
        messagebox.showinfo("已保存", "%s\n（游戏内 2 秒内自动热重载）" % self.ini_path)

    def save_and_deploy(self):
        self.save()
        dirs = find_game_dirs()
        if not dirs:
            messagebox.showwarning("没找到游戏目录", "请检查 scripts/env.ps1 的 $GameDir / $ClientGame")
            return
        written = deploy(self.ini_path, dirs)
        if not written:
            messagebox.showwarning("未部署", "目标游戏目录里既没有 BepInEx\\config 也没有 UserData")
            return
        messagebox.showinfo("已部署", "\n".join(written))


def _photo(img):
    from PIL import ImageTk
    return ImageTk.PhotoImage(img)


def _fits(path, e, dw, dh):
    if e["mode"] in ("none", "simple"):
        return True
    l, t, r, b = baked_border(e)
    return (l + r) + 8 <= dw and (t + b) + 8 <= dh


# ---------------------------------------------------------------- CLI

def main():
    # 控制台可能是 GBK：遇到打不出的字符就替掉，绝不因为一个符号把工具弄崩
    try:
        sys.stdout.reconfigure(errors="replace")
    except Exception:
        pass
    ap = argparse.ArgumentParser(description="OpenNestUIKit 九宫格切片工具", add_help=True)
    ap.add_argument("--dir", default="ref/ui_sprites_all", help="PNG + ui_sprites_manifest.txt 所在目录")
    ap.add_argument("--match", help="按名字正则过滤（只处理匹配的图）")
    ap.add_argument("--ini", help="要读写的切片定义 ini（默认 ref/ui_slices.ini）")
    ap.add_argument("--out", help="输出 ini 路径（带 .png 后缀时表示预览图路径）")
    ap.add_argument("--list", action="store_true", help="列出图与作者 border / 当前定义")
    ap.add_argument("--template", action="store_true", help="把匹配到的图按作者原值写成模板 ini")
    ap.add_argument("--set", action="append", default=[], metavar="NAME=L,T,R,B[,scale[,mode[,center[,tag]]]]",
                    help="设置/修改某条定义（可多次）")
    ap.add_argument("--tag", action="append", default=[], metavar="NAME=TAG",
                    help="只打用途标记（例：--tag \"SGRounded=panel\"；TAG 给空串=取消标记）")
    ap.add_argument("--tags", action="store_true", help="列出已用的 tag 与对应素材")
    ap.add_argument("--remove", action="append", default=[], metavar="NAME",
                    help="删除某条定义（可多次；素材文件不动）")
    ap.add_argument("--remove-match", metavar="正则",
                    help="删除名字匹配该正则的全部定义（例：排除不是组件的素材）")
    ap.add_argument("--preview", metavar="NAME", help="导出该图的 9 宫格预览 PNG")
    ap.add_argument("--sizes", default="120x32,240x56,420x160", help="预览尺寸，逗号分隔")
    ap.add_argument("--selftest", action="store_true", help="无界面自检（渲染/压缩/ini 往返/GUI 坐标）")
    ap.add_argument("--check", action="store_true", help="部署前体检：素材是否存在、border 是否越界、哪些尺寸会被压缩")
    ap.add_argument("--deploy", nargs="?", const="", metavar="GAMEDIR",
                    help="把 ini 拷到游戏配置目录（不给参数则从 scripts/env.ps1 发现$GameDir/$ClientGame）")
    ap.add_argument("--env", default="scripts/env.ps1", help="用于 --deploy 自动发现游戏路径的 env 脚本")
    ap.add_argument("--gui", action="store_true", help="打开图形界面")
    args = ap.parse_args()

    # 相对路径统一以仓库根为基准（便于在任意目录下运行）
    args.dir = repo_path(args.dir)
    if args.ini:
        args.ini = repo_path(args.ini)
    if args.out and args.out.lower().endswith(".ini"):
        args.out = repo_path(args.out)
    args.env = repo_path(args.env)

    if args.selftest:
        return selftest(args.dir, args.ini or default_ini_path())

    items = collect(args.dir, args.match)
    ini_path = args.ini or default_ini_path()
    out_path = args.out or ini_path
    store = load_ini(ini_path)

    if args.check:
        rc = check_defs(args.dir, store, parse_sizes(args.sizes))
        if args.deploy is None:
            return rc

    # 图形界面（推荐用法）
    if args.gui:
        if not HAS_TK:
            print("没有 tkinter，无法开界面（用命令行模式）")
            return 1
        root = tk.Tk()
        SliceToolGUI(root, items, store, out_path)
        root.mainloop()
        return 0

    if args.list:
        man = load_manifest(args.dir)
        print("%-40s %-22s %-22s %s" % ("name", "author border", "current def", "tag"))
        for name, _p in items:
            a = man.get(name, {}).get("border")
            e = store.get(name)
            cur = ("border=%s scale=%g mode=%s center=%d" % (e["border"], e["scale"], e["mode"], e["center"])) if e else "-"
            print("%-40s %-22s %-22s %s" % (name[:40], str(a), cur, (e or {}).get("tag") or "-"))
        print("\n共 %d 张；ini=%s" % (len(items), ini_path))
        return 0

    if args.tags:
        by_tag = {}
        for n, e in store.items():
            t = (e or {}).get("tag") or "(未标记)"
            by_tag.setdefault(t, []).append(n)
        for t in sorted(by_tag, key=lambda x: (x == "(未标记)", x)):
            names = sorted(by_tag[t], key=str.lower)
            print("%-18s %d 条：%s" % (t, len(names), ", ".join(names)))
        print("\n共 %d 条定义；命令：库按 tag 优先为对应控件挑素材（panel/button/dialog/separator/input/tab/row/frame）" % len(store))
        return 0

    if args.template:
        man = load_manifest(args.dir)
        for name, _p in items:
            store.setdefault(name, entry(border=man.get(name, {}).get("border") or (0, 0, 0, 0)))
        os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(dump_ini(store))
        print("模板已写出：%s（%d 条）" % (out_path, len(store)))
        return 0

    for spec in args.set:
        if "=" not in spec:
            print("跳过（格式应为 NAME=L,T,R,B）：", spec)
            continue
        name, val = spec.split("=", 1)
        name = name.strip()
        parts = [p.strip() for p in val.split(",")]
        try:
            border = tuple(float(p) for p in parts[:4]) if len(parts) >= 4 else None
        except ValueError:
            border = None
        if border is None:
            print("跳过（border 解析失败）：", spec)
            continue
        cur = store.setdefault(name, entry())
        cur["border"] = border
        if len(parts) >= 5:
            cur["scale"] = max(0.01, float(parts[4]))
        if len(parts) >= 6:
            cur["mode"] = parts[5].lower()
        if len(parts) >= 7:
            cur["center"] = int(float(parts[6]))
        if len(parts) >= 8:
            cur["tag"] = parts[7].strip()
        print("set %s → border=%s scale=%g mode=%s center=%d" % (name, cur["border"], cur["scale"], cur["mode"], cur["center"]))

    for spec in args.remove:
        gone = [k for k in list(store.keys()) if k.lower() == spec.strip().lower()]
        for k in gone:
            del store[k]
            print("removed: %s" % k)
        if not gone:
            print("未找到该定义（名字要写全）：%s" % spec)

    for spec in args.tag:
        if "=" not in spec:
            print("跳过（格式应为 NAME=TAG）：", spec)
            continue
        name, tag = spec.split("=", 1)
        name, tag = name.strip(), tag.strip()
        if name not in store:
            print("跳过（这条还没有定义，先切再打标记）：", name)
            continue
        store[name]["tag"] = tag
        print("tag %s → %s" % (name, tag or "(取消标记)"))

    if args.remove_match:
        rx = re.compile(args.remove_match, re.IGNORECASE)
        gone = [k for k in list(store.keys()) if rx.search(k)]
        for k in gone:
            del store[k]
        print("removed(match '%s')：%d 条%s" % (args.remove_match, len(gone),
              ("  " + ", ".join(gone[:10])) if gone else ""))

    if args.set or args.remove or args.remove_match or args.tag:
        os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(dump_ini(store))
        print("已写出：%s（%d 条）" % (out_path, len(store)))
        if args.preview is None and args.deploy is None:
            return 0

    if args.preview:
        hit = [(n, p) for n, p in items if n.lower() == args.preview.lower()] or \
              [(n, p) for n, p in items if args.preview.lower() in n.lower()]
        if not hit:
            print("找不到图：", args.preview)
            return 1
        name, path = hit[0]
        e = store.get(name) or entry(border=load_manifest(args.dir).get(name, {}).get("border") or (0, 0, 0, 0))
        out = args.out if (args.out and args.out.lower().endswith(".png")) else ("%s_preview.png" % re.sub(r"\W+", "_", name))
        preview_sheet(path, e, parse_sizes(args.sizes), out, label=name)
        print("预览已导出：%s" % out)
        print("  border(烤后)=%s scale=%g mode=%s center=%d" % (baked_border(e), e["scale"], e["mode"], e["center"]))
        return 0

    if args.template:
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(dump_ini(store))
        print("已写出：%s（%d 条）" % (out_path, len(store)))
        return 0

    # 部署到游戏配置目录（可选，最后做）
    if args.deploy is not None:
        src = out_path if os.path.exists(out_path) else ini_path
        if not os.path.exists(src):
            print("没有可部署的 ini：", src)
            return 1
        dirs = [args.deploy] if args.deploy else find_game_dirs(args.env)
        if not dirs:
            print("没找到游戏目录（给 --deploy <GAMEDIR> 或检查 %s）" % args.env)
            return 1
        w = deploy(src, dirs)
        for p in w:
            print("已部署：%s" % p)
        if not w:
            print("目标游戏目录里既没有 BepInEx\\config 也没有 UserData：", dirs)
            return 1
        return 0

    ap.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())

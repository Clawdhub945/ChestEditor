# -*- coding: utf-8 -*-
"""D3 拆分 codemod v2：DragonService / ChestService -> partial 子文件（纯移动，零逻辑改动）。

约定（v1 的教训）：find_line 一律返回**1-based 行号**；切分区间一律 1-based 闭区间；
接缝断言用**含缩进的整行精确匹配**（v1 的 `.strip()` 断言没能发现 off-by-one）。

- 每个分件 = 统一 using 头 + `internal static partial class` + `{` + 区段原文 + `}`。
- 唯一改动：类声明行加 `partial`。
- 守恒校验：各分件非空行（strip 后）多重集之和 == 原类体非空行多重集。
- 原文件删除。全部 UTF-8 无 BOM + LF 原子写。
"""
import os
from collections import Counter

ROOT = r"C:\AI\mod\ChestEditor"

def read_lines(path):
    with open(path, "r", encoding="utf-8") as f:
        return f.read().split("\n")

def write_file(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    os.replace(tmp, path)

def find_line(lines, needle):
    """返回 1-based 行号（第一个包含 needle 的行）。"""
    for i, l in enumerate(lines):
        if needle in l:
            return i + 1
    raise SystemExit(f"ANCHOR NOT FOUND: {needle!r}")

def at(lines, ln):
    """1-based 取行。"""
    return lines[ln - 1]

def body(lines, a, b):
    """1-based 闭区间 [a, b]，剥掉首尾空行。"""
    seg = lines[a - 1 : b]
    while seg and seg[0].strip() == "": seg.pop(0)
    while seg and seg[-1].strip() == "": seg.pop()
    return "\n".join(seg) + "\n" if seg else ""

def header(lines, class_ln, class_decl_new):
    """1..class_ln（含类声明行，打补丁为 partial）+ '{'。"""
    seg = lines[:class_ln]
    seg[-1] = class_decl_new
    return "\n".join(seg) + "\n{\n"

def nonblank_counter(text):
    ls = text.split("\n")
    i = ls.index("{")
    assert ls[-2] == "}" and ls[-1] == "", "part must end with '}'"
    return Counter(l.strip() for l in ls[i + 1 : -2] if l.strip())

def check_conservation(parts, lines, class_ln, close_ln, tag):
    orig = Counter(l.strip() for l in lines[class_ln + 1 : close_ln - 1] if l.strip())
    tot = Counter()
    for t in parts.values():
        tot += nonblank_counter(t)
    assert tot == orig, f"{tag} conservation failed: +{tot - orig} -{orig - tot}"
    return sum(orig.values())

# ================= DragonService =================
p = os.path.join(ROOT, "Game", "DragonService.cs")
L = read_lines(p)
assert len(L) == 1233, f"DragonService line count changed: {len(L)}"  # 1232 行 + 尾空

C_CLASS = 14
C_OPEN  = 15
C_BAG   = find_line(L, "// ====== 龙素材背包")            # 17
C_SUM   = find_line(L, "// ====== 召唤龙 ======")          # 467
C_WJDOC = find_line(L, "把反射读取到的任意值写成 JSON 值") - 1  # 508（/// <summary>）
C_WJEND = find_line(L, "default: w.WriteStringValue(value.GetType().Name); break;") + 2  # 531（方法收口 '    }'）
C_SUM2  = find_line(L, "internal static string SummonDragon")           # 534
C_SOUL  = find_line(L, "// ====== 龙魂列表读取 ======")     # 612
C_SOULP = find_line(L, "// ====== 龙魂强化持久化 ======")   # 755
C_ENT   = find_line(L, "搜索地图上的龙实体 GameObject") - 1  # 864（/// <summary>）
C_BAG2  = find_line(L, "private static MethodInfo? _dragonAddMethod;")   # 1148
C_CLOSE = 1232

assert at(L, C_CLASS).rstrip() == "internal static class DragonService"
assert at(L, C_OPEN).rstrip() == "{"
assert at(L, C_BAG).rstrip() == "    // ====== 龙素材背包 (Game.w.dragon_stuff_bag) ======"
assert at(L, C_SUM).rstrip() == "    // ====== 召唤龙 ======"
assert at(L, C_WJDOC).rstrip() == "    /// <summary>"
assert at(L, C_WJEND).rstrip() == "    }"
assert at(L, C_SUM2).rstrip() == "    internal static string SummonDragon(int dragonStuffId, int[]? natureIds = null)"
assert at(L, C_SOUL).rstrip() == "    // ====== 龙魂列表读取 ======"
assert at(L, C_SOULP).rstrip() == "    // ====== 龙魂强化持久化 ======"
assert at(L, C_ENT).rstrip() == "    /// <summary>"
assert at(L, C_BAG2).rstrip() == "    private static MethodInfo? _dragonAddMethod;"
assert at(L, C_CLOSE).rstrip() == "}"

H = header(L, C_CLASS, "internal static partial class DragonService")

dragon_parts = {
    "DragonService.cs":          H + body(L, C_WJDOC, C_WJEND)                 + "}\n",  # 共享 WriteJsonValue
    "DragonService.Bag.cs":      H + body(L, C_BAG, C_SUM - 1) + body(L, C_BAG2, C_CLOSE - 1) + "}\n",
    "DragonService.Summon.cs":   H + body(L, C_SUM, C_WJDOC - 1) + body(L, C_WJEND + 1, C_SOUL - 1) + "}\n",
    "DragonService.Soul.cs":     H + body(L, C_SOUL, C_ENT - 1)                + "}\n",
    "DragonService.Entities.cs": H + body(L, C_ENT, C_BAG2 - 1)                + "}\n",
}
nb = check_conservation(dragon_parts, L, C_CLASS, C_CLOSE, "DragonService")
for name, text in dragon_parts.items():
    write_file(os.path.join(ROOT, "Game", "Dragon", name), text)
os.remove(p)
print(f"DragonService -> {len(dragon_parts)} parts OK (nonblank {nb})")

# ================= ChestService =================
p = os.path.join(ROOT, "Game", "ChestService.cs")
L = read_lines(p)
assert len(L) == 865, f"ChestService line count changed: {len(L)}"  # 864 行 + 尾空

C_CLASS  = 17
C_OPEN   = 18
C_SCAN   = find_line(L, "// ====== 核心逻辑 ======")                       # 209
C_LOC    = find_line(L, "internal static void LocateFacility")             # 425
C_TEMP   = find_line(L, "// ====== 永恒神殿（驯龙→龙素材面板） ======")       # 510
C_PLAN   = find_line(L, "internal static string GetFiltersJson")           # 633
C_LOCSEC = find_line(L, "// ====== 定位 ======")                            # 755
C_JSON   = find_line(L, "// ====== JSON 构建（带 500ms TTL 缓存，操作后 Invalidate） ======")  # 766
C_CLOSE  = 864

assert at(L, C_CLASS).rstrip() == "internal static class ChestService"
assert at(L, C_OPEN).rstrip() == "{"
assert at(L, C_SCAN).rstrip() == "    // ====== 核心逻辑 ======"
assert at(L, C_LOC).rstrip() == "    internal static void LocateFacility(float targetX, float targetY)"
assert at(L, C_TEMP).rstrip() == "    // ====== 永恒神殿（驯龙→龙素材面板） ======"
assert at(L, C_PLAN).rstrip() == "    internal static string GetFiltersJson() => JsonBuilder.Build(w =>"
assert at(L, C_LOCSEC).rstrip() == "    // ====== 定位 ======"
assert at(L, C_JSON).rstrip() == "    // ====== JSON 构建（带 500ms TTL 缓存，操作后 Invalidate） ======"
assert at(L, C_CLOSE).rstrip() == "}"

H = header(L, C_CLASS, "internal static partial class ChestService")

chest_parts = {
    "ChestService.cs":        H + body(L, C_OPEN + 2, C_SCAN - 1) + "}\n",                 # 结构体/状态/物品增删
    "ChestService.Scan.cs":   H + body(L, C_SCAN, C_LOC - 1) + "}\n",                      # 扫描与 Bag 读取器
    "ChestService.Locate.cs": H + body(L, C_LOC, C_TEMP - 1) + body(L, C_LOCSEC, C_JSON - 1) + "}\n",
    "ChestService.Temple.cs": H + body(L, C_TEMP, C_PLAN - 1) + "}\n",
    "ChestService.Plan.cs":   H + body(L, C_PLAN, C_LOCSEC - 1) + "}\n",                   # 计划库存 + 筛选
    "ChestService.Json.cs":   H + body(L, C_JSON, C_CLOSE - 1) + "}\n",
}
nb = check_conservation(chest_parts, L, C_CLASS, C_CLOSE, "ChestService")
for name, text in chest_parts.items():
    write_file(os.path.join(ROOT, "Game", "Chest", name), text)
os.remove(p)
print(f"ChestService -> {len(chest_parts)} parts OK (nonblank {nb})")

print("OK")

#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ChestEditor 数据表生成器
========================

把原先硬编码在 C# 里的游戏数据表提取为嵌入 JSON 资源（Data/*.json）。

数据来源优先级
--------------
1. 官方配置表 C:/AI/yuanma/json_data/*.json  —— 权威来源，可随游戏版本重跑
2. 现有 C# 代码                             —— 仅用于【代码与官方不一致】的表，
                                              以保持运行时行为 100% 不变

生成产物（均为「数组的数组」紧凑格式，便于流式解析与 git diff）
--------------------------------------------------------------
  Data/items.json          [[id, name, stuff_type], ...]        来自 stuff.json + stuff2.json
  Data/soldier_types.json  [[id, name], ...]                    来自 soldier_equip.json + 名称表
  Data/chest_filters.json  [[id, name, enabled01], ...]         来自 ChestService.cs（名称手写，见报告）
  Data/dragon_types.json   [[name, cn, baseId], ...]            来自 DragonService.cs
  Data/dragon_natures.json [[id, name], ...]                    来自 dragon_nature.json

用法
----
  python _tools/generate_data_tables.py            # 生成并校验
  python _tools/generate_data_tables.py --check    # 只校验不写文件
"""

import argparse
import json
import os
import re
import sys

# ----------------------------------------------------------------------------- 路径

GAME_DATA = os.environ.get("CHESTEDITOR_GAME_DATA", r"C:\AI\yuanma\json_data")
HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(HERE)
DATA_OUT = os.path.join(PROJ, "Data")

# ----------------------------------------------------------------------------- 工具


def load_game_json(name):
    path = os.path.join(GAME_DATA, name)
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def read_source(rel):
    with open(os.path.join(PROJ, rel), encoding="utf-8") as f:
        return f.read()


def dump_rows(path, rows, check_only):
    """写紧凑 JSON：一行一条记录。"""
    text = "[\n" + ",\n".join(
        "  " + json.dumps(r, ensure_ascii=False, separators=(",", ":")) for r in rows
    ) + "\n]\n"
    if check_only:
        return text
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return text


def diff_sets(label, expected, actual):
    """expected/actual 为 dict；打印差异，返回是否一致。"""
    only_e = {k: expected[k] for k in expected if k not in actual}
    only_a = {k: actual[k] for k in actual if k not in expected}
    changed = {k: (expected[k], actual[k]) for k in expected if k in actual and expected[k] != actual[k]}
    ok = not (only_e or only_a or changed)
    print(f"  [{'OK ' if ok else '!! '}] {label}: 预期 {len(expected)} 条 / 实际 {len(actual)} 条")
    if only_e:
        print(f"        仅预期有: {list(only_e.items())[:5]}")
    if only_a:
        print(f"        仅实际有: {list(only_a.items())[:5]}")
    if changed:
        print(f"        值不同: {list(changed.items())[:5]}")
    return ok


# ----------------------------------------------------------------------------- 各表提取


def build_items():
    """物品表：官方 stuff.json + stuff2.json，按 stuff_id 升序。"""
    rows = load_game_json("stuff.json") + load_game_json("stuff2.json")
    items = {}
    for r in rows:
        items[r["stuff_id"]] = (r.get("stuff_namezh-CN", ""), r.get("stuff_type", -1))
    return [[i, items[i][0], items[i][1]] for i in sorted(items)]


def code_items():
    """从现有 ItemCatalog.cs 解析，用于交叉校验。"""
    src = read_source("Core/ItemCatalog.cs")
    pairs = re.findall(r'\{\s*(\d+),\s*\("([^"]*)",\s*(\d+)\)\s*\}', src)
    if not pairs:
        return None
    return {int(a): (b, int(c)) for a, b, c in pairs}


def build_soldier_types():
    """士兵类型：官方 soldier_equip.json 的 soldier_type_id + 官方名称；另加特例 0=市民。"""
    ids = sorted({r["soldier_type_id"] for r in load_game_json("soldier_equip.json")})
    names = {}
    for r in load_game_json("stuff.json") + load_game_json("stuff2.json"):
        names[r["stuff_id"]] = r.get("stuff_namezh-CN", "")
    rows = [[0, "市民"]]
    rows += [[i, names.get(i, "")] for i in ids]
    return rows


def code_soldier_types():
    src = read_source("Game/EntityScan.cs")
    m = re.search(r'SoldierTypeNames\s*=\s*new\(\)\s*\{(.*?)\n    \};', src, re.S)
    if not m:
        return None
    return {int(a): b for a, b in re.findall(r'\{\s*(\d+),\s*"([^"]*)"\s*\}', m.group(1))}


def build_chest_filters():
    """箱子筛选表：以 C# 现值为准（部分显示名与官方不同，属有意为之）。"""
    src = read_source("Game/ChestService.cs")
    m = re.search(r'_filterItems\s*=\s*new\(\)\s*\{(.*?)\n    \};', src, re.S)
    if not m:
        raise SystemExit("找不到 ChestService._filterItems 定义")
    rows = [[int(a), b, 1 if c == "true" else 0]
            for a, b, c in re.findall(r'\{\s*(\d+),\s*\("([^"]*)",\s*(true|false)\)\s*\}', m.group(1))]
    if not rows:
        raise SystemExit("_filterItems 解析为空")
    return rows


def build_dragon_types():
    """龙类型：代码中无官方对应表，以 C# 现值为准。"""
    src = read_source("Game/DragonService.cs")
    m = re.search(r'DragonTypes\s*=\s*new\[\]\s*\{(.*?)\n    \};', src, re.S)
    if not m:
        raise SystemExit("找不到 DragonService.DragonTypes 定义")
    rows = [[a, b, int(c)] for a, b, c in
            re.findall(r'\("([^"]*)",\s*"([^"]*)",\s*(\d+)\)', m.group(1))]
    if not rows:
        raise SystemExit("DragonTypes 解析为空")
    return rows


def build_dragon_natures():
    """龙天性：官方 dragon_nature.json。"""
    rows = load_game_json("dragon_nature.json")
    return [[r["dragon_nature_id"], r.get("name_zh-CN", "")] for r in sorted(rows, key=lambda x: x["dragon_nature_id"])]


# ----------------------------------------------------------------------------- 主流程


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="只校验，不写文件")
    ap.add_argument("--verify", action="store_true", help="额外与现有 C# 表做一致性对比")
    args = ap.parse_args()

    print(f"游戏数据源: {GAME_DATA}")
    print(f"输出目录  : {DATA_OUT}\n")

    ok = True

    items = build_items()
    print(f"items.json          {len(items):>5} 条 (官方 stuff+stuff2)")
    if args.verify:
        ci = code_items()
        if ci is not None:
            ok &= diff_sets("ItemCatalog(代码) vs 官方", ci, {r[0]: (r[1], r[2]) for r in items})
    dump_rows(os.path.join(DATA_OUT, "items.json"), items, args.check)

    soldiers = build_soldier_types()
    print(f"soldier_types.json  {len(soldiers):>5} 条 (官方 soldier_equip + 特例 0)")
    if args.verify:
        cs = code_soldier_types()
        if cs is not None:
            ok &= diff_sets("SoldierTypeNames(代码) vs 官方",
                            {k: v for k, v in cs.items() if k != 0},
                            {r[0]: r[1] for r in soldiers if r[0] != 0})
    dump_rows(os.path.join(DATA_OUT, "soldier_types.json"), soldiers, args.check)

    filters = build_chest_filters()
    print(f"chest_filters.json  {len(filters):>5} 条 (C# 现值为准)")
    official_names = {r["stuff_id"]: r.get("stuff_namezh-CN", "")
                      for r in load_game_json("stuff.json") + load_game_json("stuff2.json")}
    diverged = [(r[0], r[1], official_names.get(r[0])) for r in filters
                if official_names.get(r[0]) != r[1]]
    if diverged:
        print("        ⚠ 以下显示名为代码手写、与官方不同（已按原样保留）:")
        for i, code_name, off_name in diverged:
            print(f"          {i}: 代码「{code_name}」 / 官方「{off_name}」")
    dump_rows(os.path.join(DATA_OUT, "chest_filters.json"), filters, args.check)

    dtypes = build_dragon_types()
    print(f"dragon_types.json   {len(dtypes):>5} 条 (C# 现值为准)")
    dump_rows(os.path.join(DATA_OUT, "dragon_types.json"), dtypes, args.check)

    dnatures = build_dragon_natures()
    print(f"dragon_natures.json {len(dnatures):>5} 条 (官方 dragon_nature.json)")
    if args.verify:
        src = read_source("Game/DragonService.cs")
        m = re.search(r'DragonNatures\s*=\s*new\[\]\s*\{(.*?)\n    \};', src, re.S)
        if m:
            cn = {int(a): b for a, b in re.findall(r'\((\d+),\s*"([^"]*)"\)', m.group(1))}
            ok &= diff_sets("DragonNatures(代码) vs 官方", cn, {r[0]: r[1] for r in dnatures})
    dump_rows(os.path.join(DATA_OUT, "dragon_natures.json"), dnatures, args.check)

    print()
    if args.verify and not ok:
        print("❌ 校验失败：代码表与官方数据存在差异")
        return 1
    print("✅ 完成" + ("（仅校验，未写文件）" if args.check else f" → {DATA_OUT}"))
    return 0


if __name__ == "__main__":
    sys.exit(main())

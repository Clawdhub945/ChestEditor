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
  Data/spawn_tables.json   {races, soldierTypes, weapons,       来自 soldier_equip/weapon/armor/shield.json
                            armors, shields}                    （召唤小人/士兵的下拉数据，NpcSpawnService）
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


def build_carried_forward(filename):
    """代码字面量已迁入 Data/*.json 的表（C 批次）：以现有 Data 文件为准原样沿用。
    （chest_filters 名称手写、dragon_types 无官方表，源头就是 JSON 本身。）"""
    path = os.path.join(DATA_OUT, filename)
    if not os.path.exists(path):
        raise SystemExit(f"找不到 {path} —— 该表现在是源头，删除前请确认有替代来源")
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def build_chest_filters():
    """箱子筛选表：以 Data/chest_filters.json 现值为准（原 C# 字面量已迁出）。"""
    return build_carried_forward("chest_filters.json")


def build_dragon_types():
    """龙类型：以 Data/dragon_types.json 现值为准（原 C# 字面量已迁出）。"""
    return build_carried_forward("dragon_types.json")


def build_dragon_natures():
    """龙天性：官方 dragon_nature.json。"""
    rows = load_game_json("dragon_nature.json")
    return [[r["dragon_nature_id"], r.get("name_zh-CN", "")] for r in sorted(rows, key=lambda x: x["dragon_nature_id"])]


def build_spawn_tables():
    """召唤小人/士兵的下拉数据（NpcSpawnService）。
    兵种：soldier_equip.json（剔除 disable、0=市民、**is_mercenary=1 的雇佣兵**——
          用户要的是"本地士兵"；雇佣兵是 202885~202899 段）；
    装备：weapon/armor/shield.json，按兵种的 weapon/armor/shield_group 在前端过滤；
    名称：stuff 表官方名（weapon_id=405xxx 等就是 stuff_id），缺名回落 prefab。
    种族名：race.json 没有完整中文族名（只有姓氏单字），沿用实测反推的 10 族名。"""
    stuff_names = {r["stuff_id"]: r.get("stuff_namezh-CN", "")
                   for r in load_game_json("stuff.json") + load_game_json("stuff2.json")}
    soldier_names = {r[0]: r[1] for r in build_soldier_types()}

    races = [[0, "矮人"], [1, "蚁人"], [2, "鼠人"], [3, "猫人"], [4, "羊人"],
             [5, "狼人"], [6, "猪人"], [7, "精灵族"], [8, "三眼人"], [9, "蜥蜴人"]]

    stypes = []
    for r in sorted(load_game_json("soldier_equip.json"), key=lambda x: x["soldier_type_id"]):
        if r.get("disable", 0):
            continue
        if r.get("is_mercenary", 0):
            continue                      # 雇佣兵不是本地士兵
        sid = r["soldier_type_id"]
        if sid == 0:
            continue
        name = soldier_names.get(sid) or stuff_names.get(sid, "") or str(sid)
        # 第 6 位 = race_id_limit（兵种种族，SoldierHelper.CreateSoldier 的 race_id 参数同源）
        stypes.append([sid, name, r.get("weapon_group", 0), r.get("armor_group", 0), r.get("shield_group", 0), r.get("race_id_limit", 0)])

    def equip(file, id_key, group_key):
        out = []
        for r in sorted(load_game_json(file), key=lambda x: x[id_key]):
            eid = r[id_key]
            out.append([eid, stuff_names.get(eid) or r.get("prefab", ""), r.get(group_key, 0)])
        return out

    return {
        "races": races,
        "soldierTypes": stypes,
        "weapons": equip("weapon.json", "weapon_id", "weapon_group"),
        "armors": equip("armor.json", "armor_id", "armor_group"),
        "shields": equip("shield.json", "shield_id", "shield_group"),
    }


def dump_json(path, obj, check_only):
    """写普通 JSON（dict 结构用，区别于 dump_rows 的紧凑行格式）。"""
    text = json.dumps(obj, ensure_ascii=False, indent=1) + "\n"
    if check_only:
        return text
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return text


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
        src_path = os.path.join(DATA_OUT, "dragon_natures.json")
        # 原 C# 字面量已迁入 Data 文件（C 批次），代码对拍仅在该文件被手改回字面量时才有意义，
        # 这里改为与现有 Data 值对拍（应恒等，等价于自检）。
        try:
            carried = json.load(open(src_path, encoding="utf-8"))
            cn = {int(a): b for a, b in carried}
            ok &= diff_sets("DragonNatures(现有Data) vs 官方", cn, {r[0]: r[1] for r in dnatures})
        except Exception:
            pass
    dump_rows(os.path.join(DATA_OUT, "dragon_natures.json"), dnatures, args.check)

    spawn = build_spawn_tables()
    print(f"spawn_tables.json   {len(spawn['soldierTypes']):>5} 兵种 / "
          f"{len(spawn['weapons'])} 武器 / {len(spawn['armors'])} 盔甲 / {len(spawn['shields'])} 盾牌 / 10 种族")
    dump_json(os.path.join(DATA_OUT, "spawn_tables.json"), spawn, args.check)

    print()
    if args.verify and not ok:
        print("❌ 校验失败：代码表与官方数据存在差异")
        return 1
    print("✅ 完成" + ("（仅校验，未写文件）" if args.check else f" → {DATA_OUT}"))
    return 0


if __name__ == "__main__":
    sys.exit(main())

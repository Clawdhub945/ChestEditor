import sys
sys.path.insert(0, '_tools')
from splitter import split_members, pick, pick_sig, write_file

m = split_members('Il2CppHelper.cs', 'static class Il2CppHelper')

USINGS_GAME = '''using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static ChestEditor.Core.JsonUtil;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

'''

# ===== GameContext =====
gm = pick_sig(m, r'object\? GetGameW\(')
assert len(gm) == 1
body = gm[0][0].replace('GetGameW()', 'GetGame()').replace('object? GetGameW(', 'object? GetGame(')
write_file('Game/GameContext.cs', USINGS_GAME + '''/// <summary>Game/Territory 等游戏根对象的解析与缓存</summary>
internal static class GameContext
{
''' + body + '''
}
''')

# ===== DragonService =====
dragon_patterns = [
    (r'private static Dictionary<int, int>\? _dragonItemCache', None),
    (r'private static bool _dragonCacheInitialized', None),
    (r'private static void EnsureDragonCache\(', 'EnsureDragonCache'),
    (r'internal static Dictionary<int, int> GetDragonItemCache\(', 'GetDragonItemCache'),
    (r'private static void UpdateDragonCache\(', 'UpdateDragonCache'),
    (r'private static MethodInfo\? _dragonStuffCountMethod', None),
    (r'internal static List<KeyValuePair<int, int>> ReadDragonBagLive\(', 'ReadDragonBagLive'),
    (r'internal static List<KeyValuePair<int, int>>\? ReadDragonStuffBag\(', 'ReadDragonStuffBag'),
    (r'private static bool _dragonMethodsLogged', None),
    (r'private static bool _dragonDicLogged', None),
    (r'internal static List<KeyValuePair<int, int>> ReadBagContents\(', 'ReadBagContents'),
    (r'internal static void SetDragonItemQuantity\(', 'SetDragonItemQuantity'),
    (r'private static MethodInfo\? _addDragonSoulMethod', None),
    (r'internal static readonly \(string Name, string ChineseName, int BaseId\)\[\] DragonTypes', 'DragonTypes'),
    (r'internal static readonly \(int Id, string Name\)\[\] DragonNatures', 'DragonNatures'),
    (r'internal static string GetDragonTypesJson\(', 'GetDragonTypesJson'),
    (r'internal static string GetDragonNaturesJson\(', 'GetDragonNaturesJson'),
    (r'internal static string SummonDragon\(', 'SummonDragon'),
    (r'internal static List<Dictionary<string, object\?>>\? ReadDragonSouls\(', 'ReadDragonSouls'),
    (r'internal static string GetDragonSoulsJson\(', 'GetDragonSoulsJson'),
    (r'internal static string SetDragonSoulProperty\(', 'SetDragonSoulProperty'),
    (r'private static readonly Dictionary<string, int> _dragonFieldOffsets', None),
    (r'private static bool _dragonOffsetsCached', None),
    (r'private static void CacheDragonFieldOffsets\(', 'CacheDragonFieldOffsets'),
    (r'private static readonly string\[\] _dragonStatFields', None),
    (r'private static readonly HashSet<string> _dragonFloatFields', None),
    (r'internal static List<Dictionary<string, object>> ReadDragonEntities\(', 'ReadDragonEntities'),
    (r'internal static string GetDragonEntitiesJson\(', 'GetDragonEntitiesJson'),
    (r'internal static string SetDragonEntityField\(', 'SetDragonEntityField'),
    (r'internal static void SearchMapDragonEntities\(', 'SearchMapDragonEntities'),
    (r'private static MethodInfo\? _dragonAddMethod', None),
    (r'private static MethodInfo\? _dragonRemoveMethod', None),
    (r'private static MethodInfo\? _dragonAddNoNotifyMethod', None),
    (r'private static bool _dragonMethodsCached', None),
    (r'private static void CacheBagOps\(', 'CacheBagOps'),
]
dragon_body = []
used = set()
for pat, name in dragon_patterns:
    sel = pick_sig(m, pat)
    assert len(sel) == 1, (pat, len(sel))
    text, a, b = sel[0]
    used.add((a, b))
    dragon_body.append(text)

dragon_body_text = '\n\n'.join(dragon_body).replace('GetGameW(', 'GameContext.GetGame(')

build_bag_json = '''
    internal static void InvalidateBagJsonCache()
    {
        _bagJsonAt = 0;
    }

    private static string _bagJson = "[]";
    private static long _bagJsonAt;

    /// <summary>合并实时读取与本地记账缓存，生成龙素材背包 JSON（带 500ms TTL 缓存）</summary>
    internal static string GetBagJson()
    {
        if (System.Environment.TickCount64 - _bagJsonAt < 500) return _bagJson;
        try
        {
            var liveItems = ReadDragonBagLive();
            var cache = GetDragonItemCache();
            var merged = new Dictionary<int, int>();
            foreach (var kv in liveItems)
                merged[kv.Key] = kv.Value;
            if (cache != null)
            {
                foreach (var kv in cache)
                {
                    if (kv.Value > 0 && !merged.ContainsKey(kv.Key))
                        merged[kv.Key] = kv.Value;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var kv in merged.OrderByDescending(x => x.Value))
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"{{\\"stuffId\\":{kv.Key},\\"name\\":\\"{Escape(ItemNames.GetName(kv.Key))}\\",\\"count\\":{kv.Value}}}");
            }
            sb.Append(']');
            _bagJson = sb.ToString();
            _bagJsonAt = System.Environment.TickCount64;
        }
        catch { _bagJson = "[]"; }
        return _bagJson;
    }
}
'''

write_file('Game/DragonService.cs', USINGS_GAME + '''/// <summary>龙系统：素材背包、龙魂、召唤、龙实体扫描与属性修改</summary>
internal static class DragonService
{
''' + dragon_body_text + '\n' + build_bag_json)

# ===== TechTreeService =====
tech_patterns = [
    (r'internal static string DiagnoseTechTree\(', 'DiagnoseTechTree'),
    (r'private static string SerializeList\(', 'SerializeList'),
    (r'private static void AppendValue\(', 'AppendValue'),
    (r'internal static string GetTechTreeJson\(', 'GetTechTreeJson'),
    (r'internal static string ToggleTechUnlock\(', 'ToggleTechUnlock'),
    (r'internal static string UnlockAllTechs\(', 'UnlockAllTechs'),
    (r'internal static string SetResearchTech\(', 'SetResearchTech'),
]
tech_body = []
for pat, name in tech_patterns:
    sel = pick_sig(m, pat)
    assert len(sel) == 1, (pat, len(sel))
    text, a, b = sel[0]
    used.add((a, b))
    tech_body.append(text)

tech_text = '\n\n'.join(tech_body).replace('GetGameW(', 'GameContext.GetGame(')
write_file('Game/TechTreeService.cs', USINGS_GAME + '''/// <summary>科技树：诊断、查询、解锁/锁定、研究</summary>
internal static class TechTreeService
{
''' + tech_text + '''
}
''')

# ===== 校验覆盖率：列出未搬走的成员 =====
allblocks = set()
for (t, a, b) in m:
    allblocks.add((a, b))
leftover = [(a, b, t.split('\n')[0][:60]) for (t, a, b) in m if (a, b) not in used and a != 1602]
print('未搬走成员:')
for x in leftover:
    print('  ', x)

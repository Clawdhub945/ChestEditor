import sys
sys.path.insert(0, '_tools')
from splitter import split_members, pick_sig, write_file
import io

# ============ 1. ReadFieldSafe/ReadIntFieldSafe -> Interop/Il2CppApi.cs ============
em = split_members('EntityEditor.cs', 'static class EntityEditor')

rfs = pick_sig(em, r'internal static IntPtr ReadFieldSafe\(')
assert len(rfs) == 1
body1 = rfs[0][0].replace('internal static IntPtr ReadFieldSafe(', 'internal static IntPtr ReadFieldSafe(')
ris = pick_sig(em, r'private static int ReadIntFieldSafe\(')
assert len(ris) == 1
body2 = ris[0][0].replace('private static int ReadIntFieldSafe(', 'internal static int ReadIntFieldSafe(')

api = io.open('Interop/Il2CppApi.cs', encoding='utf-8').read()
addition = '\n    /// <summary>安全读取对象的指针字段（含父类搜索，校验偏移范围）</summary>\n' + \
    body1 + '\n\n' + body2 + '\n'
api = api.rstrip()
assert api.endswith('}')
api = api[:-1] + addition + '}\n'
io.open('Interop/Il2CppApi.cs', 'w', encoding='utf-8', newline='\n').write(api)
print('Il2CppApi.cs 追加 ReadFieldSafe/ReadIntFieldSafe')

# ============ 2. EntityEditor -> EntityScan / EntityDestroyer / NpcEditor / ModificationStore ============
USINGS_GAME = '''using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static ChestEditor.Core.JsonUtil;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

'''

used = set()

# --- EntityScan: 头部模型 + ScanAll..ListMethods + FindEntityByPtr ---
def take(pat, sig=True):
    global em, used
    sel = pick_sig(em, pat) if sig else pick(em, pat)
    assert len(sel) == 1, (pat, len(sel))
    text, a, b = sel[0]
    used.add((a, b))
    return text

header_block = take(r'private static readonly List<EditorEntity> _entities')
soldier_table = take(r'private static readonly Dictionary<int, string> SoldierTypeNames')
get_soldier = take(r'internal static string GetSoldierTypeName\(int id\)')
entity_class = take(r'internal class EditorEntity')
npc_keywords = take(r'private static readonly string\[\] NpcClassKeywords')
scan_all = take(r'internal static void ScanAll\(')
get_all_json = take(r'internal static string GetAllJson\(')
get_fields_json = take(r'internal static string GetFieldsJson\(int ptrHash\)')
get_pos_json = take(r'internal static string GetEntityPositionJson\(int ptrHash\)')
set_field = take(r'internal static string SetField\(int ptrHash, string fieldName, float value\)')
list_methods = take(r'internal static string ListMethods\(int ptrHash\)')
find_by_ptr = take(r'private static EditorEntity\? FindEntityByPtr\(IntPtr ptr\)')

scan_extra = '''
    internal static List<EditorEntity> Entities => _entities;

    /// <summary>按 ptrHash 查找已扫描实体</summary>
    internal static EditorEntity? FindByPtrHash(int ptrHash)
    {
        foreach (var e in _entities)
            if (e.PtrHash == ptrHash) return e;
        return null;
    }
'''

scan_text = '\n\n'.join([header_block, npc_keywords, soldier_table, get_soldier,
                          entity_class, scan_all, get_all_json, get_fields_json,
                          get_pos_json, set_field, list_methods, find_by_ptr, scan_extra])
write_file('Game/EntityScan.cs', USINGS_GAME + '''/// <summary>
/// 统一实体编辑器 - 扫描(含stuff_id)和NPC查找(类名含Npc)，实体列表与字段读写
/// </summary>
internal static class EntityScan
{
''' + scan_text + '''
}
''')

# --- EntityDestroyer: DestroyEntity + 辅助（不含 ReadFieldSafe/ReadIntFieldSafe，已移 Interop） ---
destroy = take(r'internal static string DestroyEntity\(int ptrHash\)')
is_facility = take(r'private static bool IsFacilityClass\(string className\)')
find_class_by_name = take(r'private static unsafe IntPtr FindClassByName\(string className\)')
find_class_instance = take(r'private static IntPtr FindClassInstance\(IntPtr classPtr\)')
call_void = take(r'private static void CallVoidMethod\(IntPtr classPtr, IntPtr objPtr, string methodName, int maxDepth\)')
find_territory = take(r'private static IntPtr FindTerritory\(')
find_build_helper = take(r'private static IntPtr FindBuildHelperFromTerritory\(')
find_map_stuff = take(r'private static IntPtr FindMapStuffHelper\(')
remove_from_list = take(r'private static bool RemoveFromListByPtr\(')
remove_from_managers = take(r'private static void RemoveFromManagers\(')
cleanup_refs = take(r'private static void CleanupReferencesInScene\(')

destroy_text = '\n\n'.join([destroy, is_facility, find_class_by_name, find_class_instance,
                             call_void, find_territory, find_build_helper, find_map_stuff,
                             remove_from_list, remove_from_managers, cleanup_refs])
# 引用替换
destroy_text = destroy_text.replace('_entities.RemoveAt(', 'EntityScan.Entities.RemoveAt(')
destroy_text = destroy_text.replace('_entities.Count', 'EntityScan.Entities.Count')
destroy_text = destroy_text.replace('var e = _entities[i];', 'var e = EntityScan.Entities[i];')
destroy_text = destroy_text.replace('foreach (var e in _entities)', 'foreach (var e in EntityScan.Entities)')
destroy_text = destroy_text.replace('for (int i = 0; i < _entities.Count; i++)', 'for (int i = 0; i < EntityScan.Entities.Count; i++)')
# 内部对 EntityScan 的引用
destroy_text = destroy_text.replace('GetSoldierTypeName(', 'EntityScan.GetSoldierTypeName(')
write_file('Game/EntityDestroyer.cs', USINGS_GAME + '''/// <summary>
/// 实体销毁：按 Npc / Facility / Ship / 通用 四级策略销毁并清理管理器引用
/// </summary>
internal static class EntityDestroyer
{
''' + destroy_text + '''
}
''')

# --- NpcEditor: 覆盖字典 + OnPostUpdate + GetNpcListJson + SetNpcField + ApplyNpcFieldChange ---
overrides = take(r'private static readonly Dictionary<int, float> _speedOverrides')
set_speed_override = take(r'internal static void SetSpeedOverride\(int npcId, float speed\)')
on_post_update = take(r'internal static void OnPostUpdate\(IntPtr instancePtr\)')
get_npc_list = take(r'internal static string GetNpcListJson\(')
set_npc_field = take(r'internal static string SetNpcField\(int ptrHash, string fieldName, float value\)')
apply_npc_change = take(r'private static void ApplyNpcFieldChange\(EditorEntity e, string fieldName, float value\)')

npc_text = '\n\n'.join([overrides, set_speed_override, on_post_update, get_npc_list, set_npc_field,
                         apply_npc_change.replace('private static void ApplyNpcFieldChange(', 'internal static void ApplyNpcFieldChange(')])
npc_text = npc_text.replace('SetField(ptrHash, fieldName, value)', 'EntityScan.SetField(ptrHash, fieldName, value)')
npc_text = npc_text.replace('foreach (var e in _entities)', 'foreach (var e in EntityScan.Entities)')
npc_text = npc_text.replace('RecordModification(e.Guid, fieldName, value)', 'ModificationStore.RecordModification(e.Guid, fieldName, value)')
npc_text = npc_text.replace('RecordModificationById(e.NpcId, fieldName, value)', 'ModificationStore.RecordModificationById(e.NpcId, fieldName, value)')
npc_text = npc_text.replace('GetSoldierTypeName(', 'EntityScan.GetSoldierTypeName(')
# 列表缓存（/api/npc/list 直接返回上次扫描结果）
npc_text = npc_text.replace('''    internal static string GetNpcListJson()''',
'''    internal static string CachedListJson = "[]";

    internal static string GetNpcListJson()''')
# GetNpcListJson 返回前写缓存
npc_text = npc_text.rstrip()
assert npc_text.endswith('}')
# 找到 GetNpcListJson 的 return sb.ToString(); —— 位于 SetNpcField 之前
npc_text = npc_text.replace('''        sb.Append(']');
        return sb.ToString();
    }''', '''        sb.Append(']');
        CachedListJson = sb.ToString();
        return CachedListJson;
    }''', 1)
npc_text = npc_text + '\n'
write_file('Game/NpcEditor.cs', USINGS_GAME + '''/// <summary>
/// NPC 编辑：NPC 列表、字段修改、speed/hp/hp_total 持续覆盖（每帧重应用）
/// </summary>
internal static class NpcEditor
{
''' + npc_text + '''
}
''')

# --- ModificationStore: 记录 + 持久化(STJ 重写) + 应用 ---
pending = take(r'private static readonly Dictionary<string, float> _pendingModifications')
record_mod = take(r'internal static void RecordModification\(int guid, string field, float value\)')
record_by_id = take(r'internal static void RecordModificationById\(int npcId, string field, float value\)')
apply_pending = take(r'internal static void ApplyPendingModifications\(')
reapply = take(r'internal static string ReapplyModifications\(')

store_text = '\n\n'.join([pending, record_mod, record_by_id, apply_pending, reapply])
store_text = store_text.replace('foreach (var e in _entities)', 'foreach (var e in EntityScan.Entities)')
store_text = store_text.replace('ScanAll();', 'EntityScan.ScanAll();')
store_text = store_text.replace('ApplyNpcFieldChange(match, fieldName, value);', 'NpcEditor.ApplyNpcFieldChange(match, fieldName, value);')

store_extra = '''
    private static string GetSavePath()
    {
        return System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "ChestEditor_modifications.json");
    }

    /// <summary>落盘（System.Text.Json；与旧版手拼格式完全兼容）</summary>
    internal static void SaveToDisk()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_pendingModifications,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            System.IO.File.WriteAllText(GetSavePath(), json);
        }
        catch (Exception ex) { Plugin.LogError($"[ModificationStore] 保存修改失败: {ex.Message}"); }
    }

    /// <summary>启动时加载（兼容旧手写解析器生成的文件），并恢复 speed/hp/hp_total 覆盖</summary>
    internal static void LoadFromDisk()
    {
        try
        {
            string path = GetSavePath();
            if (!System.IO.File.Exists(path)) return;
            string json = System.IO.File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json) || json == "{}") return;

            var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, float>>(json);
            if (loaded == null) return;
            _pendingModifications.Clear();
            foreach (var kv in loaded)
                _pendingModifications[kv.Key] = kv.Value;

            // 提取 npc: 覆盖
            foreach (var kv in _pendingModifications)
            {
                if (!kv.Key.StartsWith("npc:")) continue;
                int firstColon = kv.Key.IndexOf(':', 4);
                if (firstColon <= 4) continue;
                if (!int.TryParse(kv.Key.Substring(4, firstColon - 4), out int npcId) || npcId <= 0) continue;
                string field = kv.Key.Substring(firstColon + 1);
                if (field == "speed") NpcEditor.SetSpeedOverride(npcId, kv.Value);
                else if (field == "hp") NpcEditor.SetHpOverride(npcId, kv.Value);
                else if (field == "hp_total") NpcEditor.SetHpTotalOverride(npcId, kv.Value);
            }
            Plugin.LogInfo($"[ModificationStore] 已加载 {_pendingModifications.Count} 条修改记录");
        }
        catch (Exception ex) { Plugin.LogError($"[ModificationStore] 加载修改失败: {ex.Message}"); }
    }
'''
# Record* 里的 SaveModificationsToDisk 调用改为 SaveToDisk（加防抖由调用侧控制，这里保持立即落盘语义但文件 IO 降低：直接替换）
store_text = store_text.replace('SaveModificationsToDisk();', 'SaveToDisk();')
write_file('Game/Modifications/ModificationStore.cs', USINGS_GAME + '''/// <summary>
/// 字段修改记录：内存记录 + 磁盘持久化 + 读档/扫描后重应用
/// </summary>
internal static class ModificationStore
{
''' + store_text + '\n' + store_extra + '''
}
''')

# ============ 3. 校验 EntityEditor 剩余未搬走的成员 ============
leftover = [(a, b, t.split(chr(10))[0][:60]) for (t, a, b) in em if (a, b) not in used]
print('EntityEditor 未搬走成员:')
for x in leftover:
    print('  ', x)

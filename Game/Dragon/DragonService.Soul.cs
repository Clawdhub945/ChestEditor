using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>龙系统：素材背包、龙魂、召唤、龙实体扫描与属性修改</summary>
internal static partial class DragonService
{
    // ====== 龙魂列表读取 ======
    internal static List<Dictionary<string, object?>>? ReadDragonSouls()
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return null;
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null) return null;

            var slType = soulList.GetType();
            int count = 0;
            var countProp = slType.GetProperty("Count", BF);
            if (countProp != null) count = Convert.ToInt32(countProp.GetValue(soulList) ?? 0);
            if (count == 0) return new List<Dictionary<string, object?>>();

            var getItem = slType.GetMethod("get_Item", BF);
            if (getItem == null) return null;

            // 读取第一个元素以获取字段布局
            var first = getItem.Invoke(soulList, new object[] { 0 });
            if (first == null) return null;

            IntPtr firstPtr = GetIl2CppPtr(first);
            if (firstPtr == IntPtr.Zero) return null;

            IntPtr classPtr = Il2CppApi.GetClass(firstPtr);
            var fields = Il2CppApi.EnumerateFields(classPtr);

            // 过滤掉静态字段 (offset=0 且不是第一个字段) 和已知常量
            var instanceFields = fields.Where(f =>
                f.Offset > 0 &&
                f.Name != "HEAD" && f.Name != "SHIELD" && f.Name != "CLAW" && f.Name != "CLOUD"
            ).ToList();

            // 获取字段类型信息（0=int, 1=string, 2=list）
            var fieldTypes = new Dictionary<string, int>();
            foreach (var f in fields)
            {
                if (f.TypeName.Contains("String")) fieldTypes[f.Name] = 1;
                else if (f.Name == "nature_list") fieldTypes[f.Name] = 2;
                else fieldTypes[f.Name] = 0;
            }

            // 读取所有龙魂数据
            var result = new List<Dictionary<string, object?>>();
            for (int i = 0; i < count; i++)
            {
                var soul = getItem.Invoke(soulList, new object[] { i });
                if (soul == null) continue;
                IntPtr ptr = GetIl2CppPtr(soul);
                if (ptr == IntPtr.Zero) continue;

                var dict = new Dictionary<string, object?>();
                foreach (var (name, offset, _) in instanceFields)
                {
                    try
                    {
                        int ft = fieldTypes.TryGetValue(name, out int v) ? v : 0;
                        if (ft == 1)
                            dict[name] = ReadIl2CppString(ptr, offset);
                        else if (ft == 2)
                        {
                            var list = ReadIl2CppIntList(ptr, offset);
                            dict[name] = list;
                        }
                        else
                            dict[name] = ReadIl2CppInt(ptr, offset);
                    }
                    catch { }
                }
                result.Add(dict);
            }
            return result;
        }
        catch { return null; }
    }


    internal static string GetDragonSoulsJson()
    {
        ApplySoulModifications(); // 读档自动恢复：直接遍历龙魂列表，无场景扫描，零卡顿
        var souls = ReadDragonSouls();
        if (souls == null) return "[]";
        return JsonBuilder.Build(w =>
        {
            w.WriteStartArray();
            foreach (var soul in souls)
            {
                w.WriteStartObject();
                foreach (var kv in soul)
                {
                    w.WritePropertyName(kv.Key);
                    WriteJsonValue(w, kv.Value);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }


    // 修改龙魂属性 (通过 IL2CPP 原生字段写入)
    internal static string SetDragonSoulProperty(int soulIndex, string property, int value)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "Game.w 为 null";
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null) return "dragon_soul_list 为 null";

            var slType = soulList.GetType();
            int count = Convert.ToInt32(slType.GetProperty("Count", BF)?.GetValue(soulList) ?? 0);
            if (soulIndex < 0 || soulIndex >= count) return "索引越界";

            var getItem = slType.GetMethod("get_Item", BF);
            if (getItem == null) return "get_Item 未找到";

            var soul = getItem.Invoke(soulList, new object[] { soulIndex });
            if (soul == null) return "龙魂为空";

            IntPtr ptr = GetIl2CppPtr(soul);
            if (ptr == IntPtr.Zero) return "无法获取原生指针";

            IntPtr classPtr = Il2CppApi.GetClass(ptr);
            var fields = Il2CppApi.EnumerateFields(classPtr);
            var field = fields.FirstOrDefault(f => f.Name == property);
            if (field.Name == null) return $"字段 {property} 未找到";
            if (field.Offset <= 0) return $"字段 {property} 是静态字段，不可修改";

            // 强化五项服务端同样钳制到 0-50（防止绕过前端直调 API）
            if (IsSoulStrengthenProp(property))
                value = Math.Clamp(value, 0, SoulStrengthenMax);

            WriteIl2CppInt(ptr, field.Offset, value);
            Plugin.LogInfo($"[DragonSoul] 设置 soul[{soulIndex}].{property} = {value}");
            RecordSoulModification(soul, property, value);
            return "ok";
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ====== 龙魂强化持久化 ======
    // 龙魂带跨读档稳定的 string guid，按 "dragon:{guid}:{field}" 记录；
    // 每次读取龙魂列表（含 3 秒轮询）前自动恢复，无场景扫描，不卡顿。

    internal const int SoulStrengthenMax = 50;

    private static readonly HashSet<string> _soulStrengthenProps = new() { "head", "claw", "shield", "cloud", "potentiality" };

    private static bool IsSoulStrengthenProp(string p) => _soulStrengthenProps.Contains(p);

    private static void RecordSoulModification(object soul, string property, int value)
    {
        try
        {
            var guid = GetProp(soul, "guid")?.ToString();
            if (!string.IsNullOrEmpty(guid))
                ModificationStore.RecordRaw($"dragon:{guid}:{property}", value);
        }
        catch { }
    }

    /// <summary>
    /// 游戏侧恢复入口（不依赖网页轮询）：龙魂强化立即恢复。
    /// 返回 true=完成（无记录或已应用）；false=游戏世界/龙魂列表未就绪，稍后重试。
    /// </summary>
    internal static bool TryRestoreSoulModifications()
    {
        if (ModificationStore.GetByPrefix("dragon:").Count == 0) return true;
        var w = GameContext.GetGame();
        if (w == null) return false;
        object? soulList = GetProp(w, "dragon_soul_list");
        if (soulList == null) return false;
        int count = Convert.ToInt32(soulList.GetType().GetProperty("Count", BF)?.GetValue(soulList) ?? 0);
        if (count == 0) return false;
        ApplySoulModifications();
        return true;
    }

    /// <summary>
    /// 游戏侧恢复入口：扫描地图实体并内联应用 dragone: 记录（需要一次场景扫描）。
    /// </summary>
    internal static void RestoreEntityModificationsViaScan(UnityEngine.GameObject[]? source = null) => ReadDragonEntities(source);

    private static void ApplySoulModifications()
    {
        try
        {
            var pending = ModificationStore.GetByPrefix("dragon:");
            if (pending.Count == 0) return;

            var w = GameContext.GetGame();
            if (w == null) return;
            object? soulList = GetProp(w, "dragon_soul_list");
            if (soulList == null) return;

            var slType = soulList.GetType();
            int count = Convert.ToInt32(slType.GetProperty("Count", BF)?.GetValue(soulList) ?? 0);
            var getItem = slType.GetMethod("get_Item", BF);
            if (getItem == null || count == 0) return;

            // guid -> (field -> value)
            var byGuid = new Dictionary<string, List<KeyValuePair<string, float>>>();
            foreach (var kv in pending)
            {
                int a = kv.Key.IndexOf(':') + 1;
                int b = kv.Key.IndexOf(':', a);
                if (b <= a) continue;
                var guid = kv.Key.Substring(a, b - a);
                var field = kv.Key.Substring(b + 1);
                if (!byGuid.TryGetValue(guid, out var list2)) { list2 = new List<KeyValuePair<string, float>>(); byGuid[guid] = list2; }
                list2.Add(new KeyValuePair<string, float>(field, kv.Value));
            }

            IntPtr? classPtrCache = null;
            var fields = new List<(string Name, int Offset, string TypeName)>();
            for (int i = 0; i < count; i++)
            {
                var soul = getItem.Invoke(soulList, new object[] { i });
                if (soul == null) continue;
                var guid = GetProp(soul, "guid")?.ToString();
                if (string.IsNullOrEmpty(guid) || !byGuid.TryGetValue(guid!, out var mods)) continue;

                IntPtr ptr = GetIl2CppPtr(soul);
                if (ptr == IntPtr.Zero) continue;
                IntPtr classPtr = Il2CppApi.GetClass(ptr);
                if (classPtr != classPtrCache)
                {
                    fields = Il2CppApi.EnumerateFields(classPtr);
                    classPtrCache = classPtr;
                }
                foreach (var m in mods)
                {
                    int target = (int)m.Value;
                    if (IsSoulStrengthenProp(m.Key)) target = Math.Clamp(target, 0, SoulStrengthenMax);
                    var f = fields.FirstOrDefault(x => x.Name == m.Key);
                    if (f.Name == null || f.Offset <= 0) continue;
                    int cur = ReadIl2CppInt(ptr, f.Offset);
                    if (cur != target)
                    {
                        WriteIl2CppInt(ptr, f.Offset, target);
                        Plugin.LogInfo($"[DragonService] 恢复龙魂 {guid}.{m.Key} = {target}");
                    }
                }
            }
        }
        catch (Exception ex) { Plugin.LogError($"[DragonService] 恢复龙魂强化失败: {ex.Message}"); }
    }
}

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
    // ====== 召唤龙 ======
    private static MethodInfo? _addDragonSoulMethod;


    /// <summary>龙类型表（数据见 Data/dragon_types.json）</summary>
    internal static readonly (string Name, string ChineseName, int BaseId)[] DragonTypes = DataTables.DragonTypes;


    /// <summary>龙天性表（数据见 Data/dragon_natures.json，源自官方 dragon_nature.json）</summary>
    internal static readonly (int Id, string Name)[] DragonNatures = DataTables.DragonNatures;


    internal static string GetDragonTypesJson() => JsonBuilder.Build(w =>
    {
        w.WriteStartArray();
        foreach (var (name, cn, baseId) in DragonTypes)
        {
            w.WriteStartObject();
            w.WriteString("name", name);
            w.WriteString("cn", cn);
            w.WriteNumber("baseId", baseId);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });


    internal static string GetDragonNaturesJson() => JsonBuilder.Build(w =>
    {
        w.WriteStartArray();
        foreach (var (id, name) in DragonNatures)
        {
            w.WriteStartObject();
            w.WriteNumber("id", id);
            w.WriteString("name", name);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    });
    internal static string SummonDragon(int dragonStuffId, int[]? natureIds = null)
    {
        try
        {
            var w = GameContext.GetGame();
            if (w == null) return "Game.w 为 null";

            // 确保驭龙数量上限足够
            int controlCount = GetInt(w, "dragon_control_count");
            var soulList = GetProp(w, "dragon_soul_list");
            int currentSouls = 0;
            if (soulList != null)
            {
                var countProp = soulList.GetType().GetProperty("Count", BF);
                if (countProp != null)
                    currentSouls = Convert.ToInt32(countProp.GetValue(soulList) ?? 0);
            }
            if (currentSouls >= controlCount)
            {
                // 提升驭龙上限
                SetInt(w, "dragon_control_count", currentSouls + 1);
                Plugin.LogInfo($"[Dragon] 驭龙上限提升: {controlCount} -> {currentSouls + 1}");
            }

            // 找 AddDragonSoul 方法
            if (_addDragonSoulMethod == null)
            {
                _addDragonSoulMethod = w.GetType().GetMethod("AddDragonSoul",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (_addDragonSoulMethod == null)
                return "AddDragonSoul 方法未找到";

            var parameters = _addDragonSoulMethod.GetParameters();

            // 构造 nature_list 参数
            object? natureList = null;
            if (parameters.Length >= 2)
            {
                var listType = parameters[1].ParameterType;
                natureList = Activator.CreateInstance(listType);
                // 添加选中的 nature
                if (natureIds != null && natureIds.Length > 0)
                {
                    var addMethod = listType.GetMethod("Add", BF);
                    if (addMethod != null)
                    {
                        foreach (int nid in natureIds)
                        {
                            try { addMethod.Invoke(natureList, new object[] { nid }); }
                            catch { }
                        }
                    }
                }
                Plugin.LogInfo($"[Dragon] 创建 nature_list: {listType.FullName}");
            }

            // 调用 AddDragonSoul
            object? result;
            if (parameters.Length >= 2)
                result = _addDragonSoulMethod.Invoke(w, new object[] { dragonStuffId, natureList! });
            else if (parameters.Length == 1)
                result = _addDragonSoulMethod.Invoke(w, new object[] { dragonStuffId });
            else
                return "AddDragonSoul 参数数量异常";

            Plugin.LogInfo($"[Dragon] AddDragonSoul 返回: {result}");
            return result?.ToString() ?? "ok";
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Dragon] SummonDragon 出错: {ex}");
            return ex.Message;
        }
    }
}

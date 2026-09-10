using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

internal static partial class EntityDestroyer
{
    /// <summary>
    /// 怪物策略：复刻游戏的 <c>MonsterHelper.DestroyRightNow(monster)</c> —— 怪物被"静默移除"
    /// （回巢/换图回收）时走的正是这条路：
    /// <list type="number">
    ///   <item>把 <c>Monster.skip_show_dead_anim</c> 置 1</item>
    ///   <item>调用 <c>DeadOnBattle(null, false)</c> —— 正常注销（RemoveMonster）+ 回收（DestroyGo），
    ///         但不掉东西、不播死亡动画/特效</item>
    /// </list>
    /// <para>
    /// 不这么做的后果：走通用兜底时会挑到 <c>BattleUnit.Leave()</c>（实机日志已确认），
    /// 它调 <c>Monster.DeadOnBattle(null,false)</c> 时 <c>skip_show_dead_anim</c> 仍是 0，
    /// 于是每只怪都走一遍死亡动画/尸体回收流程（<c>MonsterHelper.TryShowMonsterDeadAnim</c>）。
    /// 一键清除动辄成百上千只，短时间内堆出海量特效表现，是本面板"清除即刷屏报错"的直接来源。
    /// </para>
    /// </summary>
    private static bool TryDestroyMonster(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        if (!className.StartsWith("Monster", StringComparison.Ordinal)) return false;
        // MonsterBody / MonsterHelper / MonsterGroup 之类不是怪物本体，没有 DeadOnBattle，会自然落到兜底
        if (className == "MonsterBody" || className == "MonsterHelper" || className == "MonsterGroup") return false;

        // 1) skip_show_dead_anim = 1：跳过死亡动画与尸体回收
        if (TryFindFieldOffset(classPtr, "skip_show_dead_anim", out int skipOffset) && skipOffset > 0)
        {
            try { WriteIl2CppByte(e.Ptr, skipOffset, 1); }
            catch (Exception ex) { Plugin.LogInfo($"[EntityEditor] [Monster] set skip_show_dead_anim failed: {ex.Message}"); }
        }
        else
            Plugin.LogInfo($"[EntityEditor] [Monster] skip_show_dead_anim field not found on {className}");

        // 2) DeadOnBattle(null, false)：沿继承链取**最派生**的重载（Monster 覆写了它）
        IntPtr dead = FindMethodMostDerived(classPtr, "DeadOnBattle", 2, 15);
        if (dead != IntPtr.Zero)
        {
            bool ok = InvokeWithArgs(dead, e.Ptr, IntPtr.Zero, IntPtr.Zero);
            Plugin.LogInfo($"[EntityEditor] [Monster] DeadOnBattle(null,false) on {name} ex={(ok ? "none" : "yes")}");
            return true;
        }

        // 3) 退路：Monster.DestroyRightNow()（只回收 GO，不注销；仅在上面找不到时用）
        IntPtr drn = FindMethodMostDerived(classPtr, "DestroyRightNow", 0, 15);
        if (drn != IntPtr.Zero)
        {
            bool ok = InvokeWithDefaults(drn, e.Ptr, 0);
            Plugin.LogInfo($"[EntityEditor] [Monster] DestroyRightNow() fallback on {name} ex={(ok ? "none" : "yes")}");
            return true;
        }

        Plugin.LogInfo($"[EntityEditor] [Monster] no silent destroy method on {className}");
        return false;
    }

    /// <summary>
    /// 沿继承链从**本类往上**找第一个同名同参数个数的方法（= 最派生的覆写）。
    /// 比 il2cpp_class_get_method_from_name 更稳：虚方法不会被解析到基类实现。
    /// </summary>
    private static IntPtr FindMethodMostDerived(IntPtr classPtr, string methodName, int paramCount, int maxDepth)
    {
        IntPtr cls = classPtr;
        int depth = 0;
        while (cls != IntPtr.Zero && depth < maxDepth)
        {
            IntPtr iter = IntPtr.Zero;
            IntPtr m;
            while ((m = Il2CppApi.ClassGetMethods(cls, ref iter)) != IntPtr.Zero)
            {
                string? mn = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(m));
                if (mn == methodName && Il2CppApi.GetMethodParamCountRaw(m) == (uint)paramCount) return m;
            }
            cls = Il2CppApi.GetParent(cls);
            depth++;
        }
        return IntPtr.Zero;
    }
}

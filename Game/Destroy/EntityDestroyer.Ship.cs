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
    /// 是否真的是"船"。
    /// <para>⚠ 绝不能用 <c>Contains("Ship"/"BattleUnit"/"Soldier")</c> 这类"包含"匹配：</para>
    /// <list type="bullet">
    ///   <item><c>MonsterAntSoldier</c>（实机 581 只）含 "Soldier"</item>
    ///   <item><c>BulletSoldier</c>（121）、<c>SoldierOrderlyItem</c>（14）同样含 "Soldier"</item>
    /// </list>
    /// <para>它们会被当成船丢进 <c>ShipHelper.DestroyShip</c>。该方法的参数是 <c>Ship</c>，
    /// 却拿到了怪物的指针 —— 它会去读怪物内存里的 <c>ship_info</c>（读到垃圾指针/垃圾
    /// <c>sailor_count</c>），然后按这个垃圾数量疯狂 <c>SoldierHelper.CreateSoldier</c>。</para>
    /// <para>实测代价：一次误判即可让进程持续生成船员 NPC，每个 NPC 预制体带 AudioSource，
    /// 把 FMOD 的声道组资源耗尽 —— Player.log 里刷屏的
    /// <c>createChannelGroup("ASrcDryGroup"/"ASrcWetGroup") Not enough memory or resources</c>
    /// 全部来自 <c>ShipHelper.DestroyShip → SoldierHelper.CreateSoldier → NpcHelper.CreateNpc</c>
    /// 这一条栈（实机一次会话 55968 条，全是同一栈）。</para>
    /// <para>判据改为：类名就是 Ship / 以 Ship 开头，或实体确实持有 <c>ship_info</c> 字段。</para>
    /// </summary>
    private static bool IsShipEntity(EntityScan.EditorEntity e, string className)
    {
        if (className == "Ship" || className.StartsWith("Ship", StringComparison.Ordinal)) return true;
        return e.FieldMeta.ContainsKey("ship_info");
    }

    /// <summary>Ship 策略：从船自身 territory 的 ship_list/ship_dic 移除，调用 ShipHelper.DestroyShip 或自身 DestroySelf</summary>
    private static bool TryDestroyShip(EntityScan.EditorEntity e, IntPtr classPtr, string className, string name)
    {
        bool called = false;
        if (!called && IsShipEntity(e, className))
        {
            Plugin.LogVerbose($"[EntityEditor] [Ship] Ship destroy start...");
            bool shipDestroyed = false;

            int guid = 0;
            if (e.FieldMeta.TryGetValue("guid", out var guidFe))
                try { guid = ReadIl2CppInt(e.Ptr, guidFe.Offset); } catch { }
            Plugin.LogVerbose($"[EntityEditor] [Ship] entity guid={guid}");

            // 从船对象自身读取 territory 字段（Ship 继承自 MyMonoBehaviour，有 territory 引用）
            IntPtr shipTerritoryPtr = ReadFieldSafe(e.Ptr, classPtr, "territory");
            if (shipTerritoryPtr == IntPtr.Zero)
            {
                // 递归搜索父类找 territory 字段
                IntPtr searchCls = classPtr;
                int depth = 0;
                while (searchCls != IntPtr.Zero && depth < 10)
                {
                    IntPtr fi = IntPtr.Zero;
                    IntPtr f;
                    while ((f = Il2CppApi.ClassGetFields(searchCls, ref fi)) != IntPtr.Zero)
                    {
                        string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                        if (fn == "territory" || fn == "_territory")
                        {
                            int offset = (int)Il2CppApi.FieldGetOffset(f);
                            if (offset >= 0x10 && offset < 0x10000)
                                unsafe { shipTerritoryPtr = *(IntPtr*)(e.Ptr + offset); }
                            if (shipTerritoryPtr != IntPtr.Zero) break;
                        }
                    }
                    if (shipTerritoryPtr != IntPtr.Zero) break;
                    searchCls = Il2CppApi.GetParent(searchCls);
                    depth++;
                }
            }

            if (shipTerritoryPtr != IntPtr.Zero)
            {
                IntPtr shipTerritoryClass = Il2CppApi.GetClass(shipTerritoryPtr);
                string? territoryName = Il2CppApi.PtrToString(Il2CppApi.ClassGetName(shipTerritoryClass));
                Plugin.LogVerbose($"[EntityEditor] [Ship] Ship's territory class={territoryName}, ptr={shipTerritoryPtr.ToInt64():X}");

                // 从船的所属 territory 读取 ship_list 和 ship_dic
                IntPtr shipListPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_list");
                IntPtr shipDicPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_dic");
                Plugin.LogVerbose($"[EntityEditor] [Ship] ship_list={shipListPtr.ToInt64():X}, ship_dic={shipDicPtr.ToInt64():X}");

                // 按指针从 ship_list 中找到并移除
                if (shipListPtr != IntPtr.Zero)
                {
                    Plugin.LogVerbose($"[EntityEditor] [Ship] Removing from ship_list by pointer match...");
                    shipDestroyed = RemoveFromListByPtr(shipListPtr, e.Ptr);
                    Plugin.LogVerbose($"[EntityEditor] [Ship] RemoveFromListByPtr result={shipDestroyed}");
                }

                // 从 ship_dic 按 guid 移除
                if (shipDicPtr != IntPtr.Zero && guid != 0)
                {
                    IntPtr dicClass = Il2CppApi.GetClass(shipDicPtr);
                    IntPtr removeMth = Il2CppApi.GetMethodFromName(dicClass, "Remove", 1);
                    if (removeMth != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr exRm = IntPtr.Zero;
                            unsafe
                            {
                                int guidArg = guid;
                                IntPtr* rmArgs = stackalloc IntPtr[1];
                                rmArgs[0] = (IntPtr)(&guidArg);
                                Il2CppApi.RuntimeInvoke(removeMth, shipDicPtr, (void**)rmArgs, ref exRm);
                            }
                            Plugin.LogVerbose($"[EntityEditor] [Ship] ship_dic.Remove({guid}) ex={exRm != IntPtr.Zero}");
                        }
                        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] [Ship] ship_dic.Remove failed: {ex.Message}"); }
                    }
                }

                // 调用 ShipHelper.DestroyShip 做完整清理
                IntPtr shipHelperPtr = ReadFieldSafe(shipTerritoryPtr, shipTerritoryClass, "ship_helper");
                if (shipHelperPtr != IntPtr.Zero)
                {
                    IntPtr shClass = Il2CppApi.GetClass(shipHelperPtr);
                    IntPtr destroyShipMth = IntPtr.Zero;
                    {
                        IntPtr shIter = IntPtr.Zero;
                        IntPtr shM;
                        while ((shM = Il2CppApi.ClassGetMethods(shClass, ref shIter)) != IntPtr.Zero)
                        {
                            string? shName = Il2CppApi.PtrToString(Il2CppApi.MethodGetName(shM));
                            if (shName == "DestroyShip") { destroyShipMth = shM; break; }
                        }
                    }
                    if (destroyShipMth != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr exDestroy = IntPtr.Zero;
                            unsafe
                            {
                                int boolArg = 0;
                                IntPtr* destroyArgs = stackalloc IntPtr[2];
                                destroyArgs[0] = e.Ptr;
                                destroyArgs[1] = (IntPtr)(&boolArg);
                                Il2CppApi.RuntimeInvoke(destroyShipMth, shipHelperPtr, (void**)destroyArgs, ref exDestroy);
                            }
                            Plugin.LogVerbose($"[EntityEditor] [Ship] DestroyShip call ex={exDestroy != IntPtr.Zero}");
                        }
                        catch (Exception ex) { Plugin.LogVerbose($"[EntityEditor] [Ship] DestroyShip failed: {ex.Message}"); }
                    }
                    else
                    {
                        CallVoidMethod(classPtr, e.Ptr, "StopMove", 0);
                        CallVoidMethod(classPtr, e.Ptr, "CloseWindow", 0);
                        CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                    }
                }
                else
                {
                    CallVoidMethod(classPtr, e.Ptr, "StopMove", 0);
                    CallVoidMethod(classPtr, e.Ptr, "CloseWindow", 0);
                    CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
                }

                // 最终验证
                if (shipListPtr != IntPtr.Zero)
                {
                    IntPtr listClass = Il2CppApi.GetClass(shipListPtr);
                    int finalSize = ReadIntFieldSafe(shipListPtr, listClass, "_size", -1);
                    Plugin.LogVerbose($"[EntityEditor] [Ship] Final: ship_list._size={finalSize}");
                }
            }
            else
            {
                Plugin.LogVerbose($"[EntityEditor] [Ship] Could not find territory field on ship, trying FindTerritory fallback...");
                // 回退到 FindTerritory
                IntPtr territoryPtr = GameChainLocator.GetTerritory();
                if (territoryPtr != IntPtr.Zero)
                {
                    IntPtr tc = Il2CppApi.GetClass(territoryPtr);
                    IntPtr shipListPtr = ReadFieldSafe(territoryPtr, tc, "ship_list");
                    if (shipListPtr != IntPtr.Zero)
                        shipDestroyed = RemoveFromListByPtr(shipListPtr, e.Ptr);
                }
                CallVoidMethod(classPtr, e.Ptr, "DestroySelf", 0);
            }

            // 兜底清理
            if (!shipDestroyed)
            {
                Plugin.LogVerbose($"[EntityEditor] [Ship] Fallback cleanup...");
                CallVoidMethod(classPtr, e.Ptr, "UnIndexUnitByPos", 0);
                CallVoidMethod(classPtr, e.Ptr, "BeforeDestroy", 0);
                // is_dead
                {
                    IntPtr idCls = classPtr;
                    int idD = 0;
                    while (idCls != IntPtr.Zero && idD < 10)
                    {
                        IntPtr fi = IntPtr.Zero;
                        IntPtr f;
                        while ((f = Il2CppApi.ClassGetFields(idCls, ref fi)) != IntPtr.Zero)
                        {
                            string? fn = Il2CppApi.PtrToString(Il2CppApi.FieldGetName(f));
                            if (fn == "is_dead")
                            {
                                int offset = (int)Il2CppApi.FieldGetOffset(f);
                                if (offset >= 0x10 && offset < 0x10000)
                                    unsafe { *(int*)(e.Ptr + offset) = 1; }
                                break;
                            }
                        }
                        idCls = Il2CppApi.GetParent(idCls);
                        idD++;
                    }
                }
                try { e.GoRef!.SetActive(false); } catch { }
            }
            called = true;
        }
        return called;
    }
}

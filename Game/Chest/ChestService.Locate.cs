using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 箱子/背包：设施扫描、物品增删、计划库存、筛选、定位、JSON 构建
/// </summary>
internal static partial class ChestService
{
    internal static void LocateFacility(float targetX, float targetY)
    {
        try
        {
            var mainScene = GameContext.GetMainScene();
            if (mainScene == null) { Plugin.LogInfo("[Locate] mainScene is null"); FallbackLocate(targetX, targetY); return; }

            // 获取 camera_helper
            var cameraHelper = GetProp(mainScene, "camera_helper");
            if (cameraHelper == null) { Plugin.LogInfo("[Locate] cameraHelper is null"); FallbackLocate(targetX, targetY); return; }

            // 调用 CameraSetTo(float, float, bool)
            var chType = cameraHelper.GetType();
            var cameraSetTo = chType.GetMethod("CameraSetTo",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(float), typeof(float), typeof(bool) }, null);
            if (cameraSetTo != null)
            {
                cameraSetTo.Invoke(cameraHelper, new object[] { targetX, targetY, true });
                Plugin.LogInfo($"[Locate] CameraSetTo({targetX}, {targetY}) via reflection done");
            }
            else
            {
                Plugin.LogInfo("[Locate] CameraSetTo(float,float,bool) not found, trying IL2CPP");
                // 回退: IL2CPP 方式
                IntPtr chPtr = GetIl2CppPtr(cameraHelper);
                if (chPtr == IntPtr.Zero) { FallbackLocate(targetX, targetY); return; }
                IntPtr chClass = Il2CppApi.GetClass(chPtr);

                // 尝试 CameraSetTo 3参数版本
                IntPtr csMth = FindMethodInHierarchy(chClass, "CameraSetTo", 3);
                if (csMth != IntPtr.Zero)
                {
                    InvokeVoid(csMth, chPtr, targetX, targetY, true);
                    Plugin.LogInfo("[Locate] CameraSetTo IL2CPP 已调用");
                }
                else
                {
                    // 直接设置 camera_con.position
                    IntPtr cameraConPtr = ReadFieldSafe(chPtr, chClass, "camera_con");
                    if (cameraConPtr != IntPtr.Zero)
                    {
                        var transform = GetProp(cameraHelper, "camera_con");
                        if (transform != null)
                        {
                            var tType = transform.GetType();
                            var setPos = tType.GetMethod("set_position",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                null, new[] { typeof(UnityEngine.Vector3) }, null);
                            if (setPos != null)
                            {
                                setPos.Invoke(transform, new object[] { new UnityEngine.Vector3(targetX, targetY, 0) });
                                Plugin.LogInfo($"[Locate] Transform.set_position via reflection done");
                            }
                        }
                    }
                    else
                        FallbackLocate(targetX, targetY);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogInfo($"[Locate] error: {ex.Message}");
            FallbackLocate(targetX, targetY);
        }
    }



    private static void FallbackLocate(float targetX, float targetY)
    {
        try
        {
            var cam = Camera.main;
            if (cam == null) return;
            var pos = cam.transform.position;
            pos.x = targetX;
            pos.y = targetY;
            cam.transform.position = pos;
        }
        catch { }
    }
    // ====== 定位 ======

    internal static string LocateChest(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count)
            return JsonBuilder.Error("chest not found");
        var c = _chests[chestIndex];
        LocateFacility(c.PosX, c.PosY);
        return JsonBuilder.Object(w => { w.WriteBoolean("ok", true); w.WriteNumber("posX", JsonBuilder.Safe(c.PosX)); w.WriteNumber("posY", JsonBuilder.Safe(c.PosY)); });
    }
}

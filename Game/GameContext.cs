using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>Game/Territory 等游戏根对象的解析与缓存</summary>
internal static class GameContext
{

    internal static object? GetGame()
    {
        // 存档未加载时不要调用 Game.get_w()，否则 IL2CPP 会 AccessViolation 崩溃
        if (SaveLoadPatches.CachedTerritory == null) return null;

        try
        {
            // Game.get_w() 是静态方法
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return null;

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) return null;

            var getW = gameType.GetMethod("get_w", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (getW != null)
                return getW.Invoke(null, null);

            // 备选：找静态字段 w 或 _w 或 Ins
            foreach (var name in new[] { "w", "_w", "Ins", "instance" })
            {
                var field = gameType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                {
                    var val = field.GetValue(null);
                    if (val != null) return val;
                }
                var prop = gameType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop != null)
                {
                    var getter = prop.GetGetMethod(true);
                    if (getter != null) return getter.Invoke(null, null);
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>Game.get_main_scene()（反编译确认：tech_helper/facility_helper 等管理器都挂在 MainScene 上）</summary>
    internal static object? GetMainScene()
    {
        try
        {
            var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (csharpAsm == null) return null;

            var gameType = csharpAsm.GetTypes().FirstOrDefault(t => t.Name == "Game");
            if (gameType == null) return null;

            var getMainScene = gameType.GetMethod("get_main_scene", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return getMainScene?.Invoke(null, null);
        }
        catch { return null; }
    }

    /// <summary>main_scene.tech_helper：科技解锁的正规入口（UnlockAllTech / UnlockNewTech）</summary>
    internal static object? GetTechHelper()
    {
        var ms = GetMainScene();
        return ms == null ? null : GetProp(ms, "tech_helper");
    }
}

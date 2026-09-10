using System;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;

namespace ChestEditor.Game;

/// <summary>
/// 地面掉落物（StuffOnMap*）删除：照抄游戏自己的删除路径
/// <c>MapStuffHelper.DestroyStuffOnMap(guid, cancel_task, show_error_log)</c>。
/// <para>
/// ⚠ 不能用通用兜底（EntityDestroyer）删掉落物：它只销毁 GameObject，不清
/// area_map 的注册表（stuff_on_map_dic / stuff_on_map_list / dic_by_pos），
/// 也不取消 NPC 挂在掉落物上的拾取任务 —— NPC 会继续跑去捡一个已经消失的东西。
/// </para>
/// <para>源码核实（MapStuffHelper.cs L154-201）：标准删除 = 三个注册表移除 +
/// StopPublishTask + RecycleToCache；返回 false 表示 guid 已不在注册表（多半被 NPC 捡走了）。</para>
/// </summary>
internal static class StuffOnMapService
{
    private static IntPtr _helper, _mth;

    /// <summary>
    /// 解析 MapStuffHelper 实例与 DestroyStuffOnMap(3 参重载)。主线程调用，一个清除批次开头调一次。
    /// <para>重载有两个：DestroyStuffOnMap(StuffOnMap,bool) 2 参 / DestroyStuffOnMap(int,bool,bool) 3 参。
    /// 用 3 参版：按 guid 查注册表（自带防重复删），show_error_log=false 让"已被捡走"静默跳过。</para>
    /// </summary>
    internal static void Resolve()
    {
        _helper = IntPtr.Zero; _mth = IntPtr.Zero;
        IntPtr helper = GameChainLocator.GetMapStuffHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper（未进存档？）");
        IntPtr mth = GetMethodFromName(GetClass(helper), "DestroyStuffOnMap", 3);
        if (mth == IntPtr.Zero)
            throw new InvalidOperationException("找不到 MapStuffHelper.DestroyStuffOnMap(int,bool,bool)");
        _helper = helper; _mth = mth;
    }

    /// <summary>删一个。<see cref="Resolve"/> 必须已成功。返回 false = 已不在注册表（多半被 NPC 捡走了）。</summary>
    internal static bool DestroyOne(int guid)
    {
        // DestroyStuffOnMap(guid, cancel_task:true, show_error_log:false)
        // cancel_task=true：把 NPC 挂在该掉落物上的拾取任务一并取消
        IntPtr boxed = Invoke(_mth, _helper, guid, true, false);
        return UnboxBool(boxed);
    }

    /// <summary>il2cpp 装箱 bool → 托管 bool。Il2CppObject 布局 = klass(8B) + monitor(8B) + data(1B)。</summary>
    private static unsafe bool UnboxBool(IntPtr boxed)
        => boxed != IntPtr.Zero && *(byte*)(boxed + 2 * sizeof(IntPtr)) != 0;
}

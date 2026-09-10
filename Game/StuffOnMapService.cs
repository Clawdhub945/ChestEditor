using System;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;

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

    /// <summary>
    /// 拾取掉落物进容器。主线程调用，分片摊帧由 handler 负责。
    /// <para>
    /// 流程（全部照抄游戏自己的路径，源码核实）：
    ///   1. 解析目标容器：'treasury' → <c>Game.main_scene.GetKingdomTreasureBox()</c>（游戏原生最稳）；
    ///      数字 → 实体表里该 stuff_id 的第一个在场 Facility（王座/箱子/货架等，需 activeInHierarchy）。
    ///   2. 每个实体：读堆叠数 count（stuff_id 扫描已存）→
    ///      <c>Facility.AddStuff(stuff_id, count)</c> 入库（Facility 基类通用方法，BagDic 字典制无容量上限）
    ///      → <see cref="DestroyOne"/>（guid 从注册表移除，等价于被"捡走"）。
    /// </para>
    /// <para>⚠ 先入库后删除：删了就读不到 count 了。空堆/实体失效不删（skipped）。</para>
    /// </summary>
    /// <param name="target">'treasury' 或目标容器的 stuff_id（十进制字符串，由前端下拉给出）</param>
    /// <returns>(picked = 成功入库并移除的堆数, skipped = 空堆/实体失效跳过数)</returns>
    internal static (int picked, int skipped, List<int> pickedHashes) PickUp(List<int> ptrHashes, string target)
    {
        IntPtr box, addStuff;
        if (target == "treasury")
        {
            IntPtr mainScene = GameChainLocator.GetMainScene();
            if (mainScene == IntPtr.Zero)
                throw new InvalidOperationException("未进入存档，找不到主场景");
            IntPtr getBox = FindMethodInHierarchy(GetClass(mainScene), "GetKingdomTreasureBox", 0);
            if (getBox == IntPtr.Zero)
                throw new InvalidOperationException("找不到 MainScene.GetKingdomTreasureBox()");
            // runtime_invoke 对引用类型返回值直接给对象指针（与 InvokeString 同一约定），Zero = 没建宝箱
            box = Invoke(getBox, mainScene);
            if (box == IntPtr.Zero)
                throw new InvalidOperationException("未找到国库宝箱（FacilityKingdomTreasureBox 还没建？）");
            addStuff = FindMethodInHierarchy(GetClass(box), "AddStuff", 2);
        }
        else
        {
            if (!int.TryParse(target, out int targetStuffId) || targetStuffId <= 0)
                throw new InvalidOperationException("无效的容器 target: " + target);
            box = IntPtr.Zero;
            foreach (var e in EntityScan.Snapshot())
            {
                if (e.ClassName == null || e.ClassName.IndexOf("Facility", StringComparison.Ordinal) != 0) continue;
                if (e.StuffId != targetStuffId) continue;
                // 池化/已拆除的容器 GO 可能已不在场，校验激活状态
                try { if (e.GoRef != null && !e.GoRef.activeInHierarchy) continue; } catch { }
                box = e.Ptr; break;
            }
            if (box == IntPtr.Zero)
                throw new InvalidOperationException("地图上找不到该容器（stuff_id=" + targetStuffId + "），先建一个或重新扫描");
            addStuff = FindMethodInHierarchy(GetClass(box), "AddStuff", 2);
        }
        if (addStuff == IntPtr.Zero)
            throw new InvalidOperationException("找不到容器 AddStuff(stuff_id, count)（基类 Facility 应有）");
        Resolve();

        int picked = 0, skipped = 0;
        var pickedHashes = new List<int>();
        foreach (int ph in ptrHashes)
        {
            var e = EntityScan.FindByPtrHash(ph);
            if (e == null || e.Guid <= 0 || e.StuffId <= 0) { skipped++; continue; }
            // count 扫描时没存，现读（字段偏移已在 FieldMeta 里，沿继承链缓存含父类）
            int count = 0;
            if (e.FieldMeta.TryGetValue("count", out var cf) && !cf.IsString && !cf.IsPointer)
                try { count = ReadIl2CppInt(e.Ptr, cf.Offset); } catch { }
            if (count <= 0) { skipped++; continue; }   // 被捡剩 0 的空堆
            Invoke(addStuff, box, e.StuffId, count);   // 入容器
            if (DestroyOne(e.Guid))
            {
                picked++;
                pickedHashes.Add(ph);
            }
            else skipped++;
        }
        return (picked, skipped, pickedHashes);
    }

    /// <summary>il2cpp 装箱 bool → 托管 bool。Il2CppObject 布局 = klass(8B) + monitor(8B) + data(1B)。</summary>
    private static unsafe bool UnboxBool(IntPtr boxed)
        => boxed != IntPtr.Zero && *(byte*)(boxed + 2 * sizeof(IntPtr)) != 0;
}

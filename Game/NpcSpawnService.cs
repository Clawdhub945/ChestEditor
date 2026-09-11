using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>
/// 召唤小人 / 召唤士兵 —— 走游戏内置"上帝模式"同款创建路径（伪 C 证实，code_meta.json）：
/// · 小精灵  = <c>NpcHelper.CreateElf(Point)</c>            游戏自己的小精灵入口（npc_type=23，
///             自带命名/随机成年年龄/离场天数）。
/// · 石头人  = <c>NpcHelper.CreateStoneMan(Point)</c>       同上（npc_type=30）。
/// · 各种族  = <c>CreateHireWorker(Point, race, count, 99999, ...)</c> 造人 + 洗成居民：
///             造完立即清 is_hire_worker（bool 单字节 0；字段在基类 BattleUnit，
///             须沿父类链查偏移）。不做传送/逐帧位置钉住——长期持 Transform 原始指针
///             在 NPC 死亡/回收后悬垂 → 原生 AV（多次闪退嫌疑）；雇工入场位置另行处理。
///             ⚠ 不直调 CreateNpc：实参矩阵（含与 CreateElf 完全一致的实参）全部在方法体内
///             NullReferenceException —— runtime_invoke 调用上下文问题，游戏自己的包装器链没事。
/// · 士兵    = <c>NpcHelper.CreateSoldierBySummon(8参)</c>  游戏"召唤士兵"原装入口：
///             (type_id, 类型名, weapon, armor, shield, mount, Vector2 落点, exist_day_count)。
///             内部查 soldier_equip_dic 取种族/随机成年年龄，is_soldier_summon=1；
///             exist_day_count 传 99999（leave=天数+99999 永不触发离场链；传 0 会得到
///             leave=0，游戏内跨天 DoOnNewDay 置 is_time_to_leave → 离场链原生 AV）；
///             weapon/armor/shield 传装备表 id（405/407/408 段 stuff_id），0 = 默认（拳头/无）。
/// 位置：借在场实体的世界坐标（与 AnimalService.Spawn 同一套合法性过滤）——
///       精灵/石头人反推格子（Point.ToVector3 = (gx+0.5, gy+0.5, 0)）。
/// ⚠ marshaling：Point/Vector2 是 8 字节 struct → 参数槽直塞 64 位打包值；
///   Vector3 是 12 字节 → 必须走 RawArg（非托管缓冲，槽装不下）。
/// </summary>
internal static class NpcSpawnService
{
    private static readonly string[] RaceNames =
        { "矮人", "蚁人", "鼠人", "猫人", "羊人", "狼人", "猪人", "精灵族", "三眼人", "蜥蜴人" };

    /// <summary>召唤小人。kind = "sprite" | "stoneman" | "race"（raceId 0..9）。</summary>
    internal static (int spawned, float x, float y) SpawnNpc(string kind, int raceId, int count)
    {
        IntPtr helper = GameChainLocator.GetNpcHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper（未进存档？）");
        IntPtr helperClass = GetClass(helper);
        count = Math.Clamp(count, 1, 10);

        var (wx, wy) = PickReferencePosition();
        var rnd = new Random();

        if (kind == "sprite" || kind == "stoneman")
        {
            string methodName = kind == "sprite" ? "CreateElf" : "CreateStoneMan";
            IntPtr m = FindMethodInHierarchy(helperClass, methodName, 1);
            if (m == IntPtr.Zero)
                throw new InvalidOperationException($"找不到 NpcHelper.{methodName}(Point)");
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                // 借参考坐标反推格子（Point.ToVector3 = (gx+0.5, gy+0.5, 0)），±2 格落地
                int gx = (int)Math.Floor(wx - 0.5f) + rnd.Next(-2, 3);
                int gy = (int)Math.Floor(wy - 0.5f) + rnd.Next(-2, 3);
                // Point 是 struct：参数槽直塞 {gx, gy} 8 字节值（x 低 32 / y 高 32）
                IntPtr npc = Invoke(m, helper, new IntPtr(gx | (gy << 32)));
                if (npc != IntPtr.Zero) { spawned++; SanitizeNpcName(npc, kind == "sprite" ? "小精灵" : "石头人"); }
            }
            Plugin.LogInfo($"[NpcSpawn] {methodName} ×{spawned}（参考 {wx:F1},{wy:F1}）");
            return (spawned, wx, wy);
        }

        if (kind == "race")
        {
            if (raceId < 0 || raceId >= RaceNames.Length)
                throw new InvalidOperationException($"未知种族 raceId={raceId}");
            string raceName = RaceNames[raceId];

            // ⚠ 不再尝试 CreateNpc 直调（15 参）：实测每次必在方法体内抛 IL2CPP 异常
            //  （消息解不出），而且**失败路径会留下半成品对象/注册状态**——游戏随后
            //  在主 tick 处理这些残留 → 随机时刻原生 AV（实测：召唤后约 3 分钟崩，
            //  崩溃日志断点无任何前缀行）。这类"部分成功再抛异常"的调用一律禁止。
            //  正确路径：游戏自己的 CreateHireWorker（见下），一次成功、无残留。

            // ===== CreateHireWorker 造人 + 洗成居民（唯一的村民创建路径）=====
            // 雇工出生在 start_pos（该入口本身接受坐标），只需清掉 is_hire_worker 标记
            // 让它变成普通居民。⚠ 不做传送/逐帧钉位置（长持 Transform 指针 = 原生 AV），
            //   ⚠ 也不动 leave_time_days（CreateHireWorker 已写 天数+99999；清 0 会触发离场链 AV）。
            IntPtr hire = FindMethodInHierarchy(helperClass, "CreateHireWorker", 7);
            if (hire == IntPtr.Zero)
                throw new InvalidOperationException("找不到 NpcHelper.CreateHireWorker(7 参)");

            int gx = (int)Math.Floor(wx - 0.5f) + rnd.Next(-2, 3);
            int gy = (int)Math.Floor(wy - 0.5f) + rnd.Next(-2, 3);
            IntPtr listPtr = Invoke(hire, helper,
                new IntPtr(gx | (gy << 32)),   // Point 是 struct：槽直塞 8 字节
                raceId,
                count,
                99999,                         // 离场天数（下面会显式清 0）
                false,                         // is_educated
                0, 0);                         // tool / clothes = 无
            if (listPtr == IntPtr.Zero)
                throw new InvalidOperationException("CreateHireWorker 返回空");

            IntPtr cntM = FindMethodInHierarchy(GetClass(listPtr), "get_Count", 0);
            IntPtr itemM = FindMethodInHierarchy(GetClass(listPtr), "get_Item", 1);
            int made = cntM != IntPtr.Zero ? InvokeInt(cntM, listPtr) : 0;
            int spawned2 = 0;
            for (int i = 0; i < made; i++)
            {
                IntPtr npcPtr = itemM != IntPtr.Zero ? Invoke(itemM, listPtr, i) : IntPtr.Zero;
                if (npcPtr == IntPtr.Zero) continue;
                spawned2 += MakeResident(npcPtr);
            }
            Plugin.LogInfo($"[NpcSpawn] 居民({raceName}) ×{spawned2}（参考 {wx:F1},{wy:F1}，格 {gx},{gy}；雇工洗白）");
            return (spawned2, wx, wy);
        }

        throw new InvalidOperationException($"未知 kind={kind}");
    }

    private static string seq(int i) => i.ToString();

    /// <summary>
    /// 雇工 → 居民：清 is_hire_worker（bool 单字节写 0，字段在基类 BattleUnit，沿父类链查偏移）。
    /// ⚠ 不做传送/位置钉住：逐帧 set_position 需要长时间持有 Transform 原始指针，
    ///   NPC 在战斗中死亡/被回收后即悬垂 → 原生 AV（多次闪退嫌疑）。
    ///   位置问题（雇工从地图边缘入场）另行处理——玩家反馈"坐标先不理"。
    /// </summary>
    private static int MakeResident(IntPtr npcPtr)
    {
        IntPtr npcClass = GetClass(npcPtr);
        // 状态机初始化：CreateHireWorker 内核不调 ChangeState → 新 NPC cur_state=null。
        // 实测后果：**保存存档必崩**（序列化解引用空状态对象；村民/士兵都中招）。
        // 居民用 ChangeStateToIdle(float)（native 居民状态 NpcStateWanderIdle 由此而来）。
        IntPtr mIdle = FindMethodInHierarchy(npcClass, "ChangeStateToIdle", 1);
        if (mIdle != IntPtr.Zero) Invoke(mIdle, npcPtr, 0f);
        else Plugin.LogInfo("[NpcSpawn] 未找到 ChangeStateToIdle/1（居民状态机未初始化）");
        // 名字兜底：悬停显示时 TMPro 解析损坏名字会崩（见 SanitizeNpcName 注释）
        SanitizeNpcName(npcPtr, "新人");
        if (TryFindFieldOffset(npcClass, "is_hire_worker", out int offHire) && offHire > 0)
        {
            WriteIl2CppByte(npcPtr, offHire, 0);
            return 1;
        }
        Plugin.LogInfo("[NpcSpawn] 未找到 is_hire_worker 字段（雇工标记未清除）");
        return 0;
    }

    // ===== 召唤士兵离场时间修复（存量 + 兜底） =====
    // ⚠ 这里原有 FixSummonedLeaveTime 存量修复泵（每 5s 遍历实体快照的裸指针改 leave），已移除。
    // 原因（崩溃实锤）：EntityScan 快照里的 Ptr / GoRef 是**扫描瞬间**的缓存值，
    // NPC 阵亡或被销毁后即悬垂；GetClass(e.Ptr) / ReadIl2CppByte(e.Ptr,..) 走裸指针，
    // 绕过 Unity 的销毁保护（GoRef 访问会抛可捕获的托管异常，Ptr 直读则是裸 AV，catch 不住），
    // 于是每 5s 的一次全量遍历就成了随机时刻的原生崩溃点。
    // 新建士兵已在源头用 exist_day_count=99999 修好，无需存量泵；长周期写游戏对象一律禁止。

    /// <summary>召唤士兵（上帝模式同款：兵种 + 武器/盔甲/盾牌，0 = 默认）。</summary>
    internal static (int spawned, float x, float y) SpawnSoldier(
        int soldierTypeId, int weaponId, int armorId, int shieldId, int count)
    {
        // ===== 本地士兵：直调 NpcHelper.CreateSoldier（13 参内核，is_soldier_summon=false）=====
        // 这是召唤入口 CreateSoldierBySummon 内部转调的**同一个函数**（今天已验证跑得通），
        // 唯一区别是 is_soldier_summon 传 false → 与兵营招募的本地兵完全同状态：
        //   · is_soldier_summon=0 → 无离场时限/无召唤标记（用户要"本地士兵"）
        //   · leave_time_days=0 → 与原生本地兵一致（实机 dump：summon=0, leave=0）
        //   · 内部自建 SoldierConfigInfo（type/weapon/armor/shield）+ Soldier.SetFightMode(1)
        // 落点 = Tile.GridIndexToPositionWithRandomOffset（兵营同款格子→世界坐标换算）。
        // 名字 = 游戏 NpcNameGenerator（姓 / 全名），与原生兵 "詹"/"詹轩宇" 一致。
        // ⚠ 不走 SoldierHelper.CreateSoldier 包装：探针实测其调用链（含 RAW _6454850096）
        //   在 runtime_invoke 环境下必抛 KeyNotFoundException（原因未明，疑与调用上下文相关）。
        IntPtr helper = GameChainLocator.GetNpcHelper();
        if (helper == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper（未进存档？）");
        count = Math.Clamp(count, 1, 10);

        string typeName = DataTables.SoldierTypeName(soldierTypeId);
        if (string.IsNullOrEmpty(typeName)) typeName = soldierTypeId.ToString();
        // 兵种 → 种族（官方 race_id_limit：骑士=0、精灵系=7；实机原生兵 raceId 与之一一吻合）
        int raceId = DataTables.SoldierTypeRace(soldierTypeId);

        IntPtr helperClass = GetClass(helper);
        // 内核 CreateSoldier 13 参：type, family, name, weapon, armor, shield, mount,
        // Vector2, age, is_male, exist_day_count, race_id, is_soldier_summon（计数不含 this）
        IntPtr mCreate = FindMethodInHierarchy(helperClass, "CreateSoldier", 13);
        if (mCreate == IntPtr.Zero)
            throw new InvalidOperationException("找不到 NpcHelper.CreateSoldier(13 参)");

        var (wx, wy) = PickReferencePosition();
        int gx = (int)Math.Floor(wx - 0.5f);
        int gy = (int)Math.Floor(wy - 0.5f);
        // 兵营同款：格子 → 世界坐标（游戏自己的换算 + 随机偏移）；Tile 不可用则退回参考点
        (float tx, float ty) = TileGridToWorld(gx, gy) ?? (wx, wy);

        var rnd = new Random();
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            var (family, full) = GameChainLocator.GenerateNpcNameParts(raceId);
            if (string.IsNullOrEmpty(full)) full = typeName;
            if (string.IsNullOrEmpty(family)) family = full;

            // ⚠⚠ 字符串参数必须 pinned GC 根（故意不释放）：il2cpp 的 Boehm GC 不扫描
            //   .NET 托管堆 —— 参数槽里的字符串一旦被回收，游戏存进 npc_name 的就是
            //   悬垂/空串 → 悬停时 TMPro 解析崩（GameAssembly+0x445291 闪退根因）。
            IntPtr famPtr = StringToIl2Cpp(family);
            GcHandleNew(famPtr, true);
            IntPtr namePtr = StringToIl2Cpp(full);
            GcHandleNew(namePtr, true);

            // Vector2 是 8 字节 struct：槽直塞 {x, y} 两个 float 的位模式（x 低 32 / y 高 32）
            long packed = (long)(uint)BitConverter.SingleToInt32Bits(tx)
                        | ((long)(uint)BitConverter.SingleToInt32Bits(ty) << 32);
            // exist_day_count=0 → leave_time_days=0；is_soldier_summon=false → 本地兵
            IntPtr npc = Invoke(mCreate, helper, soldierTypeId, famPtr, namePtr,
                weaponId, armorId, shieldId, 0, packed,
                rnd.Next(20, 41), rnd.Next(2) == 0, 0, raceId, false);
            if (npc == IntPtr.Zero) continue;
            spawned++;
            PostprocessSoldier(npc, raceId, full, family);
        }
        Plugin.LogInfo($"[NpcSpawn] 本地士兵({typeName}) ×{spawned} 武器{weaponId} 盔甲{armorId} 盾牌{shieldId}（参考 {wx:F1},{wy:F1}→落 {tx:F1},{ty:F1}，race={raceId}）");
        return (spawned, tx, ty);
    }

    /// <summary>
    /// Tile.GridIndexToPositionWithRandomOffset(Vector3Int)：游戏自己的"格子→世界坐标"换算
    /// （兵营招募同款，含随机偏移）。返回值是装箱 Vector3（值在 +16 起）。
    /// </summary>
    private static (float, float)? TileGridToWorld(int gx, int gy)
    {
        try
        {
            IntPtr tileClass = FindClassByName("Tile");
            if (tileClass == IntPtr.Zero) { Plugin.LogInfo("[NpcSpawn] 找不到 Tile 类"); return null; }
            IntPtr m = FindMethodInHierarchy(tileClass, "GridIndexToPositionWithRandomOffset", 1);
            if (m == IntPtr.Zero) { Plugin.LogInfo("[NpcSpawn] 找不到 Tile.GridIndexToPositionWithRandomOffset"); return null; }
            // Vector3Int 是 12 字节 struct（m_X,m_Y,m_Z）→ 槽装不下，走 RawArg
            IntPtr vec = Marshal.AllocHGlobal(12);
            try
            {
                Marshal.WriteInt32(vec, 0, gx);
                Marshal.WriteInt32(vec, 4, gy);
                Marshal.WriteInt32(vec, 8, 0);
                IntPtr boxed = Invoke(m, IntPtr.Zero, new RawArg(vec));
                if (boxed == IntPtr.Zero) return null;
                return (ReadIl2CppFloat(boxed, 16), ReadIl2CppFloat(boxed, 20));
            }
            finally { Marshal.FreeHGlobal(vec); }
        }
        catch (Exception ex) { Plugin.LogInfo($"[NpcSpawn] Tile 换算失败: {ex.Message}"); return null; }
    }

    /// <summary>兵营路径的收尾（FacilityBarracks.DoCreateSoldier 同款）：训练度拉满、精灵/三眼人项链、名字兜底。</summary>
    private static void PostprocessSoldier(IntPtr npc, int raceId, string fallbackName, string family)
    {
        IntPtr npcClass = GetClass(npc);
        if (TryFindFieldOffset(npcClass, "training_progress", out int offTrain) && offTrain > 0)
            WriteIl2CppFloat(npc, offTrain, 1.0f);
        if ((raceId == 7 || raceId == 8) && TryFindFieldOffset(npcClass, "nacklace_stuff_id", out int offNeck) && offNeck > 0)
            WriteIl2CppInt(npc, offNeck, 427001);
        // 状态机收尾：兵营包装 CreateSoldier 返回前必调虚方法 ChangeStateToIdle，
        // 13 参内核不调 → 新兵 _cur_state_name 为空（当前状态对象 null）。
        // 实测后果：**保存存档必崩**（GameAssembly+0x24c2a09 / 0x24c2a56，两次同点）——
        // 序列化 NPC 时解引用空状态对象。
        // native 兵状态 = NpcStateSoldier（ChangeStateToSoldier 0 参）；兜底 ChangeStateToIdle(float)
        IntPtr mState = FindMethodInHierarchy(npcClass, "ChangeStateToSoldier", 0);
        if (mState != IntPtr.Zero)
        {
            Invoke(mState, npc);
        }
        else if ((mState = FindMethodInHierarchy(npcClass, "ChangeStateToIdle", 1)) != IntPtr.Zero)
        {
            Invoke(mState, npc, 0f);
        }
        else
        {
            Plugin.LogInfo("[NpcSpawn] 状态机收尾方法都没找到");
        }
        // 名字兜底：正常情况下游戏内部已生成人名；这里只挡损坏值（TMPro 悬停崩的教训）
        SanitizeNpcName(npc, GameChainLocator.GenerateNpcName(raceId) ?? fallbackName);
        // family_name 兜底：实测新兵该字段常为垃圾（'DTime'/'mage'/NUL，游戏延迟初始化所致），
        // 存档序列化同样有风险 → 写入我们生成的姓（原生兵 family_name='詹' 即姓）
        SanitizeStringField(npc, npcClass, "family_name",
            string.IsNullOrEmpty(family) ? fallbackName : family);
    }

    /// <summary>按字段名兜底字符串字段：读回不合法（空/坏字符/超长）就写入 fallback（pinned 根保护）。</summary>
    private static void SanitizeStringField(IntPtr npcPtr, IntPtr npcClass, string fieldName, string fallback)
    {
        try
        {
            if (string.IsNullOrEmpty(fallback)) return;
            if (!TryFindFieldOffset(npcClass, fieldName, out int off) || off <= 0) return;
            string? cur = ReadIl2CppString(npcPtr, off);
            if (IsSafeDisplayName(cur)) return;
            IntPtr s = StringToIl2Cpp(fallback);
            GcHandleNew(s, true);              // 保护到写入完成（NPC 字段持有后即可达）
            WriteIl2CppPointer(npcPtr, off, s);
            Plugin.LogInfo($"[NpcSpawn] {fieldName} 不合法({Describe(cur)}) → 已修正为 '{fallback}'");
        }
        catch (Exception ex) { Plugin.LogInfo($"[NpcSpawn] SanitizeStringField({fieldName}) 失败: {ex.Message}"); }
    }

    /// <summary>
    /// 【诊断·临时】分步执行 SoldierHelper.CreateSoldier 的内部逻辑（每步一个日志标记），
    /// 用 LogOutput 中"最后一步标记 + 异常行"定位 KeyNotFoundException 的确切来源。
    /// </summary>
    internal static string ProbeSoldierChain(int soldierTypeId, int raceId)
    {
        var log = new List<string>();
        void Mark(string m) { Plugin.LogInfo($"[ProbeS] >>> {m}"); log.Add(m); }

        IntPtr soldierHelper = GameChainLocator.GetSoldierHelper();
        log.Add($"soldierHelper=0x{soldierHelper:X} class={Il2CppApi.GetClassName(GetClass(soldierHelper)) ?? "?"}");
        IntPtr helperClass = GetClass(soldierHelper);

        Mark("m6 = CreateSoldier/6");
        IntPtr m6 = FindMethodInHierarchy(helperClass, "CreateSoldier", 6);
        log.Add($"m6=0x{m6:X} paramCount={(m6 != IntPtr.Zero ? GetMethodParamCountRaw(m6).ToString() : "n/a")}");

        Mark("m5 = CreateSoldier/5 (raw)");
        IntPtr m5 = FindMethodInHierarchy(helperClass, "CreateSoldier", 5);
        log.Add($"m5=0x{m5:X} paramCount={(m5 != IntPtr.Zero ? GetMethodParamCountRaw(m5).ToString() : "n/a")}");

        Mark("D.Ins");
        IntPtr clsD = FindClassByName("D");
        IntPtr ins = IntPtr.Zero;
        foreach (var fh in EnumerateFieldHandles(clsD))
        {
            if (fh.Name == "Ins" && fh.IsStatic) { ins = ReadStaticFieldValue(fh.Field); break; }
        }
        log.Add($"D.Ins=0x{ins:X} class={(ins != IntPtr.Zero ? Il2CppApi.GetClassName(GetClass(ins)) ?? "?" : "null")}");
        IntPtr insClass = GetClass(ins);

        Mark("equip_dic[202301]");
        IntPtr equipDic = ReadFieldSafe(ins, insClass, "soldier_equip_dic");
        IntPtr mGetItem = FindMethodInHierarchy(GetClass(equipDic), "get_Item", 1);
        IntPtr equip = Invoke(mGetItem, equipDic, soldierTypeId);
        log.Add($"equip=0x{equip:X}");

        Mark("race_dic[0]");
        IntPtr raceDic = ReadFieldSafe(ins, insClass, "race_dic");
        IntPtr raceInfo = Invoke(mGetItem2(GetClass(raceDic)), raceDic, raceId);
        log.Add($"raceInfo=0x{raceInfo:X}");

        Mark("GetRandomFamilyName(0)");
        IntPtr genClass = FindClassByName("NpcNameGenerator");
        IntPtr mFam = FindMethodInHierarchy(genClass, "GetRandomFamilyName", 1);
        IntPtr fam = Invoke(mFam, IntPtr.Zero, raceId);
        log.Add($"family='{(fam != IntPtr.Zero ? ReadStringObject(fam) : null)}'");

        Mark("GetName(1, race, family)");
        IntPtr mName = FindMethodInHierarchy(genClass, "GetName", 3);
        IntPtr nameObj = fam != IntPtr.Zero && mName != IntPtr.Zero ? Invoke(mName, IntPtr.Zero, 1, raceId, fam) : IntPtr.Zero;
        log.Add($"name='{(nameObj != IntPtr.Zero ? ReadStringObject(nameObj) : null)}'");

        Mark("config new + fields");
        IntPtr configClass = FindClassByName("SoldierConfigInfo");
        IntPtr config = ObjectNew(configClass);
        IntPtr ctor = FindMethodInHierarchy(configClass, ".ctor", 0);
        if (ctor != IntPtr.Zero) Invoke(ctor, config);
        if (TryFindFieldOffset(configClass, "soldier_type_id", out int offType)) WriteIl2CppInt(config, offType, soldierTypeId);
        log.Add($"config=0x{config:X}");

        var (wx, wy) = PickReferencePosition();
        int gx = (int)Math.Floor(wx - 0.5f), gy = (int)Math.Floor(wy - 0.5f);

        Mark("RAW _6454850096(troop=0,team=0,config,Point,race)");
        IntPtr r1 = m5 != IntPtr.Zero ? Invoke(m5, soldierHelper, IntPtr.Zero, IntPtr.Zero, config,
            new IntPtr(gx | (gy << 32)), raceId) : IntPtr.Zero;
        log.Add($"raw→0x{r1:X}");

        Mark("WRAPPER CreateSoldier(config,0,0,Point,race,null)");
        IntPtr r2 = Invoke(m6, soldierHelper, config, IntPtr.Zero, IntPtr.Zero,
            new IntPtr(gx | (gy << 32)), raceId, IntPtr.Zero);
        log.Add($"wrapper→0x{r2:X}");

        return string.Join(" | ", log);
    }

    private static IntPtr mGetItem2(IntPtr dicClass) => FindMethodInHierarchy(dicClass, "get_Item", 1);

    /// <summary>
    /// 【诊断·临时】直调 UI.DoSave(folder, null) 触发游戏保存（复现"召唤兵保存崩"用）。
    /// cb 传 null：保存完成后游戏回调处可能 NRE，但此时存档数据已写完，不影响验证。
    /// </summary>
    internal static string ProbeSave(string folderName)
    {
        IntPtr uiClass = FindClassByName("UI");
        if (uiClass == IntPtr.Zero) return "no UI class";
        IntPtr ui = IntPtr.Zero;
        foreach (var fh in EnumerateFieldHandles(uiClass))
        {
            if (fh.Name == "Ins" && fh.IsStatic) { ui = ReadStaticFieldValue(fh.Field); break; }
        }
        if (ui == IntPtr.Zero) return "UI.Ins null";
        IntPtr mDoSave = FindMethodInHierarchy(uiClass, "DoSave", 2);
        if (mDoSave == IntPtr.Zero) return "no DoSave/2";
        IntPtr folder = StringToIl2Cpp(folderName);
        GcHandleNew(folder, true);
        // cb 必须是真委托：null 会在保存完成后回调处延迟崩（实测）。托管委托 → il2cpp Action。
        var managed = new System.Action(() => Plugin.LogInfo("[SaveProbe] ★ 保存完成回调触发"));
        var cb = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(managed);
        IntPtr cbPtr = GetIl2CppPtr(cb);
        Plugin.LogInfo("[SaveProbe] >>> UI.DoSave 开始");
        IntPtr r = Invoke(mDoSave, ui, folder, cbPtr);
        Plugin.LogInfo($"[SaveProbe] <<< UI.DoSave 返回 0x{r:X}");
        return $"DoSave invoked r=0x{r:X}";
    }

    /// <summary>
    /// 【诊断·临时】对比两个 NPC 的全部引用类型字段（沿父类链；只读 CLASS/GENERICINST/ARRAY
    /// 种类的字段——值类型/字符串/位域跳过，否则垃圾指针 GetClass = 裸 AV）。
    /// </summary>
    internal static string ProbeRefFields(IntPtr oursPtr, IntPtr nativePtr)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (ptr, tag) in new[] { (oursPtr, "OURS"), (nativePtr, "NATIVE") })
        {
            sb.Append($"== {tag} 0x{ptr:X} ==\n");
            int depth = 0;
            for (IntPtr walk = GetClass(ptr); walk != IntPtr.Zero && depth < 10; walk = GetParent(walk), depth++)
            {
                foreach (var fh in EnumerateFieldHandles(walk))
                {
                    if (fh.IsStatic || fh.Offset <= 0) continue;
                    IntPtr typePtr = FieldGetType(fh.Field);
                    if (typePtr == IntPtr.Zero) continue;
                    int kind = TypeGetType(typePtr);
                    if (kind != 0x12 && kind != 0x15 && kind != 0x14 && kind != 0x1d) continue;  // 仅引用类型
                    try
                    {
                        IntPtr v = ReadIl2CppPointer(ptr, fh.Offset);
                        string state = v == IntPtr.Zero
                            ? "NULL"
                            : "obj:" + (Il2CppApi.GetClassName(GetClass(v)) ?? "?");
                        sb.Append($"{fh.Name} = {state}\n");
                    }
                    catch { sb.Append($"{fh.Name} = <read err>\n"); }
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>按字段名写 SoldierConfigInfo 的 int 字段（偏移沿父类链查，找不到记日志跳过）。</summary>
    private static void WriteConfigInt(IntPtr config, IntPtr configClass, string fieldName, int value)
    {
        if (TryFindFieldOffset(configClass, fieldName, out int off) && off > 0)
        {
            WriteIl2CppInt(config, off, value);
            int back = ReadIl2CppInt(config, off);
            Plugin.LogInfo($"[NpcSpawn] config.{fieldName} off={off} 写{value} 读回{back}" + (back == value ? "" : " ⚠读回不一致"));
        }
        else
            Plugin.LogInfo($"[NpcSpawn] SoldierConfigInfo 未找到字段 {fieldName}");
    }

    /// <summary>
    /// 校验并修正 NPC 的显示名（npc_name）。
    /// 为什么必须做：鼠标悬停到 NPC 时游戏走
    /// `Npc.OnPointerEnter → UI.ShowMouseTip(npc名) → MyUtil.ForceRebuildLayoutImmediate
    ///  → TMPro.TMP_Text.ParseInputText`，若名字是空/含 NUL/未配对代理项，
    /// TMP 计算文本长度会得到负数 → `Array.Resize(newSize)` 抛
    /// ArgumentOutOfRangeException → 原生 AV（实测崩溃偏移固定 GameAssembly+0x445291）。
    /// 兜底：名字不合法就写入一个安全中文名（名字重复无副作用，优先保证不崩）。
    /// </summary>
    private static void SanitizeNpcName(IntPtr npcPtr, string fallback)
    {
        try
        {
            IntPtr cls = GetClass(npcPtr);
            if (!TryFindFieldOffset(cls, "npc_name", out int off) || off <= 0) return;
            string? nm = ReadIl2CppString(npcPtr, off);
            if (IsSafeDisplayName(nm)) return;

            IntPtr s = StringToIl2Cpp(fallback);
            GcHandleNew(s, true);              // 保护到写入完成（NPC 字段持有后即可达）
            WriteIl2CppPointer(npcPtr, off, s);
            Plugin.LogInfo($"[NpcSpawn] npc_name 不合法({Describe(nm)}) → 已修正为 '{fallback}'");
        }
        catch (Exception ex) { Plugin.LogInfo($"[NpcSpawn] SanitizeNpcName 失败: {ex.Message}"); }
    }

    /// <summary>名字是否能安全交给 TMPro 渲染：非空、无 NUL/控制字符、无未配对代理项、长度合理。</summary>
    private static bool IsSafeDisplayName(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length > 24) return false;
        foreach (char c in s)
        {
            if (c == '\0' || char.IsSurrogate(c) || char.IsControl(c)) return false;
        }
        return true;
    }

    private static string Describe(string? s)
    {
        if (s == null) return "null";
        if (s.Length == 0) return "空串";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s)
        {
            if (sb.Length >= 12) { sb.Append("…"); break; }
            sb.Append(c < 0x20 || char.IsSurrogate(c) ? $"\\u{(int)c:X4}" : c.ToString());
        }
        return $"len={s.Length} '{sb}'";
    }

    /// <summary>
    /// 参考落点：从实体快照里挑一个坐标合法的在场实体（我方建筑/动物/NPC）。
    /// 与 AnimalService.Spawn 同一套过滤（±5000 之外是脏数据；全图随机会召唤到未加载区块）。
    /// </summary>
    private static (float x, float y) PickReferencePosition()
    {
        var candidates = new List<(float x, float y)>();
        int total = 0, skipNotMatch = 0, skipInactive = 0, skipCoord = 0, skipZero = 0, skipEx = 0, skipNoGo = 0;
        foreach (var e in EntityScan.Snapshot())
        {
            total++;
            if (e.GoRef == null) { skipNoGo++; continue; }
            string? cn = e.ClassName;
            bool okSrc = cn == "Animal" || cn == "Npc"
                || (cn != null && cn.IndexOf("Facility", StringComparison.Ordinal) == 0);
            if (!okSrc) { skipNotMatch++; continue; }
            try
            {
                if (!e.GoRef.activeInHierarchy) { skipInactive++; continue; }
                var p = e.GoRef.transform.position;
                if (p.x == 0 && p.y == 0) { skipZero++; continue; }
                if (Math.Abs(p.x) > 5000f || Math.Abs(p.y) > 5000f) { skipCoord++; continue; }
                candidates.Add((p.x, p.y));
            }
            catch { skipEx++; }
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"没有坐标合法的参考实体（实体 {total}：类型不符 {skipNotMatch}、未激活 {skipInactive}、" +
                $"坐标超限 {skipCoord}、零坐标 {skipZero}、异常 {skipEx}、无GO {skipNoGo}）——请先重新扫描");
        var rnd = new Random();
        var pick = candidates[rnd.Next(candidates.Count)];
        Plugin.LogInfo($"[NpcSpawn] 参考候选 {candidates.Count} 个，取 ({pick.x:F1},{pick.y:F1})");
        return pick;
    }

    /// <summary>
    /// 【诊断·临时】把实体世界坐标转成屏幕坐标 —— 供自动化把鼠标精确移到它上面
    /// （悬停复现用）。注意 Unity 屏幕坐标原点在左下，Win32 SetCursorPos 在左上，
    /// 调用方需 y = 屏幕高 - sy。
    /// </summary>
    internal static string GetScreenPos(int ptrHash)
    {
        UnityEngine.Transform? tr = null;
        foreach (var e in EntityScan.Snapshot())
            if (e.PtrHash == ptrHash) { tr = e.GoRef != null ? e.GoRef.transform : null; break; }
        if (tr == null) throw new InvalidOperationException($"未找到 ptrHash={ptrHash}（或对象已销毁）");
        var cam = UnityEngine.Camera.main;
        if (cam == null) throw new InvalidOperationException("Camera.main 为空");
        var wp = tr.position;
        var sp = cam.WorldToScreenPoint(wp);
        int h = UnityEngine.Screen.height;
        Plugin.LogInfo($"[ScreenPos] world=({wp.x:F2},{wp.y:F2},{wp.z:F2}) screen=({sp.x:F1},{sp.y:F1}) z={sp.z:F1} cam={cam.name} h={h} w={UnityEngine.Screen.width}");
        return JsonBuilder.Object(w =>
        {
            w.WriteNumber("sx", JsonBuilder.Safe(sp.x));
            w.WriteNumber("sy", JsonBuilder.Safe(sp.y));
            w.WriteNumber("z", JsonBuilder.Safe(sp.z));
            w.WriteNumber("screenH", h);
            w.WriteNumber("screenW", UnityEngine.Screen.width);
        });
    }

    /// <summary>
    /// 【诊断·临时】模拟游戏"鼠标悬停到该 NPC"时执行的读取路径
    /// （NpcInfoPanel.UpdateSoldierInfo 前半段：读装备字段 → IsSoldier → GetSoldierAttrDesc）。
    /// 每步调用**前**先写日志 —— 若崩溃，LogOutput.log 最后一行即崩点。定位完可删。
    /// </summary>
    internal static string ProbeHover(int ptrHash)
    {
        IntPtr npc = IntPtr.Zero;
        foreach (var e in EntityScan.Snapshot())
            if (e.PtrHash == ptrHash) { npc = e.Ptr; break; }
        if (npc == IntPtr.Zero) throw new InvalidOperationException($"未找到 ptrHash={ptrHash}");

        IntPtr cls = GetClass(npc);
        var sb = new System.Text.StringBuilder();
        sb.Append($"cls={(cls != IntPtr.Zero ? "ok" : "NULL")} ");
        TryFindFieldOffset(cls, "soldier_type_id", out int offSid);
        TryFindFieldOffset(cls, "weapon", out int offW);
        TryFindFieldOffset(cls, "armor", out int offA);
        TryFindFieldOffset(cls, "shield", out int offSh);
        TryFindFieldOffset(cls, "weapon_level_stuff_id", out int offWl);
        TryFindFieldOffset(cls, "armor_level_stuff_id", out int offAl);
        TryFindFieldOffset(cls, "shield_level_stuff_id", out int offSl);
        TryFindFieldOffset(cls, "npc_type", out int offTyp);
        if (offSid > 0) sb.Append($"sid={ReadIl2CppInt(npc, offSid)} ");
        if (offTyp > 0) sb.Append($"npc_type={ReadIl2CppInt(npc, offTyp)} ");
        if (offW > 0) sb.Append($"weapon={ReadIl2CppInt(npc, offW)} ");
        if (offA > 0) sb.Append($"armor={ReadIl2CppInt(npc, offA)} ");
        if (offSh > 0) sb.Append($"shield={ReadIl2CppInt(npc, offSh)} ");
        if (offWl > 0) sb.Append($"wl_stuff={ReadIl2CppInt(npc, offWl)} ");
        if (offAl > 0) sb.Append($"al_stuff={ReadIl2CppInt(npc, offAl)} ");
        if (offSl > 0) sb.Append($"sl_stuff={ReadIl2CppInt(npc, offSl)} ");
        Plugin.LogInfo($"[Probe] ①字段读取: {sb}");

        // 状态对象指针：若为 NULL，悬停时读状态名可能空/崩
        if (TryFindFieldOffset(cls, "_cur_state", out int offSt) && offSt > 0)
        {
            IntPtr st = ReadIl2CppPointer(npc, offSt);
            Plugin.LogInfo($"[Probe] ②状态指针 _cur_state = {(st != IntPtr.Zero ? "0x" + st.ToString("X") : "NULL")}");
        }
        else Plugin.LogInfo("[Probe] ②未找到 _cur_state 字段");

        IntPtr mIs = FindMethodInHierarchy(cls, "IsSoldier", 0);
        Plugin.LogInfo($"[Probe] ③准备调用 IsSoldier (方法={(mIs != IntPtr.Zero ? "ok" : "NULL")})");
        if (mIs != IntPtr.Zero)
            Plugin.LogInfo($"[Probe] ③IsSoldier 返回 {InvokeInt(mIs, npc)}");

        IntPtr mDesc = FindMethodInHierarchy(cls, "GetSoldierAttrDesc", 4);
        Plugin.LogInfo($"[Probe] ④准备调用 GetSoldierAttrDesc (方法={(mDesc != IntPtr.Zero ? "ok" : "NULL")})");
        if (mDesc != IntPtr.Zero)
        {
            IntPtr buf = Marshal.AllocHGlobal(32);
            try
            {
                for (int i = 0; i < 4; i++) Marshal.WriteIntPtr(buf, i * 8, IntPtr.Zero);
                Invoke(mDesc, npc, buf, buf + 8, buf + 16, buf + 24);
                Plugin.LogInfo($"[Probe] ④GetSoldierAttrDesc → hp='{ReadStringObject(Marshal.ReadIntPtr(buf))}' " +
                    $"weapon='{ReadStringObject(Marshal.ReadIntPtr(buf, 8))}' " +
                    $"armor='{ReadStringObject(Marshal.ReadIntPtr(buf, 16))}' " +
                    $"shield='{ReadStringObject(Marshal.ReadIntPtr(buf, 24))}'");
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        Plugin.LogInfo("[Probe] ⑤全部读取通过（未崩溃）");
        return JsonBuilder.Object(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteBoolean("allReadPassed", true);
        });
    }
}

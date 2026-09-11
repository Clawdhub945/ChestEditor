namespace ChestEditor.Web;

/// <summary>
/// 召唤小人/士兵接口（盒子1「小人数值修改」顶部召唤区）。
/// 创建路径见 NpcSpawnService 注释（游戏内置"上帝模式"同款）。
/// 召唤成功后服务端直接把相机飞到落点（ChestService.LocateFacility），前端只需重扫刷新。
/// </summary>
internal static class NpcSpawnHandlers
{
    internal static void Register()
    {
        // 下拉数据：{races:[[id,name]], soldierTypes:[[typeId,name,weaponGroup,armorGroup,shieldGroup]],
        //           weapons:[[id,name,group]], armors, shields} —— spawn_tables.json 原样透传
        Router.Add("GET", "/api/npcspawn/options", _ => DataTables.SpawnTablesJson());

        // POST /api/npcspawn/npc {kind:"sprite"|"stoneman"|"race", raceId:0..9, count:1..10}
        Router.Add("POST", "/api/npcspawn/npc", ctx =>
        {
            string kind = ctx.JsonStr("kind") ?? "";
            int raceId = ctx.JsonInt("raceId");
            int count = Math.Max(1, ctx.JsonInt("count"));
            if (string.IsNullOrEmpty(kind)) throw new HttpError(400, "missing kind");
            return MainThread.Run(() =>
            {
                var (spawned, x, y) = Game.NpcSpawnService.SpawnNpc(kind, raceId, count);
                if (spawned > 0) ChestService.LocateFacility(x, y);
                return JsonBuilder.Object(w =>
                {
                    w.WriteBoolean("ok", true);
                    w.WriteNumber("spawned", spawned);
                    w.WriteNumber("x", JsonBuilder.Safe(x));
                    w.WriteNumber("y", JsonBuilder.Safe(y));
                });
            }, 30000);
        });

        // POST /api/npcspawn/soldier {soldierTypeId, weaponId, armorId, shieldId, count}（装备 0 = 默认）
        Router.Add("POST", "/api/npcspawn/soldier", ctx =>
        {
            int soldierTypeId = ctx.JsonInt("soldierTypeId");
            int weaponId = ctx.JsonInt("weaponId");
            int armorId = ctx.JsonInt("armorId");
            int shieldId = ctx.JsonInt("shieldId");
            int count = Math.Max(1, ctx.JsonInt("count"));
            if (soldierTypeId <= 0) throw new HttpError(400, "missing soldierTypeId");
            return MainThread.Run(() =>
            {
                var (spawned, x, y) = Game.NpcSpawnService.SpawnSoldier(soldierTypeId, weaponId, armorId, shieldId, count);
                if (spawned > 0) ChestService.LocateFacility(x, y);
                return JsonBuilder.Object(w =>
                {
                    w.WriteBoolean("ok", true);
                    w.WriteNumber("spawned", spawned);
                    w.WriteNumber("x", JsonBuilder.Safe(x));
                    w.WriteNumber("y", JsonBuilder.Safe(y));
                });
            }, 30000);
        });
    }
}

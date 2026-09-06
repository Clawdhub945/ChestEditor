using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>龙系统接口：素材背包、召唤、龙魂、龙实体</summary>
internal static class DragonHandlers
{
    internal static void Register()
    {
        Router.Add("GET", "/api/dragon", _ =>
            MainThread.Run(() => DragonService.GetBagJson()));

        // 永恒神殿（驯龙→龙素材面板）
        Router.Add("GET", "/api/temple", _ =>
            MainThread.Run(() => ChestService.GetTempleJson()));

        Router.Add("POST", "/api/temple/set", ctx =>
        {
            int stuffId = ctx.JsonInt("stuffId");
            int count = ctx.JsonInt("count");
            if (stuffId <= 0) throw new HttpError(400, "invalid stuffId");
            return MainThread.Run(() => ChestService.SetTempleItem(stuffId, count));
        });

        Router.Add("POST", "/api/temple/plan", ctx =>
        {
            int stuffId = ctx.JsonInt("stuffId");
            int count = ctx.JsonInt("count");
            if (stuffId <= 0) throw new HttpError(400, "invalid stuffId");
            return MainThread.Run(() => ChestService.SetTemplePlan(stuffId, count));
        });

        Router.Add("POST", "/api/dragon/set", ctx =>
        {
            int stuffId = ctx.JsonInt("stuffId");
            int count = ctx.JsonInt("count");
            return MainThread.Run(() =>
            {
                DragonService.SetDragonItemQuantity(stuffId, count);
                DragonService.InvalidateBagJsonCache();
                return DragonService.GetBagJson();
            });
        });

        Router.Add("POST", "/api/dragon/summon", ctx =>
        {
            int typeIndex = ctx.JsonInt("typeIndex");
            int level = System.Math.Clamp(ctx.JsonInt("level", 1), 1, 10);
            var natures = ctx.JsonIntList("natures");
            var types = DragonService.DragonTypes;
            if (typeIndex < 0 || typeIndex >= types.Length)
                throw new HttpError(400, "invalid typeIndex");
            int dragonStuffId = types[typeIndex].BaseId + level - 1;
            return MainThread.Run(() =>
            {
                string result = DragonService.SummonDragon(dragonStuffId, natures.Count > 0 ? natures.ToArray() : null);
                return $"{{\"result\":\"{ChestEditor.Core.JsonUtil.Escape(result)}\"}}";
            });
        });

        // 静态表，直接返回
        Router.Add("GET", "/api/dragon/types", _ => DragonService.GetDragonTypesJson());
        Router.Add("GET", "/api/dragon/natures", _ => DragonService.GetDragonNaturesJson());

        // 龙魂：读游戏内存，走主线程
        Router.Add("GET", "/api/dragon/souls", _ =>
            MainThread.Run(() => DragonService.GetDragonSoulsJson()));

        Router.Add("POST", "/api/dragon/soul/set", ctx =>
        {
            int index = ctx.JsonInt("index");
            string? prop = ctx.JsonStr("property");
            int value = ctx.JsonInt("value");
            if (string.IsNullOrEmpty(prop)) throw new HttpError(400, "missing property");
            return MainThread.Run(() =>
            {
                string result = DragonService.SetDragonSoulProperty(index, prop, value);
                return result == "ok" ? "{\"ok\":true}" : $"{{\"error\":\"{result}\"}}";
            });
        });

        Router.Add("POST", "/api/dragon/searchmap2", _ =>
            MainThread.Run(() =>
            {
                DragonService.SearchMapDragonEntities();
                return "{\"ok\":true}";
            }, 10000));

        Router.Add("GET", "/api/dragon/entities", _ =>
            MainThread.Run(() => DragonService.GetDragonEntitiesJson(), 10000));

        Router.Add("POST", "/api/dragon/entity/set", ctx =>
        {
            int guid = ctx.JsonInt("guid");
            string? field = ctx.JsonStr("field");
            float value = ctx.JsonFloat("value");
            if (guid == 0 || string.IsNullOrEmpty(field)) throw new HttpError(400, "missing guid/field");
            return MainThread.Run(() =>
            {
                string result = DragonService.SetDragonEntityField(guid, field, value);
                return result == "ok" ? "{\"ok\":true}" : $"{{\"error\":\"{result}\"}}";
            });
        });
    }
}

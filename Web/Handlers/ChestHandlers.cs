using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>箱子/物品/筛选/计划库存相关接口</summary>
internal static class ChestHandlers
{
    internal static void Register()
    {
        Router.Add("GET", "/api/chests", _ =>
            MainThread.Run(() => ChestService.GetChestsJson()));

        Router.Add("GET", "/api/items", _ =>
            MainThread.Run(() => ChestService.GetItemsJson()));

        Router.Add("POST", "/api/refresh", _ =>
            MainThread.Run(() =>
            {
                ChestService.RefreshChestList();
                ChestService.InvalidateCaches();
                return "{\"ok\":true}";
            }));

        Router.Add("POST", "/api/chest/{index}/add", ctx => ChestAction(ctx, true));
        Router.Add("POST", "/api/chest/{index}/remove", ctx => ChestAction(ctx, false));

        Router.Add("POST", "/api/chest/{index}/locate", ctx =>
            MainThread.Run(() => ChestService.LocateChest(ctx.Int("index"))));

        Router.Add("POST", "/api/chest/{index}/plan", ctx =>
        {
            int index = ctx.Int("index");
            int stuffId = ctx.JsonInt("stuffId");
            int count = ctx.JsonInt("count");
            return MainThread.Run(() =>
            {
                ChestService.SetPlanStock(index, stuffId, count);
                ChestService.InvalidateCaches();
                return "{\"ok\":true}";
            });
        });

        Router.Add("GET", "/api/filters", _ =>
            MainThread.Run(() => ChestService.GetFiltersJson()));

        Router.Add("POST", "/api/filters/toggle", ctx =>
        {
            int stuffId = ctx.JsonInt("stuffId");
            return MainThread.Run(() =>
            {
                ChestService.ToggleFilter(stuffId);
                ChestService.InvalidateCaches();
                return "{\"ok\":true}";
            });
        });

        Router.Add("POST", "/api/filters/all", ctx =>
        {
            bool enabled = ctx.Body.Contains("true"); // 与旧版兼容：body 里含 true 即全选
            return MainThread.Run(() =>
            {
                ChestService.SetAllFilters(enabled);
                ChestService.InvalidateCaches();
                return "{\"ok\":true}";
            });
        });
    }

    private static object? ChestAction(RequestCtx ctx, bool isAdd)
    {
        int index = ctx.Int("index");
        int stuffId = ctx.JsonInt("stuffId");
        int count = ctx.JsonInt("count", 1);
        if (stuffId <= 0) throw new HttpError(400, "invalid stuffId");

        return MainThread.Run(() =>
        {
            if (isAdd) ChestService.AddAndUpdate(index, stuffId, count);
            else ChestService.RemoveAndUpdate(index, stuffId, count);
            ChestService.InvalidateCaches();
            return ChestService.BuildChestJson(index);
        });
    }
}

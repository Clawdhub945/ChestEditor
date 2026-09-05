using System.Text.Json.Nodes;
using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>统一实体编辑器接口：扫描、字段读写、销毁、定位、方法枚举</summary>
internal static class EntityHandlers
{
    internal static void Register()
    {
        Router.Add("POST", "/api/editor/scan", _ =>
            MainThread.Run(() =>
            {
                EntityScan.ScanAll();
                ModificationStore.ApplyPendingModifications();
                return "{\"ok\":true}";
            }, 30000));

        Router.Add("GET", "/api/editor/entities", _ =>
            MainThread.Run(() => EntityScan.GetAllJson(), 10000));

        Router.Add("GET", "/api/editor/fields/{ptrHash}", ctx =>
            MainThread.Run(() => EntityScan.GetFieldsJson(ctx.Int("ptrHash")), 10000));

        Router.Add("POST", "/api/editor/set", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            string? field = ctx.JsonStr("field");
            float value = ctx.JsonFloat("value");
            if (ptrHash == 0 || string.IsNullOrEmpty(field)) throw new HttpError(400, "missing ptrHash/field");
            return MainThread.Run(() =>
            {
                string result = EntityScan.SetField(ptrHash, field, value);
                return result == "ok" ? "{\"ok\":true}" : $"{{\"error\":\"{result}\"}}";
            });
        });

        Router.Add("POST", "/api/editor/destroy", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            if (ptrHash == 0) throw new HttpError(400, "missing ptrHash");
            return MainThread.Run(() =>
            {
                string result = EntityDestroyer.DestroyEntity(ptrHash);
                return result == "ok" ? "{\"ok\":true}" : $"{{\"error\":\"{result}\"}}";
            });
        });

        Router.Add("POST", "/api/editor/locate", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            if (ptrHash == 0) throw new HttpError(400, "missing ptrHash");
            var posJson = MainThread.Run(() => EntityScan.GetEntityPositionJson(ptrHash));
            float px = 0, py = 0;
            bool parsed = false;
            try
            {
                var node = JsonNode.Parse(posJson);
                if (node?["x"] != null && node["y"] != null)
                {
                    px = node["x"]!.GetValue<float>();
                    py = node["y"]!.GetValue<float>();
                    parsed = true;
                }
            }
            catch { }

            if (!parsed) return posJson;

            MainThread.Run(() => ChestService.LocateFacility(px, py), 3000);
            return $"{{\"ok\":true,\"x\":{px.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"y\":{py.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
        });

        Router.Add("POST", "/api/editor/listmethods", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            if (ptrHash == 0) throw new HttpError(400, "missing ptrHash");
            return MainThread.Run(() =>
            {
                string methods = EntityScan.ListMethods(ptrHash);
                Plugin.LogInfo($"[EntityEditor] ListMethods ptrHash={ptrHash}:\n{methods}");
                return $"{{\"methods\":\"{ChestEditor.Core.JsonUtil.Escape(methods)}\"}}";
            }, 10000);
        });
    }
}

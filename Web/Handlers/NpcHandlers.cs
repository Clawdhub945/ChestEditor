using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>NPC 面板接口：扫描、列表、字段读写、字段翻译</summary>
internal static class NpcHandlers
{
    internal static void Register()
    {
        Router.Add("POST", "/api/npc/scan", _ =>
            MainThread.Run(() =>
            {
                EntityScan.ScanAll();
                ModificationStore.ApplyPendingModifications();
                return NpcEditor.GetNpcListJson();
            }, 30000));

        Router.Add("GET", "/api/npc/list", _ => NpcEditor.CachedListJson);

        Router.Add("GET", "/api/npc/fields/{ptrHash}", ctx =>
            MainThread.Run(() => EntityScan.GetFieldsJson(ctx.Int("ptrHash")), 10000));

        Router.Add("POST", "/api/npc/set", ctx =>
        {
            int ptrHash = ctx.JsonInt("ptrHash");
            string? field = ctx.JsonStr("field");
            float value = ctx.JsonFloat("value");
            if (ptrHash == 0 || string.IsNullOrEmpty(field)) throw new HttpError(400, "missing ptrHash or field");
            return MainThread.Run(() =>
            {
                string result = NpcEditor.SetNpcField(ptrHash, field, value);
                return result == "ok" ? "{\"ok\":true}" : $"{{\"error\":\"{result}\"}}";
            }, 10000);
        });

        Router.Add("GET", "/api/npc/translations", _ =>
        {
            // 优先嵌入资源；允许插件目录下同名文件覆盖（便于运行时调整）
            var embedded = HttpUtil.GetEmbeddedResource("ChestEditor.field_translations.json");
            if (embedded != null) return System.Text.Encoding.UTF8.GetString(embedded);
            var modDir = System.IO.Path.GetDirectoryName(typeof(NpcHandlers).Assembly.Location) ?? "";
            var filePath = System.IO.Path.Combine(modDir, "field_translations.json");
            try
            {
                return System.IO.File.Exists(filePath)
                    ? System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8)
                    : "{}";
            }
            catch { return "{}"; }
        });
    }
}

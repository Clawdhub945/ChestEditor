using ChestEditor.Threading;

namespace ChestEditor.Web;

/// <summary>NPC 面板接口：扫描、列表、字段读写、字段翻译</summary>
internal static class NpcHandlers
{
    /// <summary>扫描每帧的时间预算（毫秒）—— 与 EntityHandlers 的取值保持一致</summary>
    private const double ScanFrameBudgetMs = 6;

    internal static void Register()
    {
        // 全量扫描：分片推进（每帧一批），扫完再在主线程上应用待写入的修改。
        // ⚠⚠ 开扫那一步（BeginScan → Resources.FindObjectsOfTypeAll）必须跑在主线程上，
        // 详见 EntityHandlers 里的说明（放 HTTP 线程上会让游戏闪退）。
        Router.Add("POST", "/api/npc/scan", _ =>
        {
            bool begun = false;
            MainThread.RunPaced(() =>
            {
                if (!begun) { begun = true; EntityScan.BeginScan(); }
                return EntityScan.StepScan(int.MaxValue, ScanFrameBudgetMs);
            }, 120000);
            return MainThread.Run(() =>
            {
                ModificationStore.ApplyPendingModifications();
                return NpcEditor.GetNpcListJson();
            }, 30000);
        });

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
                return result == "ok" ? JsonBuilder.Ok() : JsonBuilder.Error(result);
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

namespace ChestEditor.Web;

/// <summary>静态内容：主页、前端资源（css/js/data）、favicon、物品图标、龙图标</summary>
internal static class StaticHandlers
{
    private const string StaticPrefix = "/static/";

    internal static void Register()
    {
        Router.Add("GET", "/", _ =>
        {
            var html = HttpUtil.GetEmbeddedResource("ChestEditor.Static.index.html");
            return html == null
                ? new RawResponse("<h1>ChestEditor 静态资源缺失</h1>", "text/html; charset=utf-8")
                : new RawResponse(html, "text/html; charset=utf-8");
        });

        Router.Add("GET", "/static/*path", ctx =>
        {
            var rel = ctx.Params.GetValueOrDefault("path", "");
            var name = "ChestEditor.Static." + rel.Replace('/', '.');
            var bytes = HttpUtil.GetEmbeddedResource(name);
            if (bytes == null) return null; // → 404
            return new RawResponse(bytes, ContentTypeOf(rel));
        });

        Router.Add("GET", "/favicon.png", _ => HttpUtil.GetEmbeddedResource("ChestEditor.ic_app.png"));
        Router.Add("GET", "/icon/{stuffId}", ctx =>
        {
            int stuffId = ctx.Int("stuffId");
            return HttpUtil.GetEmbeddedResource($"ChestEditor.Icon.ui_{stuffId}.png");
        });
        Router.Add("GET", "/api/dragon/icon/{index}", ctx =>
        {
            int index = ctx.Int("index");
            if (index < 0 || index > 15) return null;
            return HttpUtil.GetEmbeddedResource($"ChestEditor.Dragon.ic_dragon{index + 1}.png");
        });
    }

    private static string ContentTypeOf(string path)
    {
        if (path.EndsWith(".html")) return "text/html; charset=utf-8";
        if (path.EndsWith(".css")) return "text/css; charset=utf-8";
        if (path.EndsWith(".js")) return "application/javascript; charset=utf-8";
        if (path.EndsWith(".json")) return "application/json; charset=utf-8";
        if (path.EndsWith(".png")) return "image/png";
        if (path.EndsWith(".svg")) return "image/svg+xml";
        return "application/octet-stream";
    }
}

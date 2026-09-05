namespace ChestEditor.Web;

/// <summary>静态内容：主页、favicon、物品图标、龙图标</summary>
internal static class StaticHandlers
{
    internal static void Register()
    {
        Router.Add("GET", "/", _ =>
        {
            // Phase 4 之前仍使用内嵌 HtmlUI；之后改为 /static/index.html
            return new RawResponse(HtmlUI.Html, "text/html; charset=utf-8");
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
}

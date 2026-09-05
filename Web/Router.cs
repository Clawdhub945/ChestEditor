using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;

namespace ChestEditor.Web;

internal sealed class Route
{
    public string Method;
    public string[] Segments;
    public Func<RequestCtx, object?> Handler;

    public Route(string method, string pattern, Func<RequestCtx, object?> handler)
    {
        Method = method;
        Segments = pattern.Trim('/').Split('/');
        Handler = handler;
    }

    public bool Match(string method, string path, out Dictionary<string, string> pathParams)
    {
        pathParams = new Dictionary<string, string>();
        if (Method != "*" && Method != method) return false;
        var parts = path.Trim('/').Split('/');
        // 末段 *name：通配剩余所有段
        if (Segments.Length > 0 && Segments[^1].StartsWith('*'))
        {
            if (parts.Length < Segments.Length) return false;
        }
        else if (parts.Length != Segments.Length) return false;

        for (int i = 0; i < Segments.Length; i++)
        {
            var seg = Segments[i];
            if (seg.StartsWith('*'))
            {
                pathParams[seg[1..]] = string.Join("/", parts[i..]);
                return true;
            }
            if (seg.StartsWith('{') && seg.EndsWith('}'))
            {
                if (parts[i].Length == 0) return false;
                pathParams[seg[1..^1]] = Uri.UnescapeDataString(parts[i]);
            }
            else if (!string.Equals(seg, parts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}

/// <summary>路由表：method + 路径模式（支持 {param} 段）→ 处理器</summary>
internal static class Router
{
    private static readonly List<Route> Routes = new();
    private static bool _registered;

    internal static void Add(string method, string pattern, Func<RequestCtx, object?> handler)
        => Routes.Add(new Route(method, pattern, handler));

    /// <summary>注册全部路由（幂等）</summary>
    internal static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;
        StaticHandlers.Register();
        ChestHandlers.Register();
        DragonHandlers.Register();
        TechHandlers.Register();
        EntityHandlers.Register();
        NpcHandlers.Register();
    }

    /// <summary>分发请求并写出响应；无匹配路由时返回 false</summary>
    internal static bool Dispatch(HttpListenerRequest req, HttpListenerResponse resp)
    {
        string path = req.Url?.AbsolutePath ?? "/";
        string method = req.HttpMethod;

        foreach (var route in Routes)
        {
            if (!route.Match(method, path, out var pathParams)) continue;

            var ctx = new RequestCtx { Method = method, Path = path, Params = pathParams };
            if (method == "POST" || method == "PUT")
            {
                using var reader = new System.IO.StreamReader(req.InputStream, req.ContentEncoding);
                ctx.Body = reader.ReadToEnd();
            }

            try
            {
                var result = route.Handler(ctx);
                if (result is RawResponse raw)
                {
                    resp.StatusCode = 200;
                    resp.ContentType = raw.ContentType;
                    resp.ContentLength64 = raw.Body.Length;
                    resp.OutputStream.Write(raw.Body, 0, raw.Body.Length);
                }
                else if (result is byte[] png) HttpUtil.SendPng(resp, png);
                else if (result is string s) HttpUtil.SendJson(resp, s);
                else if (result != null)
                    HttpUtil.SendJson(resp, JsonSerializer.Serialize(result));
                else
                    HttpUtil.SendJson(resp, ErrorJson("not found"), 404);
            }
            catch (HttpError he)
            {
                HttpUtil.SendJson(resp, ErrorJson(he.Message), he.Status);
            }
            catch (TimeoutException)
            {
                HttpUtil.SendJson(resp, ErrorJson("timeout"), 408);
            }
            catch (InvalidOperationException ioe) when (ioe.Message == "mod not ready")
            {
                HttpUtil.SendJson(resp, ErrorJson("mod not ready"), 503);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"[HttpServer] {method} {path} 处理异常: {ex}");
                HttpUtil.SendJson(resp, ErrorJson(ex.Message), 500);
            }
            return true;
        }
        return false;
    }

    private static string ErrorJson(string msg)
    {
        var safe = msg.Replace("\\", "\\\\").Replace("\"", "'");
        return $"{{\"error\":\"{safe}\"}}";
    }
}

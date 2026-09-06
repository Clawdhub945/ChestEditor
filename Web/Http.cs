using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChestEditor.Web;

/// <summary>业务错误：带 HTTP 状态码，由 Router 统一转为 {"error":...} 响应</summary>
internal class HttpError : Exception
{
    public int Status;
    public HttpError(int status, string message) : base(message) { Status = status; }
}

/// <summary>非 JSON 响应（HTML/CSS/JS 等静态内容），由 Router 原样写出</summary>
internal sealed class RawResponse
{
    public byte[] Body;
    public string ContentType;
    public RawResponse(string content, string contentType)
    {
        Body = System.Text.Encoding.UTF8.GetBytes(content);
        ContentType = contentType;
    }
    public RawResponse(byte[] body, string contentType)
    {
        Body = body;
        ContentType = contentType;
    }
}

/// <summary>单个 HTTP 请求的上下文：路径参数、请求体 JSON、便捷取值</summary>
internal sealed class RequestCtx
{
    public string Method = "";
    public string Path = "";
    public string Body = "";
    public Dictionary<string, string> Params = new();

    private JsonNode? _json;
    private bool _jsonParsed;

    /// <summary>请求体解析出的 JSON（惰性；解析失败或空体为 null）</summary>
    public JsonNode? Json
    {
        get
        {
            if (!_jsonParsed)
            {
                _jsonParsed = true;
                if (!string.IsNullOrWhiteSpace(Body))
                {
                    try { _json = JsonNode.Parse(Body); }
                    catch { _json = null; }
                }
            }
            return _json;
        }
    }

    // ===== 路径参数 =====

    public int Int(string name) => int.TryParse(Params.GetValueOrDefault(name), out var v) ? v : 0;

    // ===== 请求体字段（兼容数字与字符串两种数值写法） =====

    public bool TryNum(string key, out double val)
    {
        val = 0;
        var n = Json?[key];
        if (n is not JsonValue v) return false;
        if (v.TryGetValue<double>(out val)) return true;
        if (v.TryGetValue<string>(out var s) &&
            double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out val))
            return true;
        return false;
    }

    public int JsonInt(string key, int def = 0) => TryNum(key, out var d) ? (int)d : def;

    public float JsonFloat(string key, float def = 0) => TryNum(key, out var d) ? (float)d : def;

    public string? JsonStr(string key)
    {
        var n = Json?[key];
        return n?.ToString();
    }

    public bool JsonBool(string key, bool def = false)
    {
        var n = Json?[key];
        if (n is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b)) return b;
            if (v.TryGetValue<string>(out var s)) return s == "true";
        }
        return def;
    }

    /// <summary>读取 int 数组字段（如 natures:[1,2,3]）</summary>
    public List<int> JsonIntList(string key)
    {
        var result = new List<int>();
        if (Json?[key] is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is JsonValue v)
                {
                    if (v.TryGetValue<int>(out var i)) result.Add(i);
                    else if (v.TryGetValue<string>(out var s) && int.TryParse(s, out var i2)) result.Add(i2);
                }
            }
        }
        return result;
    }
}

/// <summary>HTTP 响应/嵌入资源的通用写出工具</summary>
internal static class HttpUtil
{
    /// <summary>
    /// 禁止浏览器启发式缓存：前端 css/js 更新随 DLL 发布，URL 不变，
    /// 不加此头时浏览器会用旧缓存的样式/脚本导致"改了没生效"
    /// </summary>
    internal static void ApplyNoCache(HttpListenerResponse resp)
    {
        try { resp.Headers["Cache-Control"] = "no-cache"; } catch { }
    }

    internal static void SendJson(HttpListenerResponse resp, string json, int status = 200)
    {
        resp.StatusCode = status;
        ApplyNoCache(resp);
        resp.ContentType = "application/json; charset=utf-8";
        var buf = Encoding.UTF8.GetBytes(json);
        resp.ContentLength64 = buf.Length;
        resp.OutputStream.Write(buf, 0, buf.Length);
    }

    internal static void SendHtml(HttpListenerResponse resp, string html)
    {
        resp.StatusCode = 200;
        ApplyNoCache(resp);
        resp.ContentType = "text/html; charset=utf-8";
        var buf = Encoding.UTF8.GetBytes(html);
        resp.ContentLength64 = buf.Length;
        resp.OutputStream.Write(buf, 0, buf.Length);
    }

    internal static void SendPng(HttpListenerResponse resp, byte[]? bytes)
    {
        if (bytes == null) { resp.StatusCode = 404; return; }
        resp.StatusCode = 200;
        ApplyNoCache(resp);
        resp.ContentType = "image/png";
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    internal static void SendCss(HttpListenerResponse resp, byte[]? bytes)
    {
        if (bytes == null) { resp.StatusCode = 404; return; }
        resp.StatusCode = 200;
        ApplyNoCache(resp);
        resp.ContentType = "text/css; charset=utf-8";
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    internal static void SendJs(HttpListenerResponse resp, byte[]? bytes)
    {
        if (bytes == null) { resp.StatusCode = 404; return; }
        resp.StatusCode = 200;
        ApplyNoCache(resp);
        resp.ContentType = "application/javascript; charset=utf-8";
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes, 0, bytes.Length);
    }

    private static readonly Dictionary<string, byte[]> ResourceCache = new();

    /// <summary>读取嵌入资源（带缓存）</summary>
    internal static byte[]? GetEmbeddedResource(string name)
    {
        lock (ResourceCache)
        {
            if (ResourceCache.TryGetValue(name, out var cached)) return cached;
            var asm = typeof(HttpUtil).Assembly;
            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) return null;
            using var ms = new System.IO.MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            ResourceCache[name] = bytes;
            return bytes;
        }
    }
}

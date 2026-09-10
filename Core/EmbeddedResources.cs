using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChestEditor.Core;

/// <summary>
/// 嵌入资源读取（带缓存）。全项目统一的资源访问入口，替代各处自建的反射/流读取。
/// </summary>
internal static class EmbeddedResources
{
    private static readonly Dictionary<string, byte[]> Cache = new();

    internal static byte[]? Get(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            using var stream = typeof(EmbeddedResources).Assembly.GetManifestResourceStream(name);
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            Cache[name] = bytes;
            return bytes;
        }
    }

    internal static string? GetText(string name)
    {
        var bytes = Get(name);
        return bytes == null ? null : Encoding.UTF8.GetString(bytes);
    }
}

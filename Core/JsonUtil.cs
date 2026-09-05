using System.Text;

namespace ChestEditor.Core;

/// <summary>手拼 JSON 时的统一转义与常用片段工具</summary>
internal static class JsonUtil
{
    /// <summary>JSON 字符串转义（含 HTML 安全的 &lt; &gt;）</summary>
    internal static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "")
        .Replace("<", "\\u003c").Replace(">", "\\u003e");

    /// <summary>输出 "key":value 片段（key 已转义）</summary>
    internal static void AppendField(StringBuilder sb, string key, string value, bool comma)
    {
        if (comma) sb.Append(',');
        sb.Append($"\"{Escape(key)}\":\"{Escape(value)}\"");
    }

    internal static void AppendField(StringBuilder sb, string key, int value, bool comma)
    {
        if (comma) sb.Append(',');
        sb.Append($"\"{Escape(key)}\":{value}");
    }

    internal static void AppendField(StringBuilder sb, string key, float value, bool comma)
    {
        if (comma) sb.Append(',');
        sb.Append($"\"{Escape(key)}\":{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    internal static void AppendField(StringBuilder sb, string key, bool value, bool comma)
    {
        if (comma) sb.Append(',');
        sb.Append($"\"{Escape(key)}\":{(value ? "true" : "false")}");
    }
}

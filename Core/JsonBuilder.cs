using System;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ChestEditor.Core;

/// <summary>
/// 全项目 JSON 输出统一入口。
///
/// 设计目标：彻底消除手工拼接 JSON 字符串 —— 转义、逗号、数字/布尔字面量格式
/// 一律交给 System.Text.Json 的 <see cref="Utf8JsonWriter"/> 负责。
/// 旧实现（StringBuilder + 字符串插值 + JsonUtil.Escape）存在几类隐患：
/// 漏转义、尾逗号 hack、浮点 ToString 未指定 InvariantCulture 产生非法 JSON。
/// </summary>
internal static class JsonBuilder
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        // 中文等非 ASCII 原样输出（不转成 \uXXXX，避免数据表体积翻数倍），
        // 同时仍然转义 &lt; &gt; &amp; ' 等 HTML 敏感字符（与旧 JsonUtil.Escape 的意图一致）。
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Indented = false,
    };

    /// <summary>构建 JSON 文本。写出异常不吞，由调用方决定如何处理。</summary>
    internal static string Build(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            write(writer);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>构建单个 JSON 对象：{"a":1,...}</summary>
    internal static string Object(Action<Utf8JsonWriter> fields) => Build(w =>
    {
        w.WriteStartObject();
        fields(w);
        w.WriteEndObject();
    });

    /// <summary>{"ok":true}</summary>
    internal static string Ok() => "{\"ok\":true}";

    /// <summary>{"ok":true,"name":"value"}，用于附带回执信息</summary>
    internal static string Ok(string name, string? value) => Object(w =>
    {
        w.WriteBoolean("ok", true);
        w.WriteString(name, value ?? "");
    });

    /// <summary>{"error":"..."}（消息自动转义）</summary>
    internal static string Error(string? message) => Object(w => w.WriteString("error", message ?? ""));

    internal static string Error(Exception ex) => Error(ex.Message);

    /// <summary>
    /// 浮点安全网：NaN / ±Infinity 无法表示为 JSON 数字，序列化器会抛异常。
    /// 旧实现会写出字面量 NaN 导致前端 JSON.parse 整体失败，这里统一降级为 0，
    /// 保证"一个坏字段不会毁掉整份响应"。
    /// </summary>
    internal static float Safe(float value) => float.IsFinite(value) ? value : 0f;
}

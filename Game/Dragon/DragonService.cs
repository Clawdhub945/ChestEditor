using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static ChestEditor.Interop.Il2CppApi;
using static ChestEditor.Interop.Il2CppInvoke;
using static ChestEditor.Interop.Il2CppMemory;
using static ChestEditor.Interop.ManagedReflect;

namespace ChestEditor.Game;

/// <summary>龙系统：素材背包、龙魂、召唤、龙实体扫描与属性修改</summary>
internal static partial class DragonService
{
    /// <summary>
    /// 把反射读取到的任意值写成 JSON 值。
    /// 旧实现用字符串拼接：字符串不转义、数字 ToString 未指定 InvariantCulture，
    /// 现在统一交给 Utf8JsonWriter。
    /// </summary>
    private static void WriteJsonValue(Utf8JsonWriter w, object? value)
    {
        switch (value)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case int i: w.WriteNumberValue(i); break;
            case long l: w.WriteNumberValue(l); break;
            case float f: w.WriteNumberValue(JsonBuilder.Safe(f)); break;
            case double d: w.WriteNumberValue(double.IsFinite(d) ? d : 0d); break;
            case string s: w.WriteStringValue(s); break;
            case List<int> ints:
                w.WriteStartArray();
                foreach (var n in ints) w.WriteNumberValue(n);
                w.WriteEndArray();
                break;
            default: w.WriteStringValue(value.GetType().Name); break;
        }
    }
}

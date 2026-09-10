using System.Runtime.InteropServices;

namespace ChestEditor.Interop;

/// <summary>
/// IL2CPP 对象内存直读直写（unsafe）。
/// IL2CPP 64 位对象布局: [klass(8)][monitor(8)][字段...]
/// IL2CPP string 布局: [klass(8)][monitor(8)][length(4)][chars(UTF-16)...]
/// </summary>
internal static unsafe class Il2CppMemory
{
    private const int StringLengthOffset = 0x10;
    private const int StringCharsOffset = 0x14;
    private const int ArrayDataOffset = 0x20; // Il2CppArray: klass(8)+monitor(8)+bounds(8)+max_length(4)+pad(4)+data

    internal static int ReadIl2CppInt(IntPtr objPtr, int offset) => *(int*)(objPtr + offset);
    /// <summary>读 1 字节（bool/byte）——勿用 ReadIl2CppInt 代替：bool 只占 1 字节，读 4 字节会吞掉后续字段。</summary>
    internal static byte ReadIl2CppByte(IntPtr objPtr, int offset) => *(byte*)(objPtr + offset);
    internal static float ReadIl2CppFloat(IntPtr objPtr, int offset) => *(float*)(objPtr + offset);
    internal static IntPtr ReadIl2CppPointer(IntPtr objPtr, int offset) => *(IntPtr*)(objPtr + offset);

    internal static void WriteIl2CppInt(IntPtr objPtr, int offset, int value) => *(int*)(objPtr + offset) = value;
    /// <summary>写 1 字节（bool/byte）——勿用 WriteIl2CppInt 代替：bool 只占 1 字节，写 4 字节会改坏后续字段。</summary>
    internal static void WriteIl2CppByte(IntPtr objPtr, int offset, byte value) => *(byte*)(objPtr + offset) = value;
    internal static void WriteIl2CppFloat(IntPtr objPtr, int offset, float value) => *(float*)(objPtr + offset) = value;

    /// <summary>读取 IL2CPP string 对象指针指向的字符串（长度非法时退化为以 NUL 结尾读取）</summary>
    internal static string? ReadStringObject(IntPtr strObjPtr)
    {
        if (strObjPtr == IntPtr.Zero) return null;
        try
        {
            int len = *(int*)(strObjPtr + StringLengthOffset);
            if (len <= 0 || len > 32768) return Marshal.PtrToStringUni(strObjPtr + StringCharsOffset);
            return new string((char*)(strObjPtr + StringCharsOffset), 0, len);
        }
        catch { return null; }
    }

    internal static string? ReadIl2CppString(IntPtr objPtr, int offset)
    {
        try { return ReadStringObject(ReadIl2CppPointer(objPtr, offset)); }
        catch { return null; }
    }

    /// <summary>读取 IL2CPP List&lt;int&gt; 字段内容（沿 _size/_items 布局）</summary>
    internal static List<int>? ReadIl2CppIntList(IntPtr objPtr, int offset)
    {
        try
        {
            IntPtr listPtr = ReadIl2CppPointer(objPtr, offset);
            if (listPtr == IntPtr.Zero) return null;

            IntPtr classPtr = Il2CppApi.GetClass(listPtr);
            if (classPtr == IntPtr.Zero) return null;

            int size = -1, itemsOffset = -1;
            foreach (var f in Il2CppApi.EnumerateFields(classPtr))
            {
                if (f.Name == "_size") size = ReadIl2CppInt(listPtr, f.Offset);
                else if (f.Name == "_items") itemsOffset = f.Offset;
            }
            if (size <= 0 || itemsOffset < 0) return null;

            IntPtr itemsPtr = ReadIl2CppPointer(listPtr, itemsOffset);
            if (itemsPtr == IntPtr.Zero) return null;

            var result = new List<int>(size);
            for (int i = 0; i < size; i++)
                result.Add(*(int*)(itemsPtr + ArrayDataOffset + i * 4));
            return result;
        }
        catch { return null; }
    }
}

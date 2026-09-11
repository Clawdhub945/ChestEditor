using static ChestEditor.Interop.Il2CppApi;

namespace ChestEditor.Interop;

/// <summary>
/// il2cpp_runtime_invoke 的统一编组入口。
/// args 支持 null / IntPtr / int / long / float / double / bool / uint / Il2CppObjectBase。
/// </summary>
internal static unsafe class Il2CppInvoke
{
    /// <summary>沿父类链按名称+参数个数查找方法指针（每级用 il2cpp_class_get_method_from_name）</summary>
    internal static IntPtr FindMethodInHierarchy(IntPtr cls, string name, int paramCount)
    {
        IntPtr search = cls;
        int depth = 0;
        while (search != IntPtr.Zero && depth < 20)
        {
            var m = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_method_from_name(search, name, paramCount);
            if (m != IntPtr.Zero) return m;
            search = Il2CppApi.GetParent(search);
            depth++;
        }
        return IntPtr.Zero;
    }

    /// <summary>对 IL2CPP 代理对象沿继承链查找 0 参方法指针</summary>
    internal static IntPtr FindIl2CppMethod(object il2cppObj, string methodName)
    {
        if (il2cppObj is not Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase obj) return IntPtr.Zero;
        IntPtr objPtr = Il2CppInterop.Runtime.IL2CPP.Il2CppObjectBaseToPtrNotNull(obj);
        return FindMethodInHierarchy(Il2CppApi.GetClass(objPtr), methodName, 0);
    }

    /// <summary>
    /// 调用方法并返回原始返回指针（void 方法返回 Zero）。异常会被记日志并吞掉（与旧行为一致）。
    /// </summary>
    internal static IntPtr Invoke(IntPtr method, IntPtr objPtr, params object?[] args)
    {
        if (method == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            IntPtr exception = IntPtr.Zero;
            IntPtr result;
            int n = args.Length;
            if (n == 0)
            {
                result = Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(method, objPtr, null, ref exception);
            }
            else
            {
                IntPtr[] slots = new IntPtr[n];
                for (int i = 0; i < n; i++)
                    slots[i] = args[i] is RawArg ? IntPtr.Zero : ToSlot(args[i]);   // RawArg 的槽值无意义（argv 直接指缓冲）

                IntPtr[] argv = new IntPtr[n];
                fixed (IntPtr* sp = slots)
                fixed (IntPtr* ap = argv)
                {
                    for (int i = 0; i < n; i++)
                        argv[i] = (args[i] is RawArg raw) ? raw.Ptr : (IntPtr)(&sp[i]);
                    result = Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(method, objPtr, (void**)ap, ref exception);
                }
            }

            if (exception != IntPtr.Zero)
            {
                // System.Exception 布局：klass(8) + monitor(8) + message(string)@16 → 直接解码真话
                //（此前把异常对象整个当字符串读，只会得到乱码）
                string msg;
                // ⚠ 异常对象未被 GC 根保护，il2cpp 调用（读消息）本身可能触发 GC把它回收
                //   → 先 pinned 根住再读（实测不根住时消息永远解不出）
                IntPtr exRoot = IntPtr.Zero;
                try { exRoot = GcHandleNew(exception, true); } catch { }
                try
                {
                    string exType = "";
                    IntPtr msgPtr = Il2CppMemory.ReadIl2CppPointer(exception, 16);
                    if (msgPtr != IntPtr.Zero)
                    {
                        IntPtr msgRoot = GcHandleNew(msgPtr, true);
                        try { msg = Il2CppMemory.ReadStringObject(msgPtr) ?? "(空消息)"; }
                        finally { GcHandleFree(msgRoot); }
                    }
                    else msg = "(消息指针为空)";
                    IntPtr exClass = System.Runtime.InteropServices.Marshal.ReadIntPtr(exception);  // Il2CppObject.klass 在 +0
                    exType = GetClassName(exClass) ?? "";
                    Plugin.LogError($"[Il2CppInvoke] 方法调用抛出异常[{exType}]: {msg}");
                }
                catch { Plugin.LogError("[Il2CppInvoke] 方法调用抛出异常(解码失败)"); }
                finally { if (exRoot != IntPtr.Zero) GcHandleFree(exRoot); }
            }
            return result;
        }
        catch (Exception ex)
        {
            Plugin.LogError($"[Il2CppInvoke] invoke 失败: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    internal static bool InvokeVoid(IntPtr method, IntPtr objPtr, params object?[] args)
    {
        try { Invoke(method, objPtr, args); return true; }
        catch { return false; }
    }

    /// <summary>
    /// 调用 int 返回值的方法。⚠ runtime_invoke 对值类型返回是【装箱对象指针】
    /// （不能直接 (int)指针，64 位指针转 int32 会 OverflowException）——
    /// 值在 IL2CPP 对象头（klass 8 + monitor 8）之后的 16 偏移处。
    /// </summary>
    internal static int InvokeInt(IntPtr method, IntPtr objPtr, params object?[] args)
    {
        IntPtr boxed = Invoke(method, objPtr, args);
        if (boxed == IntPtr.Zero) return 0;
        return Il2CppMemory.ReadIl2CppInt(boxed, 16);
    }

    /// <summary>调用返回 IL2CPP string 的方法并解码为托管字符串</summary>
    internal static string? InvokeString(IntPtr method, IntPtr objPtr, params object?[] args)
        => Il2CppMemory.ReadStringObject(Invoke(method, objPtr, args));

    /// <summary>对已解析的对象指针按名沿继承链查找并调用 0 参方法，返回字符串</summary>
    internal static string? InvokeStringByName(IntPtr objPtr, IntPtr classPtr, string methodName, params object?[] args)
    {
        IntPtr m = FindMethodInHierarchy(classPtr, methodName, args.Length);
        return m == IntPtr.Zero ? null : InvokeString(m, objPtr, args);
    }

    /// <summary>
    /// 按方法签名逐参数填默认值（bool → 1，其余 → 0/null）后调用。
    /// 用于"按名找到重载但不确知精确签名，需以最小副作用参数试探调用"的场景。
    /// </summary>
    /// <returns>IL2CPP 异常指针为空则 true；失败不抛托管异常，由调用方记日志。</returns>
    internal static bool InvokeWithDefaults(IntPtr method, IntPtr objPtr, int paramCount)
    {
        if (method == IntPtr.Zero) return false;
        IntPtr exception = IntPtr.Zero;
        if (paramCount <= 0)
        {
            Il2CppApi.RuntimeInvoke(method, objPtr, null, ref exception);
            return exception == IntPtr.Zero;
        }

        IntPtr[] slots = new IntPtr[paramCount];
        for (int i = 0; i < paramCount; i++)
        {
            IntPtr paramType = Il2CppApi.GetMethodParam(method, (uint)i);
            string? tn = paramType != IntPtr.Zero ? Il2CppApi.PtrToString(Il2CppApi.TypeGetName(paramType)) : null;
            slots[i] = (tn == "System.Boolean" || tn == "bool") ? (IntPtr)1 : IntPtr.Zero;
        }
        return InvokeRaw(method, objPtr, slots, ref exception);
    }

    /// <summary>
    /// 以显式给定的参数槽调用方法（引用类型放对象指针，值类型放值本身）。未给的槽请在数组里填 <see cref="IntPtr.Zero"/>。
    /// </summary>
    internal static bool InvokeWithArgs(IntPtr method, IntPtr objPtr, params IntPtr[] args)
    {
        if (method == IntPtr.Zero) return false;
        IntPtr exception = IntPtr.Zero;
        if (args == null || args.Length == 0)
        {
            Il2CppApi.RuntimeInvoke(method, objPtr, null, ref exception);
            return exception == IntPtr.Zero;
        }
        return InvokeRaw(method, objPtr, args, ref exception);
    }

    /// <summary>把参数槽的地址数组交给 il2cpp_runtime_invoke；slots 本身只读。</summary>
    private static bool InvokeRaw(IntPtr method, IntPtr objPtr, IntPtr[] slots, ref IntPtr exception)
    {
        IntPtr[] argv = new IntPtr[slots.Length];
        fixed (IntPtr* sp = slots)
        {
            for (int i = 0; i < slots.Length; i++) argv[i] = (IntPtr)(&sp[i]);
            fixed (IntPtr* ap = argv)
            {
                Il2CppApi.RuntimeInvoke(method, objPtr, (void**)ap, ref exception);
            }
        }
        return exception == IntPtr.Zero;
    }

    // 值类型参数：把值放进 IntPtr 槽的低 4 字节；引用类型：槽里放对象指针。
    // string → ManagedStringToIl2Cpp（IL2CPP 字符串对象，生命周期由 il2cpp GC 管，无需释放）。
    private static IntPtr ToSlot(object? arg) => arg switch
    {
        null => IntPtr.Zero,
        IntPtr p => p,
        int v => new IntPtr(v),
        long v => new IntPtr(v),
        uint v => new IntPtr(v),
        float f => new IntPtr(BitConverter.SingleToInt32Bits(f)),
        double d => throw new NotSupportedException("double 参数请先显式转换"),
        bool b => new IntPtr(b ? 1 : 0),
        string s => Il2CppApi.StringToIl2Cpp(s),
        Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o => Il2CppApi.GetIl2CppPtr(o),
        _ => throw new NotSupportedException($"不支持的参数类型: {arg.GetType().Name}")
    };

    /// <summary>
    /// 原始指针参数：argv[i] 直接用 <see cref="Ptr"/>（不再指向槽）。
    /// 用于 &gt;8 字节的值类型（如 UnityEngine.Vector3 = 12 字节）——槽只有 8 字节装不下，
    /// 调用方需自行分配非托管内存写入结构体数据（用完自行 FreeHGlobal）。
    /// </summary>
    internal sealed class RawArg
    {
        internal IntPtr Ptr;
        internal RawArg(IntPtr ptr) { Ptr = ptr; }
    }
}

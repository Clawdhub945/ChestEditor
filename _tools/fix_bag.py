import io

NL = chr(10)
p = 'Game/ChestService.cs'
lines = io.open(p, encoding='utf-8').read().split(NL)

# 方法体: 1-based 333 到 547（547 是闭括号"}"），替换为 332 索引起（0-based 332 = 333行）
start = 332
end = 547  # 0-based切片 [332:547) 即 1-based 333..547
assert lines[start].lstrip().startswith('private static List<ItemInfo> ReadItemsFromBag'), lines[start]
assert lines[end - 1].strip() == '}', lines[end - 1]

new_method = '''    private static List<ItemInfo> ReadItemsFromBag(object facility)
    {
        var items = new List<ItemInfo>();

        object? bag = GetProp(facility, "bag");
        if (bag == null) return items;

        // 首选：bag.dic 是 BagDic，反编译确认其直接继承 Dictionary<int,int>，
        // 直接枚举全部物品（O(物品种类)），替代旧的 620 次 GetStuffCount 反射调用
        object? bagDic = GetProp(bag, "dic");
        if (bagDic is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppDic)
        {
            try
            {
                var d = il2cppDic.TryCast<Il2CppSystem.Collections.Generic.Dictionary<int, int>>();
                if (d != null)
                {
                    var enumerator = d.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var kv = enumerator.Current;
                        if (kv.Value > 0)
                            items.Add(new ItemInfo { StuffId = kv.Key, Count = kv.Value });
                    }
                    return items;
                }
            }
            catch { }
        }

        // 兜底：bag.GetStuffCount(stuffId, excludeDic1, excludeDic2) 逐个查询可入箱物品
        //（签名来自反编译 Bag__GetStuffCount；exclude 传 null 即原始数量）
        try
        {
            MethodInfo? getStuffCountMethod = null;
            foreach (var m in bag.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name == "GetStuffCount")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(int))
                    {
                        getStuffCountMethod = m;
                        break;
                    }
                }
            }
            if (getStuffCountMethod != null)
            {
                foreach (var kvp in ItemCatalog.GetAllItems())
                {
                    try
                    {
                        var result = getStuffCountMethod.Invoke(bag, new object?[] { kvp.Key, null, null });
                        int count = Convert.ToInt32(result ?? 0);
                        if (count > 0)
                            items.Add(new ItemInfo { StuffId = kvp.Key, Count = count });
                    }
                    catch { }
                }
            }
        }
        catch { }

        return items;
    }'''

lines[start:end] = new_method.split(NL)
s = NL.join(lines)

# ReadCapacityFromBag: limit 字段语义注释（反编译 Bag__IsBagFullByCount 确认 limit>0 才限量）
s = s.replace('''            object? bag = GetProp(facility, "bag");
            if (bag == null) return;

            maxCap = GetInt(bag, "limit");
            if (maxCap == 0) maxCap = GetInt(bag, "Limit");''',
'''            object? bag = GetProp(facility, "bag");
            if (bag == null) return;

            // Bag.limit 字段真实存在（反编译 Bag__IsBagFullByCount）；
            // 0 表示不限量（普通箱子），>0 表示按数量限量（码头/熔炉等）
            maxCap = GetInt(bag, "limit");''')

io.open(p, 'w', encoding='utf-8', newline=NL).write(s)
print('ReadItemsFromBag rewritten')

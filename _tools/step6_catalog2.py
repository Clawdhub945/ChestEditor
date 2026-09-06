import io, json

NL = chr(10)
BS = chr(92)
base = r'C:\AI\yuanma\json_data' + BS

stuff = json.load(io.open(base + 'stuff.json', encoding='utf-8')) + \
        json.load(io.open(base + 'stuff2.json', encoding='utf-8'))
print('总条数', len(stuff))

lines = []
for x in sorted(stuff, key=lambda v: v['stuff_id']):
    name = (x.get('stuff_namezh-CN') or '').replace(BS + BS, BS + BS + BS + BS).replace('"', BS + BS + '"')
    lines.append('        {{ {0}, ("{1}", {2}) }},'.format(x['stuff_id'], name, x.get('stuff_type', 0)))

out = '''using System.Collections.Generic;
using System.Linq;

namespace ChestEditor.Core;

/// <summary>
/// 统一物品目录（由游戏官方配置 json_data/stuff.json + stuff2.json 生成，共 %d 条）。
/// 每条携带官方 stuff_type，"可入箱"判定 = stuff_type ∈ {{3,4,6}}（与游戏数据 100%% 相关，
/// 替代旧的 3xx/4xx/6xx ID 段魔数）。再生成脚本见 README。
/// </summary>
internal static class ItemCatalog
{
    private static readonly Dictionary<int, (string Name, int StuffType)> _items = new()
    {
%s
    };

    /// <summary>物品名称，未知 ID 返回 "未知(id)"</summary>
    internal static string GetName(int stuffId)
        => _items.TryGetValue(stuffId, out var v) ? v.Name : $"未知({stuffId})";

    /// <summary>官方类型：3=食物 4=工具/武器/装备 6=资源/材料</summary>
    internal static bool IsStorageItem(int stuffId)
        => _items.TryGetValue(stuffId, out var v) && v.StuffType is 3 or 4 or 6;

    /// <summary>可入箱物品列表（按 ID 排序，用于添加物品面板与背包兜底扫描）</summary>
    internal static List<KeyValuePair<int, string>> GetAllItems()
        => _items.Where(x => IsStorageItem(x.Key)).OrderBy(x => x.Key)
                 .Select(x => new KeyValuePair<int, string>(x.Key, x.Value.Name)).ToList();
}
''' % (len(stuff), NL.join(lines))

io.open('Core/ItemCatalog.cs', 'w', encoding='utf-8', newline=NL).write(out)
print('ItemCatalog.cs 已重生成（含 stuff_type）')

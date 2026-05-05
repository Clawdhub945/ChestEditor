using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ChestEditor;

public class ChestEditorComponent : MonoBehaviour
{
    internal static ChestEditorComponent? Instance;

    // HTTP 服务器写请求队列
    internal struct WriteRequest
    {
        public int ChestIndex;
        public int StuffId;
        public int Count;
        public bool IsAdd; // true=添加, false=删除
        public string? ResultJson;
        public System.Threading.ManualResetEventSlim Signal;
    }
    internal readonly ConcurrentQueue<WriteRequest> WriteQueue = new();

    // JSON 缓存（主线程写，HTTP 线程读，volatile 保证可见性）
    internal volatile string ChestsJson = "[]";
    internal volatile string ItemsJson = "[]";

    private bool _showWindow;
    private Rect _windowRect = new Rect(100, 100, 750, 700);
    private Vector2 _scrollPos;
    private List<ChestInfo> _chests = new();
    private string _searchText = "";
    private HashSet<int> _collapsedChests = new();

    // 延迟操作队列
    private Action? _pendingAction;

    // 筛选
    private static readonly Dictionary<int, (string Name, bool Enabled)> _filterItems = new()
    {
        { 103001, ("大箱子", true) },
        { 103002, ("料堆", true) },
        { 103003, ("货架", true) },
        { 103004, ("交易台", false) },
        { 103005, ("通商口岸", false) },
        { 103008, ("周转箱", false) },
        { 103009, ("冰窖", false) },
        { 103012, ("马车站", false) },
        { 104002, ("讲台", false) },
        { 104004, ("布道台", false) },
        { 104007, ("诊断台", false) },
        { 104016, ("餐桌", false) },
        { 104019, ("接待台", false) },
        { 104020, ("宴会桌", false) },
        { 104021, ("厕所", false) },
        { 104026, ("幸运轮盘", false) },
        { 105015, ("堆肥桶", false) },
        { 105026, ("码头", false) },
        { 105031, ("资源搜集点", false) },
        { 106001, ("军营", false) },
        { 106002, ("牢房", false) },
        { 106004, ("野营", false) },
        { 106005, ("王座", false) },
        { 106008, ("训练场", false) },
        { 107004, ("强盗营地", false) },
        { 107005, ("蛮族军营", false) },
        { 108012, ("永恒圣殿", false) },
        { 109005, ("国库", false) },
        { 111002, ("泰坦之手", false) },
    };

    internal struct ItemInfo { public int StuffId; public int Count; }
    internal struct ChestInfo { public int Guid; public int StuffId; public string Name; public List<ItemInfo> Items; public int MaxCap; public int UsedCap; public float PosX; public float PosY; public object Facility; }

    // 添加物品弹窗
    private bool _showAddItemWindow;
    private Rect _addWindowRect = new Rect(300, 200, 400, 500);
    private Vector2 _addItemScrollPos;
    private string _addItemSearch = "";
    private int _addItemCount = 1;
    private ChestInfo? _selectedChest;
    private int _selectedChestIndex = -1;

    private static List<KeyValuePair<int, string>>? _allItems;

    // 缓存方法
    private static MethodInfo? _addStuffMethod;
    private static MethodInfo? _removeStuffMethod;
    private static MethodInfo? _addStuffNoNotifyMethod;
    private static bool _methodCached;

    public ChestEditorComponent(IntPtr ptr) : base(ptr)
    {
        Instance = this;
    }

    internal List<ChestInfo> GetChests() => _chests;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            _showWindow = !_showWindow;
            if (_showWindow) RefreshChestList();
        }

        // 每帧更新 JSON 缓存
        UpdateJsonCache();

        // 处理 HTTP 写请求
        ProcessWriteRequests();
    }

    private void UpdateJsonCache()
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < _chests.Count; i++)
            {
                var c = _chests[i];
                if (i > 0) sb.Append(',');
                sb.Append($"{{\"index\":{i},\"guid\":{c.Guid},\"stuffId\":{c.StuffId},\"name\":\"{Escape(c.Name)}\",\"maxCap\":{c.MaxCap},\"usedCap\":{c.UsedCap},\"posX\":{c.PosX.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"posY\":{c.PosY.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"items\":[");
                for (int j = 0; j < c.Items.Count; j++)
                {
                    var item = c.Items[j];
                    if (j > 0) sb.Append(',');
                    sb.Append($"{{\"stuffId\":{item.StuffId},\"name\":\"{Escape(ItemNames.GetName(item.StuffId))}\",\"count\":{item.Count}}}");
                }
                sb.Append("]}");
            }
            sb.Append(']');
            ChestsJson = sb.ToString();

            // 物品列表（用于添加物品下拉）
            if (_allItems == null)
                _allItems = ItemNames.GetAllItems().ToList();
            if (_allItems != null)
            {
                var sb2 = new StringBuilder();
                sb2.Append('[');
                bool first = true;
                foreach (var kvp in _allItems)
                {
                    if (!first) sb2.Append(',');
                    first = false;
                    sb2.Append($"{{\"stuffId\":{kvp.Key},\"name\":\"{Escape(kvp.Value)}\"}}");
                }
                sb2.Append(']');
                ItemsJson = sb2.ToString();
            }
        }
        catch { }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    private void ProcessWriteRequests()
    {
        while (WriteQueue.TryDequeue(out var req))
        {
            try
            {
                // ChestIndex == -1 表示刷新操作
                if (req.ChestIndex == -1)
                {
                    RefreshChestList();
                    req.ResultJson = "{\"ok\":true}";
                }
                else if (req.IsAdd)
                {
                    AddAndUpdate(req.ChestIndex, req.StuffId, req.Count);
                }
                else
                {
                    RemoveAndUpdate(req.ChestIndex, req.StuffId, req.Count);
                }

                // 返回更新后的箱子 JSON（非刷新操作时）
                if (req.ChestIndex >= 0 && req.ChestIndex < _chests.Count)
                {
                    var c = _chests[req.ChestIndex];
                    var sb = new StringBuilder();
                    sb.Append("{\"index\":").Append(req.ChestIndex);
                    sb.Append(",\"guid\":").Append(c.Guid);
                    sb.Append(",\"stuffId\":").Append(c.StuffId);
                    sb.Append(",\"name\":\"").Append(Escape(c.Name)).Append('"');
                    sb.Append(",\"maxCap\":").Append(c.MaxCap);
                    sb.Append(",\"usedCap\":").Append(c.UsedCap);
                    sb.Append(",\"items\":[");
                    for (int j = 0; j < c.Items.Count; j++)
                    {
                        var item = c.Items[j];
                        if (j > 0) sb.Append(',');
                        sb.Append($"{{\"stuffId\":{item.StuffId},\"name\":\"{Escape(ItemNames.GetName(item.StuffId))}\",\"count\":{item.Count}}}");
                    }
                    sb.Append("]}");
                    req.ResultJson = sb.ToString();
                }
                else if (req.ChestIndex >= 0)
                {
                    req.ResultJson = "{\"error\":\"chest not found\"}";
                }
            }
            catch (Exception ex)
            {
                req.ResultJson = $"{{\"error\":\"{Escape(ex.Message)}\"}}";
            }
            finally
            {
                req.Signal.Set();
            }
        }
    }

    private static GUIStyle? _titleStyle;
    private static GUIStyle? _chestHeaderStyle;
    private static GUIStyle? _itemCountStyle;
    private static GUIStyle? _emptyStyle;
    private static GUIStyle? _highlightItemStyle;
    private static GUIStyle? _searchFieldStyle;
    private static GUIStyle? _addButtonStyle;
    private static GUIStyle? _removeButtonStyle;
    private static bool _stylesInited;

    private static void InitStyles()
    {
        if (_stylesInited) return;
        _stylesInited = true;

        _titleStyle = new GUIStyle { fontSize = 14 };
        _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        _chestHeaderStyle = new GUIStyle { fontSize = 13 };
        _chestHeaderStyle.normal.textColor = new Color(0.6f, 0.9f, 1f);

        _itemCountStyle = new GUIStyle { fontSize = 11, wordWrap = true };
        _itemCountStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        _emptyStyle = new GUIStyle();
        _emptyStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

        _searchFieldStyle = new GUIStyle { fontSize = 13 };
        _searchFieldStyle.normal.textColor = Color.white;
        var fieldBg = new Texture2D(1, 1);
        fieldBg.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f, 1f));
        fieldBg.Apply();
        _searchFieldStyle.normal.background = fieldBg;
        _searchFieldStyle.focused.textColor = Color.white;
        _searchFieldStyle.focused.background = fieldBg;
        _searchFieldStyle.padding = new RectOffset(4, 4, 2, 2);

        _highlightItemStyle = new GUIStyle();
        var hlTex = new Texture2D(1, 1);
        hlTex.SetPixel(0, 0, new Color(1f, 0.85f, 0.2f, 0.35f));
        hlTex.Apply();
        _highlightItemStyle.normal.background = hlTex;
        _highlightItemStyle.padding = new RectOffset(2, 2, 2, 2);

        _addButtonStyle = new GUIStyle();
        _addButtonStyle.normal.textColor = new Color(0.2f, 1f, 0.2f);
        _addButtonStyle.fontSize = 12;
        _addButtonStyle.normal.background = fieldBg;
        _addButtonStyle.padding = new RectOffset(4, 4, 2, 2);

        _removeButtonStyle = new GUIStyle();
        _removeButtonStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
        _removeButtonStyle.fontSize = 12;
        _removeButtonStyle.normal.background = fieldBg;
        _removeButtonStyle.padding = new RectOffset(4, 4, 2, 2);
    }

    private void OnGUI()
    {
        if (!_showWindow) return;
        InitStyles();
        _windowRect = GUI.Window(999001, _windowRect, (GUI.WindowFunction)DrawWindow, "箱子编辑器 (F11 关闭)");

        if (_showAddItemWindow && _selectedChest.HasValue)
        {
            _addWindowRect = GUI.Window(999002, _addWindowRect, (GUI.WindowFunction)DrawAddItemWindow, "添加物品");
        }

        // 执行延迟操作
        if (_pendingAction != null)
        {
            var action = _pendingAction;
            _pendingAction = null;
            action();
        }
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新", GUILayout.Width(44))) RefreshChestList();
        if (GUILayout.Button("收起", GUILayout.Width(44)))
            foreach (var c in _chests) _collapsedChests.Add(c.Guid);
        if (GUILayout.Button("展开", GUILayout.Width(44)))
            _collapsedChests.Clear();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"共 {_chests.Count} 个箱子", _titleStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(20))) _showWindow = false;
        GUILayout.EndHorizontal();

        bool filterChanged = false;
        GUILayout.BeginHorizontal();
        GUILayout.Label("筛选:", GUILayout.Width(36));
        if (GUILayout.Button("全选", GUILayout.Width(40)))
        {
            foreach (var k in _filterItems.Keys.ToList())
                _filterItems[k] = (_filterItems[k].Name, true);
            filterChanged = true;
        }
        if (GUILayout.Button("反选", GUILayout.Width(40)))
        {
            foreach (var k in _filterItems.Keys.ToList())
                _filterItems[k] = (_filterItems[k].Name, !_filterItems[k].Enabled);
            filterChanged = true;
        }
        GUILayout.EndHorizontal();

        int fi = 0;
        int perRow = 8;
        foreach (var kvp in _filterItems)
        {
            if (fi % perRow == 0)
            {
                if (fi > 0) GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(36);
            }
            string label = kvp.Value.Enabled ? $"[✓]{kvp.Value.Name}" : $"[  ]{kvp.Value.Name}";
            if (GUILayout.Button(label, GUILayout.Width(78)))
            {
                _filterItems[kvp.Key] = (kvp.Value.Name, !kvp.Value.Enabled);
                filterChanged = true;
            }
            fi++;
        }
        if (fi > 0) GUILayout.EndHorizontal();
        if (filterChanged) RefreshChestList();

        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索:", GUILayout.Width(36));
        _searchText = GUILayout.TextField(_searchText, _searchFieldStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("清除", GUILayout.Width(40))) _searchText = "";
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        bool searching = !string.IsNullOrEmpty(_searchText);
        string searchLower = searching ? _searchText.ToLower() : "";

        for (int ci = 0; ci < _chests.Count; ci++)
        {
            var chest = _chests[ci];
            if (searching)
            {
                bool chestHasMatch = false;
                foreach (var item in chest.Items)
                {
                    if (ItemNames.GetName(item.StuffId).ToLower().Contains(searchLower))
                    {
                        chestHasMatch = true;
                        break;
                    }
                }
                if (!chestHasMatch) continue;
            }

            bool collapsed = _collapsedChests.Contains(chest.Guid);
            string arrow = collapsed ? "▶" : "▼";
            string capText = chest.MaxCap > 0
                ? $"{chest.UsedCap:N0}/{chest.MaxCap:N0}"
                : $"{chest.Items.Count}";

            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button($"{arrow} {chest.Name}", _chestHeaderStyle))
            {
                if (collapsed) _collapsedChests.Remove(chest.Guid);
                else _collapsedChests.Add(chest.Guid);
            }
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+添加", _addButtonStyle, GUILayout.Width(50)))
            {
                _selectedChest = chest;
                _selectedChestIndex = ci;
                _showAddItemWindow = true;
                _addItemSearch = "";
                _addItemCount = 1;

            }

            if (GUILayout.Button("定位", GUILayout.Width(36)))
                LocateFacility(chest.PosX, chest.PosY);
            GUILayout.Label(capText, _itemCountStyle, GUILayout.Width(130));
            var chestTex = GetIconTexture(chest.StuffId);
            if (chestTex != null)
                GUILayout.Label(chestTex, GUILayout.Width(32), GUILayout.Height(32));
            GUILayout.EndHorizontal();

            if (collapsed) continue;

            if (chest.Items.Count == 0)
            {
                GUILayout.Label("  (空)", _emptyStyle);
                continue;
            }

            int col = 0;
            for (int i = 0; i < chest.Items.Count; i++)
            {
                var item = chest.Items[i];
                string n = ItemNames.GetName(item.StuffId);
                bool isMatch = searching && n.ToLower().Contains(searchLower);

                if (searching && !isMatch) continue;

                if (col % 8 == 0) GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(isMatch ? _highlightItemStyle : "box", GUILayout.Width(76));
                DrawItemIcon(item.StuffId, 36);
                string line1 = n.Length <= 6 ? n : n[..6];
                string line2 = n.Length <= 6 ? "" : n[6..];
                if (line2.Length > 6) line2 = line2[..6];
                GUILayout.Label(line1, _itemCountStyle, GUILayout.Width(72), GUILayout.Height(14));
                GUILayout.Label(line2, _itemCountStyle, GUILayout.Width(72), GUILayout.Height(14));
                GUILayout.Label($"x{item.Count}", _itemCountStyle, GUILayout.Width(72), GUILayout.Height(14));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-1", _removeButtonStyle, GUILayout.Width(24), GUILayout.Height(18)))
                {
                    int chestIndex = _chests.IndexOf(chest);
                    int capturedStuffId = item.StuffId;
                    _pendingAction = () => RemoveAndUpdate(chestIndex, capturedStuffId, 1);
                }
                if (GUILayout.Button("-All", _removeButtonStyle, GUILayout.Width(30), GUILayout.Height(18)))
                {
                    int chestIndex = _chests.IndexOf(chest);
                    int capturedStuffId = item.StuffId;
                    int capturedCount = item.Count;
                    _pendingAction = () => RemoveAndUpdate(chestIndex, capturedStuffId, capturedCount);
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                col++;
                if (col % 8 == 0) GUILayout.EndHorizontal();
            }
            if (col % 8 != 0) GUILayout.EndHorizontal();

            GUILayout.Space(6);
        }

        if (_chests.Count == 0)
            GUILayout.Label("无匹配箱子", _emptyStyle);

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private void DrawAddItemWindow(int id)
    {
        if (!_selectedChest.HasValue)
        {
            _showAddItemWindow = false;
            return;
        }

        var chest = _selectedChest.Value;

        GUILayout.Label($"向 [{chest.Name}] 添加物品", _titleStyle);
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索:", GUILayout.Width(36));
        _addItemSearch = GUILayout.TextField(_addItemSearch, _searchFieldStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("清除", GUILayout.Width(40))) _addItemSearch = "";
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("数量:", GUILayout.Width(36));
        string countStr = GUILayout.TextField(_addItemCount.ToString(), GUILayout.Width(80));
        if (int.TryParse(countStr, out int newCount) && newCount > 0)
            _addItemCount = newCount;
        if (GUILayout.Button("+1", GUILayout.Width(30))) _addItemCount++;
        if (GUILayout.Button("+10", GUILayout.Width(36))) _addItemCount += 10;
        if (GUILayout.Button("+100", GUILayout.Width(42))) _addItemCount += 100;
        if (GUILayout.Button("最大", GUILayout.Width(36))) _addItemCount = 99999;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        _addItemScrollPos = GUILayout.BeginScrollView(_addItemScrollPos);

        if (_allItems == null)
            _allItems = ItemNames.GetAllItems().ToList();

        string searchLower = _addItemSearch.ToLower();
        bool searching = !string.IsNullOrEmpty(_addItemSearch);

        foreach (var kvp in _allItems)
        {
            if (searching && !kvp.Value.ToLower().Contains(searchLower))
                continue;

            GUILayout.BeginHorizontal("box");
            DrawItemIcon(kvp.Key, 24);
            GUILayout.Label($"{kvp.Value} (ID:{kvp.Key})", _itemCountStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button($"添加 {_addItemCount}", _addButtonStyle, GUILayout.Width(70)))
            {
                int chestIndex = _selectedChestIndex;
                int capturedStuffId = kvp.Key;
                int capturedCount = _addItemCount;

                _pendingAction = () => AddAndUpdate(chestIndex, capturedStuffId, capturedCount);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(4);
        if (GUILayout.Button("关闭", GUILayout.Height(24)))
            _showAddItemWindow = false;

        GUI.DragWindow();
    }

    // ====== 物品操作方法 ======

    private static void CacheBagMethods(object bag)
    {
        if (_methodCached) return;
        _methodCached = true;

        var bagType = bag.GetType();
        var methods = bagType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // 找 AddStuffWithoutNotify(int, int)
        foreach (var m in methods)
        {
            if (m.Name == "AddStuffWithoutNotify")
            {
                var p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int))
                {
                    _addStuffNoNotifyMethod = m;

                    break;
                }
            }
        }

        // 找 AddStuff(int, int, bool) 作为备选
        if (_addStuffNoNotifyMethod == null)
        {
            foreach (var m in methods)
            {
                if (m.Name == "AddStuff")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) && p[2].ParameterType == typeof(bool))
                    {
                        _addStuffMethod = m;

                        break;
                    }
                }
            }
        }

        // 找 RemoveStuff(int, int, bool)
        foreach (var m in methods)
        {
            if (m.Name == "RemoveStuff")
            {
                var p = m.GetParameters();
                if (p.Length == 3 && p[0].ParameterType == typeof(int) && p[1].ParameterType == typeof(int) && p[2].ParameterType == typeof(bool))
                {
                    _removeStuffMethod = m;

                    break;
                }
            }
        }
    }

    internal void RemoveAndUpdate(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        try
        {
            object? bag = GetProp(chest.Facility, "bag");
            if (bag == null)
            {
                Plugin.LogError("bag 为 null");
                return;
            }

            CacheBagMethods(bag);

            if (_removeStuffMethod != null)
            {
                _removeStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                Plugin.LogInfo($"删除成功: {ItemNames.GetName(stuffId)}({stuffId}) x{count}");

                // 只更新当前箱子的物品数据
                UpdateChestItems(chestIndex);
            }
            else
            {
                Plugin.LogError("RemoveStuff 方法未找到");
            }
        }
        catch (Exception ex)
        {
            Plugin.LogError($"RemoveAndUpdate 异常: {ex.Message}");
        }
    }

    internal void AddAndUpdate(int chestIndex, int stuffId, int count)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        try
        {
            object? bag = GetProp(chest.Facility, "bag");
            if (bag == null)
            {
                Plugin.LogError("bag 为 null");
                return;
            }

            CacheBagMethods(bag);

            // 优先用 AddStuffWithoutNotify 避免回调卡死
            if (_addStuffNoNotifyMethod != null)
            {
                _addStuffNoNotifyMethod.Invoke(bag, new object[] { stuffId, count });
                Plugin.LogInfo($"添加成功: {ItemNames.GetName(stuffId)}({stuffId}) x{count}");
            }
            else if (_addStuffMethod != null)
            {
                _addStuffMethod.Invoke(bag, new object[] { stuffId, count, false });
                Plugin.LogInfo($"添加成功: {ItemNames.GetName(stuffId)}({stuffId}) x{count}");
            }
            else
            {
                Plugin.LogError("AddStuff 方法未找到");
                return;
            }

            // 只更新当前箱子的物品数据
            UpdateChestItems(chestIndex);
        }
        catch (Exception ex)
        {
            Plugin.LogError($"AddAndUpdate 异常: {ex.Message}");
        }
    }

    internal void UpdateChestItems(int chestIndex)
    {
        if (chestIndex < 0 || chestIndex >= _chests.Count) return;

        var chest = _chests[chestIndex];
        var newItems = ReadItemsFromBag(chest.Facility);

        int maxCap = 0, usedCap = 0;
        ReadCapacityFromBag(chest.Facility, ref maxCap, ref usedCap);

        _chests[chestIndex] = new ChestInfo
        {
            Guid = chest.Guid,
            StuffId = chest.StuffId,
            Name = chest.Name,
            Items = newItems,
            MaxCap = maxCap,
            UsedCap = usedCap,
            PosX = chest.PosX,
            PosY = chest.PosY,
            Facility = chest.Facility
        };

        // 调试输出

    }

    // ====== 反射工具 ======

    private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    private static object? GetProp(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var prop = t.GetProperty(name, BF);
                if (prop != null) return prop.GetValue(obj);
                t = t.BaseType;
            }
        }
        catch { }
        return null;
    }

    private static int GetInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var field = t.GetField(name, BF);
                if (field != null) return (int)(field.GetValue(obj) ?? 0);
                var prop = t.GetProperty(name, BF);
                if (prop != null) return (int)(prop.GetValue(obj) ?? 0);
                t = t.BaseType;
            }
        }
        catch { }
        return 0;
    }

    private static int GetGuid(object obj)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                foreach (var name in new[] { "Guid", "guid" })
                {
                    var prop = t.GetProperty(name, BF);
                    if (prop != null) { var g = prop.GetGetMethod(); if (g != null) return (int)(g.Invoke(obj, null) ?? 0); }
                }
                t = t.BaseType;
            }
        }
        catch { }
        return 0;
    }

    // ====== 图片加载 ======

    private static readonly Dictionary<int, Texture2D?> _iconCache = new();
    private static Dictionary<string, Sprite>? _spriteDict;
    private static bool _spriteScanned;

    private static void EnsureScanned()
    {
        if (_spriteScanned) return;
        _spriteScanned = true;
        _spriteDict = new Dictionary<string, Sprite>();
        foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sp != null && !string.IsNullOrEmpty(sp.name) && !_spriteDict.ContainsKey(sp.name))
                _spriteDict[sp.name] = sp;
        }
    }

    private static Texture2D? GetIconTexture(int stuffId)
    {
        if (_iconCache.TryGetValue(stuffId, out var cached)) return cached;

        try
        {
            EnsureScanned();
            string targetName = $"ui_{stuffId}";

            if (_spriteDict != null && _spriteDict.TryGetValue(targetName, out var sprite))
            {
                var atlas = sprite.texture;
                var texRect = sprite.textureRect;
                int sx = (int)texRect.x, sy = (int)texRect.y;
                int sw = (int)texRect.width, sh = (int)texRect.height;

                Color[]? pixels = null;
                try
                {
                    pixels = atlas.GetPixels(sx, sy, sw, sh);
                }
                catch
                {
                    int aw = atlas.width, ah = atlas.height;
                    var atlasRT = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(atlas, atlasRT);
                    var prev = RenderTexture.active;
                    RenderTexture.active = atlasRT;

                    var tmpTex = new Texture2D(aw, ah, TextureFormat.RGBA32, false);
                    tmpTex.ReadPixels(new Rect(0, 0, aw, ah), 0, 0);
                    tmpTex.Apply();
                    pixels = tmpTex.GetPixels(sx, sy, sw, sh);
                    UnityEngine.Object.Destroy(tmpTex);

                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(atlasRT);
                }

                var smallTex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
                smallTex.SetPixels(pixels);
                smallTex.Apply();

                _iconCache[stuffId] = smallTex;
                return smallTex;
            }

            _iconCache[stuffId] = null;
            return null;
        }
        catch
        {
            _iconCache[stuffId] = null;
            return null;
        }
    }

    private static void DrawItemIcon(int stuffId, float size)
    {
        var tex = GetIconTexture(stuffId);
        if (tex != null)
            GUILayout.Label(tex, GUILayout.Width(size), GUILayout.Height(size));
        else
            GUILayout.Space(size);
    }

    // ====== 核心逻辑 ======

    internal void RefreshChestList()
    {
        _chests.Clear();
        _collapsedChests.Clear();

        try
        {
            var territory = SaveLoadPatches.CachedTerritory;
            if (territory == null)
            {

                return;
            }

            object? facilityDic = GetProp(territory, "facility_dic");
            if (facilityDic == null) return;

            var values = GetProp(facilityDic, "Values");
            if (values == null) return;

            var getEnum = values.GetType().GetMethod("GetEnumerator", BF);
            if (getEnum == null) return;
            var enumerator = getEnum.Invoke(values, null);
            var moveNext = enumerator.GetType().GetMethod("MoveNext", BF);
            var current = enumerator.GetType().GetProperty("Current", BF);

            while ((bool)(moveNext.Invoke(enumerator, null) ?? false))
            {
                var facility = current.GetValue(enumerator);
                if (facility == null) continue;

                int stuffId = GetInt(facility, "stuff_id");
                bool show = _filterItems.ContainsKey(stuffId) && _filterItems[stuffId].Enabled;
                if (!show) continue;

                int guid = GetGuid(facility);

                string name = "";
                string logPrefix = GetProp(facility, "LOG_PREFIX")?.ToString() ?? "";
                if (!string.IsNullOrEmpty(logPrefix))
                {
                    var parts = logPrefix.Split(' ');
                    if (parts.Length >= 2) name = parts[1];
                }
                if (string.IsNullOrEmpty(name))
                    name = GetProp(facility, "stuff_name_with_id_index")?.ToString() ?? "";
                if (string.IsNullOrEmpty(name))
                    name = ItemNames.GetName(stuffId);

                var items = ReadItemsFromBag(facility);

                int maxCap = 0, usedCap = 0;
                ReadCapacityFromBag(facility, ref maxCap, ref usedCap);

                float px = 0, py = 0;
                ReadFacilityPos(facility, ref px, ref py);

                _chests.Add(new ChestInfo
                {
                    Guid = guid,
                    StuffId = stuffId,
                    Name = name,
                    Items = items,
                    MaxCap = maxCap,
                    UsedCap = usedCap,
                    PosX = px,
                    PosY = py,
                    Facility = facility
                });
            }

            // 默认全部收起
            foreach (var c in _chests)
                _collapsedChests.Add(c.Guid);


        }
        catch (Exception ex)
        {

        }
    }

    private List<ItemInfo> ReadItemsFromBag(object facility)
    {
        var items = new List<ItemInfo>();

        object? bag = GetProp(facility, "bag");
        if (bag == null) return items;

        // 获取 BagDic 对象
        object? bagDic = GetProp(bag, "dic");

        // 1. 找到 bag.GetStuffCount(int, Dictionary, Dictionary) 方法
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

        // 2. 从 BagDic 中提取两个 Dictionary<int,int> 参数
        object? dict1 = null, dict2 = null;
        if (bagDic != null)
        {
            var dicType = bagDic.GetType();
            var dicFields = new List<object>();
            // 遍历 BagDic 的所有字段，找到 Dictionary<int,int> 类型的
            var t = dicType;
            while (t != null && t != typeof(object))
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        var val = field.GetValue(bagDic);
                        if (val == null) continue;
                        var valType = val.GetType();
                        if (valType.IsGenericType)
                        {
                            var args = valType.GetGenericArguments();
                            if (args.Length == 2 && args[0] == typeof(int) && args[1] == typeof(int))
                            {

                                dicFields.Add(val);
                            }
                        }
                    }
                    catch { }
                }
                t = t.BaseType;
            }

            if (dicFields.Count >= 2)
            {
                dict1 = dicFields[0];
                dict2 = dicFields[1];
            }
            else if (dicFields.Count == 1)
            {
                dict1 = dicFields[0];
            }
        }

        // 3. 用 GetStuffCount 逐个检查物品，传入实际字典
        if (getStuffCountMethod != null)
        {
            var possibleIds = new List<int>();
            foreach (var kvp in ItemNames.GetAllItems())
                possibleIds.Add(kvp.Key);



            try
            {
                foreach (int stuffId in possibleIds)
                {
                    try
                    {
                        var result = getStuffCountMethod.Invoke(bag, new object[] { stuffId, dict1, dict2 });
                        int count = Convert.ToInt32(result ?? 0);
                        if (count > 0)
                            items.Add(new ItemInfo { StuffId = stuffId, Count = count });
                    }
                    catch { }
                }

                if (items.Count > 0)
                {

                    return items;
                }
                else
                {

                }
            }
            catch
            {

            }
        }

        // 4. 备选：直接遍历 BagDic 的 Dictionary<int,int> 字段
        if (bagDic != null)
        {

            var t = bagDic.GetType();
            while (t != null && t != typeof(object))
            {
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        var val = field.GetValue(bagDic);
                        if (val == null) continue;

                        var valType = val.GetType();
                        if (valType.IsGenericType)
                        {
                            var args = valType.GetGenericArguments();
                            if (args.Length == 2 && args[0] == typeof(int) && args[1] == typeof(int))
                            {

                                var ge = valType.GetMethod("GetEnumerator", BF);
                                if (ge != null)
                                {
                                    var en = ge.Invoke(val, null);
                                    var mn = en.GetType().GetMethod("MoveNext", BF);
                                    var cr = en.GetType().GetProperty("Current", BF);

                                    while ((bool)(mn.Invoke(en, null) ?? false))
                                    {
                                        var entry = cr.GetValue(en);
                                        if (entry == null) continue;

                                        int key = GetInt(entry, "Key");
                                        int v = GetInt(entry, "Value");
                                        if (v > 0)
                                            items.Add(new ItemInfo { StuffId = key, Count = v });
                                    }

                                    if (items.Count > 0)
                                    {

                                        return items;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                t = t.BaseType;
            }
        }

        // 5. 备选：尝试 bag.GetAllStuff()
        try
        {
            var getAllStuff = bag.GetType().GetMethod("GetAllStuff", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getAllStuff != null)
            {

                var result = getAllStuff.Invoke(bag, null);
                if (result != null)
                {
                    // 尝试遍历返回的集合
                    var resultType = result.GetType();
                    var countProp = resultType.GetProperty("Count", BF);
                    if (countProp != null)
                    {
                        int count = (int)(countProp.GetValue(result) ?? 0);

                        // 遍历每一项
                        var getItem = resultType.GetMethod("get_Item", BF);
                        if (getItem != null)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    var entry = getItem.Invoke(result, new object[] { i });
                                    if (entry == null) continue;
                                    int key = GetInt(entry, "stuff_id") != 0 ? GetInt(entry, "stuff_id") : GetInt(entry, "Key") != 0 ? GetInt(entry, "Key") : GetInt(entry, "StuffId");
                                    int val = GetInt(entry, "count") != 0 ? GetInt(entry, "count") : GetInt(entry, "Value") != 0 ? GetInt(entry, "Value") : GetInt(entry, "Count");
                                    if (key > 0 && val > 0)
                                        items.Add(new ItemInfo { StuffId = key, Count = val });
                                }
                                catch { }
                            }
                        }
                    }

                    if (items.Count > 0)
                    {

                        return items;
                    }
                }
            }
        }
        catch
        {

        }


        return items;
    }

    private static int GetListCount(object list)
    {
        try
        {
            var countProp = list.GetType().GetProperty("Count", BF);
            if (countProp != null) return (int)(countProp.GetValue(list) ?? 0);
        }
        catch { }
        return 0;
    }

    private static int GetListItem(object list, int index)
    {
        try
        {
            var getItem = list.GetType().GetMethod("get_Item", BF);
            if (getItem != null) return (int)(getItem.Invoke(list, new object[] { index }) ?? 0);
        }
        catch { }
        return 0;
    }

    private void ReadCapacityFromBag(object facility, ref int maxCap, ref int usedCap)
    {
        try
        {
            object? bag = GetProp(facility, "bag");
            if (bag == null) return;

            maxCap = GetInt(bag, "limit");
            if (maxCap == 0) maxCap = GetInt(bag, "Limit");

            // 计算已用容量
            var items = ReadItemsFromBag(facility);
            usedCap = items.Sum(x => x.Count);
        }
        catch { }
    }

    private void ReadFacilityPos(object facility, ref float px, ref float py)
    {
        try
        {
            string[] xNames = { "pos_x", "posX", "PosX", "x", "X", "tile_x", "tileX", "TileX", "grid_x", "gridX" };
            string[] yNames = { "pos_y", "posY", "PosY", "y", "Y", "tile_y", "tileY", "TileY", "grid_y", "gridY" };

            foreach (var n in xNames)
            {
                float v = GetFloat(facility, n);
                if (v != 0) { px = v; break; }
            }
            foreach (var n in yNames)
            {
                float v = GetFloat(facility, n);
                if (v != 0) { py = v; break; }
            }

            if (px == 0 && py == 0)
            {
                var pos = GetProp(facility, "position") ?? GetProp(facility, "Position") ?? GetProp(facility, "pos");
                if (pos != null)
                {
                    px = GetFloat(pos, "x");
                    py = GetFloat(pos, "y");
                }
            }
        }
        catch { }
    }

    private static float GetFloat(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            while (t != null && t != typeof(object))
            {
                var field = t.GetField(name, BF);
                if (field != null) return Convert.ToSingle(field.GetValue(obj) ?? 0);
                var prop = t.GetProperty(name, BF);
                if (prop != null) return Convert.ToSingle(prop.GetValue(obj) ?? 0);
                t = t.BaseType;
            }
        }
        catch { }
        return 0;
    }

    private void LocateFacility(float targetX, float targetY)
    {
        try
        {
            var cam = Camera.main;
            if (cam == null) return;
            var pos = cam.transform.position;
            pos.x = targetX;
            pos.y = targetY;
            cam.transform.position = pos;
        }
        catch { }
    }
}

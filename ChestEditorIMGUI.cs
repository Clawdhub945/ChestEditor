using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChestEditor;

public partial class ChestEditorComponent
{
    // IMGUI 窗口状态
    private bool _showWindow;
    private Rect _windowRect = new Rect(100, 100, 750, 700);
    private Vector2 _scrollPos;
    private string _searchText = "";

    // 添加物品弹窗
    private bool _showAddItemWindow;
    private Rect _addWindowRect = new Rect(300, 200, 400, 500);
    private Vector2 _addItemScrollPos;
    private string _addItemSearch = "";
    private int _addItemCount = 1;
    private ChestInfo? _selectedChest;
    private int _selectedChestIndex = -1;

    // 延迟操作队列
    private Action? _pendingAction;

    // IMGUI 样式
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

}

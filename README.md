# ChestEditor（Territory 游戏修改器）

基于 BepInEx 6 (IL2CPP) 的 Territory 游戏修改器：游戏内启动 HTTP 服务器（:8765），浏览器打开交互式修改页面。
支持：箱子/背包物品编辑、龙素材/龙魂/召唤、统一实体编辑器（实体/NPC 字段修改+持久化）、科技树解锁、NPC 字段翻译。

## 目录结构

```
Plugin.cs                入口：Harmony 补丁注册、组件注入、HTTP 服务器启动
SaveLoadPatches.cs       Harmony 补丁（读档后重放修改、速度覆盖钩子），捕获 CachedTerritory
GlobalUsings.cs          全局命名空间导入

Core/                    基础层
  JsonBuilder.cs         JSON 输出统一入口（System.Text.Json / Utf8JsonWriter）
  EmbeddedResources.cs   嵌入资源统一读取（带缓存）
  DataTables.cs          游戏数据表加载：物品 / 士兵类型 / 箱子筛选 / 龙类型 / 龙天性

Data/                    游戏数据表（嵌入资源，由 _tools/generate_data_tables.py 生成）
  items.json             [[id,name,stuff_type]]   1840 条，源自 stuff.json + stuff2.json
  soldier_types.json     [[id,name]]              45 条，源自 soldier_equip.json + 名称表
  chest_filters.json     [[id,name,enabled]]      34 条箱子设施筛选（显示名以原代码为准）
  dragon_types.json      [[name,cn,baseId]]       16 条龙类型
  dragon_natures.json    [[id,name]]              18 条，源自 dragon_nature.json

Interop/                 IL2CPP 互操作层（全项目唯一定义处，禁止私拷）
  Il2CppApi.cs           原生 API 唯一切入口/门面：指针提取、类/字段/方法枚举、安全字段读取、静态字段
                         （业务层一律经此访问，不得直接调用 Il2CppInterop.Runtime.IL2CPP.*）
  Il2CppMemory.cs        unsafe 内存直读直写（int/float/string/List<int>）
  Il2CppInvoke.cs        方法查找 + il2cpp_runtime_invoke 参数编组
  ManagedReflect.cs      对 IL2CPP 代理对象的 C# 反射读写（MemberInfo 缓存）

Game/                    游戏业务服务（每个领域一个文件）
  GameContext.cs         Game/Territory 根对象解析
  ChestService.cs        箱子/背包：设施扫描、物品增删、计划库存、筛选、定位、JSON 构建（TTL 缓存）
  DragonService.cs       龙系统：素材背包（本地记账+实时读）、龙魂、召唤、龙实体
  TechTreeService.cs     科技树
  EntityScan.cs          统一实体扫描 + 字段读写（EditorEntity 模型）
  NpcEditor.cs           NPC 列表/字段修改 + speed/hp/hp_total 每帧持续覆盖
  EntityDestroyer.cs     实体销毁（Npc/Facility/Ship/通用 四级策略）
  Modifications/
    ModificationStore.cs 修改记录：内存 + 磁盘持久化（System.Text.Json，与旧格式兼容）+ 重应用

Threading/
  MainThread.cs          主线程调度：HTTP 线程 Run(闭包) 同步等待，游戏主线程每帧 Pump

Component/
  ChestEditorComponent.cs IL2CPP 注入的 MonoBehaviour（快捷键 F11、重应用调度、任务泵）

Web/                     HTTP 层
  HttpServer.cs          监听 + 线程池分发
  Router.cs              路由表（method + 路径模式，支持 {param} 与 *通配段）
  Http.cs                RequestCtx（请求体 JSON 解析）+ 响应工具
  Handlers/              按功能拆分的路由注册：Static / Chest / Dragon / Tech / Entity / Npc
  Static/                前端（嵌入资源，零构建）
    index.html           页面骨架
    css/app.css          样式
    js/                  按面板拆分：state(全局状态) ui(通用) api(请求) chest dragon npc tech entity main(入口)
    data/                前端数据表：npc_types / tech_tree / tech_info / tech_icon
```

## 线程模型

- **HTTP 线程**：只做路由、请求体解析（System.Text.Json）、提交任务、回写响应。
- **主线程**（`MainThread.Pump`）：所有触碰游戏对象/IL2CPP 内存的读写都在这里执行，
  HTTP 线程通过 `MainThread.Run(() => Service.Xxx(), timeout)` 同步等待结果。
- 只读 JSON（箱子列表/物品表/龙背包）带 500ms TTL 缓存，替代旧的每帧全量重建。

## JSON 输出约定

**全项目禁止手工拼接 JSON 字符串**（`StringBuilder` + 插值 + 自行转义），一律走 `Core/JsonBuilder`：

```csharp
return JsonBuilder.Object(w => { w.WriteNumber("stuffId", id); w.WriteString("name", name); });
return JsonBuilder.Build(w => { w.WriteStartArray(); /* ... */ w.WriteEndArray(); });
return JsonBuilder.Ok();                       // {"ok":true}
return JsonBuilder.Ok("action", "added");      // {"ok":true,"action":"added"}
return JsonBuilder.Error(ex);                  // {"error":"..."}
```

- 转义、逗号、数字/布尔字面量全部由 `System.Text.Json` 的 `Utf8JsonWriter` 负责。
- 编码器用 `JavaScriptEncoder.Create(UnicodeRanges.All)`：中文原样输出（不膨胀成 `\uXXXX`），
  同时仍转义 `< > & '` 等 HTML 敏感字符。
- 来自游戏内存的浮点写出口一律套 `JsonBuilder.Safe(...)`：NaN/±Infinity 降级为 `0`，
  避免一个坏字段让整份响应变成非法 JSON。

## 持久化

- 修改记录：`BepInEx/config/ChestEditor_modifications.json`
  格式 `{"guid:字段": 数值, "npc:npcId:字段": 数值}`（与旧版手写格式完全兼容，改用 System.Text.Json 读写）。
  读档后自动重放 5 次；NPC 的 speed/hp/hp_total 通过 Harmony postfix 每帧持续覆盖。
- 字段翻译：`field_translations.json` 已作为嵌入资源打进 DLL，无需再手动拷贝到插件目录
  （插件目录存在同名文件时优先使用，便于运行时调整）。

## 数据来源（游戏源码/反编译验证）

以下结论均已对照开发者源码（C:\AI\yuanma）与 IDA 反编译向量库核实：

- `Bag.dic`（BagDic）直接继承 `Dictionary<int,int>`，读背包=直接枚举；`Bag.limit` 字段真实存在（0=不限量）
- `Bag.AddStuff(id,count,notify)` / `RemoveStuff(id,count,notify)` / `GetStuffCount(id,dic1,dic2)` 为官方增删查入口
- `main_scene.tech_helper` 提供 `UnlockAllTech(except_equip)` 与 `UnlockNewTech(tech_id, notify)`，
  科技解锁全部走这两个正规入口（含设施联动/通知副作用）
- `Npc : Soldier`：`_npc_type`、`npc_id`、`move_agent.SetSpeed`、`UpdateAgentMoveSpeed`、
  `Soldier.UpdateHpProgressBarTotal`、`Npc.LeaveMapAndDestroy(reason)` 均与源码一致
- `W.dragon_soul_list` 为 `List<DragonSoul>`；DragonSoul 字段为小写 `head/claw/shield/cloud/potentiality/nature_list`

## 数据表再生成（游戏更新后）

游戏官方配置位于 `C:\AI\yuanma\json_data\`（游戏导出），以下文件由此生成：

统一由 `_tools/generate_data_tables.py` 生成（`--check` 只校验不写文件；`--verify` 额外与现有 C# 表逐条比对）：

| 本项目文件 | 来源 | 说明 |
|---|---|---|
| `Data/items.json` | stuff.json + stuff2.json | 1840 条，与重构前 ItemCatalog 逐条一致（`--verify` 已校验） |
| `Data/soldier_types.json` | soldier_equip.json + 名称表 | 44 条 + 特例 `0=市民`，与原硬编码表逐条一致 |
| `Data/chest_filters.json` | ChestService 原表 | 显示名以原代码为准：4 条与官方不同（属有意为之，脚本会打印告警） |
| `Data/dragon_types.json` | DragonService 原表 | 无官方对应表，故以代码现值为准 |
| `Data/dragon_natures.json` | dragon_nature.json | 18 条，与原硬编码表逐条一致 |
| `Web/Static/data/npc_types.json` | career.json | npc_type → npc_type_name_zh-CN（78 条） |
| `Web/Static/data/tech_tree.json` | tech_tree.json | 已确认与官方 152 条一致（含手工注释 t 字段，故未自动再生成） |


## 构建与部署

1. `dotnet build`（需游戏本体的 BepInEx interop DLL，路径见 csproj 的 `$(GameDir)`）
2. 构建产物自动复制到 `C:\TerritoryModTest`
3. 将 DLL 拷入游戏 mod 目录：`BepInEx/plugins/1000/Assemblies/ChestEditor.dll`
4. 启动游戏，按 **F11** 自动打开浏览器 `http://localhost:8765/`

## 测试清单（每次改动后过一遍）

- 主页加载：侧栏五组分类可展开，3 秒轮询正常
- 箱子：展开箱子 → 物品 +/-/All → 添加物品（搜索/数量）→ 计划库存增删 → 定位
- 筛选：单选切换 / 全选 / 清空
- 龙素材：数量修改；龙魂列表读取与属性修改；召唤（类型/等级/天性）；龙实体扫描与改字段
- 实体编辑器：扫描 → 实体分组 → 字段表格读取/修改 → 消灭 → 定位 → 列方法
- NPC：扫描 → 字段翻译显示中文 → 勾选字段修改 → 读档后修改仍在（重放）
- 科技树：树渲染 → 单个解锁/锁定 → 全部解锁 → 研究

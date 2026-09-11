# Territory 远程自动化手册：启动 → 读档 → 操作 → 退出

> **读者**：AI 会话 / 开发者（本文自包含，不需要其他上下文即可照做）。
> **目标**：纯命令行远程驱动 Territory 游戏（Steam AppID **1455910**）完成
> 「启动游戏 → 进入存档 → 调 mod API 操作 → 读日志 → 优雅退出」全流程。
> 依据 ChestEditor mod（commit `aa04184` 及之后）。所有接口**仅监听 localhost**，端口 **8765**。

---

## 0. 前提与通用约定

| 项 | 值 |
|---|---|
| 游戏 | Territory，`C:\Program Files (x86)\Steam\steamapps\common\Territory` |
| 启动方式 | **只能**走 Steam 协议：`steam://rungameid/1455910` |
| mod | ChestEditor（BepInEx 插件），部署在 `C:\TerritoryModTest\`，游戏启动时自动加载 |
| mod 源码 | `C:\AI\mod\ChestEditor`；构建 `dotnet build -c Release --no-restore`（产物自动复制到 ModDir；改了 mod 必须重启游戏才生效） |
| HTTP 基址 | `http://localhost:8765`（HttpListener） |
| 存档根目录 | `C:\Users\<user>\AppData\LocalLow\Looming\Territory\TerritoryArchive\UserData`（即游戏 `ArchiveManager.get_dir_path()`；备选探测路径 `...\Looming\Territory\Save`） |
| 存档形态 | 每个存档 = 根目录下一个子文件夹，名如 `2026-09-11_00_37018a61...`（日期_时间_GUID，自动档带 `_autosaveN` 后缀），内含 `summary.sav` / `archive_zip.sav` |

**curl 通用约定（两个老坑，必看）**：

- 必须用 `localhost`，用 `127.0.0.1` 会 400 `Invalid Hostname`。
- POST 无 body 时必须加 `-H "Content-Length: 0"`（否则 411 / 请求挂起），或干脆 `-d '{}'`。
- 需要 touching 游戏对象的接口，mod 内部会切到 Unity 主线程执行，HTTP 请求阻塞到完成（各端点有自己的超时）。

---

## 1. 启动游戏

```powershell
# PowerShell
Start-Process "steam://rungameid/1455910"
# Git Bash
cmd //c start steam://rungameid/1455910
```

- ⚠ 直接运行 `Territory.exe` 会**秒退**（Steam API 校验失败），别走这条。
- 启动到主菜单 + BepInEx 就绪 ≈ **10~55 秒**（文件缓存冷热而定）。
- **就绪探测**：每 2~5s 轮询一次：

  ```bash
  curl -s http://localhost:8765/api/editor/state
  ```

  返回 JSON 即算 mod 活了：
  `{"inSave":false,"loading":false,"saveLoads":0,"scanning":false}`
  - `inSave` = 已定位到 AreaMap（= 处于存档世界内）。主菜单时恒 `false`。
  - `saveLoads` = 读档成功次数计数器，每次成功 DoLoad 后 +1。
- 60s+ 仍连不上 → 见 §6 排障（进程没起来 / 闪退）。

---

## 2. 进入存档（远程读档）

1. **选存档**：列存档根目录子文件夹按修改时间排序（或用户指定）。`dir` 参数就是**文件夹名**（不是路径）。
2. **发指令**：

   ```bash
   curl -s -X POST http://localhost:8765/api/editor/debug/load \
        -H "Content-Type: application/json" \
        -d '{"dir":"2026-09-11_00_37018a61e00f465a8883085617739107"}'
   ```

   → `{"ok":true,"result":true}` = **指令已受理**。
   机制：主线程上调游戏自己的 `UI.StartGame(folder)`，并设置
   `MainScene.auto_load_archive_folder_name` 静态字段（与游戏"继续游戏"同一条路）。
   `result:false` = 受理失败（看 `BepInEx\LogOutput.log` 里 `[UI读档]` 行）。
3. **等完成**：读档异步进行。继续轮询 `GET /api/editor/state`，直到
   `inSave:true` **且** `saveLoads` 比进档前 +1（期间 `loading:true` = 正在读）。大档几十秒，2~5s 一拍，别急。
4. **禁止的路径（实测踩坑）**：
   - 直接调 `ArchiveManager.Load/DoLoad` —— 路径/文件校验全对，但游戏内部校验**静默返回 false**，无任何报错。
   - `UI.ExitAndLoadGame` —— 只适用于"**游戏中**换档"；主菜单下它不设置待载信息，等于空操作。
   - 本 mod 刻意不做"启动即自动进档"：游戏启动后永远停在主菜单，进档必须显式调 `debug/load`。

---

## 3. 操作（`inSave:true` 之后）

- **先全量扫描**拿实体清单（后续一切按 ptrHash/guid 定位）：

  ```bash
  curl -s -X POST http://localhost:8765/api/editor/scan -H "Content-Length: 0"
  ```

  ≈ 4s（游戏失焦时全速；有焦点时会为不卡游戏放缓）。返回全部在场实体（实测 ~6500-11000 条）。
- **常用只读端点**：
  - `GET /api/editor/state` — inSave / loading / saveLoads / scanning
  - `GET /api/editor/entities` — 全量实体 JSON（返回上次扫描缓存）
  - `GET /api/editor/fields/{ptrHash}` — 单实体全部字段（≈15-25ms，一次主线程往返）
- **常用写端点**（完整注册表在源码 `Web/Handlers/*.cs` 搜 `Router.Add`）：
  - `POST /api/editor/destroy/batch {"ptrHashes":[...]}` — 批量销毁实体
  - `POST /api/editor/scale/batch {"ptrHashes":[...],"factor":10}` — 战斗力缩放
  - `POST /api/editor/stuff/batch {"guids":[...]}` — 清地面掉落物；`stuff/pickup` — 拾取入国库
  - `POST /api/editor/animal/spawn {"stuffId":501005,"count":3}` — 召唤家畜（1..10；落点由游戏族群/牧场逻辑接管，野生动物会被生态回收）
- **循环读档的注意点**：每轮"操作→重进档"后 `saveLoads` 都会变。任何长驻自动逻辑
  （定时清理等）都应监控 `saveLoads` 变化 / `inSave` 变 false 并自动停止，防止操作到半个世界。

---

## 4. 读日志 / 退出

**日志（排障与"读最后一条日志"类任务）**：

| 用途 | 文件 |
|---|---|
| mod 托管日志（`[EntityEditor]` / `[UI读档]` 行） | `<游戏目录>\BepInEx\LogOutput.log`（**跨 session 追加不轮转**，按时间戳取最新一段） |
| Unity 引擎层报错（FMOD / Instantiate / native） | `C:\Users\<user>\AppData\LocalLow\Looming\Territory\Player.log`（上一轮是 `Player-prev.log`） |
| 闪退（进程整个没了） | **先看** `<游戏目录>\BepInEx\ErrorLog.log`（Fatal error + native 栈；托管 try/catch 拦不住的那类） |

**退出**：

```bash
curl -s -X POST http://localhost:8765/api/editor/debug/quit -H "Content-Length: 0"
```

→ `Application.Quit()` 优雅退出（日志正常落盘）。验证：PowerShell
`Get-Process Territory -ErrorAction SilentlyContinue` 应为空。
⚠ 漏掉 `Content-Length` 头的表现是"请求挂起"，不是退出失败。

---

## 5. 最小可用 walkthrough（照抄即可）

```bash
# ① 启动（PowerShell 或 cmd 启动器）
cmd //c start steam://rungameid/1455910

# ② 等 mod 就绪（循环直到返回 JSON）
until curl -s http://localhost:8765/api/editor/state | grep -q inSave; do sleep 3; done

# ③ 进档（dir=存档文件夹名；按需替换为最新一个）
curl -s -X POST http://localhost:8765/api/editor/debug/load \
     -H "Content-Type: application/json" \
     -d '{"dir":"<最新存档文件夹名>"}'

# ④ 轮询直到 inSave:true 且 saveLoads 增加
curl -s http://localhost:8765/api/editor/state

# ⑤ 扫描 + 业务操作
curl -s -X POST http://localhost:8765/api/editor/scan -H "Content-Length: 0"
# ...destroy/batch、fields/{h}、animal/spawn 等

# ⑥ 读日志（按需）
tail -n 50 "/c/Program Files (x86)/Steam/steamapps/common/Territory/BepInEx/LogOutput.log"

# ⑦ 退出
curl -s -X POST http://localhost:8765/api/editor/debug/quit -H "Content-Length: 0"
```

---

## 6. 排障速查

| 症状 | 处置 |
|---|---|
| state 一直连不上（>60s） | 游戏没起来或闪退：查 `Territory` 进程；看 `BepInEx\ErrorLog.log`；确认 mod DLL 在 `C:\TerritoryModTest\` 且游戏经 steam:// 启动 |
| 请求 000 / 408 超时 | Unity 主线程没在跑：正在读档/重载场景就等；旧版 DLL 失焦会饿死主线程（新版已修：runInBackground + 失焦放开帧预算）——先升级 mod 再排查 |
| 400 Invalid Hostname | 用 `localhost`，别用 `127.0.0.1` |
| 411 / 请求挂起 | POST 加 `-H "Content-Length: 0"` 或 `-d '{}'` |
| load `result:true` 但永远 `inSave:false` | `LogOutput.log` 搜 `[UI读档]` / `存档加载成功`；确认 `dir` 是存档根目录下的**子文件夹名**而非路径；确认没有走 ArchiveManager 直调的老路 |
| 游戏闪退 | `BepInEx\ErrorLog.log`；最常见根因是 mod 侧在非主线程调 Unity API |

---

## 7. 实现位置（要改机制时看这里）

- **端点注册**：`Web/Handlers/EntityHandlers.cs`（`debug/load`、`debug/quit`、`state`、`scan` 等）。
- **读档机制**：`SaveLoadPatches.cs` —— 核心 =
  `UI.StartGame(folder)`（反射，兼容 1/2 参）+ `il2cpp_field_static_set_value` 写
  `MainScene.auto_load_archive_folder_name`（字段指针来自 `NativeFieldInfoPtr_auto_load_archive_folder_name`，
  字符串经 `ManagedStringToIl2Cpp`）；另有 `UI.ExitGame` / `UI.Start` 两个 Harmony 前缀
  （标题画面跳过 OnGameExit 空引用、UI.Start 阶段延迟 3s 补调 StartGame）。
- **参考原型**：`C:\AI\mod\GameMCP\Plugin\`（`AutoLoadSavePatch.cs` / `Handlers/SaveHandler.cs`）。
  注意 GameMCP 含"启动自动进最新档"行为，ChestEditor 版**刻意不移植**。

## 8. 实测基线（2026-09-11，`aa04184`）

Steam 启动 → `debug/load` → `inSave:true, saveLoads:1` → ScanAll **6463** 实体
（失焦全速：分片均 1777ms）→ `debug/quit` → 进程优雅退出。**4/4 通过**。

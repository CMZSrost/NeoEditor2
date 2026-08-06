# 用 Ruffle 运行游戏

NeoEditor 支持通过 **Ruffle**（第三方 Flash/AIR 模拟器）以独立进程方式运行游戏的 SWF 文件（`NEOScavenger.swf`），并在运行期间捕获 Ruffle 的完整日志，方便排查兼容性与脚本报错。

> 该功能采用**第三方扩展模式**：编辑器不捆绑、不自动下载 Ruffle，需要你自行安装；只有编辑器检测到 Ruffle 后，「用 Ruffle 启动」按钮才会出现。

---

## 1. 安装 Ruffle

1. 打开 Ruffle 的 GitHub Releases 页面：<https://github.com/ruffle-rs/ruffle/releases>
2. 下载最新的 Windows 64 位包，例如 `ruffle-0.4.1-windows-x86_64.zip`
3. 解压到任意目录（例如 `D:\Tools\ruffle`），解压后目录里应有 `ruffle.exe`

---

## 2. 让编辑器识别 Ruffle（二选一）

### 方式 A：设置环境变量 `RUFFLE_PATH`（推荐）

1. 按 `Win + R`，输入 `sysdm.cpl` 回车，打开「系统属性」
2. 「高级」选项卡 → 「环境变量…」
3. 在「用户变量」中点击「新建」：
   - 变量名：`RUFFLE_PATH`
   - 变量值：`ruffle.exe` 的**完整路径**，例如 `D:\Tools\ruffle\ruffle.exe`
4. 确定保存

### 方式 B：把 Ruffle 加入 PATH

把 `ruffle.exe` 所在目录（例如 `D:\Tools\ruffle`）添加到 PATH 环境变量，或直接把 `ruffle.exe` 复制到 PATH 中已有的目录。

检测优先级：`RUFFLE_PATH` 环境变量 → PATH 中的 `ruffle.exe`。

> ⚠️ 设置环境变量后需**重启 NeoEditor**（最稳妥），或之后切换一次编辑/浏览模式，「用 Ruffle 启动」按钮才会出现。检测本身是实时的——点击启动时也会重新读取环境变量。

---

## 3. 启动游戏

1. 打开任一 Mod 的编辑视图（合并视图）
2. 工具栏右侧会出现 **▶ 用 Ruffle 启动** 按钮
3. 点击按钮，Ruffle 窗口即开始运行游戏 SWF（以 AIR 模拟方式启动）
4. **再次点击按钮 = 停止** Ruffle
5. 游戏关闭（或手动停止）后，编辑器会提示：
   `Ruffle 已退出（代码 N）。日志文件：…`

要点：

- **按钮未出现** = 未检测到 Ruffle（检查第 2 步）或当前处于只读浏览模式（只读时不显示）
- 建议先**保存并导出**再启动游戏——游戏读取的是导出到游戏目录的 XML 数据
- 编辑器同一时刻只允许运行**一个** Ruffle 实例；想重新启动请先停止当前实例
- 原有「保存并启动」（启动 `NEOScavenger.exe`）不受影响，两种启动方式可并存

---

## 4. 日志

Ruffle 运行期间的日志会被捕获到编辑器工作目录下的 `logs` 文件夹（与 `modeditor-*.log` 同目录）：

| 文件 | 内容 |
|------|------|
| `logs/ruffle-<时间戳>.log` | 每次运行的实时日志（stdout/stderr 捕获） |
| `logs/ruffle-cache/log/ruffle.log` | Ruffle 官方日志文件（内部模块日志，更完整，可作为兜底） |

排查问题（黑屏、加载失败、脚本报错）时，把这两个日志文件一起提供给开发者或 Ruffle 社区，能大幅加快定位。

默认日志级别为 `warn,ruffle=info,avm_trace=info`。如需更详细的日志，可自行设置环境变量 `RUST_LOG`（例如 `RUST_LOG=ruffle=debug`）后重启编辑器。

---

## 5. 常见问题

| 问题 | 处理 |
|------|------|
| 按钮不显示 | 检查 `RUFFLE_PATH` 是否设置正确并已重启编辑器；只读浏览模式下按钮隐藏 |
| 点击后提示「Ruffle 启动失败」 | 查看 `logs/ruffle-*.log` 是否有内容；确认游戏根目录存在 `NEOScavenger.swf` |
| Ruffle 窗口黑屏或游戏无法加载 | 属于 Ruffle 对 AIR 目标的兼容性问题，先看日志中的报错；可尝试升级到最新版 Ruffle |
| 游戏读不到我导出的数据 | 先执行「保存并导出」，确认 XML 已写入游戏目录的 `data` 文件夹 |
| 存档在哪 | Ruffle 的存档（SharedObjects）在 `%LOCALAPPDATA%\ruffle\SharedObjects`，与原版游戏存档**分开**，不会污染原存档 |
| 提示已在运行 | 单实例限制：先停止当前 Ruffle 实例再启动 |

---

## 6. 关闭功能

删除 `RUFFLE_PATH` 环境变量（或移走 `ruffle.exe`），重启编辑器后「用 Ruffle 启动」按钮即不再显示，编辑器恢复原样。

> 实现细节与开发计划见 `Docs/40-ruffle-game-runner-plan.md`。

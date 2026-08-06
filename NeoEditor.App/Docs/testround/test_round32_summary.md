# 架构测试第32轮 — Docs/41 保存工作流收敛 + 新手引导（648/648）

> 日期：2026-08-03 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round31_summary.md](test_round31_summary.md)（Ruffle 游戏运行器 P1，635/635）
> 本轮目标：**保存语义重构 + 非侵入式新手引导**——编辑/增删自动落 DB（无感缓存），黄/绿高亮 = "已缓存、未导出"，唯一显式保存 = Save & Export（Ctrl+Shift+S）；空状态三步卡片；一次性提示；字段 `?` 图标
> 计划：[Docs/41-save-workflow-onboarding-plan.md](../41-save-workflow-onboarding-plan.md)（v2.2 ✅ 已实施）
> 本清单由**人工逐项验收**，请在「结果」列标记 ✅ / ❌ / ⚠️，并记录异常日志。

---

## 0. 准备

- [ ] 从仓库根执行 `dotnet build NeoEditor.sln` → 预期 0 错误。
- [ ] 确认单测：`dotnet test NeoEditor.sln` → 预期 **648/648**。
- [ ] 从输出目录启动：`cd NeoEditor.App/bin/Debug/net10.0 && ./NeoEditor.exe`（须 CWD=输出目录，否则找不到 appsettings.json）。
- [ ] Settings 已配置 GameRootDir；至少导入一个 mod（或新建），打开后底部出现数据表。

> 提示：自动保存日志为 `[AutoSave] persisted N entities to DB`（控制台/stdout 可见），用于确认触发时机。

---

## 1. 自动保存（P1.1，事件驱动 + 防抖）

**前置**：已打开一个 mod 的数据表（非空，任选一行）。

- [ ] **1.1 KV 编辑自动落库**：底部表选中一行 → Left KV 改一个字段 → Enter/失焦：
  - 该行立即出现**淡黄高亮**（`255,255,220`）；
  - 约 1 秒后日志出现一次 `[AutoSave] persisted N entities`；
  - **无 toast、无 "Saving…" 闪烁**（无感）；
  - **高亮保留**（不清除）。
- [ ] **1.2 新建实体自动落库**：工具栏 `+` Add Row → 添加成功：
  - 新行**淡绿高亮**（`220,255,220`）；
  - 自动落库（日志）。
- [ ] **1.3 删除自动落库**：选中一行 Delete → 行消失，自动落库；重启应用后该行**不复活**。
- [ ] **1.4 防抖合并**：在 KV 里快速连续改 3 个字段（1 秒内）→ 日志只出现**一次** `[AutoSave]`（不是 3 次）。
- [ ] **1.5 Undo 同步 DB**：改字段 → 等自动保存 → 点 Undo → 约 1 秒后再次自动保存；重启应用验证值 = undo 后的值（DB 已同步）。
- [ ] **1.6 XML 编辑自动落库**：Center XML Tab 改内容 → Apply → 行黄高亮 + 自动落库。

## 2. 高亮语义 = "已缓存未导出"（P1.2/P1.3）

**前置**：完成 §1.1 的编辑（行是黄色）。

- [ ] **2.1 自动保存后高亮保留**：等待自动保存完成（日志出现）→ 黄高亮**仍在**。
- [ ] **2.2 Ctrl+S 不清高亮**：按 Ctrl+S → 无高亮变化；状态正常。
- [ ] **2.3 导出预览取消 = 全回滚**：点 Save & Export → diff 预览弹出 → **取消**：
  - 高亮保留；
  - 游戏 XML 文件**未改动**（可对比文件内容或看 diff 预览内容）；
  - DB 未提交本轮（R26 事务语义）。
- [ ] **2.4 导出确认后清除**：再次 Save & Export → 预览 → **确认**：
  - 全部黄/绿高亮**消失**；
  - 首次出现 toast「已写入游戏 XML ✅ 可点 ▶ 直接进游戏验证」（见 §6.1）；
  - 游戏 XML 文件已更新。
- [ ] **2.5 新建行导出后绿变正常**：§1.2 的新建行在导出后绿色消失（成为普通行）。

## 3. 按钮与快捷键（P1.4）

- [ ] **3.1 工具栏**：无「Quick Save」按钮；只有「Save & Export」按钮（悬停提示含 Ctrl+Shift+S）。
- [ ] **3.2 自动保存后按钮仍可用**：编辑 → 等自动保存完成 → Save & Export 按钮**未变灰**（可点击）。
- [ ] **3.3 Ctrl+S**：仍可用（保存当前 tab 语义，无报错、无高亮清除）。
- [ ] **3.4 Ctrl+Shift+S**：直接打开 Save & Export 的 diff 预览（与点击按钮等效）；取消/确认均正常。
- [ ] **3.5 无编辑时 Ctrl+Shift+S**：预览显示无变化/空 diff（不崩溃，可取消）。

## 4. WAL 与崩溃恢复（P1.1 抑制 + R09 回归）

- [ ] **4.1 防抖窗口内强杀**：编辑字段后**立即**（1 秒内）强杀进程（任务管理器结束）：
  - 重启 → 该编辑**恢复**（tab 带 `*` 号 / 行高亮）；
  - **自动保存未误触发**（恢复的编辑不会立即被落库——可用日志确认无 `[AutoSave]` 紧跟恢复）。
- [ ] **4.2 自动保存后强杀**：编辑 → 等 `[AutoSave]` 日志出现 → 再强杀 → 重启 → 编辑仍在（来自 DB），无 WAL 重放（`*` 号不出现）。
- [ ] **4.3 ⚠ 徽章**：编辑后（防抖窗口内）Sidebar Mod 面板/HomePage 短暂出现 ⚠；自动保存完成后消失。

## 5. 空状态三步卡片（P2）

**前置**：新建一个空 mod（无任何实体）并打开。

- [ ] **5.1 三步卡片可见**：空表中央显示三步卡片：
  - ① 添加第一个实体（含 + 按钮，可点击打开 AddRow 对话框）
  - ② 在左侧面板编辑字段
  - ③ 点 Save & Export 写入游戏
  - 底部说明：自动保存/黄=修改/绿=创建/灰=覆盖/可撤销。
- [ ] **5.2 不再显示**：点「不再显示」→ 横幅消失；重启应用 → **仍隐藏**。
- [ ] **5.3 重置恢复**：Settings 底部「重置新手提示」→ 回到空 mod → 横幅**再次出现**。
- [ ] **5.4 有实体后隐藏**：添加第一个实体后横幅自动消失（无论是否关闭过）。

## 6. 一次性提示（P3）

- [ ] **6.1 first-export**：§2.4 中确认导出后 toast 出现；**再次**导出 → 不再出现（仅一次）。
- [ ] **6.2 first-game-entity-edit**：在包含 Game 数据的 Merge 视图中，尝试**直接编辑某行游戏数据**（cell 编辑）：
  - 出现只读提示；
  - **首次**额外出现引导「可用工具栏 Copy Row 复制到你的 Mod 后再编辑」；
  - 再次尝试 → 只读提示仍在、引导不再出现。
- [ ] **6.3 重置后再次触发**：Settings「重置新手提示」→ 再次导出 / 再次编辑 Game 数据 → 提示重新出现。
- [ ] **6.4 提示不影响操作**：提示为 toast，不阻塞任何操作（无弹窗拦截）。

## 7. 字段级文档（P4）

- [ ] **7.1 KV `?` 图标**：Left KV 面板中，有描述说明的字段名左侧显示灰色 `?`；悬停 300ms 后显示字段含义 tooltip；无描述的字段**不显示** `?`。
- [ ] **7.2 AddRowDialog 说明**：Add Row 对话框「目标 XML」下拉框下方有一行灰色小字（实体按 XML 文件分组、游戏按文件名叠加）。
- [ ] **7.3 空状态卡片步骤②可呼应**：卡片步骤② 提及悬停字段名——按 7.1 实际验证成立。

## 8. 回归

- [ ] **8.1 Merge 视图三色**：打开含 Game + mod 的 profile → 被覆盖的行灰色、自己 mod 新建绿、自己 mod 修改黄（语义正确）。
- [ ] **8.2 切换 mod**：编辑 → 自动保存 → 切到另一个 mod → 切回 → 编辑还在、高亮重置（会话级）。
- [ ] **8.3 CSV 导入**：Mod Database → Import CSV（若有现成 CSV）→ 导入成功后自动落库（日志 `[AutoSave]`），高亮按导入差异显示。
- [ ] **8.4 只读浏览模式**：Data Browser 打开数据（无保存按钮）→ 界面不出现自动保存报错；若有编辑入口，行为与 §1 一致。
- [ ] **8.5 全量单测**：`dotnet test NeoEditor.sln` 收尾再跑一遍 → **648/648**。

---

## 9. Docs/41 增补：字段级 diff / 只读保护 / Debug Dock / 列头 / Welcome（2026-08-04）

### 9.1 字段级 diff（Value Editor）
- [ ] **9.1.1** KV 中修改某字段 → 字段名旁出现**黄色 ●** 标记（"已修改，尚未导出" tooltip）
- [ ] **9.1.2** 自动保存完成后 ● 仍在（不清除）；Save & Export 确认后 ● 消失
- [ ] **9.1.3** 旧黄横幅（"This entity has unsaved changes. Press Ctrl+S"）**不再出现**
- [ ] **9.1.4** XML 编辑（Center XML Tab 改字段 Apply）→ 对应字段 ● 出现

### 9.2 只读保护
- [ ] **9.2.1** KV 中主键（id/nID）与元数据（EntityId/ModId/FilePath，若显示）为**只读文本**，不可编辑
- [ ] **9.2.2** XML Tab 修改 `<column name="id">` 或 EntityId 行 → Apply 后**不产生变更**（diff 无、不标脏）

### 9.3 XML Diff 视图
- [ ] **9.3.1** XML Tab 顶部有 **Diff 切换按钮**；点击后进入左右对比视图（旧=会话开始快照，新=当前）
- [ ] **9.3.2** 修改 XML 后切到 Diff → 修改行在 DiffPreviewTrack 高亮；切回编辑模式内容不变
- [ ] **9.3.3** 未修改时 Diff 视图显示无差异

### 9.4 Debug Tool Dock（仅 DEBUG 构建）
- [ ] **9.4.1** 左侧出现 **Debug: Command Log** 面板：显示 WAL `command_log` 条目（`[Id] mod:X seq=N CommandType`），Refresh 按钮可用
- [ ] **9.4.2** 编辑一个实体后 Refresh → 出现对应 EditCell/BatchEdit 命令行
- [ ] **9.4.3** 左侧出现 **Debug: Session Dirty** 面板：编辑后实时显示 `dirty:` 与 `edited:` 行；自动保存后 dirty 行消失、edited 行保留；导出后 edited 清空

### 9.5 DataTable 列头
- [ ] **9.5.1** 列头显示**字段说明**（如物品名称/重量等语义文本，非 strName 技术名）；过长截断为省略号
- [ ] **9.5.2** 悬停列头 → tooltip 显示完整说明
- [ ] **9.5.3** 枚举列且选项 ≤6 → tooltip 末尾含"可选值: …"；枚举选项多 → 无值域

### 9.6 Welcome 页
- [ ] **9.6.1** 打开 session 的欢迎页 → 快捷键列表为新语义（Ctrl+Shift+S 导出、Ctrl+S 当前 tab、Ctrl+Z/Ctrl+Shift+Z/Ctrl+Y、Ctrl+E），不再是旧"Ctrl+S — Save session"
- [ ] **9.6.2** 设置 Language=zh 后重启 → 欢迎页标题/加载文案/快捷键全中文

---

## 10. 2026-08-04 增补：字段级 diff / AI Chat / MCP 评审 / 验收修复

### 10.1 DataTable 字段级 diff + 主键锚点
- [ ] **10.1.1** KV 改 `strName` → 该行 **strName 单元格**变黄 + **主键单元格**变黄，**其他单元格不变**（不再是整行黄）
- [ ] **10.1.2** 再改 `fWeight` → 只有这两个单元格 + 主键亮
- [ ] **10.1.3** 新建实体 → 整行绿（行级保留）；Merge 覆盖行整行灰
- [ ] **10.1.4** Save & Export 后单元格黄 + 主键黄全部清除
- [ ] **10.1.5** KV 编辑后 DataTable **即时刷新**（无需滚动/切换）

### 10.2 AI Chat
- [ ] **10.2.1** AI 回复渲染为 **Markdown**（标题/列表/代码块样式正确），不再是纯文本
- [ ] **10.2.2** 白色主题下 MD 内容白底深字可读；深色主题保持 Dark+ 风格
- [ ] **10.2.3** 气泡头部 📋 按钮 → 点击复制该条生成内容到剪贴板

### 10.3 MCP 新工具（stdio 客户端验证）
- [ ] **10.3.1** `tools/list` 返回 **19** 个工具（新增 BatchEditEntity / FindReferencingEntities / DiscardChanges）
- [ ] **10.3.2** `BatchEditEntity`：一次传多个字段 → 全部生效，`GetDiff` 可核对，Save 落库
- [ ] **10.3.3** `FindReferencingEntities`：对一个被引用的实体（如某个 ImageAsset）→ 返回引用它的实体/属性列表
- [ ] **10.3.4** `SearchAllTypes` 空 query + `filtersJson`（如 `[{"field":"Weight","op":">=","value":"1.5"}]`）→ 只按过滤器返回
- [ ] **10.3.5** `DiscardChanges`：编辑后调用 → 该实体移出 dirty 集，后续 Save 不写它

### 10.4 Debug 面板语义
- [ ] **10.4.1** 编辑后看 **Command Log**：防抖窗口（~1s）内有 BatchEdit 命令；自动保存后显示"WAL is empty — auto-save already persisted & cleared it"（正常提示，非 bug）
- [ ] **10.4.2** **Session Dirty**：编辑后 EditStore 有 `edited: <eid> :: strName`（**精确列名**；`*` 仅出现在 WAL 恢复的 Add/Delete 命令与升级前旧数据）；自动保存后 dirty 清空但 EditStore 保留；**pending_export mod=N: M entities** 出现
- [ ] **10.4.3** KV 只读值（entity_id/file_path）长值**换行显示**不截断

---

## 已知边界（验收时注意，非缺陷）

- 重启应用后**未导出的高亮会恢复**（`pending_export` 按列持久化；修复前写入的无列名旧标记在打开工作区时**一次性升级**为字段级——磁盘 XML diff 还原列名，失败回退整行 `*` 标记）。
- 自动保存只写 DB（缓存层）；**写入游戏 XML 的唯一路径是 Save & Export**（含 Ctrl+Shift+S 与 ▶ 启动前置的保存）。
- 外部 "N dirty" 计数为**实体数**（pending_export ∪ WAL 窗口，非 mod 数）。

## 结果汇总

| 章节 | 通过 | 失败 | 备注 |
|------|:--:|:--:|------|
| 1 自动保存 | | | |
| 2 高亮语义 | | | |
| 3 按钮/快捷键 | | | |
| 4 WAL 崩溃恢复 | | | |
| 5 空状态卡片 | | | |
| 6 一次性提示 | | | |
| 7 字段文档 | | | |
| 8 回归 | | | |
| **合计** | | | |

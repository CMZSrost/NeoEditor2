# [R09] 脏数据视觉指示：Sidebar + HomePage 提示未保存编辑

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | open-questions Q1 / 2026-06-29 用户确认 |
| **最后修订** | 2026-07-17 — 移除拦截弹窗，改为视觉指示器 |
| **创建时间** | 2026-06-29 |

**是什么**
> WAL（Write-Ahead Log）按 `("mod", modId)` 隔离每个 Mod 的编辑。切换 Mod/Profile
> 时无需拦截弹窗——每个 Mod 的未保存命令独立持久化在 `command_log` 中，切回来
> 时自动恢复（`sequence > snapshot` 的命令会被 WAL 重放）。
>
> 脏数据通过以下视觉指示器告知用户：
> - **侧边栏 Mod 面板**：标题下方显示 `"⚠ N mod(s) with unsaved edits"`（来源：
>   `ModDatabaseViewModel.DirtyModCountText`）。
> - **侧边栏 Profile 面板**：标题下方显示 `"⚠ N profile(s) with unsaved edits"`
>   （来源：`ModIndexViewModel.DirtyProfileCountText`，检查 Profile 下每个 Mod 的
>   `HasUnsavedCommandsAsync`）。
> - **HomePage 欢迎页**：Recent Mods 列表条目上显示 `⚠ unsaved` 徽章，Profiles
>   列表条目上显示 `⚠ {n} dirty` 徽章（来源：`HomePageViewModel.LoadDirtyStateAsync()`，
>   直接在现有 `RecentMods` / `Profiles` 条目上置 `HasUnsavedEdits` / `DirtyModCount`
>   属性，由 DataTemplate 绑定控制徽章可见性）。

**为什么**（修订）
> ~~单活跃 Session（[R02](R02-single-active-session.md)）重建会替换 store，未拦截会静默丢失~~
> ~~用户编辑。由 Session 统一托管脏状态，才能在唯一的重建入口处做一致拦截。~~
>
> **2026-07-17 修订**：WAL 按 Mod 隔离持久化后，上述"静默丢失"的前提不复存在。
> `command_log` 中 `sequence > workspace_snapshot.LastCommandSequence` 的命令在
> `LoadModDataContextAsync` 重放时自动恢复。拦截弹窗已移除，改为视觉指示器告知
> 用户哪些 Mod 有未保存编辑，用户随时可切回继续编辑。

**实现细节**
> - `IWorkspacePersistenceService.HasUnsavedCommandsAsync(targetType, targetId)`：
>   判断 `max(sequence) > snapshot.LastCommandSequence`。
> - `ModDatabaseViewModel.RefreshDirtyCountAsync()`：遍历所有 Mod，统计脏 Mod 数。
> - `ModIndexViewModel.RefreshDirtyCountAsync()`：遍历所有 Profile，若其任一组成
>   Mod 有脏数据，则该 Profile 标记为脏。
> - `HomePageViewModel.LoadDirtyStateAsync()`：首页加载后扫描 `RecentMods` 和
>   `Profiles` 条目，在现有条目上置 `HasUnsavedEdits` / `DirtyModCount` 属性，
>   由 DataTemplate 中的绑定控制徽章可见性。
> - `DocumentWorkspaceViewModel.Receive(OpenModGameDataDocumentMessage)` 已移除
>   拦截调用，直接执行 `LoadModDataContextAsync`。
> - 以下原有实现要点保持有效（仅与 WAL 恢复和脏标记相关，不在拦截流程中）：
>   - WAL 恢复后必须填充 `DirtyEntities` 并标记受影响的 tab
>   - `EntityEditorDocument` 构造函数必须检查 `DirtyEntities`
>   - 保存完成后必须 `DirtyEntities.Clear()`
>   - `KeyValueEditorViewModel.LoadEntity` 检查 `DirtyEntities`
>   - `ApplyChanges()` 末尾必须设 `IsCurrentEntityDirty = false`

**决策边界**
> 适用：Sidebar Mod/Profile 面板显示、HomePage 欢迎页显示、WAL 崩溃恢复后 Session
> 初始化。
> 不适用：同 Session 内多开文档之间的切换（无拦截也无视觉提示）。
> 相关：[R01](R01-state-single-owner.md) 所有者；[R11](R11-save-granularity.md) 保存粒度。

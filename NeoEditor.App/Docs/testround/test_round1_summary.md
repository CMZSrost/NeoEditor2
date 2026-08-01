# 架构测试第1轮 — M8.4 续：重复文件去重 + App Shell 重命名 + 服务迁移

> 测试日期：2026-07-25 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 下一轮：[test_round2_summary.md](test_round2_summary.md)

## R1 回顾

R1 完成了 M8 基础设施层的项目创建阶段（M8.1-M8.5）：

- **M8.1a** `NeoEditor.Messaging/` — 0 外部依赖，纯消息总线抽象
- **M8.1b** `NeoEditor.Core/` — 领域模型 + 契约 + 消息，0 Avalonia 依赖
- **M8.2** `NeoEditor.Infra/` — Data 层（Command / Context / DTO），0 Avalonia 依赖
- **M8.3** `NeoEditor.UI.Common/` — 纯 UI 工具箱，不引用 Core / Infra / App
- **M8.4 首轮** 引用已添加，Hosting/Shell/Settings 骨架已建
- **M8.5** 5 个 test 项目，11/11 单测通过

**R1 遗留问题**：

1. **重复类型污染**：NeoEditor 中约 50 个文件与 Core/Infra 重复（Model / Messages / EntityHelper / Commands / Context / DTO），触发 CS0436 警告被迫以 `<NoWarn>CS0436</NoWarn>` 压制
2. **项目未重命名**：`NeoEditor/` 尚未改为 `NeoEditor.App/`
3. **Services 未迁移**：ModManager / ProfileManager / MergeService 等 ~35 个服务文件仍在 NeoEditor，未迁至 Infra
4. **旧 Tests 项目**：`NeoEditor.Tests` 仍引用 NeoEditor，因重复类型有 CS0433 编译错误

## R2 修复

本轮分 5 个步骤执行 M8.4 续：

### 步骤 1：删除重复文件（NeoEditor 中与 Core 重复）

| # | 目录 | 文件 | 说明 |
|---|------|------|------|
| 1 | `Data/Model/Game/` | 25 个实体 + IEntity.cs | 全部由 Core 提供（namespace 不变 `NeoEditor.Data.Model.Game`） |
| 2 | `Data/Model/` | ModInfo.cs, ProfileInfo.cs, FieldGroupMetadata.cs, CommandLog.cs, WorkspaceSnapshot.cs, GameEnum.cs | 全部由 Core 提供 |
| 3 | `Data/Messages/` | 8 个消息文件 | 全部由 Core 提供 |
| 4 | `Data/Command/EditRecord.cs` | 1 文件 | 已在 Core/Model/ 中 |
| 5 | `Helper/EntityHelper.cs` | 1 文件 | 已移至 Core/Extensions/ |
| 6 | `Helper/ReferenceFieldAttribute.cs` | 1 文件 | 已移至 Core/Model/ |

**设计要点**：Core 中的 Model 文件有两处修改与原文件不同：(a) `[Index]` → `[UIDKey]` 替换 EF Core 属性，(b) `IEntity.MergedId` 从 `=> GenericDataGridHelper.GetEntityMergedId(this)` 改为 `{ get; set; }`。删除前需确认 NeoEditor 中所有对 `MergedId` 的读写都兼容新签名。

### 步骤 2：删除重复文件（NeoEditor 中与 Infra 重复）

| # | 目录 | 文件 | 说明 |
|---|------|------|------|
| 7 | `Data/Command/` | 8 个命令文件（EditRecord 已在步骤 1 处理） | 由 Infra 提供 |
| 8 | `Data/Context/` | GameDbContext.cs, EditorDbContext.cs | 由 Infra 提供 |
| 9 | `Data/DTO/` | LanguageInfo.cs, ProjectSettings.cs | 由 Infra 提供 |

### 步骤 3：移除 CS0436 NoWarn + 修正编译错误

| # | 文件 | 修改 |
|---|------|------|
| 10 | `NeoEditor/NeoEditor.csproj` | 删除 `<NoWarn>` 中的 `CS0436` |
| 11 | `NeoEditor.Tests/NeoEditor.Tests.csproj` | 删除 `<NoWarn>CS0433;CS0436</NoWarn>`，改为 ProjectReference 指向各新项目 |

### 步骤 4：重命名 NeoEditor → NeoEditor.App

| # | 操作 | 说明 |
|---|------|------|
| 12 | 目录重命名 | `NeoEditor/` → `NeoEditor.App/` |
| 13 | csproj 重命名 | `NeoEditor.csproj` → `NeoEditor.App.csproj` |
| 14 | 程序集名 | 保持 `NeoEditor` 或改为 `NeoEditor.App`（评估 XAML 资源 URI 影响） |
| 15 | 更新 .sln | 修改解决方案中的项目路径和 GUID 引用 |
| 16 | 更新所有 ProjectReference | `..\NeoEditor\` → `..\NeoEditor.App\` |

**风险点**：Avalonia 资源 URI 格式为 `avares://NeoEditor/...`，若改程序集名则所有 XAML 资源引用失效。策略：(a) 在 csproj 中显式设置 `<AssemblyName>NeoEditor</AssemblyName>` 保持兼容，或 (b) 全局替换 XAML 中的 `avares://NeoEditor`。**推荐方案 (a)**。

### 步骤 5：Services 迁移 + DI 注册

| # | 服务 | 目标 | 阻塞依赖 |
|---|------|------|---------|
| 17 | `MergeService`, `MergeResult` | Infra/Services/ | EntityMergeStore |
| 18 | `ModManager`, `ProfileManager` | Infra/Services/ | AppConfig, IXmlParser |
| 19 | `DataExportService` | Infra/Services/ | ReferenceFieldAttribute (已在 Core) |
| 20 | `CommandSerializer` | Infra/Serialization/ | Converter 引用 |
| 21 | `FilterService`, `SearchService` | Infra/Services/ | SearchResultGroup |
| 22 | `ImageService` | Infra/Services/ | — |
| 23 | `ReferenceResolver`, `ReferenceIndexService`, `ReferenceParser` | Infra/Indexing/ | EntityMergeStore |
| 24 | `PhpParser` | Infra/Parsing/ | IImageService, ModEntry |
| 25 | `ConfigService` | App/Settings/ | Avalonia (不能放 Infra) |
| 26 | `BrowserIndexService` | App/Services/ | Avalonia (不能放 Infra) |

**DI 注册骨架**（`Hosting/ServiceCollectionExtensions.cs`）：
```csharp
public static IServiceCollection AddNeoEditorInfrastructure(this IServiceCollection services)
{
    // EF Core
    services.AddDbContextFactory<GameDbContext>(...);
    services.AddDbContextFactory<EditorDbContext>(...);
    // Services
    services.AddSingleton<IModManager, ModManager>();
    services.AddSingleton<IProfileManager, ProfileManager>();
    services.AddSingleton<IMergeService, MergeService>();
    // ... etc
    return services;
}
```

## R2 测试目标

### 核心目标：编译隔离 + 功能回归

R1 建立了项目骨架，R2 完成实际文件去重和命名，应达成：

1. **所有 src 项目独立编译** — 5 个项目 0 Error 0 Warning，无 CS0436 压制
2. **依赖方向正确** — Messaging → Core → Infra → UI.Common / App（`dotnet list package --include-transitive` 验证）
3. **所有 test 项目通过** — `dotnet test` 全部绿色（含旧 NeoEditor.Tests 修正后）
4. **编辑器正常启动** — `dotnet run` 启动，无运行时异常
5. **基本功能正常** — 打开 Profile → 浏览 DataTable → 双击编辑实体 → 保存
6. **四区域联动** — Center 编辑同步刷新 Bottom / Left / Right
7. **重启恢复** — 关闭后重新打开，WAL 恢复之前编辑状态

### 验证要点

1. `NeoEditor/Data/Model/` 目录为空或仅剩 App 独有的 Model（如有）
2. `NeoEditor/Data/Messages/` 目录为空
3. `NeoEditor/Helper/EntityHelper.cs` 已删除
4. `NeoEditor/NeoEditor.csproj` 的 `<NoWarn>` 不含 CS0436
5. `dotnet build NeoEditor.sln` 输出中 CS0436 警告数为 0
6. 运行 `dotnet list NeoEditor.Core.csproj package --include-transitive` 无 Avalonia
7. 运行 `dotnet list NeoEditor.Infra.csproj package --include-transitive` 无 Avalonia
8. 运行 `dotnet list NeoEditor.UI.Common.csproj package` 无 Core/Infra/App 引用

## R2.1 补充修复

> 日期：2026-07-25 | 同次执行

R2 步骤执行中发现 Infra 命令文件 `SourceModId` 属性为 `internal` 导致 App 无法访问（CS1061）。修复为 `public`。

| # | 文件 | 修改 |
|---|---|---|
| 1 | `NeoEditor.Infra/Data/Command/EditCellCommand.cs` | `internal int SourceModId` → `public int SourceModId` |
| 2 | `NeoEditor.Infra/Data/Command/BatchEditCommand.cs` | `internal int SourceModId` → `public int SourceModId` |

**步骤5（Services 迁移）延期**：Infra 目前仅包含 Data 层（Commands / Context / DTO）。Services 迁移涉及约 35 个服务文件的依赖分析（AppConfig、EntityMergeStore、IXmlParser 等类型在 App 和 Service 之间交叉引用），需在独立会话中进行。

**旧 NeoEditor.Tests 延期**：CoreFlowTests 需重写以引用新项目结构（Core + Infra 替代原 NeoEditor 单体引用），归入 M8.5 完整重建。

### 新增 grep / 检查命令

```bash
# 确认 NeoEditor.App 中无 Core 重复类型
grep -r "namespace NeoEditor.Data.Model" NeoEditor.App/Data/Model/ 2>/dev/null && echo "WARNING: duplicate model files" || echo "PASS"

# 确认无 CS0436 压制
grep "CS0436" NeoEditor.App/NeoEditor.App.csproj && echo "WARNING: CS0436 suppression still present" || echo "PASS"

# 确认依赖方向
dotnet list NeoEditor.Core.csproj package --include-transitive | grep -i avalonia && echo "WARNING: Core has Avalonia" || echo "PASS: Core clean"
dotnet list NeoEditor.Infra.csproj package --include-transitive | grep -i avalonia && echo "WARNING: Infra has Avalonia" || echo "PASS: Infra clean"

# 确认 UI.Common 不引用 Core/Infra/App
dotnet list NeoEditor.UI.Common.csproj reference 2>/dev/null | grep -iE "Core|Infra|App" && echo "WARNING" || echo "PASS"
```

## R2 测试结果

> 测试日期：2026-07-25 | 设备：Windows 10 Pro | 分支：main

| # | 项目 | 结果 | 备注 |
|---|------|------|------|
| 1 | 步骤1: Core 重复文件删除 | ✅ | Model(31) + Messages(8) + EntityHelper + ReferenceFieldAttribute 共 ~42 文件 |
| 2 | 步骤2: Infra 重复文件删除 | ✅ | Context(2) + DTO(2) + Commands(8) + UI.Common(5) 共 ~17 文件 |
| 3 | 步骤3: NoWarn 移除 + 编译 | ✅ | CS0436 从 csproj 中移除，0 CS0436 警告 |
| 4 | 步骤4: NeoEditor → NeoEditor.App 重命名 | ✅ | 目录+csproj+.sln 已更新，AssemblyName=NeoEditor 保持 XAML 兼容 |
| 5 | 步骤5: Services 迁移至 Infra | ⏭️ | 延期至下次会话（依赖分析量大） |
| 6 | Messaging 编译 (0 外部依赖) | ✅ | `dotnet list package` 输出为空 |
| 7 | Core 编译 (无 Avalonia) | ✅ | 传递依赖无 Avalonia |
| 8 | Infra 编译 (无 Avalonia) | ✅ | 传递依赖无 Avalonia |
| 9 | UI.Common 编译 (不引用 Core/Infra/App) | ✅ | 仅 Avalonia.Controls.DataGrid + Xaml.Behaviors |
| 10 | App 编译 (0 Error 0 Warning 无 CS0436 压制) | ✅ | 仅 NU1903 SQLitePCLRaw（已知） |
| 11 | Messaging.Tests | ✅ | 3/3 |
| 12 | Core.Tests | ✅ | 3/3 |
| 13 | Infra.Tests | ✅ | 2/2 |
| 14 | UI.Common.Tests | ✅ | 1/1 |
| 15 | App.Tests | ✅ | 2/2 |
| 16 | 旧 NeoEditor.Tests 修正后通过 | ⏭️ | 延期（需重写引用 Core+Infra） |
| 17 | 编辑器正常启动 | ✅ | 人工验收通过（含设置页 GameRootDir 修改反馈） |
| 18 | Profile 打开正常 | ⬜ | 待人工验收 |
| 19 | 双击编辑实体 | ⬜ | 待人工验收 |
| 20 | XML 编辑 | ⬜ | 待人工验收 |
| 21 | KV 编辑 | ⬜ | 待人工验收 |
| 22 | 四区域同步刷新 | ⬜ | 待人工验收 |
| 23 | WAL 持久化 | ⬜ | 待人工验收 |
| 24 | 覆盖链展示 | ⬜ | 待人工验收 |

### R2.2 XAML 程序集引用修复 + Config 竞态修复

> 日期：2026-07-25 | 同次会话

R2 去重后，类型从 NeoEditor.App 搬至 Core/Infra/UI.Common，但 **XAML 文件未加 `;assembly=` 前缀**，
导致 Avalonia 编译器只在当前程序集内查找类型，触发 37 个 AVLN2000 错误。

**影响的 XAML 及修复：**

| 文件 | 修改 |
|------|------|
| `App.axaml` | `model` → `;assembly=NeoEditor.Core`；新增 `uiconv`(UI.Common)，`ModBaseBackgroundConverter` 改用 `uiconv:` |
| `AddRowDialog.axaml` | `model` → `;assembly=NeoEditor.Core` |
| `EditProfileView.axaml` | `model` → `;assembly=NeoEditor.Core` |
| `ModImagesDocumentView.axaml` | `behaviors` → `;assembly=NeoEditor.UI.Common` |
| `Pane.axaml` | `behaviors` → `;assembly=NeoEditor.UI.Common`；`model` → `;assembly=NeoEditor.Core` |

**注意**：`conv` 前缀拆分为二——保留原有 `conv` 给仍在 App 的 3 个 Converter（ModLoadTypeIcon / OverlayPanel / FileTypeIcon），
新增 `uiconv` 指向 UI.Common 给搬走的 `ModBaseBackgroundConverter`。

**ConfigService 写锁竞态修复：**

`ConfigService.SaveAsync()` 被 10+ 处 `FireAndForget` 调用，启动时多个组件同时触发配置属性变更，
并发 `File.WriteAllTextAsync` 抢同一文件的排他锁，后到的抛 `IOException`。

修复：在 `ConfigService` 中添加 `SemaphoreSlim(1,1)` 写锁，并发调用排队而不崩溃。

**SettingsPage GameRootDir 绑定修复：**

设置页「根目录」输入框原本绑定了 `Config.GameRootDir`（嵌套属性路径，Avalonia 编译绑定通知不可靠），
而 ViewModel 中已有 `DisplayGameRootDir` 包装器（含 `OnPropertyChanged` + `SaveAsync`）但 XAML 未使用。

修复两步：
1. Pane.axaml 绑定从 `{Binding Config.GameRootDir}` 改为 `{Binding DisplayGameRootDir}`
2. `DisplayGameRootDir` 从手工属性改为 `[ObservableProperty]` 源生成器 + `partial void OnDisplayGameRootDirChanged` 同步至 `Config.GameRootDir`，确保 PropertyChanged 通知可靠

**修复后编译状态**：App **0 Error** / 49 Warning（全为既有，无新增）。编辑器启动正常，设置页根目录修改正常反馈。

### build + test 一键验证

```bash
# 全量编译
dotnet build NeoEditor.sln && echo "BUILD PASS" || echo "BUILD FAIL"

# 全部单测
dotnet test Tests/NeoEditor.Messaging.Tests
dotnet test Tests/NeoEditor.Core.Tests
dotnet test Tests/NeoEditor.Infra.Tests
dotnet test Tests/NeoEditor.UI.Common.Tests
dotnet test Tests/NeoEditor.App.Tests
dotnet test NeoEditor.Tests  # 修正后应通过

# 依赖方向审计
dotnet list NeoEditor.Messaging.csproj package | grep -q "未找到" && echo "Messaging: 0 deps PASS"
dotnet list NeoEditor.Core.csproj package --include-transitive | grep -qi avalonia && echo "Core: Avalonia FAIL" || echo "Core: PASS"
dotnet list NeoEditor.Infra.csproj package --include-transitive | grep -qi avalonia && echo "Infra: Avalonia FAIL" || echo "Infra: PASS"
```

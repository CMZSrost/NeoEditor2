# 架构测试第3轮 — M8 Services 迁移验收

> 日期：2026-07-25 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round2_summary.md](test_round2_summary.md)
> 后续：[test_round4_summary.md](test_round4_summary.md) (M8 收尾 — Bug 修复)

## 本轮变更

| 变更 | 内容 |
|------|------|
| Services → Infra | 19 个核心服务/数据类迁至 `NeoEditor.Infra/Services/` |
| Helper → Infra | 14 个纯数据/解析/接口类型迁至 `NeoEditor.Infra/Helper/` |
| Data → Infra | `Constants.cs` 迁至 `NeoEditor.Infra/Data/` |
| 留存 App/Services | 17 个 Avalonia/UI 依赖服务 |
| 留存 App/Helper | ~20 个 UI 工具类 (GenericDataGridHelper, RefNode, Converters, Visualizers 等) |
| 重建 | `RefNode.cs`（visualizer 调用模式重构）|
| 修复 | `ReferenceResolver.cs`（M8 接口方法补齐 + DI 构造函数）|
| XAML | 4 个 axaml `xmlns:helper` 添加 `;assembly=NeoEditor.Infra` |

## 前置条件

- [x] `bash build.sh` 编译通过（10/10 项目，0 Error）
- [x] `dotnet test` 11/11 通过

---

## 验收清单

### 1. 编辑器正常启动

| 操作 | 预期 |
|------|------|
| `dotnet run --project NeoEditor.App` | 编辑器窗口正常打开，无崩溃 |

- [x] 通过
- 问题记录：无

---

### 2. Profile 打开正常

| 操作 | 预期 |
|------|------|
| 左侧栏切换到 **Profiles** tab | Profile 列表正常显示 |
| 点击 Profile → 展开 | Mod 列表完整，加载顺序正确 |

- [x] 通过
- 问题记录：无

---

### 3. 双击编辑实体（RefNode 重建验证）

| 操作 | 预期 |
|------|------|
| Mod Database → 展开 Mod → 点击 XML | DataTable 正常显示 |
| 双击 DataTable 行 | 编辑视图弹开，Visual/XML/KV 切换可用 |

- [x] 通过
- 问题记录：无

---

### 4. XML ↔ KV 编辑同步

| 操作 | 预期 |
|------|------|
| XML 编辑器修改字段 → 切换 KV | 值已更新 |
| KV 编辑器修改字段 → 切换回 XML | 值已更新 |

- [x] 通过
- 问题记录：无

---

### 5. 脏数据视觉指示（R09） — ❌ 回归

| 操作 | 预期 | 实际 |
|------|------|------|
| KV 编辑器中修改字段 | DataTable 行背景变黄 | 无黄色背景 |
| 同上 | Value Editor 顶部出现 Alert 提示 | 无 Alert 提示 |

- [ ] 通过
- 问题记录：**DataTable 修改行黄色背景消失，编辑器顶端未保存 Alert 消失。** 根因待查，怀疑与 `EditTrackingStore` / `IWorkspaceSession.DirtyEntities` 迁至 Infra 后 DI 链路未完全适配有关。

---

### 6. 搜索功能（SearchService / FilterService 迁 Infra）

| 操作 | 预期 |
|------|------|
| DataTable 搜索框输入关键词 | 过滤/高亮匹配行 |

- [x] 通过
- 问题记录：无

---

### 7. 覆盖链展示（MergeService / EntityMergeStore 迁 Infra）

| 操作 | 预期 |
|------|------|
| 多 Mod Profile 合并视图 | 覆盖层级排列、来源标注 |

- [x] 通过
- 问题记录：无

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| 1 | 编辑器正常启动 | ✅ |
| 2 | Profile 打开正常 | ✅ |
| 3 | 双击编辑实体 | ✅ |
| 4 | XML ↔ KV 编辑同步 | ✅ |
| 5 | 脏数据视觉指示 | ❌ |
| 6 | 搜索功能 | ✅ |
| 7 | 覆盖链展示 | ✅ |

**通过 / 总计**：6 / 7（1 个回归 Bug）

---

## 下一轮预告 (test_round4)

M8 收尾 — Bug 修复：脏数据视觉指示恢复（DataTable 黄色行背景 + Value Editor Alert）。

# 架构测试第2轮 — M8 功能回归验收

> 日期：2026-07-25 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round1_summary.md](test_round1_summary.md)
> 后续：[test_round3_summary.md](test_round3_summary.md) (M8 收尾 — Services 迁移)

## 本轮目标

R1/R2 完成了 M8 基础设施层（5 src + 5 test 项目、文件去重、程序集重命名）。
**本轮只做一件事**：人工验证编辑器的 **7 项核心功能** 在架构变更后未回归。

> 不改代码，只验收。发现 bug 就记下来，下轮修。

---

## 前置条件

- [x] `bash build.sh` 编译通过（10/10 项目）
- [x] `dotnet run --project NeoEditor.App` 编辑器正常启动
- [x] 游戏根目录已配置（设置页 `D:\software\Steam\steamapps\common\Neo Scavenger`）

---

## 验收清单

### 1. Profile 打开正常

| 操作 | 预期 |
|------|------|
| 左侧栏切换到 **Profiles** tab | 显示 Profile 列表 |
| 点击一个 Profile → 展开 | 显示该 Profile 下的 Mod 列表、每个 Mod 的名称/路径 |
| Mod 列表展示完整 | 加载顺序正确，覆盖链层级可见 |

- [ ] 通过
- 问题记录：

---

### 2. 双击编辑实体

| 操作 | 预期 |
|------|------|
| 左侧栏切换到 **Mod Database** tab | 显示 Mod 列表 |
| 展开一个已加载的 Mod → 点击 XML 文件 | 中央区域打开 DataTable |
| 双击 DataTable 中任意一行 | 弹出编辑视图，左上角出现 XML / KV 切换按钮 |

- [ ] 通过
- 问题记录：

---

### 3. XML 编辑 ↔ KV 编辑同步

| 操作 | 预期 |
|------|------|
| 在 XML 编辑器中修改一个字段值 | 切换到 KV 模式，值已更新 |
| 在 KV 编辑器中修改一个字段 | 切换回 XML 模式，值已更新 |

- [ ] 通过
- 问题记录：

---

### 4. KV 编辑 → 保存 → DataTable 刷新

| 操作 | 预期 |
|------|------|
| 在 KV 编辑器中修改一个字段 | 点击 Save |
| 回到 DataTable | 对应行的值已更新 |

- [ ] 通过
- 问题记录：

---

### 5. 四区域同步刷新

| 操作 | 预期 |
|------|------|
| Center 编辑一个实体 → 保存 | Bottom DataTable 对应行刷新 |
| 同上 | Left 侧边栏的引用索引更新（如有引用关系） |
| 同上 | Right 属性面板更新（如有） |

- [ ] 通过
- 问题记录：

---

### 6. WAL 持久化恢复

| 操作 | 预期 |
|------|------|
| 编辑实体 → 不要手动保存 → 关闭编辑器 | 重启编辑器 |
| 重新打开同一个 Profile / Mod / XML | 之前的编辑状态恢复（未保存的更改仍在） |

- [ ] 通过
- 问题记录：

---

### 7. 覆盖链展示

| 操作 | 预期 |
|------|------|
| 打开一个包含多个 Mod 的 Profile 合并视图 | 同命名空间的多个 Mod 按加载顺序排列 |
| 高亮被覆盖的字段 / 实体 | 来源 Mod 清晰标注 |

- [ ] 通过
- 问题记录：

---

## 验证命令

```bash
# 启动前确认编译清洁
bash build.sh

# 启动编辑器
dotnet run --project NeoEditor.App
```

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| 1 | Profile 打开正常 | ⬜ |
| 2 | 双击编辑实体 | ⬜ |
| 3 | XML ↔ KV 编辑同步 | ⬜ |
| 4 | KV 编辑 → 保存 → DataTable 刷新 | ⬜ |
| 5 | 四区域同步刷新 | ⬜ |
| 6 | WAL 持久化恢复 | ⬜ |
| 7 | 覆盖链展示 | ⬜ |

**通过 / 总计**：0 / 7

---

## 下一轮预告 (test_round3)

M8 收尾：B1 Services 迁移至 Infra（ModManager / MergeService / ReferenceResolver 等 ~35 文件）。

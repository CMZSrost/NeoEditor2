# [R02] 单活跃 Session

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | 决策 D2 / 2026-06-29 用户确认 |
| **创建时间** | 2026-06-29 |

**是什么**
> 同一时刻只有一个活跃 `IWorkspaceSession`，对应一个 Profile/Mod 的 store+索引。
> 切换 Profile = 关闭旧 Session、重建新 Session。多开的 Center 文档全部共享这一个 Session。

**为什么**
> 允许多 Profile 的 store 并存会让索引、覆盖关系、Mod 归属互相缠绕，复杂度陡增。
> 单活跃语义与文档 23「Scope 唯一性」一致，也最简单、最不易出 bug。

**决策边界**
> 适用：Profile/Mod 切换、Session 生命周期。
> 不适用：Center 可多开多个实体文档——它们共享同一 Session，不是多 Session。
> 相关：[R01](R01-state-single-owner.md) 定义所有者；切 Session 时未保存编辑的处置见 open-questions Q1。

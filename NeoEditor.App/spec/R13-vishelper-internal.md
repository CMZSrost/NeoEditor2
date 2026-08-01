# [R13] VisHelper 为 internal static 单一辅助类

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | open-questions Q5 / 2026-06-29 用户确认 |
| **创建时间** | 2026-06-29 |

**是什么**
> 拆分 `EntityVisualizers.cs` 时，`VisHelper` 从 `file static` 提为独立文件、可见性放宽到
> `internal static`（同程序集可见，不对外暴露），保留为单一辅助类。25 个 `*EntityVisualizer`
> 各拆一文件。

**为什么**
> internal 满足同程序集所有 Visualizer 共用，又不污染公共 API。符合 [R14](R14-folder-convention-layering.md)
> 单程序集的落地方式，改动最小。

**决策边界**
> 适用：M2 拆分 EntityVisualizers 阶段。
> 不适用：未来若分多程序集需跨界引用，再另行决策放宽到 public（新建规则）。
> 相关：[R04](R04-view-assembles-only.md) View 只组装；[N03](N03-no-logic-in-view.md)。

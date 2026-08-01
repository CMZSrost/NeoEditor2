# [R04] View 只组装控件

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | 决策 D4 / 2026-06-29 代码审查 |
| **创建时间** | 2026-06-29 |

**是什么**
> Visualizer（View 层）只负责组装 Avalonia 控件，并通过 `RefNode`/`NavLeaf` 工厂
> **声明**「这里有个指向 X 的引用」。点击后的解析走注入的 `IReferenceResolver`，
> 导航/Peek 走注入的 `INavigationRouter`。

**为什么**
> `EntityVisualizers.cs`（8761 行）把建控件 + 66 处导航逻辑 + 引用查询混在一起，
> 既难拆分又无法测试，且把 View 绑死在静态解析器上。职责分离后 View 可纯组装、
> 可拆文件，导航逻辑集中可控。

**决策边界**
> 适用：所有 Visualizer 与 UI 控件构建代码。
> 不适用：纯展示型布局（无引用、无导航）无需引入工厂。
> 相关：[R03](R03-reference-resolver-injected.md) 解析注入；[N03](N03-no-logic-in-view.md) 禁止 View 写业务逻辑。

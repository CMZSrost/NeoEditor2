# 32 — Agent 编排增强计划（系统提示词 + MCP + RAG + Streaming）

> v1.1 · 2026-07-31
> 上承: [30-post-m12-development-plan.md](30-post-m12-development-plan.md) §六/§九
> 目标: ChatService 升级为完整的 Agent 编排引擎

---

## 〇、现状

| 组件 | 状态 | 说明 |
|------|:--:|------|
| ChatService | ✅ 基础 function-calling loop + Streaming | 手动 while 循环，max 10 次迭代，支持 streaming typewriter 效果 |
| ChatHistoryManager | ✅ 100 条消息窗口 | 纯计数截断，无 token 估算 |
| MCP 工具集成 | ✅ IMcpToolProvider | ChatService 自动发现并注入 12 个 EditorTools |
| 系统提示词 | ✅ 已完成 (A1 2026-07-30) | SystemPromptBuilder 自动生成实体 Schema + UI 可编辑面板 |
| RAG 服务 | ✅ 已完成 (A2 2026-07-30) | RagService (OpenAI Embedding + 内存向量库 + 自动上下文注入) + EntitySummaryBuilder |
| MCP 工具 | ✅ 增强完成 (A3 2026-07-30) | 3 新工具 (GetEntitySchema/SearchAllTypes/GetModInfo) + 描述优化 + 结果截断 |
| Streaming | ✅ 已完成 (A4 2026-07-31) | CompleteChatStreamingAsync + 逐 token typewriter 效果 + 工具调用状态指示 |
| Token 统计 | ❌ 待定 | 无法知道用量 |

---

## 一、目标架构

```
用户输入
    │
    ▼
┌─────────────────────────────────────────────┐
│              ChatService (Agent)              │
│                                               │
│  ① 组装系统提示词                               │
│     ├── 基础提示词（编辑器能力说明）                │
│     ├── 实体 Schema 提示词（游戏数据结构）          │
│     └── RAG 检索结果（相关 XML 上下文）            │
│                                               │
│  ② 构建 MCP 工具列表（EditorTools × 8）          │
│                                               │
│  ③ Function-calling 循环                       │
│     ├── 调用 LLM                               │
│     ├── 检测 tool_calls → 执行 → 回传结果        │
│     └── 检测 text → 返回给用户                    │
│                                               │
│  ④ 后处理                                      │
│     ├── 记录 token 用量                         │
│     └── 保存对话历史                             │
└─────────────────────────────────────────────┘
```

---

## 二、分阶段计划

### Phase A1: 系统提示词系统

#### A1.1 默认系统提示词

ChatService 启动时注入基础提示词，描述编辑器的能力边界：

```
You are NeoEditor Assistant, an AI agent integrated into the NeoEditor game mod editor.
You have access to MCP tools that can read and edit game data entities.

Available capabilities:
- Query entities by type (ItemType, Creature, Recipe, Encounter, etc.)
- Create, edit, and delete entities
- Resolve references between entities
- View field-level diffs before saving
- List and filter entities by name

Guidelines:
- Always confirm destructive actions (delete) with the user.
- When creating entities, ask for the required properties.
- Use 'list' before 'get' to explore what's available.
- Format entity data clearly when presenting to the user.
```

#### A1.2 实体 Schema 注入

从 `Constants.GameTypes` 动态生成数据结构说明，注入系统提示词：

```
Available entity types and their key reference fields:
- ItemType: weaponRef, armorRef, recipeRef, ...
- Creature: attackModes, battleMoves, inventory, ...
- Recipe: ingredient1-4, toolRef, productRef, ...
...
```

#### A1.3 系统提示词 UI

在 AiChatView 添加：
- "System Prompt" 可折叠面板（默认收起）
- 可编辑的 TextBox（显示当前系统提示词）
- "Reset to default" 按钮

涉及文件：
- `AiChatViewModel` — 新增 `SystemPrompt` 属性 + `SetSystemPromptCommand`
- `ChatHistoryManager` — 已有 `SetSystemPrompt`，无需改动
- `AiChatView.axaml` — 新增可折叠面板

### Phase A2: RAG 服务（XML 索引）

#### A2.1 索引构建

```csharp
// Services/RagService.cs
public class RagService
{
    private readonly IHostService _hostService;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    // 构建索引：遍历所有实体 → 序列化为 XML 摘要 → 生成 embedding → 存入向量库
    public async Task BuildIndexAsync(CancellationToken ct = default);

    // 搜索：将用户查询 embedding → 在向量库中搜索 top-K 相似 XML → 返回文本
    public async Task<IReadOnlyList<RagResult>> SearchAsync(string query, int topK = 5, CancellationToken ct = default);
}
```

#### A2.2 索引内容

每个实体的索引文本 = 结构化的 XML 摘要（非完整 XML，而是属性列表 + 关键引用值）：

```
Entity: ItemType / item_weapon_sword
Subject: Iron Sword
Properties: weaponType=melee, damage=15, weight=3.5, value=100
References: weaponRef→attack_slash, recipeRef→recipe_sword_craft
```

> 设计理由：完整 XML 太长（embedding 模型有 token 限制），结构化摘要保留关键语义。

#### A2.3 嵌入模型

两个选项：

| 方案 | 优点 | 缺点 |
|------|------|------|
| **本地 ONNX 模型** (bge-small-en) | 离线可用，零成本 | 需额外 NuGet 包，首次加载慢 |
| **复用 OpenAI API** (text-embedding-3-small) | 简单，质量高 | 依赖网络，有费用 |

> **推荐方案 B**：复用现有 `OPENAI_ENDPOINT`，加环境变量 `OPENAI_EMBEDDING_MODEL`。用户若用 Ollama 本地模型，设置 `OPENAI_ENDPOINT=http://localhost:11434/v1` 和 `OPENAI_EMBEDDING_MODEL=nomic-embed-text` 即可。

#### A2.4 索引触发

| 触发方式 | 时机 |
|---------|------|
| **手动** | AI Chat 面板 "Build Index" 按钮 |
| **自动** | 加载 Mod/Profile 后自动重建 |
| **增量** | 实体保存后更新单个 embedding（未来优化） |

#### A2.5 RAG 结果注入

ChatService 在每次对话前：
1. 将用户最后一条消息作为查询
2. 调用 `RagService.SearchAsync(query, topK=3)`
3. 将结果包装为系统消息追加到上下文：

```
Relevant game data for context:
[Entity: ItemType/item_weapon_sword ...]
[Entity: Recipe/recipe_sword_craft ...]
[Entity: AttackMode/attack_slash ...]
```

涉及文件：
- `Services/RagService.cs` — 新文件
- `Services/ChatService.cs` — 注入 RAG 结果
- `Services/ServiceCollectionExtensions.cs` — DI 注册
- `ViewModels/AiChatViewModel.cs` — BuildIndexCommand
- `Views/AiChatView.axaml` — Build Index 按钮

### Phase A3: MCP 工具增强

#### A3.1 工具描述优化

当前 `EditorTools` 的 `[Description]` 需增强，让 LLM 更好理解何时调用：

```csharp
[McpServerTool, Description(
    "List all entities of a given type. Use this BEFORE GetEntity to explore available entities. " +
    "Supports substring filtering on entity name. Returns entity ID and display subject.")]
```

#### A3.2 新增工具

| 工具 | 用途 |
|------|------|
| `GetEntitySchema` | 返回指定实体类型的属性列表 + 引用字段元数据 |
| `SearchAllTypes` | 跨类型全文搜索（替代逐类型 list） |
| `GetModInfo` | 返回当前加载的 Mod/Profile 信息 |

#### A3.3 工具调用结果截断

当前工具返回完整 JSON（如 `ListEntities` 可能返回数千条），需截断防止爆 token：
- ListEntities → 默认 limit=20，结果超过 500 字符截断
- 截断时附加 `"(truncated, X more results)"` 提示

### Phase A4: Streaming 响应 ✅ 已完成 (2026-07-31)

`SendMessageStreamingAsync` 使用 `CompleteChatStreamingAsync` 逐 token 返回：
- `IChatService` 新增 `IAsyncEnumerable<string> SendMessageStreamingAsync` 方法
- `ChatService` 实现 streaming function-calling loop（工具调用在 streaming 中透明处理）
- `AiChatViewModel.SendMessageAsync` 改为 streaming 驱动，增量更新 assistant 消息
- `ChatMessageItem.IsThinking` 属性 + UI "..." 指示器表示等待首 token
- 工具调用时显示 `[tool: executing Xxx]` 状态标记
- `SendMessageAsync` 内部委托给 `SendMessageStreamingAsync`，保持向后兼容

> ✅ 2026-07-31 完成。AiChatView 有 typewriter 效果 + thinking 指示器。

---

## 三、新增/修改文件清单

| 文件 | 操作 | Phase |
|------|:--:|:--:|
| `AiChat/Services/RagService.cs` | 新增 | A2 |
| `AiChat/Services/ChatService.cs` | 修改 | A1, A2, A3 |
| `AiChat/Services/ChatHistoryManager.cs` | 修改 | A1 |
| `AiChat/ViewModels/AiChatViewModel.cs` | 修改 | A1, A2 |
| `AiChat/Views/AiChatView.axaml` | 修改 | A1, A2 |
| `AiChat/ServiceCollectionExtensions.cs` | 修改 | A1, A2 |
| `Mcp/Tools/EditorTools.cs` | 修改 | A3 |
| `Core/Abstractions/` | 可能新增 IRagService 接口 | A2 (R17 合规) |

---

## 四、时间估算

| Phase | 工作量 | 说明 |
|:-----:|:------:|------|
| A1 系统提示词 | 1-2h | 默认提示词 + Schema 注入 + UI |
| A2 RAG 服务 | 3-4h | VectorStore + Embedding + 索引构建/搜索 |
| A3 MCP 增强 | 1-2h | 工具描述 + 新工具 + 截断 |
| A4 Streaming | 1-2h | 可选，优先级低 — **✅ 2026-07-31 完成** |
| **合计** | **6-10h** | 全部完成 ✅ |

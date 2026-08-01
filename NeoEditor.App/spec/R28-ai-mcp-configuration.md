# R28 — AI/MCP 配置界面与启动路径

> 生效：2026-08-01 | 来源：用户决策 Phase 9D
> 依从：D01 Plugin 架构方向 · R20 DI 注册在 App
> 类型：基石(DO)

---

## 规则

1. **AI Chat Plugin 的 UI 必须可访问**——在 Dock 中有对应的 Tool 面板
2. **MCP Server 必须有可用的启动路径**——`--mcp` CLI 标志启动 stdio transport
3. **AI/MCP 配置必须有 UI**——在 SettingsPage 中提供配置界面，不再仅依赖环境变量

---

## 三部分要求

### 4.1 AI Chat Dock 接入

- AiChatView 必须在 RightToolPane 中可访问
- 在 Phase 9E（动态 Dock）完成前，用手写 Tool 过渡接入
- `AiChatTool : Tool { Id="AiChat", Title="AI Chat" }`

### 4.2 MCP Server 启动

- `Program.cs` 解析 `--mcp` CLI 标志
- `--mcp`：初始化 DI，启动 `McpServerHost.RunAsync()`（stdio transport），**不启动 GUI**
- `--mcp --mcp-port <port>`：预留 TCP transport
- 无 `--mcp`：正常 GUI 启动

### 4.3 AI/MCP 配置界面

`AppConfig` 配置模型（持久化到 `config.json`）——**Endpoint + ApiKey 按 Provider 分组，每个模型选用其中一个 Provider**：

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `AiProviders` | `[]` | Provider 列表（每项：`Id` / `Name` / `Endpoint` / `ApiKey`） |
| `AiModelProviderId` | (空) | 对话模型使用的 Provider Id（空 = 第一个） |
| `AiEmbeddingProviderId` | (空) | RAG 嵌入模型使用的 Provider Id（空 = 第一个） |
| `ImageProviderId` | (空) | 图片生成模型使用的 Provider Id（空 = 第一个） |
| `AiModel` | `gpt-4o` | 对话模型 |
| `AiEmbeddingModel` | `text-embedding-3-small` | RAG 嵌入模型 |
| `ImageModel` | `dall-e-3` | 图片生成模型 |
| `McpEnabled` | `false` | GUI 内启动 MCP TCP Server |
| `McpPort` | `0` | MCP TCP 端口（0 = stdio） |

每个 Provider 的 `ApiKey` 写入时 `ProtectedData.Protect` 加密。解析回退链见 `AiProviderResolver`（provider → 环境变量 → 内置默认；无任何 key 时 AI 功能禁用）。

**SettingsPage 新增 "AI & MCP" 分组**：Provider 列表编辑器 + 每模型 Provider 下拉 + 模型名输入 + MCP 开关 / 端口。

**配置读取优先级**：Provider 列表 > 环境变量（`OPENAI_ENDPOINT` / `OPENAI_API_KEY`）> 内置默认；无任何 key 时 AI 功能禁用。

---

## 为什么

1. **AiChat Panel 不可见**：完整实现了 ViewModel+View+Service，但 `AiChatView` 从未被放入 Dock。代码存在但功能不可达
2. **MCP 不可用**：注释写"用 --mcp 标志启动"，但此标志从未实现。外部 AI 客户端无法通过 MCP 连接编辑器
3. **零配置 UI**：API Key、Model 选择完全依赖环境变量，普通用户无法配置。需要重启应用才能换 Key/Model

---

## 决策边界

### 适用

- AI Chat / MCP / Image Generation 的配置和启动
- API Key 的安全存储

### 不适用

- AI 功能的具体实现（ChatService, RagService 等）——不在本规则范围
- Plugin 内部架构——不改变

---

## 验收

- AiChatView 在右侧 Tool Dock 可见且可交互
- `--mcp` 启动后，外部 MCP 客户端可连接并执行工具
- SettingsPage 有 "AI & MCP" 分组，可编辑 Provider 列表、每模型的 Provider 选择与模型名等
- ApiKey 在 config.json 中不以明文存储（每个 Provider 均加密）

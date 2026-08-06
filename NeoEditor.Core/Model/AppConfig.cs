using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;

namespace NeoEditor.Core.Model;

/// <summary>Application configuration POCO. M9: moved from App to Core so Infra can reference it.</summary>
public partial class AppConfig : ObservableRecipient
{
    [ObservableProperty] public partial string GameRootDir { get; set; } = Path.GetFullPath("./");
    [ObservableProperty] public partial string Language { get; set; } = "zh";
    [ObservableProperty] public partial string Theme { get; set; } = "System";
    [ObservableProperty] public partial int FontSize { get; set; } = 12;
    [ObservableProperty] public partial int ActiveProfileId { get; set; } = -1;
    [ObservableProperty] public partial int AutoSaveInterval { get; set; } = 60;
    [ObservableProperty] public partial string DefaultExportFormat { get; set; } = "csv";
    [ObservableProperty] public partial int GridRowHeight { get; set; } = 0;
    [ObservableProperty] public partial int SnapshotInterval { get; set; } = 10;

    /// <summary>
    /// Player loopback server port (Docs/42 v2.36): 0 = pick one and persist. PERSISTED —
    /// a random port per launch would change the page origin and WebView2 isolates
    /// localStorage per origin, making game saves appear to vanish between runs.
    /// </summary>
    [ObservableProperty] public partial int ServerPort { get; set; }

    // ── Onboarding hints (Docs/41 P2/P3) ────────────────────────────────
    /// <summary>One-shot hints the user has already seen/dismissed (Settings → Reset).</summary>
    public HashSet<string> DismissedHints { get; set; } = new();

    /// <summary>Empty-mod three-step banner dismissed by user (Docs/41 P2).</summary>
    public bool EmptyModHintDismissed { get; set; }

    // Panel layout persistence
    [ObservableProperty] public partial double LeftPanelWidth { get; set; } = 220;
    [ObservableProperty] public partial double RightPanelWidth { get; set; } = 280;
    [ObservableProperty] public partial double BottomPanelHeight { get; set; } = 150;
    [ObservableProperty] public partial bool LeftPanelVisible { get; set; } = true;
    [ObservableProperty] public partial bool RightPanelVisible { get; set; } = true;
    [ObservableProperty] public partial bool BottomPanelVisible { get; set; } = true;

    /// <summary>Per-table visible column sets. Table not in dict → default (hide ModId/FilePath/EntityId).</summary>
    public Dictionary<string, HashSet<string>> ColumnVisibility { get; set; } = new();

    public Dictionary<string, List<string>> ModImageOrders { get; set; } = new();

    // ── AI / MCP configuration (Phase 9D R28 + provider list) ────────────
    // Each model (chat / embedding / image) selects a provider from AiProviders by Id;
    // empty Id = first provider. When no provider has a key, environment variables act as
    // a fallback (OPENAI_API_KEY / OPENAI_ENDPOINT). Changes take effect after restart
    // (clients are created at DI resolution time).

    /// <summary>List of OpenAI-compatible API providers (endpoint + key).</summary>
    public List<AiProviderConfig> AiProviders { get; set; } = new();

    /// <summary>Provider used by the chat model. Empty = first provider.</summary>
    [ObservableProperty]
    public partial string AiModelProviderId { get; set; } = "";

    /// <summary>Provider used by the RAG embedding model. Empty = first provider.</summary>
    [ObservableProperty]
    public partial string AiEmbeddingProviderId { get; set; } = "";

    /// <summary>Provider used by the image generation model. Empty = first provider.</summary>
    [ObservableProperty]
    public partial string ImageProviderId { get; set; } = "";

    /// <summary>Chat model id (OPENAI_MODEL fallback).</summary>
    [ObservableProperty]
    public partial string AiModel { get; set; } = "gpt-4o";

    /// <summary>RAG embedding model id (OPENAI_EMBEDDING_MODEL fallback).</summary>
    [ObservableProperty]
    public partial string AiEmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Image generation model id (OPENAI_IMAGE_MODEL fallback).</summary>
    [ObservableProperty]
    public partial string ImageModel { get; set; } = "dall-e-3";

    /// <summary>Default AI candidate count for the create-image gallery (1-8).</summary>
    [ObservableProperty]
    public partial int AiCandidateCount { get; set; } = 4;

    /// <summary>Start MCP TCP server inside the GUI (reserved; stdio via --mcp flag).</summary>
    [ObservableProperty]
    public partial bool McpEnabled { get; set; }

    /// <summary>MCP TCP port. 0 = stdio only.</summary>
    [ObservableProperty]
    public partial int McpPort { get; set; }

    /// <summary>Max MCP tool-call iterations per AI Chat turn (guards runaway loops).</summary>
    [ObservableProperty]
    public partial int MaxToolCallsPerConversation { get; set; } = 30;

    // ── ParaTranz translation platform (D03) ───────────────────────────────
    // Token is encrypted at rest by ConfigService (DPAPI, same as AiProviders[].ApiKey).
    // API sync (channels A/B/C) only needs the token; the web workbench (channel D)
    // uses a separate web login.

    /// <summary>ParaTranz API Token (personal settings page; Bearer auth).</summary>
    [ObservableProperty]
    public partial string ParatranzToken { get; set; } = "";

    /// <summary>Selected ParaTranz project id. 0 = not selected.</summary>
    [ObservableProperty]
    public partial int ParatranzProjectId { get; set; }

    public AppConfig()
    {
        IsActive = true;
    }

    partial void OnGameRootDirChanged(string value)
    {
        Messenger.Send(new GameRootDirChangedMessage(value));
    }
}
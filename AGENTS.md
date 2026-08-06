# project overview

NeoEditor is a Mod editor for the game **NeoScavenger** (Avalonia desktop app).
It lets modders view, edit, validate and export game data (XML files) and image
assets without hand-writing XML. All game entity data is persisted to SQLite
(`game.db`) via EF Core and exported back to the game's XML files.

- Language: C# (.NET 10, `LangVersion=preview`, `Nullable=enable`)
- Build tool: `dotnet` (SDK pinned in `global.json`)
- Framework: Avalonia 12.1 (desktop UI), Dock.Avalonia, CommunityToolkit.Mvvm
- Data: EF Core 10 + SQLite; XML export via `IXmlParser`
- Testing Framework: xUnit (11 test projects under `Tests/`)

## architecture notes

- `IHostService` (Core/Abstractions) is the **single funnel for all entity CRUD**
  (R24): commands (`ExecuteAsync`/`ExecuteBatchAsync`), persistence
  (`SaveAsync`/`SaveAllAsync`/`PublishAsync`), XML export
  (`ExportModAsync`/`CommitExportAsync`), search (`SearchEntitiesAsync`) and the
  entity cache. UI / CLI / MCP / CSV import / XML export must all flow through it
  — direct `GameDbContext` writes are prohibited.
- `IEntityRepository<T>` (RepositoryBase) routes command-facade CRUD through
  `IHostService.ExecuteAsync`; `DbRepository`/`XmlRepository` are only
  instantiated by HostService.
- `IModManager` resolves to the `HostService` instance (mod import/create/delete
  all go through the host pipeline).
- Projects: `NeoEditor.Core` (abstractions), `NeoEditor.Infra` (services/data),
  `NeoEditor.App` (GUI shell), `NeoEditor.Plugins.*` (feature plugins),
  `NeoEditor.UI.Common`; specs under `NeoEditor.App/spec/` (R##/D##/N##).

## build / run

- Build: `dotnet build NeoEditor.sln`
- Run (GUI): `dotnet run --project NeoEditor.App`
- Run (headless MCP server): `dotnet run --project NeoEditor.App -- --mcp`
- Tests: `dotnet test NeoEditor.sln`

No web server; no fixed ports (MCP TCP port is configurable).

## dev environment (ZCode)

- The session model cannot read images directly (attachments are filtered). Image
  recognition is provided by the ZCode **deepseek-vision** MCP (MiMo v2.5,
  integrated 2026-08-05): locate the base64 attachment
  (`C:\Users\Cromzst\.zcode\cli\artifacts\<session-id>\prompt-attachment-upload-*.txt`,
  match by modified time), decode it to `.png`, then run
  `node C:\Users\Cromzst\.zcode\workspace\default\deepseek-vision\analyze.js <png> [prompt]`.
  Credentials come from `mcp.servers["deepseek-vision"].env` in
  `C:\Users\Cromzst\.zcode\cli\config.json` — never hardcode keys.
  Full flow: global `C:\Users\Cromzst\.zcode\AGENTS.md`.

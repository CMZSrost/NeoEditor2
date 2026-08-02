using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Serilog;

namespace NeoEditor.Services;

/// <summary>
/// Browser index service — injected singleton.
/// Manages the EntityMergeStore and ReferenceIndexService for the data browser.
/// Replaces the former static BrowserIndexService.
/// </summary>
public class BrowserIndexService : IBrowserIndexService
{
    private readonly IWorkspaceSession _session;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IConfigService _configService;
    private readonly INotificationService _notification;
    private readonly PhpParser _phpParser;
    private readonly IReferenceResolver _referenceResolver;

    private bool _built;
    private Task? _buildTask;
    private readonly object _buildLock = new();

    public ReferenceIndexService? Index { get; private set; }

    public bool IsBuilding
    {
        get
        {
            lock (_buildLock)
                return !_built && _buildTask is { IsCompleted: false };
        }
    }

    public string IndexDbPath => Path.Combine(Directory.GetCurrentDirectory(), "index.db");

    public Dictionary<Type, Dictionary<int, BrowserIndexCacheEntry>> GlobalBrowserCache { get; } = new();
    public Dictionary<string, string> GlobalModNames { get; } = new();

    public BrowserIndexService(
        IWorkspaceSession session,
        IDbContextFactory<GameDbContext> gameDbFactory,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        IConfigService configService,
        INotificationService notification,
        PhpParser phpParser,
        IReferenceResolver referenceResolver)
    {
        _session = session;
        _gameDbFactory = gameDbFactory;
        _editorDbFactory = editorDbFactory;
        _configService = configService;
        _notification = notification;
        _phpParser = phpParser;
        _referenceResolver = referenceResolver;
    }

    public void Invalidate()
    {
        lock (_buildLock)
        {
            _built = false;
            _buildTask = null;
        }
        _session.SetBrowserStore(null);
        // Q8=B: GDH bridge sync removed. Use _session.SetBrowserStore() only.
        GlobalBrowserCache.Clear();
        GlobalModNames.Clear();
        Index?.Close();
        Index?.Dispose();
        Index = null;
        try { if (File.Exists(IndexDbPath)) File.Delete(IndexDbPath); } catch { /* ignore */ }
    }

    public Task EnsureBuiltAsync()
    {
        if (_built) return Task.CompletedTask;
        lock (_buildLock)
        {
            if (_built) return Task.CompletedTask;
            if (_buildTask is { IsCompleted: false })
                return _buildTask;
            _buildTask = IndexDbHasData() ? RestoreFromDiskAsync() : RebuildAsync();
            return _buildTask;
        }
    }

    private bool IndexDbHasData()
    {
        try
        {
            if (!File.Exists(IndexDbPath)) return false;
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={IndexDbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM reference_index;";
            var count = (long)(cmd.ExecuteScalar() ?? 0L);
            return count > 0;
        }
        catch { return false; }
    }

    private Task RebuildAsync() => BuildStoreAndIndexAsync(rebuildIndex: true);
    private Task RestoreFromDiskAsync()
    {
        Log.Logger.Information("[BrowserIndex] index.db found with valid data, restoring from disk");
        return BuildStoreAndIndexAsync(rebuildIndex: false);
    }

    private async Task BuildStoreAndIndexAsync(bool rebuildIndex)
    {
        try
        {
            Log.Logger.Information(rebuildIndex
                ? "[BrowserIndex] Rebuild started"
                : "[BrowserIndex] Restoring store from disk");

            if (rebuildIndex)
                Dispatcher.UIThread.Post(() => _notification.ShowInfo("正在构建数据索引，请稍候…", "索引构建中"));

            await using var db = await _gameDbFactory.CreateDbContextAsync();
            await using var editorDb = await _editorDbFactory.CreateDbContextAsync();

            var pathToNs = new Dictionary<string, string>();
            var gameRoot = _configService.Config?.GameRootDir;
            if (!string.IsNullOrEmpty(gameRoot))
            {
                var getmodsPath = Path.Combine(gameRoot, "getmods.php");
                if (File.Exists(getmodsPath))
                {
                    foreach (var modEntry in _phpParser.ParseMods(getmodsPath))
                        pathToNs[modEntry.Path] = modEntry.Name;
                    Log.Logger.Information("[BrowserIndex] parsed {Count} mod namespaces from getmods.php", pathToNs.Count);
                }
            }

            var modNames = new Dictionary<int, string> { [-1] = "Game" };
            var modNsNames = new Dictionary<int, string> { [-1] = "0" };
            var modLoadOrder = new List<int> { -1 };
            foreach (var mi in editorDb.ModInfos.Where(m => !m.IsBase).OrderBy(m => m.ModId))
            {
                modNames[mi.ModId] = mi.Name;
                modNsNames[mi.ModId] = pathToNs.TryGetValue(mi.Path, out var ns) ? ns : mi.Name;
                modLoadOrder.Add(mi.ModId);
            }

            var store = new EntityMergeStore();
            GlobalBrowserCache.Clear();
            GlobalModNames.Clear();

            Index?.Dispose();
            Index = ReferenceIndexService.CreateFileBased(IndexDbPath);
            Index.Open();

            var entityTypes = typeof(GameDbContext).GetProperties()
                .Where(p => p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                .Select(p => p.PropertyType.GetGenericArguments()[0])
                .Where(t => typeof(IEntity).IsAssignableFrom(t))
                .ToList();

            var indexEntries = rebuildIndex ? new List<ReferenceIndexService.IndexEntry>() : null;

            foreach (var eType in entityTypes)
            {
                var m = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set),
                    Type.EmptyTypes)!.MakeGenericMethod(eType);
                var dbSet = (System.Collections.IEnumerable)m.Invoke(db, null)!;
                var items = new List<object>();
                var keyProp = EntityHelper.ResolveKeyProperty(eType);

                var groupIdProp = eType.GetProperty("GroupId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var subgroupIdProp = eType.GetProperty("SubgroupId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                foreach (var obj in dbSet)
                {
                    if (obj is not IEntity entity) continue;
                    items.Add(entity);
                    modNames.TryGetValue(entity.ModId, out var modName);
                    var mn = modName ?? $"mod_{entity.ModId}";
                    store.EntityModNames[entity.EntityId] = mn;
                    GlobalModNames[entity.EntityId] = mn;
                    modNsNames.TryGetValue(entity.ModId, out var nsName);
                    var ns2 = nsName ?? mn;
                    store.EntityNamespaces[entity.EntityId] = ns2;

                    var pk = keyProp?.GetValue(entity)?.ToString();
                    if (pk is not null && indexEntries is not null)
                    {
                        int? gid = null, sid = null;
                        if (groupIdProp is not null && subgroupIdProp is not null)
                        {
                            if (groupIdProp.GetValue(entity) is int g) gid = g;
                            if (subgroupIdProp.GetValue(entity) is int s) sid = s;
                        }
                        indexEntries.Add(new ReferenceIndexService.IndexEntry(
                            eType.Name, ns2, pk, entity.EntityId, gid, sid));
                    }
                }
                store.ReferenceLookups[eType] = items;

                static bool InMergeSpace(IEntity e, EntityMergeStore s) =>
                    s.EntityNamespaces.TryGetValue(e.EntityId, out var ns) && ns == "0";
                var maxMergeKey = items.OfType<IEntity>()
                    .Where(e => InMergeSpace(e, store))
                    .Select(e => keyProp?.GetValue(e)).OfType<int>()
                    .DefaultIfEmpty(0).Max();
                var nextInsertId = maxMergeKey + 1;

                var typeCache = new Dictionary<int, BrowserIndexCacheEntry>();
                foreach (var obj in items)
                {
                    if (obj is not IEntity entity) continue;
                    if (keyProp?.GetValue(entity) is int k)
                    {
                        int mergedId = InMergeSpace(entity, store) ? k : nextInsertId++;
                        store.EntityMergedIds[entity.EntityId] = mergedId;
                        typeCache[mergedId] = new BrowserIndexCacheEntry(entity.EntityId, entity.ModId, k, entity.Subject);
                    }
                }
                GlobalBrowserCache[eType] = typeCache;
            }

            store.NamespaceToModName["0"] = "Game";

            var totalEntityCount = store.ReferenceLookups.Sum(kv => kv.Value.Count);
            if (totalEntityCount == 0)
            {
                Log.Logger.Information("[BrowserIndex] Rebuild skipped — no entities in database (mods not loaded yet)");
                return;
            }

            if (rebuildIndex)
            {
                indexEntries!.Sort((a, b) =>
                {
                    var typeCmp = string.CompareOrdinal(a.EntityType, b.EntityType);
                    if (typeCmp != 0) return typeCmp;
                    var nsA = a.Namespace == "0" ? 0 : 1;
                    var nsB = b.Namespace == "0" ? 0 : 1;
                    return nsA.CompareTo(nsB);
                });

                await Index.BuildAsync(indexEntries);
                Log.Logger.Information("[BrowserIndex] SQLite index built — {Count} entries", Index.Count);

                store.IndexService = Index;
                // R30 (P4/H2): build the in-memory ReferenceIndex first (reverse-index
                // resolution and DataGrid display both go through it), then publish the
                // browser store BEFORE building the reverse index.
                await store.Index.BuildAsync();
                _session.SetBrowserStore(store);
                await _referenceResolver.BuildReverseIndexAsync(Index, store);
            }
            else
            {
                store.IndexService = Index;
                // R30 (P4): browser store display resolution needs the in-memory ReferenceIndex
                // (LookupSubject has no SQLite fallback).
                await store.Index.BuildAsync();
                _session.SetBrowserStore(store);
                Log.Logger.Information("[BrowserIndex] Restored from disk — {Count} entries", Index.Count);
            }

            _built = true;
            Log.Logger.Information("[BrowserIndex] {Action} complete", rebuildIndex ? "Rebuild" : "Restore");

            if (rebuildIndex)
                Dispatcher.UIThread.Post(() => _notification.ShowSuccess("数据索引已就绪，可以浏览数据了", "索引就绪"));
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "[BrowserIndex] {Action} FAILED", rebuildIndex ? "Rebuild" : "Restore");
        }
    }
}

public class BrowserIndexCacheEntry
{
    public string Eid { get; set; } = "";
    public int Mid { get; set; }
    public int K { get; set; }
    public string Sub { get; set; } = "";
    public BrowserIndexCacheEntry() { }
    public BrowserIndexCacheEntry(string entityId, int modId, int key, string subject)
    { Eid = entityId; Mid = modId; K = key; Sub = subject; }
}

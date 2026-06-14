using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Serilog;

namespace NeoEditor.Services;

/// <summary>
/// Global browser index service. Manages the EntityMergeStore used by the data browser.
/// Extracted from EntityBrowserDocument to remove static-god-class coupling.
/// 
/// Lifecycle: built once on startup from the DB, persisted to disk, invalidated on mod/profile changes.
/// Thread-safe: only one build in flight at a time.
/// </summary>
public static class BrowserIndexService
{
    private static bool _built;
    private static Task? _buildTask;
    private static readonly object BuildLock = new();

    /// <summary>True while the index is being rebuilt (not yet complete).</summary>
    public static bool IsBuilding
    {
        get
        {
            lock (BuildLock)
            {
                return !_built && _buildTask is { IsCompleted: false };
            }
        }
    }

    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoEditor", "browser_index_cache.json");
    private static string IndexCachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoEditor", "browser_reference_index.json");

    /// <summary>Global browser lookup cache. Maps entity type → (MergedId → cache entry).</summary>
    public static Dictionary<Type, Dictionary<int, BrowserIndexCacheEntry>> GlobalBrowserCache { get; } = new();
    /// <summary>Entity EntityId → mod display name.</summary>
    public static Dictionary<string, string> GlobalModNames { get; } = new();

    /// <summary>Mark index as stale; next EnsureBuiltAsync will trigger a full rebuild.</summary>
    public static void Invalidate()
    {
        lock (BuildLock)
        {
            _built = false;
            _buildTask = null;
        }
        GenericDataGridHelper.BrowserStore = null;
        GlobalBrowserCache.Clear();
        GlobalModNames.Clear();
        try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { /* ignore */ }
        try { if (File.Exists(IndexCachePath)) File.Delete(IndexCachePath); } catch { /* ignore */ }
    }

    /// <summary>Ensure the browser index is built, returning immediately if already done.
    /// If a build is in flight, wait for it. Thread-safe.</summary>
    public static Task EnsureBuiltAsync()
    {
        if (_built) return Task.CompletedTask;
        lock (BuildLock)
        {
            if (_built) return Task.CompletedTask;
            if (_buildTask is { IsCompleted: false })
                return _buildTask;
            _buildTask = RebuildAsync();
            return _buildTask;
        }
    }

    /// <summary>Full rebuild: load all entities from DB, compute MergedIds, build ReferenceIndex.</summary>
    private static async Task RebuildAsync()
    {
        try
        {
            Log.Logger.Information("[BrowserIndex] Rebuild started");

            // Toast: build started
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                App.Notification.ShowInfo("正在构建数据索引，请稍候…", "索引构建中"));

            // Invalidate old disk caches
            try { File.Delete(CachePath); } catch { /* ignore */ }
            try { File.Delete(IndexCachePath); } catch { /* ignore */ }

            await using var db = await App.ServiceProvider!
                .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<GameDbContext>>()
                .CreateDbContextAsync();
            await using var editorDb = await App.ServiceProvider!
                .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<EditorDbContext>>()
                .CreateDbContextAsync();

            var modNames = new Dictionary<int, string> { [-1] = "Game" };
            var modNsNames = new Dictionary<int, string> { [-1] = "0" };
            foreach (var mi in editorDb.ModInfos)
            {
                modNames[mi.ModId] = mi.Name;
                modNsNames[mi.ModId] = mi.Name;
            }

            var store = new EntityMergeStore();
            GlobalBrowserCache.Clear();
            GlobalModNames.Clear();

            var entityTypes = typeof(GameDbContext).GetProperties()
                .Where(p => p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
                .Select(p => p.PropertyType.GetGenericArguments()[0])
                .Where(t => typeof(IEntity).IsAssignableFrom(t))
                .ToList();

            foreach (var eType in entityTypes)
            {
                var m = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set),
                    Type.EmptyTypes)!.MakeGenericMethod(eType);
                var dbSet = (System.Collections.IEnumerable)m.Invoke(db, null)!;
                var items = new List<object>();
                var keyProp = EntityHelper.ResolveKeyProperty(eType);
                foreach (var obj in dbSet)
                {
                    if (obj is not IEntity entity) continue;
                    items.Add(entity);
                    modNames.TryGetValue(entity.ModId, out var modName);
                    var mn = modName ?? $"mod_{entity.ModId}";
                    store.EntityModNames[entity.EntityId] = mn;
                    GlobalModNames[entity.EntityId] = mn;
                    modNsNames.TryGetValue(entity.ModId, out var nsName);
                    store.EntityNamespaces[entity.EntityId] = nsName ?? mn;
                }
                store.ReferenceLookups[eType] = items;

                // MergedId: consistent with MergeService.ComputeTypeMerge
                //   ns="0" (merge space) → MergedId = primary key
                //   ns≠"0" (insert space) → auto-increment from max merge key + 1
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
                        int mergedId;
                        if (InMergeSpace(entity, store))
                            mergedId = k;
                        else
                            mergedId = nextInsertId++;
                        store.EntityMergedIds[entity.EntityId] = mergedId;
                        typeCache[mergedId] = new BrowserIndexCacheEntry(entity.EntityId, entity.ModId, k, entity.Subject);
                    }
                }
                GlobalBrowserCache[eType] = typeCache;
            }

            store.NamespaceToModName["0"] = "Game";

            // Skip index build if database has no entities yet (app just started, mods not loaded)
            var totalEntityCount = store.ReferenceLookups.Sum(kv => kv.Value.Count);
            if (totalEntityCount == 0)
            {
                Log.Logger.Information("[BrowserIndex] Rebuild skipped — no entities in database (mods not loaded yet)");
                // Do NOT set _built = true — allow rebuild when data is available
                return;
            }

            // Try load ReferenceIndex from disk; if empty MergedFallback, force rebuild
            if (store.Index.TryLoadFromDisk(IndexCachePath))
            {
                if (store.Index.MergedFallbackCount == 0)
                {
                    Log.Logger.Warning("[BrowserIndex] Disk cache has empty MergedFallback — forcing rebuild");
                    try { File.Delete(IndexCachePath); } catch { /* ignore */ }
                }
                else
                {
                    Console.WriteLine($"[BrowserIndex] ReferenceIndex loaded from disk: {store.ReferenceLookups.Count} types");
                    GenericDataGridHelper.BrowserStore = store;
                    _built = true;
                    SaveToDisk();
                    Log.Logger.Information("[BrowserIndex] Rebuild complete (disk cache hit)");
                    return;
                }
            }

            await store.Index.BuildAsync();
            Log.Logger.Information(
                "[BrowserIndex] BuildAsync done — mergedIdIdx={MergedIdIdx} totalEntities={TotalEnt}",
                store.Index.MergedFallbackCount,
                store.ReferenceLookups.Sum(kv => kv.Value.Count));
            store.Index.SaveToDisk(IndexCachePath);

            GenericDataGridHelper.BrowserStore = store;
            _built = true;
            SaveToDisk();
            Log.Logger.Information("[BrowserIndex] Rebuild complete (full build)");

            // Toast: build complete
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                App.Notification.ShowSuccess("数据索引构建完成，现在可以浏览数据了", "索引就绪"));
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "[BrowserIndex] Rebuild FAILED");
            // Do NOT set _built = true — next caller will retry
        }
    }

    private static bool TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(CachePath)) return false;
            var json = File.ReadAllText(CachePath);
            var data = System.Text.Json.JsonSerializer.Deserialize<BrowserIndexCacheData>(json);
            if (data?.Types is null) return false;

            GlobalBrowserCache.Clear();
            GlobalModNames.Clear();
            if (data.EntityModNames is not null)
                foreach (var (k, v) in data.EntityModNames) GlobalModNames[k] = v;

            foreach (var (typeName, entries) in data.Types)
            {
                var type = Type.GetType(typeName);
                if (type is null) continue;
                var dict = new Dictionary<int, BrowserIndexCacheEntry>();
                foreach (var e in entries) dict[e.K] = e;
                GlobalBrowserCache[type] = dict;
            }

            Console.WriteLine($"[BrowserIndex] Loaded from disk: {GlobalBrowserCache.Count} types, {GlobalBrowserCache.Sum(kv => kv.Value.Count)} entities");
            return GlobalBrowserCache.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BrowserIndex] Disk load failed: {ex.Message}");
            return false;
        }
    }

    private static void SaveToDisk()
    {
        try
        {
            var data = new BrowserIndexCacheData
            {
                Types = GlobalBrowserCache.ToDictionary(
                    kv => kv.Key.FullName!,
                    kv => kv.Value.Values.ToList()),
                EntityModNames = new Dictionary<string, string>(GlobalModNames)
            };
            var dir = Path.GetDirectoryName(CachePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(CachePath, System.Text.Json.JsonSerializer.Serialize(data));
            Console.WriteLine($"[BrowserIndex] Cache saved to {CachePath}");
        }
        catch (Exception ex) { Console.WriteLine($"[BrowserIndex] Cache save failed: {ex.Message}"); }
    }
}

// ── Cache data types (moved from Documents.cs) ──

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

internal class BrowserIndexCacheData
{
    public Dictionary<string, List<BrowserIndexCacheEntry>> Types { get; set; } = new();
    public Dictionary<string, string>? EntityModNames { get; set; }
}

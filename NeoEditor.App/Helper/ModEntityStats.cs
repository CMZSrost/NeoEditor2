using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

/// <summary>
/// Groups a mod's DB entities by source XML file and entity type — the data behind
/// the Profile Tool's Mod → XML → non-empty data-class tree (task 5).
/// </summary>
public static class ModEntityStats
{
    /// <summary>
    /// Returns <c>normalized absolute path → (entity type name → count)</c> for one mod,
    /// by querying every entity type in <see cref="Constants.GameTypes"/> filtered by ModId.
    /// </summary>
    public static Dictionary<string, Dictionary<string, int>> LoadModEntityStats(
        GameDbContext db, int modId, string? gameRoot = null)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (typeName, type) in Constants.GameTypes)
        {
            var entities = db.GetDbSet(type)
                .Cast<IEntity>()
                .Where(e => e.ModId == modId)
                .AsEnumerable()
                .Where(static e => !string.IsNullOrWhiteSpace(e.FilePath));

            foreach (var entity in entities)
            {
                // DB paths may be relative to the game root (e.g. "data/itemtypes.xml") —
                // resolve them so the key matches the absolute paths scanned on disk.
                var key = Normalize(ResolvePath(entity.FilePath, gameRoot));
                if (!result.TryGetValue(key, out var types))
                {
                    types = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    result[key] = types;
                }

                types[typeName] = types.GetValueOrDefault(typeName) + 1;
            }
        }

        return result;
    }

    private static string ResolvePath(string path, string? gameRoot)
    {
        if (Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(gameRoot))
            return path;
        return Path.Combine(gameRoot, path);
    }

    /// <summary>Normalizes a path for DB-to-disk matching (absolute, forward slashes, case-insensitive).</summary>
    public static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
        catch
        {
            return path.Trim().Replace('\\', '/');
        }
    }
}
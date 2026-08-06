using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>One image source: the base game or a single mod, with a resolved content root.</summary>
public sealed record ModContentRoot(string Name, string ContentRoot, bool IsGame);

/// <summary>
/// Resolves the image content roots for the currently active profile.
/// The base game is always <c>gameRoot</c>; each mod's content root is derived from its
/// <see cref="ModInfo.Path"/> (relative to gameRoot) as recorded in the profile's
/// ModLoadInfos — NOT from a hardcoded <c>gameRoot/Mods</c> convention. This lets mods
/// imported from arbitrary folders be found by the image tools.
/// </summary>
public interface IProfileModSourceProvider
{
    /// <summary>
    /// UI-thread snapshot of the current content roots. Call BEFORE entering a background
    /// thread — the mod list is read from the last received profile (which other receivers
    /// populate synchronously during message dispatch) and is returned as an immutable copy.
    /// </summary>
    IReadOnlyList<ModContentRoot> GetContentRoots();

    ProfileInfo? CurrentProfile { get; }
}

/// <summary>
/// Implementation driven by <see cref="LoadProfileMessage"/> / <see cref="OpenMergeEditorMessage"/>.
/// Stores only the ProfileInfo reference in the message handler — the ModLoadInfos are populated
/// by other receivers after message dispatch, so they must be read lazily from
/// <see cref="GetContentRoots"/> (not inside the handler).
/// </summary>
public sealed class ProfileModSourceProvider : IProfileModSourceProvider
{
    private readonly IConfigService _config;
    private volatile ProfileInfo? _currentProfile;

    public ProfileModSourceProvider(IConfigService config, IMessenger messenger)
    {
        _config = config;

        // Store only the reference synchronously. ModLoadInfos may still be empty here
        // (receivers fill them during the same dispatch, in registration order) — reading
        // them now would race, so GetContentRoots() snapshots them later.
        messenger.Register<LoadProfileMessage>(this, (_, m) => _currentProfile = m.ProfileInfo);
        messenger.Register<OpenMergeEditorMessage>(this, (_, m) => _currentProfile = m.ProfileInfo);
    }

    public ProfileInfo? CurrentProfile => _currentProfile;

    public IReadOnlyList<ModContentRoot> GetContentRoots()
    {
        var gameRoot = _config.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            return [];

        // 1. Base game is always gameRoot (read-only source + shared-image fallback).
        var roots = new List<ModContentRoot>
        {
            new("Base Game", gameRoot, IsGame: true)
        };

        var mods = _currentProfile?.ModLoadInfos
            .Where(m => m.Info is not null && !string.IsNullOrWhiteSpace(m.Info.Path))
            .Select(static m => m.Info!)
            .ToList();

        if (mods is { Count: > 0 })
        {
            foreach (var mod in mods)
            {
                if (IsGameMod(mod))
                    continue; // game source already added above — avoid a duplicate "data" source

                var contentRoot = ResolveContentRoot(mod, gameRoot);
                if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
                    continue; // directory gone — skip rather than emit a dead source

                roots.Add(new ModContentRoot(mod.Name, contentRoot, IsGame: false));
            }

            return roots;
        }

        // 2. Fallback: no profile or empty ModLoadInfos → keep the legacy Mods/ scan.
        var modsDir = Path.Combine(gameRoot, "Mods");
        if (Directory.Exists(modsDir))
        {
            foreach (var dir in Directory.GetDirectories(modsDir))
                roots.Add(new ModContentRoot(Path.GetFileName(dir), dir, IsGame: false));
        }

        return roots;
    }

    private static bool IsGameMod(ModInfo mod) =>
        mod.ModId == -1 || string.Equals(mod.Name, "Game", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveContentRoot(ModInfo mod, string gameRoot)
    {
        var path = Path.IsPathRooted(mod.Path) ? mod.Path : Path.Combine(gameRoot, mod.Path);
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }
}
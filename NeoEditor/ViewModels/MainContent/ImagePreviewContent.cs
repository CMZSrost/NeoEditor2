using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.ViewModels;

namespace NeoEditor.ViewModels.MainContent;

public partial class ImagePreviewContent : LocalizedObservableObject
{
    [ObservableProperty] public partial string StatusText { get; set; } = "Select a row to preview images.";
    public ObservableCollection<ImageEntry> ImagePaths { get; } = [];
    private static List<string>? _cachedImgDirs;
    private static string? _cachedGameRoot;

    public void ShowEntity(IEntity? entity)
    {
        ImagePaths.Clear();
        if (entity is null) { StatusText = "No entity selected."; return; }

        var type = entity.GetType();
        var config = App.ServiceProvider!.GetRequiredService<Services.IConfigService>().Config;
        var gameRoot = config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            StatusText = "Game root directory not set or invalid. Check Settings.";
            return;
        }

        var searchDirs = GetSearchDirs(gameRoot, entity);
        if (searchDirs.Count == 0)
        {
            StatusText = $"No img directories found under '{gameRoot}'.";
            return;
        }

        var imgFields = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(string)
                && (p.Name.Contains("Img", StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains("Sprite", StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains("Image", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (imgFields.Count == 0)
        {
            StatusText = $"{entity.Subject} [{type.Name}] — no Img/Sprite/Image fields. " +
                         $"String props: {string.Join(", ", type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.PropertyType == typeof(string)).Select(p => p.Name).Take(10))}";
            return;
        }

        int found = 0, total = 0;
        foreach (var prop in imgFields)
        {
            var value = prop.GetValue(entity)?.ToString();
            if (string.IsNullOrWhiteSpace(value)) continue;

            foreach (var part in value.Split(',', '|'))
            {
                var trimmed = part.Trim().Trim('[', ']');
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                total++;

                var colonIdx = trimmed.IndexOf(':');
                var imgName = colonIdx > 0 ? trimmed[(colonIdx + 1)..] : trimmed;

                var candidates = imgName.Contains('.')
                    ? new[] { imgName }
                    : new[] { imgName + ".png", imgName };

                var foundPath = searchDirs
                    .SelectMany(dir => candidates.Select(c => Path.Combine(dir, c)))
                    .FirstOrDefault(File.Exists);

                if (foundPath is not null)
                {
                    found++;
                    ImagePaths.Add(new ImageEntry(imgName, prop.Name, foundPath));
                }
                else
                    ImagePaths.Add(new ImageEntry(imgName, prop.Name, null));
            }
        }

        StatusText = found > 0
            ? $"{entity.Subject} — {found}/{total} found"
            : $"{entity.Subject} — 0/{total} found in {searchDirs.Count} dirs. FilePath: {entity.FilePath}";
    }

    private static List<string> GetSearchDirs(string gameRoot, IEntity entity)
    {
        var dirs = new List<string>();

        var gameImg = Path.Combine(gameRoot, "img");
        if (Directory.Exists(gameImg)) dirs.Add(gameImg);

        if (_cachedGameRoot != gameRoot)
        {
            _cachedImgDirs = ScanAllModImgDirs(gameRoot);
            _cachedGameRoot = gameRoot;
        }
        if (_cachedImgDirs is not null) dirs.AddRange(_cachedImgDirs);

        if (!string.IsNullOrWhiteSpace(entity.FilePath))
        {
            var xmlDir = Path.GetDirectoryName(entity.FilePath.Replace('\\', '/'));
            if (xmlDir is not null)
            {
                var modImg = Path.GetFullPath(Path.Combine(gameRoot, xmlDir, "img"));
                if (Directory.Exists(modImg) && !dirs.Contains(modImg, StringComparer.OrdinalIgnoreCase))
                    dirs.Add(modImg);
            }
        }

        return dirs;
    }

    private static List<string> ScanAllModImgDirs(string gameRoot)
    {
        var dirs = new List<string>();
        var modsRoot = Path.Combine(gameRoot, "Mods");
        if (!Directory.Exists(modsRoot)) return dirs;

        try
        {
            foreach (var topDir in Directory.EnumerateDirectories(modsRoot))
            {
                var topImg = Path.Combine(topDir, "img");
                if (Directory.Exists(topImg)) dirs.Add(topImg);
                foreach (var subDir in Directory.EnumerateDirectories(topDir))
                {
                    var subImg = Path.Combine(subDir, "img");
                    if (Directory.Exists(subImg)) dirs.Add(subImg);
                }
            }
        }
        catch { }

        return dirs;
    }

    public void Clear()
    {
        StatusText = "Select a row to preview images.";
        ImagePaths.Clear();
    }
}

public record ImageEntry(string FileName, string FieldName, string? FullPath)
{
    public string DisplayText => FullPath is not null ? FileName : $"(missing) {FileName}";
    public bool IsFound => FullPath is not null;
}

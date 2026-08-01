using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AvaloniaEdit.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.ViewModels;

namespace NeoEditor.Services;

public interface IProfileManager
{
    public ProfileInfo CreateProfile(string? name = null, string? description = null, string? profilePath = null);
    public ProfileInfo LoadProfile(string? name = null, string? description = null, string? profilePath = null);
    public IList<ModLoadInfo> LoadMods(string content);
}

public class ProfileManager : IProfileManager
{
    private readonly IConfigService _configService;
    private readonly PhpParser _parser;
    private readonly IDbContextFactory<EditorDbContext> _dbContextFactory;
    private AppConfig Config => _configService.Config;

    public ProfileManager(IConfigService configService, PhpParser parser,
        IDbContextFactory<EditorDbContext> dbContextFactory)
    {
        _configService = configService;
        _parser = parser;
        _dbContextFactory = dbContextFactory;
    }

    private string ProfileDir => Path.Combine(Config.GameRootDir, "Profiles");

    public IList<ModLoadInfo> LoadMods(string content)
    {
        var entities = _parser.ParseModsContent(content);
        using var db = _dbContextFactory.CreateDbContext();
        var existedMods = db.ModInfos.ToDictionary(m => m.Path, m => m);
        return entities.Select(entry => new ModLoadInfo()
        {
            Type = existedMods.ContainsKey(entry.Path) ? entry.Type : ModType.Unknown,
            Namespace = entry.Name, // strModName from getmods.php
            Info = existedMods.ContainsKey(entry.Path) switch
            {
                true => existedMods[entry.Path],
                false => new ModInfo()
                {
                    Name = entry.Name,
                    Path = entry.Path,
                    IsBase = false,
                    LastImport = DateTime.Now,
                    LastModified = DateTime.Now
                }
            }
        }).ToList();
    }

    public ProfileInfo LoadProfile(string? name = null, string? description = null, string? profilePath = null)
    {
        profilePath ??= Path.Combine(ProfileDir, $"getmods_{Guid.NewGuid()}.php");

        var content = File.Exists(profilePath)
            ? File.ReadAllText(profilePath)
            : _parser.GenerateModsPhp([]);

        bool isBase = (profilePath == Path.Combine(Config.GameRootDir, "getmods.php"));
        name = isBase ? "Game" : (name ?? Path.GetFileNameWithoutExtension(profilePath));

        var profile = new ProfileInfo
        {
            ProfileId = isBase ? -1 : 0,
            Name = name,
            Path = profilePath,
            Description = description ?? "",
            Content = content,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        };

        profile.ModLoadInfos.Clear();
        profile.ModLoadInfos.AddRange(LoadMods(content));
        return profile;
    }

    public ProfileInfo CreateProfile(string? name = null, string? description = null, string? profilePath = null)
    {
        var profile = LoadProfile(name, description, profilePath);

        using var db = _dbContextFactory.CreateDbContext();
        db.Add(profile);
        db.SaveChanges();
        return profile;
    }
}
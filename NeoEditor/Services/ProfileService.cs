using System;
using System.IO;
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
}

public class ProfileManager : IProfileManager
{
    private readonly IConfigService _configService;
    private readonly PhpParser _parser;
    private readonly IDbContextFactory<EditorDbContext> _dbContextFactory;
    private AppConfig Config => _configService.Config;

    public ProfileManager() : this(App.ServiceProvider.GetRequiredService<IConfigService>(),
        App.ServiceProvider.GetRequiredService<PhpParser>(),
        App.ServiceProvider.GetRequiredService<IDbContextFactory<EditorDbContext>>())
    {
    }

    public ProfileManager(IConfigService configService, PhpParser parser,
        IDbContextFactory<EditorDbContext> dbContextFactory)
    {
        _configService = configService;
        _parser = parser;
        _dbContextFactory = dbContextFactory;
    }

    private string ProfileDir => Path.Combine(Config.GameRootDir, "Profiles");

    public ProfileInfo CreateProfile(string? name = null, string? description = null, string? profilePath = null)
    {
        profilePath ??= Path.Combine(ProfileDir, $"getmods_{Guid.NewGuid()}.php");

        var profile = new ProfileInfo()
        {
            Name = name ?? Path.GetFileNameWithoutExtension(profilePath),
            Description = description ?? "",
            Path = profilePath,
            Content = _parser.GenerateModsPhp([]),
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        };
        using var db = _dbContextFactory.CreateDbContext();
        db.Add(profile);
        db.SaveChanges();
        return profile;
    }
}
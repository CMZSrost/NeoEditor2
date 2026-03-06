// Services/ModManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dock.Model.Core;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.ViewModels;

namespace NeoEditor.Services;

public interface IModManager
{
    Task ImportModAsync(string modFullPath);
    Task CreateModAsync(string name, string author);
    Task DeleteMod(string name, string author);
    Task DeleteMod(ModInfo modInfo);
    Task DeleteMod(string modPath);
}

public class ModManager : IModManager
{
    private readonly IDbContextFactory<EditorDbContext> _dbContextFactory;
    private readonly PhpParser _parser;
    private readonly IConfigService _configService;

    private AppConfig Config => _configService.Config;
    private string ModPath => Path.Combine(Config.GameRootDir, "Mods");

    private const string DefaultModXml =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <pma_xml_export version="1.0">
            <database name="neogame">
            </database>
        </pma_xml_export>
        """;

    public ModManager(PhpParser parser, IDbContextFactory<EditorDbContext> dbContextFactory,
        IConfigService configService)
    {
        _parser = parser;
        _dbContextFactory = dbContextFactory;
        _configService = configService;
    }

    public void CreateDirectory(string dirPath)
    {
        if (Directory.Exists(dirPath))
            throw new InvalidOperationException($"目录已存在: {dirPath}");

        Directory.CreateDirectory(dirPath);
    }

    public void DeleteDirectory(string dirPath, bool recursive = true)
    {
        if (!Directory.Exists(dirPath))
        {
            Console.WriteLine($"目录不存在: {dirPath}");
            return;
        }

        Directory.Delete(dirPath, recursive: recursive);
    }

    public async Task ImportModAsync(string modFullPath)
    {
        modFullPath = modFullPath.Replace(Config.GameRootDir ?? "", "").Replace("\\", "/").TrimStart('/');
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            await dbContext.ModInfos.AddAsync(new ModInfo
            {
                Name = Path.GetFileName(modFullPath),
                Path = modFullPath,
                IsBase = false,
                LastImport = DateTime.Now
            });
            await dbContext.SaveChangesAsync();
            App.Notification!.ShowSuccess($"mod {modFullPath} imported");
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"mod {modFullPath} not imported: {e.Message}", "Import Warning");
        }
    }

    public async Task CreateModAsync(string name, string author)
    {
        var projectDir = Path.Combine(ModPath, author, name);
        CreateDirectory(projectDir);

        try
        {
            // 创建数据
            await using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                context.ModInfos.Add(new ModInfo
                {
                    Name = name,
                    IsBase = false,
                    LastImport = DateTime.Now,
                    Path = projectDir.Replace(Config.GameRootDir, "").Replace("\\", "/").TrimStart('/'),
                    LastModified = DateTime.Now,
                });
                await context.SaveChangesAsync();
            }

            var getImagesPath = Path.Combine(projectDir, "getimages.php");
            var getImagesContent = _parser.GenerateImagePhp([]);
            await File.WriteAllTextAsync(getImagesPath, getImagesContent, Encoding.UTF8);

            var modDataPath = Path.Combine(projectDir, "neogame.xml");
            await File.WriteAllTextAsync(modDataPath, DefaultModXml, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Directory.Delete(projectDir, true);
            throw;
        }
    }

    public Task DeleteMod(string name, string author) => DeleteMod(Path.Combine(ModPath, author, name));

    public async Task DeleteMod(ModInfo modInfo)
    {
        var projectPath = Path.Combine(Config.GameRootDir, modInfo.Path);
        DeleteDirectory(projectPath);
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.ModInfos.FindAsync(modInfo.ModId) is
            not { } mod)
            return;
        context.ModInfos.Remove(mod);
        await context.SaveChangesAsync();
    }

    public async Task DeleteMod(string projectPath)
    {
        DeleteDirectory(projectPath);
        var relativePath = projectPath.Replace(Config.GameRootDir ?? "", "").Replace("\\", "/").TrimStart('/');
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        if (await context.ModInfos.FirstOrDefaultAsync(m => m.Path == relativePath) is
            not { } mod)
            return;
        context.ModInfos.Remove(mod);
        await context.SaveChangesAsync();
    }
}
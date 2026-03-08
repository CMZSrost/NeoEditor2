using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Reflection;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.ViewModels;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace NeoEditor.Services;

public interface IModManager
{
    Task ImportModAsync(string modFullPath);
    Task LoadModAsync(ModInfo modInfo);
    Task CreateModAsync(string name, string author);
    Task DeleteMod(string name, string author);
    Task DeleteMod(ModInfo modInfo);
    Task DeleteMod(string modPath);
}

public class ModManager : IModManager
{
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;
    private readonly PhpParser _phpParser;
    private readonly XmlParser _xmlParser;
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

    public ModManager() : this(App.ServiceProvider.GetRequiredService<PhpParser>(),
        App.ServiceProvider.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>(),
        App.ServiceProvider.GetRequiredService<IConfigService>(),
        App.ServiceProvider.GetRequiredService<XmlParser>())
    {
    }

    public ModManager(PhpParser phpParser, IDbContextFactory<EditorDbContext> editorDbFactory,
        IDbContextFactory<GameDbContext> gameDbFactory,
        IConfigService configService, XmlParser xmlParser)
    {
        _phpParser = phpParser;
        _editorDbFactory = editorDbFactory;
        _gameDbFactory = gameDbFactory;
        _configService = configService;
        _xmlParser = xmlParser;
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
        try
        {
            var modInfo = new ModInfo
            {
                Name = Path.GetFileNameWithoutExtension(modFullPath),
                Path = modFullPath.Replace(Config.GameRootDir ?? "", "").Replace("\\", "/").TrimStart('/'),
                IsBase = false,
                LastImport = DateTime.Now
            };

            await using var dbContext = await _editorDbFactory.CreateDbContextAsync();
            await dbContext.ModInfos.AddAsync(modInfo);
            await dbContext.SaveChangesAsync();

            await LoadModAsync(modInfo);
            App.Notification!.ShowSuccess($"mod {modFullPath} imported");
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"mod {modFullPath} not imported: {e.Message}", "Import Warning");
        }
    }

    public async Task LoadModAsync(ModInfo modInfo)
    {
        var modFullPath = Path.Combine(Config.GameRootDir, modInfo.Path);
        if (!Directory.Exists(modFullPath))
        {
            App.Notification.ShowWarning($"mod path {modFullPath} not found");
            return;
        }

        try
        {
            // 遍历mod目录下的所有xml文件并导入到数据库中
            var xmlFilePaths = Directory.GetFiles(modFullPath, "*.xml", SearchOption.AllDirectories);

            await using var db = await _gameDbFactory.CreateDbContextAsync();
            foreach (var xmlPath in xmlFilePaths)
            {
                var doc = XDocument.Load(xmlPath);
                // var entities = _xmlParser.ImportEntities<AttackMode>(doc, modId, xmlPath);
                foreach (var gameType in Constants.GameTypes)
                {
                    var method = typeof(XmlParser).GetMethod(nameof(XmlParser.ImportEntities))
                        ?.MakeGenericMethod(gameType.Value);
                    if (method == null) continue;
                    var entities = method.Invoke(_xmlParser, new object[] { doc, modInfo.ModId, xmlPath });
                    if (entities == null) continue;
                    try
                    {
                        await db.DbBulkInsertOrUpdate(gameType.Value, entities);
                        Console.WriteLine(
                            $"load {entities.GetType()} {(entities as IList)?.Count} {gameType.Key} from {xmlPath} with modId {modInfo.ModId}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"load {gameType.Key} from {xmlPath} with modId {modInfo.ModId} failed: {e.Message} as {JsonConvert.SerializeObject(entities, Formatting.Indented)}");
                        throw;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            App.Notification!.ShowWarning($"load {modFullPath} failed: {e.Message}");
        }
    }

    public async Task CreateModAsync(string name, string author)
    {
        var projectDir = Path.Combine(ModPath, author, name);
        CreateDirectory(projectDir);

        try
        {
            // 创建数据
            await using (var context = await _editorDbFactory.CreateDbContextAsync())
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
            var getImagesContent = _phpParser.GenerateImagePhp([]);
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
        await using var context = await _editorDbFactory.CreateDbContextAsync();
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
        await using var context = await _editorDbFactory.CreateDbContextAsync();
        if (await context.ModInfos.FirstOrDefaultAsync(m => m.Path == relativePath) is
            not { } mod)
            return;
        context.ModInfos.Remove(mod);
        await context.SaveChangesAsync();
    }
}
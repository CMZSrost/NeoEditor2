using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    Task<ModInfo?> ImportModAsync(string modFullPath);
    Task LoadModAsync(ModInfo modInfo);
    Task CreateModAsync(string name, string author);
    Task DeleteMod(string name, string author);
    Task DeleteMod(ModInfo modInfo);
    Task DeleteMod(string modPath);
    Task ExportModToZipAsync(ModInfo modInfo, string outputPath);
    Task<ModInfo> ImportModFromZipAsync(string zipPath);
}

public class ModManager : IModManager
{
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;
    private readonly PhpParser _phpParser;
    private readonly IXmlParser _xmlParser;
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
        App.ServiceProvider.GetRequiredService<IXmlParser>())
    {
    }

    public ModManager(PhpParser phpParser, IDbContextFactory<EditorDbContext> editorDbFactory,
        IDbContextFactory<GameDbContext> gameDbFactory,
        IConfigService configService, IXmlParser xmlParser)
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
            Serilog.Log.Logger.Warning("目录不存在: {DirPath}", dirPath);
            return;
        }

        Directory.Delete(dirPath, recursive: recursive);
    }

    public async Task<ModInfo?> ImportModAsync(string modFullPath)
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
            return modInfo;
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"mod {modFullPath} not imported: {e.Message}", "Import Warning");
            return null;
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

        // Quick check: skip if data already loaded (check one table)
        await using var checkDb = await _gameDbFactory.CreateDbContextAsync();
        var existingCount = await checkDb.AttackModes.CountAsync(e => e.ModId == modInfo.ModId);
        if (existingCount > 0) return;

        try
        {
            // 遍历mod目录下的所有xml文件并导入到数据库中
            var xmlFilePaths = Directory.GetFiles(modFullPath, "*.xml", SearchOption.AllDirectories);

            await using var db = await _gameDbFactory.CreateDbContextAsync();
            foreach (var xmlPath in xmlFilePaths)
            {
                var doc = LoadXmlFile(xmlPath);
                // var entities = _xmlParser.ImportEntities<AttackMode>(doc, modId, xmlPath);
                foreach (var gameType in Constants.GameTypes)
                {
                    var method = typeof(IXmlParser).GetMethod(nameof(IXmlParser.ImportEntities))
                        ?.MakeGenericMethod(gameType.Value);
                    if (method == null) continue;
                    var entities = method.Invoke(_xmlParser, new object[] { doc, modInfo.ModId, xmlPath });
                    if (entities == null) continue;
                    try
                    {
                        await db.DbBulkInsertOrUpdate(gameType.Value, entities);
                        Serilog.Log.Logger.Information("[ModManager] load {Type} {Count} {EntityType} from {Path} modId={ModId}",
                            entities.GetType(), (entities as IList)?.Count, gameType.Key, xmlPath, modInfo.ModId);
                    }
                    catch (Exception e)
                    {
                        Serilog.Log.Logger.Error(e, "[ModManager] load {EntityType} from {Path} modId={ModId} failed",
                            gameType.Key, xmlPath, modInfo.ModId);
                        throw;
                    }
                }
            }

            // Update import timestamp
            await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
            if (await editorDb.ModInfos.FindAsync(modInfo.ModId) is { } mod)
            {
                mod.LastImport = DateTime.Now;
                await editorDb.SaveChangesAsync();
            }

            // Invalidate browser index so it gets rebuilt with the newly loaded data
            BrowserIndexService.Invalidate();
        }
        catch (Exception e)
        {
            Serilog.Log.Logger.Error(e, "ModManager operation failed");
            App.Notification!.ShowWarning($"load {modFullPath} failed: {e.Message}");
        }
    }

    /// <summary>Loads an XML file, fixing common encoding issues (e.g. "utf8" → "utf-8").</summary>
    private static XDocument LoadXmlFile(string path)
    {
        var text = File.ReadAllText(path);
        if (text.Contains("encoding=\"utf8\"", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("encoding=\"utf8\"", "encoding=\"utf-8\"", StringComparison.OrdinalIgnoreCase);
        return XDocument.Parse(text);
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
            Serilog.Log.Logger.Error(e, "ModManager operation failed");
            Directory.Delete(projectDir, true);
            throw;
        }
    }

    public Task DeleteMod(string name, string author) => DeleteMod(Path.Combine(ModPath, author, name));

    public async Task DeleteMod(ModInfo modInfo)
    {
        if (modInfo.IsBase)
            throw new InvalidOperationException("Cannot delete base game data.");
        var projectPath = Path.Combine(Config.GameRootDir, modInfo.Path);
        var normalizedDataPath = Path.GetFullPath(Path.Combine(Config.GameRootDir, "data"));
        if (string.Equals(Path.GetFullPath(projectPath), normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot delete the game data directory.");
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

    public Task ExportModToZipAsync(ModInfo modInfo, string outputPath)
    {
        var modDir = Path.GetFullPath(Path.Combine(Config.GameRootDir, modInfo.Path));
        if (!Directory.Exists(modDir))
            throw new DirectoryNotFoundException($"Mod directory not found: {modDir}");

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        ZipFile.CreateFromDirectory(modDir, outputPath, CompressionLevel.Optimal, false);
        return Task.CompletedTask;
    }

    public async Task<ModInfo> ImportModFromZipAsync(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Zip file not found: {zipPath}");

        var extractDir = Path.Combine(Path.GetTempPath(), $"mod_import_{Guid.NewGuid():N}");
        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var hasData = Directory.GetFiles(extractDir, "neogame.xml", SearchOption.AllDirectories).Length > 0
                       || Directory.GetFiles(extractDir, "*.xml", SearchOption.AllDirectories).Length > 0;
            var hasGetmods = File.Exists(Path.Combine(extractDir, "getmods.php"));
            var hasGetimages = File.Exists(Path.Combine(extractDir, "getimages.php"));

            if (!hasData && !hasGetmods)
                throw new InvalidOperationException("Zip does not contain valid mod data (no XML or PHP files found).");

            await ImportModAsync(extractDir);

            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var importedMod = await db.ModInfos
                .OrderByDescending(m => m.ModId)
                .FirstOrDefaultAsync();
            return importedMod!;
        }
        finally
        {
            try { Directory.Delete(extractDir, true); } catch { /* best effort */ }
        }
    }
}
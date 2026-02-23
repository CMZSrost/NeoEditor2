// Services/ProjectManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Model;

namespace NeoEditor.Services;

public interface IProjectManager
{
    Task<ProjectSettings> CreateProjectAsync(string name, string gameRoot);
    Task DeleteProjectAsync(string projectPath);
    Task<ProjectSettings> OpenProjectAsync(string projectPath);
    Task SaveProjectAsync(ProjectSettings settings);
    Task<List<ModEntry>> GetModsAsync(ProjectSettings settings);
    public List<ModInfo> GetFullModList(ProjectSettings settings, List<ModEntry> modEntries);
    public List<ModInfo> GetFullModList(ProjectSettings settings);
    public List<ModInfo> GetFullModList(string gameRootPath);
    Task SaveModsAsync(ProjectSettings settings, List<ModEntry> mods);
}

public class ProjectManager : IProjectManager
{
    private readonly IProjectDbContextFactory _dbContextFactory;
    private readonly GetModsParser _parser;
    private readonly string _workspaceRoot;

    public ProjectManager(GetModsParser parser, IProjectDbContextFactory dbContextFactory)
    {
        _parser = parser;
        _dbContextFactory = dbContextFactory;
        _workspaceRoot = Path.Combine(Directory.GetCurrentDirectory(), "Workspace");
        if (!Directory.Exists(_workspaceRoot))
            Directory.CreateDirectory(_workspaceRoot);
    }

    public async Task<ProjectSettings> CreateProjectAsync(string name, string gameRoot)
    {
        var projectDir = Path.Combine(_workspaceRoot, name);
        if (Directory.Exists(projectDir))
            throw new InvalidOperationException($"项目目录已存在: {projectDir}");

        Directory.CreateDirectory(projectDir);

        var settings = new ProjectSettings
        {
            Name = name,
            ProjectName = projectDir,
            DatabasePath = Path.Combine(projectDir, "data.db"),
            GameRootPath = gameRoot
        };

        try
        {
            // 创建数据库
            await using (var context = _dbContextFactory.CreateDbContext(settings.DatabasePath))
            {
                await context.Database.EnsureCreatedAsync();
            }

            // 创建默认的 getmods.php
            var getModsPath = Path.Combine(projectDir, "getmods.php");
            var emptyMods = new List<ModEntry>();
            var getModsContent = _parser.Generate(emptyMods);
            await File.WriteAllTextAsync(getModsPath, getModsContent, Encoding.UTF8);

            // 保存项目配置文件
            var settingsPath = Path.Combine(projectDir, $"{name}.neproj");
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(settingsPath, json, Encoding.UTF8);

            return settings;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Directory.Delete(projectDir, true);
            throw;
        }
    }

    public async Task DeleteProjectAsync(string projectPath)
    {
        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException($"项目目录不存在: {projectPath}");

        Directory.Delete(projectPath, true);
        await Task.CompletedTask;
    }

    public async Task<ProjectSettings> OpenProjectAsync(string projectPath)
    {
        if (Directory.Exists(projectPath))
        {
            var files = Directory.GetFiles(projectPath, "*.neproj");
            if (files.Length == 0)
                throw new FileNotFoundException("在指定目录中未找到项目文件 (.neproj)");
            if (files.Length > 1)
                throw new InvalidOperationException("指定目录中存在多个项目文件，请指定具体文件路径");
            projectPath = files[0];
        }

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"项目文件不存在: {projectPath}");

        var json = await File.ReadAllTextAsync(projectPath, Encoding.UTF8);
        var settings = JsonSerializer.Deserialize<ProjectSettings>(json);
        settings.ProjectName = Path.GetDirectoryName(projectPath);
        // 确保 DatabasePath 是绝对路径
        if (!Path.IsPathRooted(settings.DatabasePath))
            settings.DatabasePath = Path.Combine(settings.ProjectName, settings.DatabasePath);

        var getModsPath = Path.Combine(settings.ProjectName, "getmods.php");
        if (!File.Exists(getModsPath))
            throw new FileNotFoundException("项目中缺少 getmods.php 文件");

        return settings;
    }

    public async Task SaveProjectAsync(ProjectSettings settings)
    {
        var settingsPath = Path.Combine(settings.ProjectName, $"{settings.Name}.neproj");
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(settingsPath, json, Encoding.UTF8);
    }

    public async Task<List<ModEntry>> GetModsAsync(ProjectSettings settings)
    {
        var getModsPath = Path.Combine(settings.ProjectName, "getmods.php");
        var content = await File.ReadAllTextAsync(getModsPath, Encoding.UTF8);
        return _parser.Parse(content);
    }

    public async Task SaveModsAsync(ProjectSettings settings, List<ModEntry> mods)
    {
        var getModsPath = Path.Combine(settings.ProjectName, "getmods.php");
        var content = _parser.Generate(mods);
        await File.WriteAllTextAsync(getModsPath, content, Encoding.UTF8);
    }

    public List<ModInfo> GetFullModList(ProjectSettings settings, List<ModEntry> modEntries)
    {
        var result = new List<ModInfo>();
        var loadOrder = 0;

        var baseDataPath = Path.Combine(settings.GameRootPath, "Data");
        if (Directory.Exists(baseDataPath))
            result.Add(new ModInfo
            {
                Name = "BaseGame",
                Path = baseDataPath,
                // LoadOrder = loadOrder++,
                // Type = ModType.Merge,
                IsBase = true
            });

        foreach (var entry in modEntries)
        {
            string fullPath;
            if (Path.IsPathRooted(entry.Path))
                fullPath = entry.Path;
            else
                fullPath = Path.Combine(settings.GameRootPath, entry.Path);

            result.Add(new ModInfo
            {
                Name = entry.Name,
                Path = fullPath,
                // LoadOrder = loadOrder++,
                // Type = entry.Type,
                IsBase = false
            });
        }

        return result;
    }

    public List<ModInfo> GetFullModList(ProjectSettings settings) => GetFullModList(settings.GameRootPath);

    public List<ModInfo> GetFullModList(string gameRootPath)
    {
        var result = new List<ModInfo>();
        var loadOrder = 0;

        var baseDataPath = Path.Combine(gameRootPath, "Data");
        if (Directory.Exists(baseDataPath))
            result.Add(new ModInfo
            {
                Name = "BaseGame",
                Path = baseDataPath,
                // LoadOrder = loadOrder++,
                // Type = ModType.Insert,
                IsBase = true
            });
        foreach (var entry in _parser.Parse(File.ReadAllText(Path.Combine(gameRootPath, "getmods.php"), Encoding.UTF8)))
        {
            string fullPath;
            if (Path.IsPathRooted(entry.Path))
                fullPath = entry.Path;
            else
                fullPath = Path.Combine(gameRootPath, entry.Path);

            result.Add(new ModInfo
            {
                Name = entry.Name,
                Path = fullPath,
                // LoadOrder = loadOrder++,
                // Type = entry.Type,
                IsBase = false
            });
        }

        return result;
    }
}
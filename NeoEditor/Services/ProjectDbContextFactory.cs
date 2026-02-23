using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Context;
using NeoEditor.Data.DTO;

namespace NeoEditor.Services;

public interface IProjectDbContextFactory
{
    /// <summary>
    ///     根据数据库文件路径创建 DbContext 实例
    /// </summary>
    ModEditorDbContext CreateDbContext(string databasePath);

    /// <summary>
    ///     根据项目设置创建 DbContext 实例（使用 settings.DatabasePath）
    /// </summary>
    ModEditorDbContext CreateDbContext(ProjectSettings settings);
}

public class ProjectDbContextFactory : IProjectDbContextFactory
{
    public ModEditorDbContext CreateDbContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<ModEditorDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .EnableDetailedErrors()
            .LogTo(Console.WriteLine, LogLevel.Information)
            .Options;
        return new ModEditorDbContext(options);
    }

    public ModEditorDbContext CreateDbContext(ProjectSettings settings)
    {
        return CreateDbContext(settings.DatabasePath);
    }
}
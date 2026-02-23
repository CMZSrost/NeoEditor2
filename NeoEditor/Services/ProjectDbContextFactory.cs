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
    GameDbContext CreateDbContext(string databasePath);

    /// <summary>
    ///     根据项目设置创建 DbContext 实例（使用 settings.DatabasePath）
    /// </summary>
    GameDbContext CreateDbContext(ProjectSettings settings);
}

public class ProjectDbContextFactory : IProjectDbContextFactory
{
    public GameDbContext CreateDbContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .EnableDetailedErrors()
            .LogTo(Console.WriteLine, LogLevel.Information)
            .Options;
        return new GameDbContext(options);
    }

    public GameDbContext CreateDbContext(ProjectSettings settings)
    {
        return CreateDbContext(settings.DatabasePath);
    }
}
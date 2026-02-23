using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Context;

public class EditorDbContext : DbContext
{
    public EditorDbContext(DbContextOptions<EditorDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModInfo> ModInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 单独配置 ModInfo
        modelBuilder.Entity<ModInfo>(e =>
        {
            e.HasKey(m => m.ModId);
            e.Property(m => m.Name).IsRequired();
            e.HasIndex(m => m.Path).IsUnique(); // 以Path作为业务唯一键
            e.Property(m => m.LastImport).ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(m => m.LastModified).ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
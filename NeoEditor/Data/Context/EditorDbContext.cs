using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.Context;

public class EditorDbContext : DbContext
{
    public EditorDbContext(DbContextOptions<EditorDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModInfo> ModInfos { get; set; }
    public DbSet<ProfileInfo> ProfileInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 单独配置 ModInfo
        modelBuilder.Entity<ModInfo>(e =>
        {
            e.HasKey(m => m.ModId);
            e.Property(m => m.Name).IsRequired();
            e.HasIndex(m => m.Path).IsUnique(); // 以Path作为业务唯一键
            e.Property(m => m.LastImport).ValueGeneratedOnAdd();
            e.Property(m => m.LastModified).ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<ProfileInfo>(e =>
        {
            e.HasKey(m => m.ProfileId);
            e.Property(m => m.Name).IsRequired();
            e.HasIndex(m => m.Path).IsUnique(); // 以Path作为业务唯一键
            e.Property(m => m.UpdateTime).ValueGeneratedOnAddOrUpdate().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(m => m.CreateTime).ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
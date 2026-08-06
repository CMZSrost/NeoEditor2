using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Model;
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
    public DbSet<CommandLog> CommandLogs { get; set; }
    public DbSet<WorkspaceSnapshot> WorkspaceSnapshots { get; set; }
    public DbSet<PendingExport> PendingExports { get; set; } // Docs/41: "edited, not yet exported" set
    public DbSet<ProfileEdit> ProfileEdits { get; set; } // Docs/41 追修(C): per-profile edit overlay

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Docs/41: pending-export markers — one row per (ModId, EntityId, ColumnName);
        // a mod entity may have several edited columns, each its own marker row.
        modelBuilder.Entity<PendingExport>(e =>
        {
            e.HasIndex(p => new { p.ModId, p.EntityId, p.ColumnName })
                .HasDatabaseName("IX_pending_export_ModId_EntityId_ColumnName")
                .IsUnique();
            e.ToTable("pending_export");
        });

        // Docs/41 追修(C): per-profile edit overlay — one row per (ProfileId, EntityId, ColumnName).
        modelBuilder.Entity<ProfileEdit>(e =>
        {
            e.HasIndex(p => new { p.ProfileId, p.EntityId, p.ColumnName }).IsUnique();
            e.ToTable("profile_edits");
        });

        // 单独配置 ModInfo
        modelBuilder.Entity<ModInfo>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).ValueGeneratedOnAdd(); // DB 自增
            e.HasIndex(m => m.ModId).IsUnique(); // 业务唯一键：编排顺序
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
        modelBuilder.Entity<CommandLog>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.TargetType).IsRequired();
            e.Property(c => c.TargetId).IsRequired();
            e.HasIndex(c => new { c.TargetType, c.TargetId });
        });
        modelBuilder.Entity<WorkspaceSnapshot>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => new { w.TargetType, w.TargetId }).IsUnique();
        });
    }
}
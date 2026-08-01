using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model;

[Table("mod_info")]
public class ModInfo
{
    [Key]
    [Column("Id")]
    [Display(Name = "Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // DB 自增主键（纯数据库概念，不参与业务逻辑）

    [Column("ModId")]
    [Display(Name = "ModId")]
    [Required]
    public int ModId { get; set; } // Profile 编排顺序：-1 = 游戏基础数据，>=0 = Mod

    [Column("Name", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Display(Name = "Name")]
    [Required]
    public string Name { get; set; } // Mod名称，如 "NSEg"

    [Column("Path", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Required]
    [Display(Name = "Path")]
    public string Path { get; set; } // 相对或绝对路径

    [Column("IsBase")]
    [Display(Name = "IsBase")]
    public bool IsBase { get; set; } // 是否为基础数据（不可编辑）

    [Column("LastModified", TypeName = "datetime")]
    [Display(Name = "LastModified")]
    public DateTime LastModified { get; set; } // 编辑器保存至DB的时间点

    [Column("LastImport", TypeName = "datetime")]
    [Display(Name = "LastImport")]
    public DateTime? LastImport { get; set; } // 从XML导入至DB的时间点

    /// <summary>DB中是否有未导出到XML的改动</summary>
    [NotMapped]
    public bool IsDirty => LastModified > (LastImport ?? DateTime.MinValue);

    /// <summary>WAL中是否有未保存的编辑（command_log sequence > snapshot）</summary>
    [NotMapped]
    public bool HasUnsavedEdits { get; set; }

    [NotMapped] public ObservableCollection<string> XmlFilePaths { get; set; } = [];
    [NotMapped] public bool XmlFilePathsLoaded { get; set; }
}

public class ModLoadInfo
{
    public ModType Type { get; set; }
    public ModInfo Info { get; set; } = null!;
    public string? Namespace { get; set; } // strModName from getmods.php, e.g. "NSEb" or "0"
}

// public class ModIndexInfo
// {
//     public string? FilePath { get; set; } // 文件路径
//     public List<ModLoadInfo> Mods { get; set; } = []; // 包含的Mod列表
// }

public enum ModType
{
    Insert, // 插入模式，主键自增
    Merge, // 合并模式，使用原有主键覆盖
    Unknown, // 未知状态，未导入过
}
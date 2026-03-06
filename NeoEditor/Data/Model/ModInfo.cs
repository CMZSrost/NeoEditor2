using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model;

[Table("mod_info")]
public class ModInfo
{
    [Key]
    [Column("ModId")]
    [Display(Name = "ModId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ModId { get; set; } // 自增主键（数据库内部）

    [Column("Name", TypeName = "varchar(255)")]
    [StringLength(255)]
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
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime LastModified { get; set; } // 文件最后修改时间，记录从编辑器保存的时间点

    [Column("LastImport", TypeName = "datetime")]
    [Display(Name = "LastImport")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime? LastImport { get; set; } // 文件最后导入时间，记录从xml导入的时间点
}

public class ModLoadInfo
{
    public ModType Type { get; set; } // 文件路径
    public ModInfo Info { get; set; } = null!;
}

public class ModIndexInfo
{
    public string? FilePath { get; set; } // 文件路径
    public List<ModLoadInfo> Mods { get; set; } = []; // 包含的Mod列表
}

public enum ModType
{
    Insert, // 插入模式，主键自增
    Merge, // 合并模式，使用原有主键覆盖
    Unknown, // 未知状态，未导入过
}
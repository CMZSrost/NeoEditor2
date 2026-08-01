using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Model;

[Table("profile_info")]
public partial class ProfileInfo : ObservableObject
{
    [Key]
    [Column("ProfileId")]
    [Display(Name = "ProfileId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProfileId { get; set; } // 自增主键（数据库内部）

    [Column("Name", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Display(Name = "Name")]
    [Required]
    public string Name { get; set; } // 文件名称，如 "getmods.php"

    [Column("Description", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Display(Name = "Description")]
    [Required]
    public string Description { get; set; } = ""; // 用户自定义名称，如 "My Mod Profile"

    [Column("Path", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Required]
    [Display(Name = "Path")]
    public string Path { get; set; } = ""; // 相对或绝对路径

    [Column("IncludeGame")]
    [Display(Name = "IncludeGame")]
    public bool IncludeGame { get; set; } = true; // true = 加载 Game 数据（ModId=-1），false = 仅加载 mod（单 Mod profile）

    /// <summary>仅单 Mod profile 非空：该 profile 只含这一个 mod，且 IncludeGame=false。
    /// 非空时 WAL 持久化按 per-mod（("mod", modId)）目标，保证单 Mod 编辑重启后可恢复。</summary>
    [Column("SingleModId")]
    [Display(Name = "SingleModId")]
    public int? SingleModId { get; set; }

    [Column("Content", TypeName = "longtext")]
    [Required]
    [Display(Name = "ProfileContent")]
    public string Content
    {
        get;
        set => SetProperty(ref field, value);
    } // 相对或绝对路径

    [Column("CreateTime", TypeName = "datetime")]
    [Display(Name = "CreateTime")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTime { get; set; } // 文件最后修改时间，记录从编辑器保存的时间点

    [Column("UpdateTime", TypeName = "datetime")]
    [Display(Name = "UpdateTime")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; } // 文件最后导入时间，记录从xml导入的时间点

    [NotMapped] public ObservableCollection<ModLoadInfo> ModLoadInfos { get; set; } = [];

    /// <summary>该Profile下是否有Mod存在WAL未保存编辑</summary>
    [NotMapped]
    public bool HasUnsavedEdits { get; set; }
}

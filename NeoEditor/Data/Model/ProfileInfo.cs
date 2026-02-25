using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using NeoEditor.ViewModels;

namespace NeoEditor.Data.Model;

[Table("profile_info")]
[Index(nameof(Path), Name = "u_index_path", IsUnique = true)]
public partial class ProfileInfo: ObservableObject
{
    [Key]
    [Column("ProfileId")]
    [Display(Name = "ProfileId")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ProfileId { get; set; } // 自增主键（数据库内部）

    [Column("Name", TypeName = "varchar(255)")]
    [StringLength(255)]
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

    [Column("Content", TypeName = "longtext")]
    [Required]
    [Display(Name = "ProfileContent")]
    public string Content { get; set; } = ""; // 相对或绝对路径

    [Column("CreateTime", TypeName = "datetime")]
    [Display(Name = "CreateTime")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreateTime { get; set; } // 文件最后修改时间，记录从编辑器保存的时间点

    [Column("UpdateTime", TypeName = "datetime")]
    [Display(Name = "UpdateTime")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdateTime { get; set; } // 文件最后导入时间，记录从xml导入的时间点

    [NotMapped] [ObservableProperty] public partial ModIndexInfo? ModIndexInfo { get; set; }
}
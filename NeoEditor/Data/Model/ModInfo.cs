using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model;

[Table("mod_info")]
public class ModInfo
{
    [Key] public int Id { get; set; } // 自增主键（数据库内部）

    [Column("Name")] public string Name { get; set; } // Mod名称，如 "NSEg"

    [Column("Path")] public string Path { get; set; } // 相对或绝对路径

    [Column("LoadOrder")] public int LoadOrder { get; set; } // 加载顺序（0-based，越大优先级越高）

    [Column("Type")] public ModType Type { get; set; } // 插入或合并

    [Column("IsBase")] public bool IsBase { get; set; } // 是否为基础数据（不可编辑）

    [Column("LastModified", TypeName = "datetime")]
    public DateTime LastModified { get; set; } // 文件最后修改时间，用于增量导入
}

public enum ModType
{
    Insert, // 插入模式，主键自增
    Merge // 合并模式，使用原有主键覆盖
}
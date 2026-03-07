using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("datafiles")]
[Comment("电子产品里的数据文本 - 定义各种电子设备中存储的数据文件")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class DataFile : IEntity
{

    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("数据名称，如'数据库'、'文本文件'等")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    [Comment("数据详情，如'某人的地址簿'")]
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("fValue", TypeName = "float")]
    [Comment("该资料的价值")]
    [Display(Name = "Value")]
    public double Value { get; set; } = 0;

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该数据所调用的图片文件名")]
    [Display(Name = "Image")]
    public string Image { get; set; } = "";
}
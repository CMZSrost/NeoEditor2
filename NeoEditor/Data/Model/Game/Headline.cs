using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("headlines")]
[Comment("头版头条（报纸） - 定义游戏中可读取的新闻内容")]
public class Headline
{
    [Display(Name = "Headline_ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Headline_Id")]
    public int Id { get; set; }

    [Column("strHeadline", TypeName = "longtext")]
    [Comment("头版头条具体文本内容")]
    [Display(Name = "Headline_HeadlineText")]
    public string HeadlineText { get; set; } = "";
}
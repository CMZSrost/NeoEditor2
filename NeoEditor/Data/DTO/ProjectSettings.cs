using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.DTO;

public class ProjectSettings
{
    /// <summary>项目名称</summary>
    public string Name { get; set; }

    /// <summary>项目目录的完整路径</summary>
    public string ProjectName { get; set; }

    /// <summary>SQLite 数据库文件路径（相对于项目目录）</summary>
    public string DatabasePath { get; set; } = "game_runtime.db";

    /// <summary>游戏根目录（只读，用于引用基础数据）</summary>
    public string GameRootPath { get; set; }
}

public partial class ModEntry : ObservableObject
{
    private string _name = string.Empty;
    private string _path = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(Type));
            }
        }
    } // mod 名称，若为 "0" 表示合并模式

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    } // mod 路径（可能是相对路径）

    public ModType Type => Name == "0" ? ModType.Merge : ModType.Insert;
}
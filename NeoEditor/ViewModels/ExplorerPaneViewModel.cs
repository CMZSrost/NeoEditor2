using System.Collections.ObjectModel;

namespace NeoEditor.ViewModels;

public class FolderItem
{
    public string Name { get; set; }
    public ObservableCollection<FolderItem> Children { get; } = new();
}

public partial class ExplorerPaneViewModel : ViewModelBase
{
    public ObservableCollection<FolderItem> Folders { get; } = new();

    public ExplorerPaneViewModel()
    {
        // 模拟数据
        Folders.Add(new FolderItem
            { Name = "项目", Children = { new FolderItem { Name = "src" }, new FolderItem { Name = "docs" } } });
        Folders.Add(new FolderItem { Name = "输出" });
    }
}

public class SearchPaneViewModel : ViewModelBase
{
    public ObservableCollection<string> RecentSearches { get; } = new() { "Avalonia", "MVVM", "Sidebar" };
}

public class SettingsPaneViewModel : ViewModelBase
{
    // 设置选项
}
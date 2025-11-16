using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.ViewModels.Data;

public partial class FileSystemNodeViewModel : ObservableObject
{
    private readonly FileSystemViewModel _fileSystemViewModel;

    public FileSystemNodeViewModel(string path, FileSystemViewModel fileSystemViewModel)
    {
        Name = Path.GetFileName(path);
        FullPath = path;
        _fileSystemViewModel = fileSystemViewModel;
        IsDirectory = Directory.Exists(path);
        if (IsDirectory)
            Children.Add(null!); // 占位符，懒加载
    }

    [ObservableProperty] public partial bool IsExpanded { get; set; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public ObservableCollection<FileSystemNodeViewModel> Children { get; } = new();

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && IsDirectory && Children.Count == 1 && Children[0] == null) LoadChildrenAsync();
    }

    private async void LoadChildrenAsync()
    {
        Children.Clear();

        await Task.Run(() =>
        {
            var directories = Directory.GetDirectories(FullPath);
            var files = Directory.GetFiles(FullPath);

            // 分批加载，避免UI线程阻塞
            const int batchSize = 5;
            var allItems = directories.Concat(files).ToList();

            for (var i = 0; i < allItems.Count; i += batchSize)
            {
                var batch = allItems.Skip(i).Take(batchSize);

                // 回到UI线程添加
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    foreach (var item in batch) Children.Add(new FileSystemNodeViewModel(item, _fileSystemViewModel));
                });

                // 给UI线程喘息机会
                if (i + batchSize < allItems.Count)
                    Thread.Sleep(10);
            }
        });
    }

    [RelayCommand]
    private void Open()
    {
        if (!IsDirectory)
            // 调用自定义打开函数
            _fileSystemViewModel.OpenFile(FullPath);
    }

    ~FileSystemNodeViewModel()
    {
        // 清理子节点
        Children.Clear();
    }
}
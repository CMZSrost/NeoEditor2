using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NeoEditor.Player.Core.ViewModels;
using NeoEditor.Player.Services;

namespace NeoEditor.Player.Views;

/// <summary>
/// R47 存档修改工具（节点编辑器）：存档管理「修改」按钮进入，预载该存档；
/// 树形展示结构（容器只读）+ 标量内联编辑，保存/另存为/保存并加载。
/// </summary>
public partial class SaveEditorWindow : Window
{
    public SaveEditorWindow()
    {
        InitializeComponent();
        // 「保存并加载」成功后 VM 触发 SavedAndLoaded（宿主已重启游戏）→ 关闭本窗口。
        Opened += (_, _) =>
        {
            if (DataContext is SaveEditorViewModel vm)
                vm.SavedAndLoaded += () => Hide();
        };
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
        => (DataContext as SaveEditorViewModel)?.SaveCommand.Execute(null);

    /// <summary>另存为：弹输入框给新 key（默认「原名-copy」）。</summary>
    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SaveEditorViewModel vm || vm.SelectedSave is null) return;
        var key = await PromptDialogWindow.PromptAsync(this,
            LocalizationManager.Instance["SaveEditor.SaveAsTitle"],
            LocalizationManager.Instance["SaveEditor.SaveAsHint"],
            defaultValue: vm.SelectedSave.Key + "-copy",
            okText: LocalizationManager.Instance["SaveEditor.Save"],
            cancelText: LocalizationManager.Instance["Common.Cancel"]);
        if (key is null) return;
        await vm.SaveAsAsync(key);
    }

    private void OnSaveAndLoadClick(object? sender, RoutedEventArgs e)
        => (DataContext as SaveEditorViewModel)?.SaveAndLoadCommand.Execute(null);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.ViewModels;

namespace NeoEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider!.GetRequiredService<MainWindowViewModel>();
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // 假设 _localizationService 是注入的本地化服务实例
        var vm = (MainWindowViewModel)DataContext!;
        GenericDataGridHelper.ConfigureColumn(e, key => vm.Loc[key], typeof(GameVar));
    }
}
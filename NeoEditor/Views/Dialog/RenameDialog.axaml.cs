using NeoEditor.Services;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NeoEditor.Views.Dialog;

public partial class RenameDialog : Window
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    public LocalizationService Loc => App.Localizor;

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        NameBox.SelectAll();
        NameBox.Focus();
    }

    public Task<string?> ShowAsync(Window owner)
    {
        ShowDialog(owner);
        return _tcs.Task;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(NameBox.Text?.Trim());
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(null);
        Close();
    }
}

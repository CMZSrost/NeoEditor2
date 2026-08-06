using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NeoEditor.Player.Views;

/// <summary>
/// Small modal prompt (v2.41): confirm dialogs (message only, OK/Cancel) and named
/// input (backup naming) share one window. PromptAsync returns the entered text (or
/// "ok" in confirm mode), null when cancelled.
/// </summary>
public partial class PromptDialogWindow : Window
{
    public PromptDialogWindow()
    {
        InitializeComponent();
    }

    public static async Task<string?> PromptAsync(Window owner, string title, string? message,
        string? defaultValue = null, string okText = "OK", string cancelText = "Cancel")
    {
        var dialog = new PromptDialogWindow { Title = title };
        if (!string.IsNullOrEmpty(message))
        {
            dialog.MessageText.Text = message;
            dialog.MessageText.IsVisible = true;
        }
        if (defaultValue is not null)
        {
            dialog.InputBox.Text = defaultValue;
            dialog.InputBox.IsVisible = true;
        }
        dialog.OkButton.Content = okText;
        dialog.CancelButton.Content = cancelText;
        return await dialog.ShowDialog<string?>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(InputBox.IsVisible ? InputBox.Text : "ok");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnOkClick(sender, e);
    }
}

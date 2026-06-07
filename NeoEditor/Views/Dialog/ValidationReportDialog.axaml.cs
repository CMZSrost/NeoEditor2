using NeoEditor.Services;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NeoEditor.Data.Validation;

namespace NeoEditor.Views.Dialog;

public partial class ValidationReportDialog : Window
{
    private bool _proceed;
    public LocalizationService Loc => App.Localizor;

    public ValidationReportDialog()
    {
        InitializeComponent();
    }

    public ValidationReportDialog(ValidationReport report) : this()
    {
        SummaryText.Text = $"{report.ErrorCount} error(s), {report.WarningCount} warning(s) — loading details...";
        LoadProgress.IsVisible = true;
        ReportText.IsVisible = false;

        if (report.HasErrors)
        {
            ProceedBtn.IsEnabled = false;
            ProceedBtn.Content = "Fix errors to continue";
        }

        // Format text on background thread to avoid blocking UI
        Task.Run(() =>
        {
            var svc = new ValidationService();
            return svc.FormatReport(report);
        }).ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ReportText.Text = t.Result;
                SummaryText.Text = $"{report.ErrorCount} error(s), {report.WarningCount} warning(s)";
                LoadProgress.IsVisible = false;
                ReportText.IsVisible = true;
            });
        }, TaskScheduler.Default);
    }

    private void OnProceedClick(object? sender, RoutedEventArgs e)
    {
        _proceed = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    public static async Task<bool> ShowAsync(Window owner, ValidationReport report)
    {
        var dialog = new ValidationReportDialog(report);
        await dialog.ShowDialog<bool?>(owner);
        return dialog._proceed;
    }
}

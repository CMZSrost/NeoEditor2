namespace NeoEditor.Infra.Services;

/// <summary>Cross-cutting notification service — extracted from App to Infra per M9 plugin migration.</summary>
public interface INotificationService
{
    void ShowSuccess(string message, string title = "Success");
    void ShowError(string message, string title = "Error");
    void ShowInfo(string message, string title = "Info");
    void ShowWarning(string message, string title = "Warning");
}

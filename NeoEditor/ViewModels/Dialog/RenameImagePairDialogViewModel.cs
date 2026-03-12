using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeoEditor.ViewModels.Dialog;

public partial class RenameImagePairDialogViewModel : ViewModelBase
{
    private string _imageDirectory = string.Empty;
    private string _currentNormalPath = string.Empty;
    private string _currentX2Path = string.Empty;
    private string _normalExtension = ".png";
    private string _x2Extension = ".png";

    [ObservableProperty] public partial string CurrentNormalFileName { get; set; } = string.Empty;
    [ObservableProperty] public partial string CurrentX2FileName { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewBaseName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProposedNormalFileName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProposedX2FileName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ValidationMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsConfirmed { get; set; }

    public EventHandler? CloseRequested;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public void Initialize(string imageDirectory, string currentNormalPath, string currentX2Path)
    {
        _imageDirectory = imageDirectory;
        _currentNormalPath = currentNormalPath;
        _currentX2Path = currentX2Path;
        _normalExtension = Path.GetExtension(currentNormalPath);
        _x2Extension = string.IsNullOrWhiteSpace(currentX2Path) ? _normalExtension : Path.GetExtension(currentX2Path);

        CurrentNormalFileName = Path.GetFileName(currentNormalPath);
        CurrentX2FileName = Path.GetFileName(currentX2Path);
        NewBaseName = Path.GetFileNameWithoutExtension(CurrentNormalFileName);
        IsConfirmed = false;
        UpdateValidation();
    }

    public (string NormalFileName, string X2FileName) GetProposedNames()
    {
        var normalizedBaseName = NormalizeBaseName(NewBaseName);
        return (
            $"{normalizedBaseName}{GetSafeExtension(_normalExtension)}",
            $"x2_{normalizedBaseName}{GetSafeExtension(_x2Extension)}");
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        UpdateValidation();
        if (HasValidationError)
        {
            return;
        }

        IsConfirmed = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanConfirm()
    {
        return !HasValidationError;
    }

    partial void OnNewBaseNameChanged(string value)
    {
        _ = value;
        UpdateValidation();
    }

    private void UpdateValidation()
    {
        var normalizedBaseName = NormalizeBaseName(NewBaseName);
        ProposedNormalFileName = string.Empty;
        ProposedX2FileName = string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedBaseName))
        {
            ValidationMessage = Loc["RenameImagePairNameRequired"];
            NotifyValidationStateChanged();
            return;
        }

        if (normalizedBaseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ValidationMessage = Loc["RenameImagePairInvalidName"];
            NotifyValidationStateChanged();
            return;
        }

        var proposedNames = GetProposedNames();
        ProposedNormalFileName = proposedNames.NormalFileName;
        ProposedX2FileName = proposedNames.X2FileName;

        var normalFileConflict = HasConflictingTarget(Path.Combine(_imageDirectory, proposedNames.NormalFileName),
            _currentNormalPath);
        var x2FileConflict = HasConflictingTarget(Path.Combine(_imageDirectory, proposedNames.X2FileName),
            _currentX2Path);
        if (normalFileConflict && x2FileConflict)
        {
            ValidationMessage = Loc["RenameImagePairBothFilesExist", proposedNames.NormalFileName,
                proposedNames.X2FileName];
            NotifyValidationStateChanged();
            return;
        }

        if (normalFileConflict)
        {
            ValidationMessage = Loc["RenameImagePairNormalFileExists", proposedNames.NormalFileName];
            NotifyValidationStateChanged();
            return;
        }

        if (x2FileConflict)
        {
            ValidationMessage = Loc["RenameImagePairX2FileExists", proposedNames.X2FileName];
            NotifyValidationStateChanged();
            return;
        }

        ValidationMessage = string.Empty;
        NotifyValidationStateChanged();
    }

    private static bool HasConflictingTarget(string targetPath, string currentPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        var normalizedTarget = Path.GetFullPath(targetPath);
        var normalizedCurrent = string.IsNullOrWhiteSpace(currentPath) ? string.Empty : Path.GetFullPath(currentPath);
        return !string.Equals(normalizedTarget, normalizedCurrent, StringComparison.OrdinalIgnoreCase) &&
               File.Exists(normalizedTarget);
    }

    private static string NormalizeBaseName(string? baseName)
    {
        var normalized = Path.GetFileName(baseName?.Trim() ?? string.Empty);
        normalized = Path.GetFileNameWithoutExtension(normalized);
        if (normalized.StartsWith("x2_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..];
        }

        return normalized.Trim();
    }

    private static string GetSafeExtension(string? extension)
    {
        return string.IsNullOrWhiteSpace(extension) ? ".png" : extension;
    }

    private void NotifyValidationStateChanged()
    {
        OnPropertyChanged(nameof(HasValidationError));
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls.DataGridFiltering;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// F4.1 — Concrete implementations of ProDataGrid filter context interfaces.
/// These replace the hand-written ColumnFilterFlyout by plugging into ProDataGrid's
/// built-in filter editor templates (DataGridFilterTextEditorTemplate etc.).
/// </summary>

/// <summary>Minimal IEnumOption implementation.</summary>
public sealed class EnumOption : IEnumOption, INotifyPropertyChanged
{
    private bool _isSelected;

    public string Display { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public EnumOption(string display, bool isSelected = false)
    {
        Display = display;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>IFilterTextContext — text filter with optional operator dropdown (Contains, StartsWith, etc.).</summary>
public sealed class TextFilterContext : IFilterTextContext, INotifyPropertyChanged
{
    private string? _text = "";

    public string Label { get; }
    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }

    public string? Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public TextFilterContext(string label, Action<string?> apply, Action clear)
    {
        Label = label;
        ApplyCommand = new RelayCommand(() => apply(Text));
        ClearCommand = new RelayCommand(clear);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>IFilterNumberContext — numeric range filter (Min-Max / Between operator).</summary>
public sealed class NumberFilterContext : IFilterNumberContext, INotifyPropertyChanged
{
    private double? _minValue;
    private double? _maxValue;

    public string Label { get; }
    public double Minimum => double.MinValue;
    public double Maximum => double.MaxValue;
    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }

    public double? MinValue
    {
        get => _minValue;
        set
        {
            if (Nullable.Equals(_minValue, value)) return;
            _minValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinValue)));
        }
    }

    public double? MaxValue
    {
        get => _maxValue;
        set
        {
            if (Nullable.Equals(_maxValue, value)) return;
            _maxValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxValue)));
        }
    }

    public NumberFilterContext(string label, Action<double?, double?> apply, Action clear)
    {
        Label = label;
        ApplyCommand = new RelayCommand(() => apply(MinValue, MaxValue));
        ClearCommand = new RelayCommand(clear);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>IFilterEnumContext — enum / bool filter with multi-select checkbox list (In operator).</summary>
public sealed class EnumFilterContext : IFilterEnumContext, INotifyPropertyChanged
{
    public string Label { get; }
    public ObservableCollection<IEnumOption> Options { get; } = [];
    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }

    public EnumFilterContext(
        string label,
        IEnumerable<string> allOptions,
        IEnumerable<string>? selected,
        Action<IReadOnlyList<string>> apply,
        Action clear)
    {
        Label = label;
        var selectedSet = new HashSet<string>(selected ?? []);
        foreach (var opt in allOptions)
            Options.Add(new EnumOption(opt, selectedSet.Contains(opt)));

        ApplyCommand = new RelayCommand(() =>
        {
            var selectedItems = new List<string>();
            foreach (var opt in Options)
                if (opt.IsSelected)
                    selectedItems.Add(opt.Display);
            apply(selectedItems);
        });
        ClearCommand = new RelayCommand(clear);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>Lightweight ICommand for filter context apply/clear.</summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged;
}

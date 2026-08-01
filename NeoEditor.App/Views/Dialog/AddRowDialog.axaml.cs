using NeoEditor.Services;
using NeoEditor.Helper;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Views.Dialog;

public partial class AddRowDialog : Window
{
    public record Result(int ModId, string FilePath, IEntity? CopyFrom);
    public ILocalizationService Loc => ViewServices.Loc;

    private record CopyFromItem(string Display, IEntity Entity)
    {
        public override string ToString() => Display;
    }

    private Result? _result;
    private readonly Dictionary<int, List<string>> _filePathsByMod;
    private readonly IConfigService? _configService;
    private bool _showCopyFrom;

    public AddRowDialog()
    {
        InitializeComponent();
        _filePathsByMod = new();
    }

    public AddRowDialog(
        IEnumerable<ModLoadInfo> mods,
        Dictionary<int, List<string>> filePathsByMod,
        IEnumerable<object> sourceRows,
        IEntity? preselectedCopyFrom = null,
        bool showCopyFrom = true,
        IConfigService? configService = null) : this()
    {
        _filePathsByMod = filePathsByMod;
        _configService = configService;
        _showCopyFrom = showCopyFrom;

        ModComboBox.ItemsSource = new ObservableCollection<ModLoadInfo>(mods);
        ModComboBox.SelectionChanged += OnModSelectionChanged;

        _copyFromItems = sourceRows
            .OfType<IEntity>()
            .Select(e => new CopyFromItem(e.Subject, e))
            .ToList();
        SourceRowComboBox.ItemsSource = new ObservableCollection<CopyFromItem>(_copyFromItems);

        if (!showCopyFrom)
        {
            CopyFromLabel.IsVisible = false;
            SourceRowComboBox.IsVisible = false;
            Height = 200;
        }

        if (preselectedCopyFrom is not null)
        {
            var match = _copyFromItems.FirstOrDefault(c => c.Entity == preselectedCopyFrom);
            if (match is not null)
                SourceRowComboBox.SelectedItem = match;
        }

        // Default select first Merge mod, and populate its file paths
        if (ModComboBox.ItemsSource is ObservableCollection<ModLoadInfo> modList && modList.Count > 0)
        {
            ModComboBox.SelectedIndex = modList
                .Select((m, i) => (m, i))
                .Where(x => x.m.Type == ModType.Merge)
                .Select(x => (int?)x.i)
                .FirstOrDefault() ?? 0;
        }
    }

    private void OnModSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModComboBox.SelectedItem is not ModLoadInfo selectedMod) return;

        var paths = _filePathsByMod.TryGetValue(selectedMod.Info.ModId, out var p)
            ? p.Distinct().ToList()
            : [];

        if (paths.Count == 0)
        {
            // Fallback: construct absolute path from mod directory
            // Q7=C: _configService injected via factory
            var modDir = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(_configService!.Config.GameRootDir,
                    selectedMod.Info.Path ?? ""));
            paths.Add(System.IO.Path.Combine(modDir, "neogame.xml"));
        }

        FilePathComboBox.ItemsSource = new ObservableCollection<string>(paths);
        FilePathComboBox.Text = paths.First();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        var selectedMod = ModComboBox.SelectedItem as ModLoadInfo;
        if (selectedMod?.Info is null) return;

        var filePath = FilePathComboBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            // Show a brief validation hint — the label already shows "*" for required
            FilePathComboBox.Focus();
            return;
        }

        var copyFrom = (SourceRowComboBox.SelectedItem as CopyFromItem)?.Entity;

        _result = new Result(
            selectedMod.Info.ModId,
            filePath,
            copyFrom);

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private readonly List<CopyFromItem> _copyFromItems = [];

    /// <summary>Static factory (Q7=C). Preferred entry point for runtime callers.</summary>
    public static AddRowDialog Create(IConfigService configService,
        IEnumerable<ModLoadInfo> mods,
        Dictionary<int, List<string>> filePathsByMod,
        IEnumerable<object> sourceRows,
        IEntity? preselectedCopyFrom = null,
        bool showCopyFrom = true)
        => new(mods, filePathsByMod, sourceRows, preselectedCopyFrom,
            showCopyFrom: showCopyFrom, configService: configService);

    public static async Task<Result?> ShowAsync(Window owner,
        IConfigService configService,
        IEnumerable<ModLoadInfo> mods,
        Dictionary<int, List<string>> filePathsByMod,
        IEnumerable<object> sourceRows,
        IEntity? preselectedCopyFrom = null)
    {
        var dialog = Create(configService, mods, filePathsByMod, sourceRows, preselectedCopyFrom);
        var confirmed = await dialog.ShowDialog<bool?>(owner);
        return confirmed == true ? dialog._result : null;
    }

    /// <summary>Simple add: only mod + xmlPath, no copy from.</summary>
    public static async Task<Result?> ShowSimpleAsync(Window owner,
        IConfigService configService,
        IEnumerable<ModLoadInfo> mods,
        Dictionary<int, List<string>> filePathsByMod)
    {
        var dialog = Create(configService, mods, filePathsByMod, [], null, showCopyFrom: false);
        dialog.Title = "Add New Row";
        var confirmed = await dialog.ShowDialog<bool?>(owner);
        return confirmed == true ? dialog._result : null;
    }
}

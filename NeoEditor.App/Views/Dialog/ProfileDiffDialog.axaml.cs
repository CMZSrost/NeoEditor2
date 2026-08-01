using NeoEditor.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Model;
using NeoEditor.Helper;

namespace NeoEditor.Views.Dialog;

public partial class ProfileDiffDialog : Window
{
    public ILocalizationService Loc => ViewServices.Loc;

    public ProfileDiffDialog()
    {
        InitializeComponent();
    }

    public static async System.Threading.Tasks.Task ShowAsync(Window owner,
        ProfileInfo profileA, ProfileInfo profileB)
    {
        var dialog = new ProfileDiffDialog();
        dialog.ProfileAName.Text = profileA.Name;
        dialog.ProfileBName.Text = profileB.Name;

        var entriesA = ParseSafe(profileA.Content);
        var entriesB = ParseSafe(profileB.Content);

        var allNames = entriesA.Select(e => e.Name)
            .Union(entriesB.Select(e => e.Name))
            .OrderBy(n => n)
            .ToList();

        var dictA = entriesA.ToDictionary(e => e.Name, e => e.Path);
        var dictB = entriesB.ToDictionary(e => e.Name, e => e.Path);

        var rows = new ObservableCollection<DiffRow>();
        foreach (var name in allNames)
        {
            var inA = dictA.TryGetValue(name, out var pathA);
            var inB = dictB.TryGetValue(name, out var pathB);
            var statusA = inA ? pathA : "—";
            var statusB = inB ? pathB : "—";
            var modName = !inA ? $"{name} (B only)" : !inB ? $"{name} (A only)" : name;
            rows.Add(new DiffRow { ModName = modName, StatusA = statusA, StatusB = statusB, ModPath = pathA ?? pathB ?? "" });
        }

        dialog.DiffGrid.ItemsSource = rows;
        await dialog.ShowDialog(owner);
    }

    private static List<ModEntry> ParseSafe(string content)
    {
        try { return ViewServices.PhpParser.ParseModsContent(content); }
        catch { return []; }
    }

    public class DiffRow
    {
        public string ModName { get; set; } = "";
        public string StatusA { get; set; } = "";
        public string StatusB { get; set; } = "";
        public string ModPath { get; set; } = "";
    }
}

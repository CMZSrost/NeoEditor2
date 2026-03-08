using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls;

public partial class ModGameDataTabsView : UserControl
{
    private readonly IDbContextFactory<GameDbContext> _gameDbContextFactory;
    private readonly LocalizationService _loc;
    private int _loadVersion;

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>("ReadOnly");

    public static readonly StyledProperty<ModInfo?> ModInfoProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, ModInfo?>(nameof(ModInfo));

    public ModInfo? ModInfo
    {
        get => GetValue(ModInfoProperty);
        set => SetValue(ModInfoProperty, value);
    }

    public ObservableCollection<GameDataTypeTabItem> Tabs { get; } = [];

    public bool ReadOnly
    {
        get { return (bool)GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }

    public ModGameDataTabsView()
    {
        InitializeComponent();
        _gameDbContextFactory = App.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
        _loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModInfoProperty)
        {
            _ = ReloadTabsAsync(ModInfo);
        }
    }

    private async Task ReloadTabsAsync(ModInfo? modInfo)
    {
        var loadVersion = ++_loadVersion;
        Tabs.Clear();

        if (modInfo is null)
        {
            return;
        }

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();
        foreach (var (_, entityType) in Constants.GameTypes.OrderBy(x => x.Key))
        {
            var items = await LoadEntitiesByTypeAsync(db, entityType, modInfo.ModId);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            Tabs.Add(new GameDataTypeTabItem
            {
                EntityType = entityType,
                Header = BuildHeader(entityType, items.Count),
                ItemsSource = items
            });
        }
    }

    private string BuildHeader(Type entityType, int count)
    {
        var title = _loc[entityType.Name];
        return $"{title} ({count})";
    }

    private async Task<IReadOnlyList<object>> LoadEntitiesByTypeAsync(GameDbContext db, Type entityType, int modId)
    {
        var method = typeof(ModGameDataTabsView)
                         .GetMethod(nameof(LoadEntitiesByTypeTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");

        var task = method.Invoke(null, [db, modId]) as Task<IReadOnlyList<object>>;
        if (task == null)
        {
            throw new InvalidOperationException($"Loading entity type {entityType.Name} did not return a task.");
        }

        return await task;
    }

    private static async Task<IReadOnlyList<object>> LoadEntitiesByTypeTypedAsync<TEntity>(GameDbContext db, int modId)
        where TEntity : IEntity
    {
        return await db.Set<TEntity>()
            .Where(x => x.ModId == modId)
            .Cast<object>()
            .ToListAsync();
    }
}

public sealed class GameDataTypeTabItem
{
    public required Type EntityType { get; init; }
    public required string Header { get; init; }
    public required IEnumerable ItemsSource { get; init; }
}
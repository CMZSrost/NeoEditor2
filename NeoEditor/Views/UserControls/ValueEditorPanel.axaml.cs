using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class ValueEditorPanel : UserControl
{
    private readonly CustomEditorRegistry _editorRegistry;
    private readonly EntityVisualizerRegistry _visualizerRegistry;
    public LocalizationService Loc => App.Localizor;
    private Type? _currentEntityType;
    private IEntity? _currentEntity;
    private Control? _activeEditorControl;
    private DataGrid? _boundGrid;

    public ValueEditorPanel()
    {
        _editorRegistry = App.ServiceProvider!.GetRequiredService<CustomEditorRegistry>();
        _visualizerRegistry = App.ServiceProvider!.GetRequiredService<EntityVisualizerRegistry>();
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => WireToSiblingDataGrid());
        // Register for entity inspection requests (previously done by RightPanelView)
        var messenger = App.ServiceProvider!.GetRequiredService<IMessenger>();
        messenger.Register<VisualEditorRequestedMessage>(this, (_, m) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Show(m.EntityType, m.Entity));
        });
    }

    private void WireToSiblingDataGrid()
    {
        var parentGrid = this.Parent as Grid;
        var searchable = parentGrid?.Children.OfType<SearchableDataGrid>().FirstOrDefault();
        var dataGrid = searchable?.FindDescendantOfType<DataGrid>()
            ?? parentGrid?.FindDescendantOfType<DataGrid>();
        if (dataGrid is null || dataGrid == _boundGrid) return;
        if (_boundGrid is not null) _boundGrid.SelectionChanged -= OnGridSelectionChanged;
        _boundGrid = dataGrid;
        _boundGrid.SelectionChanged += OnGridSelectionChanged;

        var tabItem = DataContext as GameDataTypeTabItem;
        if (tabItem?.EntityType is { } entityType)
            Show(entityType, dataGrid.SelectedItem as IEntity);
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var tabItem = DataContext as GameDataTypeTabItem;
        if (tabItem?.EntityType is { } entityType)
            Show(entityType, (sender as DataGrid)?.SelectedItem as IEntity);
    }

    public void Show(Type entityType, IEntity? entity)
    {
        Placeholder.IsVisible = false;
        EditorHost.IsVisible = true;

        if (entity is null)
        {
            Placeholder.IsVisible = true;
            EditorHost.IsVisible = false;
            EditorTitle.Text = Loc["ValueEditorEmpty"];
            _currentEntityType = entityType;
            return;
        }

        // 1. Try EntityVisualizerRegistry for compact overview (new system)
        var visualizer = _visualizerRegistry.Get(entityType);
        if (visualizer is not null)
        {
            _currentEntityType = entityType;
            _currentEntity = entity;

            // Badge info: ModId:Name, MergedId, primary key
            var modName = GenericDataGridHelper.EntityModNames.TryGetValue(entity.EntityId, out var mn)
                ? mn : $"mod_{entity.ModId}";
            var mergedId = GenericDataGridHelper.EntityMergedIds.TryGetValue(entity.EntityId, out var mid)
                ? mid : -1;
            var pkProp = entity.GetType().GetProperty("Id") ?? entity.GetType().GetProperty("nID");
            var pkVal = pkProp?.GetValue(entity) is int pk ? pk : -1;
            EditorTitle.Text = $"{Loc["RightPanelEditor"]} - {entity.Subject ?? entityType.Name}  [mod={entity.ModId}:{modName} mid={mergedId} pk={pkVal} eid={entity.EntityId[..8]}]";

            var overview = visualizer.BuildOverview(entity);

            // Wrap with mod badge header so ALL entity types show ModId:ModName
            var wrapper = new StackPanel();
            wrapper.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 4),
                Margin = new Thickness(8, 6, 8, 2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = entity.Subject ?? entityType.Name,
                            FontWeight = FontWeight.Bold,
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new Border
                        {
                            CornerRadius = new CornerRadius(3),
                            Background = Brush.Parse(entity.ModId >= 10000 ? "#1B5E20" : "#1565C0"),
                            Padding = new Thickness(6, 2),
                            Child = new TextBlock
                            {
                                Text = $"{entity.ModId}:{modName}",
                                FontSize = 10,
                                Foreground = Brushes.White
                            }
                        },
                        new Border
                        {
                            CornerRadius = new CornerRadius(3),
                            Background = Brush.Parse("#E65100"),
                            Padding = new Thickness(5, 2),
                            Child = new TextBlock
                            {
                                Text = $"mid={mergedId}",
                                FontSize = 10,
                                Foreground = Brushes.White
                            }
                        },
                        new Border
                        {
                            CornerRadius = new CornerRadius(3),
                            Background = Brush.Parse("#6A1B9A"),
                            Padding = new Thickness(5, 2),
                            Child = new TextBlock
                            {
                                Text = $"pk={pkVal}",
                                FontSize = 10,
                                Foreground = Brushes.White
                            }
                        },
                        new Border
                        {
                            CornerRadius = new CornerRadius(3),
                            Background = Brush.Parse("#37474F"),
                            Padding = new Thickness(5, 2),
                            Child = new TextBlock
                            {
                                Text = entity.EntityId.Length > 10 ? entity.EntityId[..10] : entity.EntityId,
                                FontSize = 9,
                                Foreground = Brushes.White
                            }
                        }
                    }
                }
            });
            wrapper.Children.Add(overview);

            // Only wrap in ScrollViewer if the visualizer didn't already
            EditorHost.Content = overview is ScrollViewer ? wrapper :
                new ScrollViewer
                {
                    Content = wrapper,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
                };
            return;
        }

        // 2. Fall back to CustomEditorRegistry (legacy)
        if (_editorRegistry.TryGet(entityType, out var editor))
        {
            if (_currentEntityType != entityType)
            {
                _currentEntityType = entityType;
                _activeEditorControl = editor.CreateEditor();
                EditorHost.Content = _activeEditorControl;
            }
            if (_currentEntity != entity && _activeEditorControl is not null)
                editor.UpdateEntity(entity);
            _currentEntity = entity;
            var modName2 = GenericDataGridHelper.EntityModNames.TryGetValue(entity.EntityId, out var mn2)
                ? mn2 : $"mod_{entity.ModId}";
            var mergedId2 = GenericDataGridHelper.EntityMergedIds.TryGetValue(entity.EntityId, out var mid2)
                ? mid2 : -1;
            var pkProp2 = entity.GetType().GetProperty("Id") ?? entity.GetType().GetProperty("nID");
            var pkVal2 = pkProp2?.GetValue(entity) is int pk2 ? pk2 : -1;
            EditorTitle.Text = $"{editor.EditorName}  [mod={entity.ModId}:{modName2} mid={mergedId2} pk={pkVal2} eid={entity.EntityId[..8]}]";
        }
        else
        {
            _currentEntityType = entityType;
            _currentEntity = entity;
            Placeholder.IsVisible = true;
            EditorHost.IsVisible = false;
            EditorTitle.Text = $"{entityType.Name} (no editor)";
        }
    }

    public void Hide()
    {
        _currentEntityType = null;
        _currentEntity = null;
    }
}

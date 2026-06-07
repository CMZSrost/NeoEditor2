using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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
            EditorTitle.Text = Loc["RightPanelEditor"] + " - " + (entity.Subject ?? entityType.Name);
            var overview = visualizer.BuildOverview(entity);
            // Only wrap in ScrollViewer if the visualizer didn't already
            EditorHost.Content = overview is ScrollViewer ? overview :
                new ScrollViewer
                {
                    Content = overview,
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
                EditorTitle.Text = editor.EditorName;
            }
            if (_currentEntity != entity && _activeEditorControl is not null)
                editor.UpdateEntity(entity);
            _currentEntity = entity;
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

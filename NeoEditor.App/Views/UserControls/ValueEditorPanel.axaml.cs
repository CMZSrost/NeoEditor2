using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls;

public partial class ValueEditorPanel : UserControl
{
    private readonly EntityVisualizerRegistry _visualizerRegistry;
    public ILocalizationService Loc => ViewServices.Loc;
    private Type? _currentEntityType;
    private IEntity? _currentEntity;
    private Control? _activeEditorControl;

    public ValueEditorPanel()
    {
        _visualizerRegistry = ViewServices.VisualizerRegistry;
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
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

        // M3: EntityVisualizerRegistry covers all entity types (24 type-specific + 1 default)
        var visualizer = _visualizerRegistry.Get(entityType);
        if (visualizer is not null)
        {
            _currentEntityType = entityType;
            _currentEntity = entity;
            EditorTitle.Text = $"{Loc["RightPanelEditor"]} - {entity.Subject ?? entityType.Name}";
            EditorHost.Content = visualizer.BuildDetail(entity);
            return;
        }

        // No visualizer found (should not happen — DefaultEntityVisualizer covers IEntity)
        _currentEntityType = entityType;
        _currentEntity = entity;
        Placeholder.IsVisible = true;
        EditorHost.IsVisible = false;
        EditorTitle.Text = $"{entityType.Name} (no editor)";
    }

    public void Hide()
    {
        _currentEntityType = null;
        _currentEntity = null;
    }
}

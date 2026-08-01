using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using NeoEditor.Services;
using NeoEditor.UI.Common.Services;

namespace NeoEditor.Plugins.EntityEditor.Views;

public partial class EntityEditorView : UserControl
{
    private EntityEditorDocument? _lastDoc;
    private CancellationTokenSource? _xmlDebounce;

    /// <summary>Resolve service from DI container registered in App.</summary>
    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public EntityEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        XmlEditor.TextChanged += (_, _) =>
        {
            _xmlDebounce?.Cancel();
            _xmlDebounce = new CancellationTokenSource();
            var token = _xmlDebounce.Token;
            Task.Delay(150, token).ContinueWith(_ =>
            {
                if (!token.IsCancellationRequested && _lastDoc != null)
                    Dispatcher.UIThread.Post(() => _lastDoc.ApplyXmlToEntityCommand.Execute(null));
            }, token);
        };

        XmlEditor.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)
                && e.Key == Key.Z)
            {
                if (XmlEditor.Document != null)
                    XmlEditor.Document.UndoStack.Redo();
                e.Handled = true;
            }
        };

        XmlEditor.GotFocus += (_, _) =>
        {
            if (_lastDoc != null) _lastDoc.IsXmlFocused = true;
        };
        XmlEditor.LostFocus += (_, _) =>
        {
            if (_lastDoc != null) _lastDoc.IsXmlFocused = false;
            FlushXmlChanges();
        };

        GotFocus += (_, _) => NotifyActiveEntity();
        PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && IsVisible) NotifyActiveEntity(); };
        PointerPressed += (_, _) => NotifyActiveEntity();
        AttachedToVisualTree += (_, _) => NotifyActiveEntity();
    }

    private void NotifyActiveEntity()
    {
        if (_lastDoc?.Entity != null)
        {
            WeakReferenceMessenger.Default.Send(new ActiveEntityChangedMessage(_lastDoc.Entity));
            var selectionService = GetService<ISelectionService>();
            selectionService.SetCurrentEntity(_lastDoc.Entity);
        }
    }

    private void FlushXmlChanges()
    {
        _xmlDebounce?.Cancel();
        _xmlDebounce = null;
        if (_lastDoc?.Entity != null)
        {
            _lastDoc.ApplyXmlToEntityCommand.Execute(null);
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        FlushXmlChanges();

        if (_lastDoc != null)
            _lastDoc.PropertyChanged -= OnDocPropertyChanged;
        _lastDoc = DataContext as EntityEditorDocument;
        if (_lastDoc != null)
        {
            _lastDoc.PropertyChanged += OnDocPropertyChanged;
            if (_lastDoc.Entity != null)
                RebuildVisualizer(_lastDoc);
            EditorTabs.SelectedIndex = 0;
            BuildContextMenu(_lastDoc);
        }
    }

    private void BuildContextMenu(EntityEditorDocument doc)
    {
        var providers = doc.ContextActionProviders.ToList();
        if (providers.Count == 0)
        {
            Root.ContextMenu = null;
            return;
        }

        var contextMenu = new ContextMenu();
        foreach (var provider in providers)
        {
            var menuItem = new MenuItem { Header = provider.ActionLabel };
            var entityType = doc.Entity?.GetType().Name ?? "";
            var entityId = doc.Entity?.EntityId ?? "";
            menuItem.IsEnabled = provider.CanHandle(entityType);
            menuItem.Click += async (_, _) =>
            {
                var result = await provider.ExecuteAsync(entityType, entityId);
                var notif = GetService<INotificationService>();
                notif.ShowInfo(result, "Image Generation");
            };
            contextMenu.Items.Add(menuItem);
        }

        Root.ContextMenu = contextMenu;
    }

    private void OnDocPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EntityEditorDocument.Entity)
            && DataContext is EntityEditorDocument doc)
            RebuildVisualizer(doc);
    }

    public void RebuildVisualizer(EntityEditorDocument doc)
    {
        if (doc.Entity == null) return;
        try
        {
            var registry = GetService<EntityVisualizerRegistry>();
            var vis = registry.Get(doc.Entity.GetType());
            VisualizationHost.Content = vis?.BuildDetail(doc.Entity);
        }
        catch { VisualizationHost.Content = null; }
    }
}

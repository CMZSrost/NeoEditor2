using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
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
            // R30 (追修 7): only USER edits auto-apply. Programmatic text sets (initial
            // load, RefreshXml) fire TextChanged too — auto-applying there wrote spurious
            // WAL edits and marked the entity dirty on every open (dirty-on-open).
            if (_lastDoc == null || !_lastDoc.IsXmlFocused) return;
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
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) NotifyActiveEntity();
        };
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
        }
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
        catch
        {
            VisualizationHost.Content = null;
        }
    }
}
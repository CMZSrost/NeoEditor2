using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class DomainBrowserView : UserControl
{
    private EntityBrowserDocument? _doc;
    private IDocumentDock? _viewerDocDock;
    private bool _layoutInitialized;

    public DomainBrowserView()
    {
        InitializeComponent();
        EntityListBox.AddHandler(InputElement.PointerPressedEvent, OnEntityClicked,
            RoutingStrategies.Bubble, true);
        SearchBox.AddHandler(TextBox.TextChangedEvent, OnSearchTextChanged,
            RoutingStrategies.Bubble, true);
        // Handle DockControl re-load on tab-switch reattach
        ViewerDockControl.Loaded += (_, _) =>
        {
            if (_doc is not null)
            {
                Console.WriteLine("[DB] DockControl.Loaded triggered — scheduling layout restore");
                // Use Background priority to ensure DockControl's XAML layout is fully parsed
                Dispatcher.UIThread.Post(() => TryRestoreLayout(0), DispatcherPriority.Background);
            }
        };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Clear stale dock reference — will be re-found on next attach
        _viewerDocDock = null;
        // Mark layout as needing re-init on reattach
        _layoutInitialized = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_doc is not null)
        {
            // Use Background priority with retry — same as Loaded handler
            Dispatcher.UIThread.Post(() => TryRestoreLayout(0), DispatcherPriority.Background);
        }
    }

    private void RestoreDockLayout(bool forceReinit = false)
    {
        if (_doc is null) return;
        
        // Re-find DocumentDock after tab-switch detach/reattach
        _viewerDocDock = null;
        
        // Force reinitialize layout if needed
        if (forceReinit || !_layoutInitialized)
        {
            if (ViewerDockControl.Layout is not null)
            {
                _doc.DockFactory.InitLayout(ViewerDockControl.Layout);
                _layoutInitialized = true;
                Console.WriteLine("[DB] Layout forcefully reinitialized");
            }
        }
        
        FindDocumentDock();

        // If still not found but layout exists, try init then find again
        if (_viewerDocDock is null && ViewerDockControl.Layout is not null)
        {
            _doc.DockFactory.InitLayout(ViewerDockControl.Layout);
            _layoutInitialized = true;
            FindDocumentDock();
        }

        if (_viewerDocDock is IDockable dd)
            dd.Proportion = double.NaN;

        // Re-sync: if VisibleDockables is empty but we have ViewerTabs, recreate all
        var visibleCount = _viewerDocDock?.VisibleDockables?.Count ?? 0;
        if (_viewerDocDock is not null && _doc.ViewerTabs.Count > 0 && visibleCount == 0)
        {
            Console.WriteLine($"[DB] VisibleDockables empty after reattach, recreating all {_doc.ViewerTabs.Count} tabs");
            foreach (var tab in _doc.ViewerTabs)
                CreateDockDocument(tab);
        }
        else if (_viewerDocDock is not null && _doc.ViewerTabs.Count > 0)
        {
            var existingIds = _viewerDocDock.VisibleDockables?
                .Where(d => d.Context is EntityViewerDocument)
                .Select(d => ((EntityViewerDocument)d.Context!).Entity.EntityId)
                .ToHashSet() ?? new HashSet<string>();

            foreach (var tab in _doc.ViewerTabs)
            {
                if (existingIds.Contains(tab.Entity.EntityId)) continue;
                Console.WriteLine($"[DB] Re-syncing tab: {tab.Entity.EntityId}");
                CreateDockDocument(tab);
            }

            // Activate the currently selected tab
            if (_doc.SelectedViewerTab is { } selected)
            {
                var activeDock = _viewerDocDock.VisibleDockables?
                    .FirstOrDefault(d => d.Context == selected);
                if (activeDock is not null)
                {
                    _doc.DockFactory.SetActiveDockable(activeDock);
                    _doc.DockFactory.SetFocusedDockable(_viewerDocDock, activeDock);
                }
            }
        }

        ForceApplyTemplates(ViewerDockControl);
        ViewerDockControl.InvalidateMeasure();
        ViewerDockControl.InvalidateArrange();
        ViewerDockControl.UpdateLayout();
    }

    /// <summary>Retry-based layout restore — waits until DockControl.Layout is ready.</summary>
    private void TryRestoreLayout(int attempt)
    {
        if (_doc is null) return;
        // Already restored — skip
        if (_viewerDocDock is not null && _viewerDocDock.VisibleDockables?.Count > 0)
        {
            Console.WriteLine($"[DB] TryRestoreLayout: already restored, skipping");
            return;
        }

        if (ViewerDockControl.Layout is null)
        {
            if (attempt < 6)
            {
                Console.WriteLine($"[DB] TryRestoreLayout: layout null, retry {attempt + 1}/6");
                Dispatcher.UIThread.Post(() => TryRestoreLayout(attempt + 1), DispatcherPriority.Background);
            }
            else
            {
                Console.WriteLine("[DB] TryRestoreLayout: gave up after 6 attempts — layout still null");
            }
            return;
        }

        Console.WriteLine($"[DB] TryRestoreLayout: layout ready at attempt={attempt}, restoring");
        RestoreDockLayout(forceReinit: true);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_doc is not null)
            _doc.FilterText = SearchBox.Text ?? "";
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_doc is not null) return;
        _doc = DataContext as EntityBrowserDocument;
        Console.WriteLine($"[DB] OnDataContextChanged: type={DataContext?.GetType().Name ?? "null"}");

        if (_doc is not null)
        {
            // Manually initialize factory + layout once (InitializeFactory=False, InitializeLayout=False in XAML)
            if (!_layoutInitialized && ViewerDockControl.Layout is not null)
            {
                _doc.DockFactory.InitLayout(ViewerDockControl.Layout);
                _layoutInitialized = true;
            }
            // Find the DocumentDock from XAML-declared layout
            Dispatcher.UIThread.Post(FindDocumentDock, DispatcherPriority.Loaded);
        }
    }

    private void FindDocumentDock()
    {
        if (_doc is null) return;

        // Walk the Layout tree to find the DocumentDock
        var layout = ViewerDockControl.Layout;
        if (layout is not null)
        {
            _viewerDocDock = FindDockById(layout, "ViewerDocumentsPane") as IDocumentDock;
        }

        // CRITICAL FIX: Set Proportion to NaN so ProportionalStackPanel auto-assigns it.
        if (_viewerDocDock is IDockable dockable)
        {
            dockable.Proportion = double.NaN;
        }

        // Wire closing event to clean up ViewerTabs when user closes a dock document
        if (_doc is not null)
        {
            _doc.DockFactory.DockableClosing += (_, args) =>
            {
                if (args.Dockable?.Context is EntityViewerDocument evd)
                {
                    _doc.ViewerTabs.Remove(evd);
                    Console.WriteLine($"[DB] Removed from ViewerTabs: {evd.Entity.EntityId}");
                }
            };
        }

        Console.WriteLine($"[DB] FindDocumentDock: found={_viewerDocDock is not null}, Layout={layout?.Id}");
        Dispatcher.UIThread.Post(LogVisualTree, DispatcherPriority.Render);
    }

    private IDock? FindDockById(IDockable dockable, string id)
    {
        if (dockable.Id == id && dockable is IDock d) return d;
        if (dockable is IDock dock && dock.VisibleDockables is not null)
        {
            foreach (var child in dock.VisibleDockables)
            {
                var found = FindDockById(child, id);
                if (found is not null) return found;
            }
        }

        return null;
    }

    private void OnEntityClicked(object? sender, PointerPressedEventArgs e)
    {
        if (_doc is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (EntityListBox.SelectedItem is not BrowserEntityRow row) return;

        // If Dock layout was lost (tab-switch), try to restore it first
        if (_viewerDocDock is null || _viewerDocDock.VisibleDockables?.Count == 0)
        {
            Console.WriteLine("[DB] OnEntityClicked: dock missing, attempting restore");
            TryRestoreLayout(0);
            // If still not ready, just return — layout will be restored async
            if (_viewerDocDock is null) return;
        }

        var existing = _doc.ViewerTabs.FirstOrDefault(d => d.Entity.EntityId == row.Entity.EntityId);
        if (existing is not null)
        {
            _doc.SelectedViewerTab = existing;
            Console.WriteLine($"[DB] Activate existing tab: {existing.Entity.EntityId}");

            // Try to find the dock document in current DocumentDock
            var dockDoc = _viewerDocDock?.VisibleDockables?
                .FirstOrDefault(d => d.Context == existing);
            if (dockDoc is not null)
            {
                _doc.DockFactory.SetActiveDockable(dockDoc);
                _doc.DockFactory.SetFocusedDockable(_viewerDocDock, dockDoc);
                return;
            }

            // dockDoc not found — tab-switch cleared visuals, re-create dock document
            Console.WriteLine($"[DB] existing tab lost visual dock, re-creating: {existing.Entity.EntityId}");
            CreateDockDocument(existing);
            return;
        }

        var newTab = new EntityViewerDocument(row.Entity);
        _doc.ViewerTabs.Add(newTab);
        _doc.SelectedViewerTab = newTab;
        Console.WriteLine($"[DB] Tab added: count={_doc.ViewerTabs.Count}, id={newTab.Entity.EntityId}");
        CreateDockDocument(newTab);

        Dispatcher.UIThread.Post(LogVisualTree, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(LogVisualTree, DispatcherPriority.Loaded);
    }

    private void CreateDockDocument(EntityViewerDocument tab)
    {
        if (_viewerDocDock is null) return;

        var dockDoc = new Dock.Model.Avalonia.Controls.Document
        {
            Id = $"viewer-{Guid.NewGuid():N}",
            Title = tab.Title,
            Context = tab,
            CanClose = true,
            Content = new ContentControl
            {
                [!ContentControl.ContentProperty] = new Binding("Context"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            }
        };

        _doc.DockFactory.AddDockable(_viewerDocDock, dockDoc);
        _doc.DockFactory.SetActiveDockable(dockDoc);
        _doc.DockFactory.SetFocusedDockable(_viewerDocDock, dockDoc);

        if (_viewerDocDock is IDockable dd)
            dd.Proportion = double.NaN;

        Console.WriteLine($"[DB] Dock doc created: VisibleDockables={_viewerDocDock.VisibleDockables?.Count}");

        Dispatcher.UIThread.Post(() =>
        {
            ForceApplyTemplates(ViewerDockControl);
            ViewerDockControl.UpdateLayout();
            Console.WriteLine($"[DB] Post-add layout forced");
        }, DispatcherPriority.Render);
    }

    private static void ForceApplyTemplates(Control? root)
    {
        if (root is null) return;
        foreach (var tc in root.GetVisualDescendants().OfType<TemplatedControl>())
            tc.ApplyTemplate();
    }

    private void LogVisualTree()
    {
        try
        {
            Console.WriteLine($"[DB] === VisualTree ===");

            // Log ALL DockControls found
            var dockCtrls = this.GetVisualDescendants().OfType<DockControl>().ToList();
            Console.WriteLine($"[DB]   DockControls: {dockCtrls.Count}");
            foreach (var dc in dockCtrls)
                Console.WriteLine($"[DB]     DC: Name={dc.Name}, Bounds={dc.Bounds}, Layout={dc.Layout?.Id}");

            var dockCtrl = dockCtrls.FirstOrDefault();
            if (dockCtrl?.Layout is IRootDock root && root.VisibleDockables is not null)
            {
                foreach (var d in root.VisibleDockables)
                    WalkDock(d, 1);
            }

            var psps = this.GetVisualDescendants()
                .OfType<Dock.Controls.ProportionalStackPanel.ProportionalStackPanel>().ToList();
            Console.WriteLine($"[DB]   ProportionalStackPanels: {psps.Count}");
            foreach (var psp in psps)
            {
                Console.WriteLine($"[DB]     PSP: Children={psp.Children.Count}, Bounds={psp.Bounds}");
                foreach (var child in psp.Children)
                {
                    var prop = Dock.Controls.ProportionalStackPanel.ProportionalStackPanel.GetProportion(child);
                    Console.WriteLine(
                        $"[DB]       child={child.GetType().Name}, Proportion={prop}, Bounds={child.Bounds}, DesiredSize={child.DesiredSize}");
                }
            }

            var evv = this.GetVisualDescendants().OfType<EntityViewerView>().FirstOrDefault();
            Console.WriteLine($"[DB]   EntityViewerView: Bounds={evv?.Bounds}, DesiredSize={evv?.DesiredSize}");

            if (evv is not null)
            {
                Console.WriteLine($"[DB]   --- Chain from EntityViewerView UP ---");
                Visual? current = evv;
                int depth = 0;
                while (current is not null && depth < 30)
                {
                    depth++;
                    if (current is Layoutable layout)
                        Console.WriteLine(
                            $"[DB]     [{depth}] {current.GetType().Name}: Bounds={layout.Bounds}, Desired={layout.DesiredSize}, HA={layout.HorizontalAlignment}, VA={layout.VerticalAlignment}");
                    else
                        Console.WriteLine($"[DB]     [{depth}] {current.GetType().Name}");
                    current = current.GetVisualParent() as Visual;
                }
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[DB] LogVisualTree ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void WalkDock(IDockable dockable, int depth)
    {
        var indent = new string(' ', depth * 2);
        if (dockable is IDock dock)
        {
            Console.WriteLine(
                $"[DB]   {indent}{dockable.GetType().Name}: Id={dockable.Id}, VisibleDockables={dock.VisibleDockables?.Count ?? 0}");
            if (dock.VisibleDockables is not null)
                foreach (var child in dock.VisibleDockables)
                    WalkDock(child, depth + 1);
        }
        else
        {
            Console.WriteLine(
                $"[DB]   {indent}{dockable.GetType().Name}: Id={dockable.Id}, Context={dockable.Context?.GetType().Name}");
        }
    }
}
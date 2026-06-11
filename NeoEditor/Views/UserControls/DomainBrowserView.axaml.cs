using System;
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

    public DomainBrowserView()
    {
        InitializeComponent();
        EntityListBox.AddHandler(InputElement.PointerPressedEvent, OnEntityClicked,
            RoutingStrategies.Bubble, true);
        SearchBox.AddHandler(TextBox.TextChangedEvent, OnSearchTextChanged,
            RoutingStrategies.Bubble, true);
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

        var existing = _doc.ViewerTabs.FirstOrDefault(d => d.Entity.EntityId == row.Entity.EntityId);
        if (existing is not null)
        {
            _doc.SelectedViewerTab = existing;
            Console.WriteLine($"[DB] Activate existing tab: {existing.Entity.EntityId}");

            if (_viewerDocDock?.VisibleDockables is not null)
            {
                var dockDoc = _viewerDocDock.VisibleDockables
                    .FirstOrDefault(d => d.Context == existing);
                if (dockDoc is not null)
                {
                    _doc.DockFactory.SetActiveDockable(dockDoc);
                    _doc.DockFactory.SetFocusedDockable(_viewerDocDock, dockDoc);
                }
            }

            return;
        }

        var newTab = new EntityViewerDocument(row.Entity);
        _doc.ViewerTabs.Add(newTab);
        _doc.SelectedViewerTab = newTab;
        Console.WriteLine($"[DB] Tab added: count={_doc.ViewerTabs.Count}, id={newTab.Entity.EntityId}");

        if (_viewerDocDock is not null)
        {
            // Create Document with Content that renders the Context
            var dockDoc = new Dock.Model.Avalonia.Controls.Document
            {
                Id = $"viewer-{_doc.ViewerTabs.Count}",
                Title = newTab.Title,
                Context = newTab,
                CanClose = true,
                // CRITICAL: Set Content to a ContentControl that binds to Context
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

            // Ensure DocumentDock.Proportion stays NaN so PSP re-assigns proportions
            if (_viewerDocDock is IDockable dd)
                dd.Proportion = double.NaN;

            Console.WriteLine($"[DB] Dock document added: VisibleDockables={_viewerDocDock.VisibleDockables?.Count}");

            // Force template application on all TemplatedControls and layout update
            // This is needed because the Dock library's ProportionalDockControl creates
            // containers with TemplatedControls (DocumentDockControl etc.) whose theme
            // templates may not be applied during the initial layout pass, causing
            // DesiredWidth=0 which propagates through ProportionalStackPanel.
            Dispatcher.UIThread.Post(() =>
            {
                ForceApplyTemplates(ViewerDockControl);
                ViewerDockControl.UpdateLayout();
                Console.WriteLine($"[DB] Post-add layout forced");
            }, DispatcherPriority.Render);
        }

        Dispatcher.UIThread.Post(LogVisualTree, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(LogVisualTree, DispatcherPriority.Loaded);
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
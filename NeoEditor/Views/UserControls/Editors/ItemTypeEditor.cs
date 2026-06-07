using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls.Editors;

public class ItemTypeEditor : ICustomTableEditor
{
    public Type EntityType => typeof(ItemType);
    public string EditorName => "Item Type Editor";
    private TabControl? _tabs;
    private readonly IImageService _imageService =
        App.ServiceProvider!.GetRequiredService<IImageService>();

    private static readonly Dictionary<int, string> SlotNames = new()
    { [20]="L-Hand", [21]="R-Hand", [22]="Back", [23]="Head", [14]="R-Shoulder", [17]="Face", [13]="L-Back", [11]="Torso", [4]="Legs", [2]="L-Foot", [3]="R-Foot" };

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return; _tabs.Items.Clear();
        if (entity is not ItemType item) return;
        _tabs.Items.Add(EditorHelper.BuildOverviewTab(item));
        if (!string.IsNullOrWhiteSpace(item.SpriteList))
            _tabs.Items.Add(EditorHelper.MakeTab("Sprite Show", BuildOverlayShow(item.SpriteList, "CreHuman.png", true)));
        if (!string.IsNullOrWhiteSpace(item.SpriteList))
            _tabs.Items.Add(EditorHelper.MakeTab("Wear Show", BuildWearShowTab(item)));
    }

    // ==================== Sprite overlay ====================
    private ScrollViewer BuildOverlayShow(string raw, string baseName, bool isSprite)
    {
        const int CW = 132, CH = 165;
        var items = new List<ImgSel>();
        foreach (var (slotStr, imgName) in ParseEntries(raw))
        {
            if (string.IsNullOrWhiteSpace(imgName)) continue;
            var resolved = StripNs(imgName);
            var p = _imageService.FindImage(resolved); if (p is null) continue;
            int.TryParse(slotStr, out var s);
            var lbl = isSprite ? $"{SlotNames.GetValueOrDefault(s, $"Slot{s}")} ({imgName})" : imgName;
            items.Add(new ImgSel { Label = lbl, Path = p, IsSelected = true });
        }
        return BuildDropdownShow(items, baseName, CW, CH);
    }

    private ScrollViewer BuildWearShowTab(ItemType item)
    {
        const int CW = 132, CH = 165;
        var items = new List<ImgSel>();
        if (!string.IsNullOrWhiteSpace(item.ImageList))
        {
            foreach (var name in item.ImageList.Split(',').Select(s => s.Trim()))
            {
                if (name.EndsWith("Stored.png", StringComparison.OrdinalIgnoreCase)) continue;
                var resolved = StripNs(name);
                var p = _imageService.FindImage(resolved);
                if (p is not null) items.Add(new ImgSel { Label = name, Path = p, IsSelected = true });
            }
        }
        return BuildDropdownShow(items, "btn_inv_body.png", CW, CH);
    }

    // ==================== Dropdown + canvas builder ====================
    private class ImgSel { public string Label { get; set; } = ""; public string Path { get; set; } = ""; public bool IsSelected { get; set; } }

    private ScrollViewer BuildDropdownShow(List<ImgSel> items, string baseName, int cw, int ch)
    {
        var canvas = new Canvas { Width = cw, Height = ch, Background = Brushes.Transparent };
        var viewbox = new Viewbox { Child = canvas, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, HorizontalAlignment = HorizontalAlignment.Stretch };
        var basePath = _imageService.FindImage(baseName);

        // Zoom/pan transforms
        var zoom = 1.0;
        var panX = 0.0;
        var panY = 0.0;
        var isPanning = false;
        var panOrigin = new Point();
        var panStartX = 0.0;
        var panStartY = 0.0;
        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform(0, 0);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        viewbox.RenderTransform = group;
        viewbox.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

        void Refresh()
        {
            canvas.Children.Clear();
            if (basePath is not null) try { canvas.Children.Add(MkImg(basePath, cw, ch)); } catch { }
            foreach (var it in items.Where(x => x.IsSelected))
                try { canvas.Children.Add(MkImg(it.Path, cw, ch)); } catch { }
        }
        Refresh();

        var countTb = new TextBlock { Text = $"{Path.GetFileNameWithoutExtension(baseName)} +{items.Count(i=>i.IsSelected)}/{items.Count}" };
        var flyout = new Flyout();
        var lb = new ListBox { ItemsSource = items, MaxHeight = 250 };
        var capturedItems = items;
        var capturedRefresh = (Action)Refresh;
        var capturedBaseName = baseName;
        lb.ItemTemplate = new FuncDataTemplate<ImgSel>((it, _) =>
        {
            if (it is null) return new TextBlock { Text = "..." };
            var hp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(4, 2) };
            var cb = new CheckBox { IsChecked = it.IsSelected };
            cb.IsCheckedChanged += (_, _) =>
            {
                it.IsSelected = cb.IsChecked == true;
                capturedRefresh();
                countTb.Text = $"{Path.GetFileNameWithoutExtension(capturedBaseName)} +{capturedItems.Count(i => i.IsSelected)}/{capturedItems.Count}";
            };
            hp.Children.Add(cb);
            hp.Children.Add(new TextBlock { Text = it.Label, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            return hp;
        });
        flyout.Content = lb;
        var btn = new Button { Content = countTb, Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Left };
        btn.Flyout = flyout;

        // Reset button
        var resetBtn = new Button
        {
            Content = "⟲", FontSize = 14, Padding = new Thickness(6, 2),
            Margin = new Thickness(2), VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(resetBtn, "Reset zoom/pan");
        resetBtn.Click += (_, _) =>
        {
            zoom = 1.0; panX = 0; panY = 0;
            scale.ScaleX = 1; scale.ScaleY = 1;
            translate.X = 0; translate.Y = 0;
        };

        // Toolbar: dropdown + reset inline
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        toolbar.Children.Add(btn);
        toolbar.Children.Add(resetBtn);

        var sp = new DockPanel();
        sp.Children.Add(new Border { Child = toolbar });
        DockPanel.SetDock(sp.Children[0], Avalonia.Controls.Dock.Top);
        sp.Children.Add(viewbox);

        var scroll = new ScrollViewer
        {
            Content = sp,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Zoom via mouse wheel (centered on cursor)
        scroll.PointerWheelChanged += (_, e) =>
        {
            var oldZoom = zoom;
            zoom *= e.Delta.Y > 0 ? 1.2 : 0.833;
            zoom = Math.Clamp(zoom, 0.1, 20.0);
            var pos = e.GetPosition(scroll);
            var ratio = zoom / oldZoom;
            panX = pos.X - ratio * (pos.X - panX);
            panY = pos.Y - ratio * (pos.Y - panY);
            scale.ScaleX = zoom;
            scale.ScaleY = zoom;
            translate.X = panX;
            translate.Y = panY;
            e.Handled = true;
        };

        // Pan via middle mouse or left-drag when zoomed — capture on ScrollViewer (always has bounds)
        void StartPan(object? snd, PointerPressedEventArgs e)
        {
            var pt = e.GetCurrentPoint(scroll);
            if (pt.Properties.IsMiddleButtonPressed || (pt.Properties.IsLeftButtonPressed && zoom > 1.0))
            {
                isPanning = true;
                panOrigin = e.GetPosition(scroll);
                panStartX = panX;
                panStartY = panY;
                e.Pointer.Capture(scroll);
                e.Handled = true;
            }
        }
        scroll.PointerPressed += StartPan;

        void DoMove(object? snd, PointerEventArgs e)
        {
            if (!isPanning) return;
            var pos = e.GetPosition(scroll);
            panX = panStartX + (pos.X - panOrigin.X);
            panY = panStartY + (pos.Y - panOrigin.Y);
            translate.X = panX;
            translate.Y = panY;
        }
        scroll.PointerMoved += DoMove;

        void EndPan(object? snd, PointerReleasedEventArgs e) { isPanning = false; e.Pointer.Capture(null); }
        scroll.PointerReleased += EndPan;

        return scroll;
    }

    private static Image MkImg(string path, int w, int h) => new() { Source = new Bitmap(path), Width = w, Height = h, Stretch = Stretch.Fill };

    // ==================== Helpers ====================
    private static List<(string Slot, string Img)> ParseEntries(string raw)
    {
        var r = new List<(string, string)>();
        foreach (var p in raw.Split(','))
        {
            var t = p.Trim(); if (string.IsNullOrWhiteSpace(t)) continue;
            var e = t.IndexOf('='); r.Add(e > 0 ? (t[..e].Trim(), t[(e + 1)..].Trim()) : ("?", t));
        }
        return r;
    }

    private static string StripNs(string name) { var c = name.IndexOf(':'); return c > 0 ? name[(c + 1)..] : name; }
}

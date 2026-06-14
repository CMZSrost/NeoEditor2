using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.Views.Dialog;

namespace NeoEditor.Views.UserControls.Editors;

/// <summary>Shared helpers for building visual editor tabs.</summary>
public static class EditorHelper
{
    private static LocalizationService? _loc;
    private static IImageService? _imgSvc;

    private static IImageService ImageService =>
        _imgSvc ??= App.ServiceProvider!.GetRequiredService<IImageService>();

    /// <summary>Build a TabItem containing an overview tree of all entity properties.</summary>
    public static TabItem BuildOverviewTab(IEntity entity)
    {
        _loc ??= App.ServiceProvider?.GetService<LocalizationService>();

        var tree = new TreeView();
        if (entity is null) return new TabItem { Header = "Overview", Content = tree };

        var entityType = entity.GetType();

        // Header with modId:modName badge first, then subject, then entityId badge
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var modName = Helper.GenericDataGridHelper.EntityModNames.TryGetValue(entity.EntityId, out var mn)
            ? mn : $"mod_{entity.ModId}";
        headerPanel.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#20000000"),
            Padding = new Thickness(5, 1),
            Child = new TextBlock { Text = $"{entity.ModId}:{modName}", FontSize = 9, Foreground = Brush.Parse("#888"), VerticalAlignment = VerticalAlignment.Center }
        });
        headerPanel.Children.Add(new TextBlock { Text = entity.Subject, FontWeight = FontWeight.Bold, Foreground = Brushes.DodgerBlue, VerticalAlignment = VerticalAlignment.Center });
        var eidShort = entity.EntityId.Length > 10 ? entity.EntityId[..10] + "…" : entity.EntityId;
        headerPanel.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#37474F"),
            Padding = new Thickness(5, 1),
            Child = new TextBlock { Text = eidShort, FontSize = 9, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
        });
        var root = new TreeViewItem { IsExpanded = true, Header = headerPanel };

        var props = entityType.GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken);

        foreach (var prop in props)
        {
            var val = prop.GetValue(entity);
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var colName = colAttr?.Name ?? prop.Name;
            var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";

            var displayName = prop.GetCustomAttribute<DisplayAttribute>()?.Name;
            var label = _loc is not null && displayName is not null ? _loc[displayName] : displayName ?? colName;

            if (refAttr is not null && !string.IsNullOrWhiteSpace(strVal))
            {
                var refNode = NewNode(label, Brushes.HotPink, true);
                BuildRefChildren(refNode, strVal, refAttr, entity.EntityId);
                root.Items.Add(refNode);
            }
            else if (!string.IsNullOrWhiteSpace(strVal))
            {
                var isImageField = colName.StartsWith("vImage", StringComparison.OrdinalIgnoreCase)
                                || colName.StartsWith("strImg", StringComparison.OrdinalIgnoreCase)
                                || colName.Contains("Sprite", StringComparison.OrdinalIgnoreCase);
                var isMapField = colName == "strDef" && entityType == typeof(Map);

                var node = NewNode($"{label}: {(strVal.Length > 80 ? strVal[..80] + "..." : strVal)}");
                root.Items.Add(node);

                var isSpriteField = colName == "vSpriteList";

                if (isImageField) AddImagePreviews(node, strVal);
                if (isSpriteField) AddSpritePreviews(node, strVal);
                if (isMapField) AddMapPreview(node, strVal);
            }
        }

        // Reverse references for types that are referenced by others
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store is not null)
        {
            if (entity is ItemProp ip)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, ip.EntityId));
            else if (entity is Ingredient ing)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, ing.EntityId));
            else if (entity is Encounter enc)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, enc.EntityId));
            else if (entity is Condition condRef)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, condRef.EntityId));
            else if (entity is TreasureTable tt)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, tt.EntityId));
            else if (entity is Recipe recipe)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, recipe.EntityId));
            else if (entity is Creature creature)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, creature.EntityId));
            else if (entity is ItemType it)
                AddReverseRefsNode(root, ReferenceResolver.ResolveReverseRefs(store, it.EntityId));
        }

        // Condition: FieldNames ↔ Modifiers paired display
        if (entity is Condition cond)
            AddConditionPairedFields(root, cond);

        // Overlay chain for merge-heavy entity types — expand each overlay layer
        if (entity is Condition || entity is TreasureTable || entity is Recipe || entity is Creature || entity is Encounter || entity is ItemType)
            AddOverlayChainNode(root, entity);

        tree.Items.Add(root);
        return new TabItem { Header = "Overview", Content = new ScrollViewer { Content = tree, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } };
    }

    private static void AddConditionPairedFields(TreeViewItem root, Condition cond)
    {
        var names = (cond.FieldNames ?? "").Split(',').Select(s => s.Trim()).ToList();
        var mods = (cond.Modifiers ?? "").Split(',').Select(s => s.Trim()).ToList();
        if (names.Count == 0 || names.All(string.IsNullOrEmpty)) return;

        var label = _loc is not null ? _loc["FieldNamesModifiers"] : "FieldNames → Modifiers";
        var pairNode = NewNode(label, Brushes.Teal, true);
        for (int i = 0; i < Math.Max(names.Count, mods.Count); i++)
        {
            var name = i < names.Count ? names[i] : "?";
            var mod = i < mods.Count ? mods[i] : "?";
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(mod)) continue;
            pairNode.Items.Add(NewNode($"{name}  →  {mod}", Brushes.CadetBlue));
        }
        root.Items.Add(pairNode);
    }

    private static void AddOverlayChainNode(TreeViewItem root, IEntity entity)
    {
        var chain = GenericDataGridHelper.GetOverlayChain(entity);
        if (chain.Count <= 1) return; // single overlay = no merge

        var chainLabel = _loc is not null ? _loc["Vis.OverlayChain"] : "Overlay Chain";
        var chainNode = NewNode($"{chainLabel} ({chain.Count} layers)", Brushes.DarkOrange, true);

        // Get FieldSources for per-field overlay annotation
        var fieldSources = GenericDataGridHelper.FieldSources;
        var entityType = entity.GetType();
        var props = entityType.GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken)
            .ToList();

        for (int i = 0; i < chain.Count; i++)
        {
            var entry = chain[i];
            var isWinner = i == chain.Count - 1;
            var prefix = isWinner ? "→ " : "   ";
            var entryLabel = $"{prefix}[{entry.ModName}] {entry.Subject} (id={entry.Id})";
            var entryNode = NewNode(entryLabel, isWinner ? Brushes.DodgerBlue : Brushes.Teal, true);

            // Show fields contributed by this overlay
            int contributed = 0;
            foreach (var prop in props)
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                var colName = colAttr?.Name ?? prop.Name;
                var key = (entity.EntityId, colName);
                if (fieldSources.TryGetValue(key, out var sourceMod) && sourceMod == entry.ModName)
                {
                    var displayName = prop.GetCustomAttribute<DisplayAttribute>()?.Name;
                    var label = _loc is not null && displayName is not null ? _loc[displayName] : displayName ?? colName;
                    var val = prop.GetValue(entity);
                    var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";
                    if (strVal.Length > 60) strVal = strVal[..57] + "...";
                    entryNode.Items.Add(NewNode($"{label}: {strVal}", Brushes.CadetBlue));
                    contributed++;
                }
            }
            var countNote = contributed > 0 ? $" ({contributed} fields)" : " (no unique fields)";
            entryNode.Header = new TextBlock { Text = entryLabel + countNote, Foreground = isWinner ? Brushes.DodgerBlue : Brushes.Teal };

            chainNode.Items.Add(entryNode);
        }

        root.Items.Add(chainNode);
    }

    private static void AddReverseRefsNode(TreeViewItem root, List<(Type SrcType, string SrcSubject, string SrcEntityId, string PropName)> refs)
    {
        if (refs.Count == 0) return;
        var revNode = NewNode("Referenced By", Brushes.DarkMagenta, true);
        foreach (var (srcType, srcSubject, srcEid, _) in refs)
        {
            var label = $"{srcType.Name}: {srcSubject}";
            var child = NewNode(label, Brushes.Magenta);
            child.Cursor = new Cursor(StandardCursorType.Hand);
            child.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(srcType, srcEid); };
            revNode.Items.Add(child);
        }
        root.Items.Add(revNode);
    }

    private static void BuildRefChildren(TreeViewItem parent, string raw, ReferenceFieldAttribute attr, string sourceEntityId)
    {
        var targetType = attr.TargetEntityType;
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(targetType, out var list) || list is null) return;

        var separator = attr.Separator;
        var parts = separator is not null ? raw.Split(separator) : [raw];
        foreach (var seg in parts)
        {
            if (seg.Contains('|'))
            {
                var orParts = seg.Split('|');
                var orGroupNode = NewNode("OR Group", Brushes.CornflowerBlue, true);
                foreach (var orSeg in orParts)
                {
                    ResolveSingleRefItem(orGroupNode, orSeg.Trim(), attr, targetType, sourceEntityId);
                }
                parent.Items.Add(orGroupNode);
            }
            else
            {
                ResolveSingleRefItem(parent, seg.Trim(), attr, targetType, sourceEntityId);
            }
        }
    }

    private static string FormatExtraInfo(string segment, string? pattern)
    {
        return ReferencePattern.FromName(pattern).FormatExtraInfo(segment);
    }

    private static void ResolveSingleRefItem(TreeViewItem parent, string trimmed, ReferenceFieldAttribute attr,
        Type targetType, string sourceEntityId)
    {
        var actualId = ReferenceParser.ExtractRawId(trimmed, attr.Pattern);
        // Use the same FindBestMatch logic as the DataGrid for consistent resolution, with source context for same-mod priority
        var match = GenericDataGridHelper.FindBestMatch(targetType, actualId, attr.TargetKey, sourceEntityId, "");
        var displayText = match?.Subject ?? actualId;
        var extra = FormatExtraInfo(trimmed, attr.Pattern);
        if (!string.IsNullOrEmpty(extra)) displayText += $" ({extra})";

        var child = NewNode(displayText, Brushes.HotPink);
        if (match is not null)
        {
            var m = match;
            child.Cursor = new Cursor(StandardCursorType.Hand);
            child.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(targetType, m.EntityId); };
        }
        parent.Items.Add(child);
    }

    private static void AddImagePreviews(TreeViewItem parent, string imageNames)
    {
        var names = imageNames.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
        if (names.Count == 0) return;
        var searchDirs = ImageService.GetImageSearchDirs();
        if (searchDirs.Count == 0) return;
        foreach (var rawName in names)
        {
            // Strip namespace prefix: "NSE:creature.png" → "creature.png"
            var colonIdx = rawName.IndexOf(':');
            var name = colonIdx > 0 ? rawName[(colonIdx + 1)..] : rawName;
            string? foundPath = null;
            var candidates = name.Contains('.') ? new[] { name } : new[] { name + ".png", name };
            foreach (var dir in searchDirs)
            {
                try
                {
                    foreach (var c in candidates)
                    {
                        var f = Directory.GetFiles(dir, c, SearchOption.AllDirectories).FirstOrDefault();
                        if (f is not null) { foundPath = f; break; }
                    }
                    if (foundPath is not null) break;
                }
                catch { }
            }
            if (foundPath is null) continue;

            try
            {
                var bmp = new Bitmap(foundPath);
                var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
                sp.Children.Add(new TextBlock { Text = Path.GetFileName(name), FontSize = 9, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(new Image { Source = bmp, MaxWidth = 96, MaxHeight = 96 });
                var item = new TreeViewItem { IsExpanded = true, Header = sp };
                item.Cursor = new Cursor(StandardCursorType.Hand);
                item.PointerPressed += (_, e) =>
                {
                    if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                        OpenImageAsDocument(Path.GetFileName(name), foundPath);
                };
                parent.Items.Add(item);
            }
            catch { }
        }
    }

    private static void AddSpritePreviews(TreeViewItem parent, string raw)
    {
        var slotNames = new Dictionary<int, string> { [20] = "Left Hand", [21] = "Right Hand", [22] = "Back" };
        var parts = raw.Split(',').Select(p => p.Trim()).Where(p => p.Contains('=')).ToList();
        var searchDirs = ImageService.GetImageSearchDirs();
        if (searchDirs.Count == 0) return;
        foreach (var part in parts)
        {
            var eqIdx = part.IndexOf('=');
            var slotStr = part[..eqIdx].Trim();
            var imgName = part[(eqIdx + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(imgName)) continue;
            var slot = int.TryParse(slotStr, out var s) ? s : -1;
            var slotName = slotNames.TryGetValue(slot, out var sn) ? sn : $"Slot {slot}";

            string? foundPath = null;
            foreach (var dir in searchDirs)
            {
                try { var c = Directory.GetFiles(dir, imgName, SearchOption.AllDirectories).FirstOrDefault(); if (c is not null) { foundPath = c; break; } } catch { }
            }
            if (foundPath is null) continue;

            try
            {
                var bmp = new Bitmap(foundPath);
                var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
                sp.Children.Add(new TextBlock { Text = $"{slotName}: {imgName}", FontSize = 9, TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(new Image { Source = bmp, MaxWidth = 64, MaxHeight = 64 });
                var item = new TreeViewItem { IsExpanded = true, Header = sp };
                item.Cursor = new Cursor(StandardCursorType.Hand);
                item.PointerPressed += (_, e) =>
                { if ((e.KeyModifiers & KeyModifiers.Control) != 0) OpenImageAsDocument(imgName, foundPath); };
                parent.Items.Add(item);
            }
            catch { }
        }
    }

    private static void AddMapPreview(TreeViewItem parent, string strDef)
    {
        try
        {
            var hexTypes = GenericDataGridHelper.GetEntities<HexType>();
            var bmp = HexMapRenderer.Render(strDef, hexTypes, 4);
            var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
            sp.Children.Add(new TextBlock { Text = "Map Preview", FontSize = 9, TextWrapping = TextWrapping.Wrap });
            sp.Children.Add(new Image { Source = bmp, MaxWidth = 256, MaxHeight = 256 });
            var item = new TreeViewItem { IsExpanded = true, Header = sp };
            item.Cursor = new Cursor(StandardCursorType.Hand);
            item.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    var zoomed = HexMapRenderer.Render(strDef, hexTypes, 10);
                    var tmpPath = Path.Combine(Path.GetTempPath(), $"map_preview_{Guid.NewGuid():N}.png");
                    zoomed.Save(tmpPath);
                    OpenImageAsDocument("Map Preview", tmpPath);
                }
            };
            parent.Items.Add(item);
        }
        catch { }
    }

    /// <summary>Open an image as a Dock workspace tab.</summary>
    public static void OpenImageAsDocument(string title, string imagePath)
    {
        try
        {
            var docVm = App.ServiceProvider?.GetService<NeoEditor.ViewModels.MainContent.DocumentWorkspaceViewModel>();
            if (docVm is null) return;
            var doc = new NeoEditor.ViewModels.MainContent.ImageDocument { ImagePath = imagePath };
            doc.SetStaticTitle(title);
            docVm.Documents.Add(doc);
        }
        catch { }
    }

    /// <inheritdoc cref="EditorUIFactory.NewNode"/>
    public static TreeViewItem NewNode(string text, IBrush? fg = null, bool bold = false)
        => EditorUIFactory.NewNode(text, fg, bold);

    /// <inheritdoc cref="EditorUIFactory.NavOnCtrl"/>
    public static void NavOnCtrl(TreeViewItem item, Action nav)
        => EditorUIFactory.NavOnCtrl(item, nav);

    /// <inheritdoc cref="EditorUIFactory.MakeTab"/>
    public static TabItem MakeTab(string header, Control content)
        => EditorUIFactory.MakeTab(header, content);

    /// <inheritdoc cref="EditorUIFactory.CreateEditorTabs"/>
    public static TabControl CreateEditorTabs()
        => EditorUIFactory.CreateEditorTabs();
}

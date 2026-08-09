using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class RecipeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Recipe);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    /// <summary>Create with injected services.</summary>
    public RecipeEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            _vis.Resolver,
            _vis.Router,
            _vis.BuildRefTooltip);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Recipe r) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(r));

        root.Children.Add(BuildHeroHeader(r));
        root.Children.Add(BuildIngredientsPanel(r));
        root.Children.Add(BuildProductPanel(r));
        if (!string.IsNullOrWhiteSpace(r.AlsoTry))
            root.Children.Add(BuildAlsoTryPanel(r));
        if (!string.IsNullOrWhiteSpace(r.HiddenId))
            root.Children.Add(BuildHiddenPanel(r));
        root.Children.Add(BuildReverseRefsPanel(r));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Recipe r)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {r.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(r, idRow);
        if (!string.IsNullOrWhiteSpace(r.Type))
            idRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"),
                Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = r.Type, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") }
            });
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var flags = new List<string>();
        if (r.Scrap) flags.Add("Scrap");
        if (r.Identify) flags.Add("Identify");
        if (r.DegradeOutput) flags.Add("DegradeOutput");
        if (r.TransferComponents) flags.Add("TransferComponents");
        if (flags.Count > 0)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"),
                Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = r.Subject ?? r.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(r.SecretName))
            identity.Children.Add(new TextBlock
            {
                Text = $"{_vis.Loc("Vis.Secret")}: {r.SecretName}", FontSize = 12, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888")
            });
        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
            { Text = $"{_vis.Loc("Vis.Hours")}: {r.Hours:F1}", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock
        {
            Text =
                $"{_vis.Loc("Vis.Reverse")}: {(r.Reverse > 0 ? _vis.Loc("Vis.Yes") : _vis.Loc("Vis.No"))}",
            FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = r.DegradeOutput
                ? $"{_vis.Loc("Vis.DegradeOutput")}: On"
                : $"{_vis.Loc("Vis.DegradeOutput")}: Off",
            FontSize = 11, Foreground = r.DegradeOutput ? Brush.Parse("#2E7D32") : Brush.Parse("#999")
        });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildIngredientsPanel(Recipe r)
    {
        var sp = new StackPanel();
        var itemProps = _dataTable?.GetEntities<ItemProp>();
        var hasAny = false;

        // ── Fieldset: legend overlaps the top border ──
        var fieldset = new Grid();

        // Content area with border (added first → behind legend)
        var contentStack = new StackPanel { Spacing = 10 };
        var contentBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderBrush = Brush.Parse("#18000000"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 18, 16, 12),
            Margin = new Thickness(0, 10, 0, 0),
            Child = contentStack
        };
        fieldset.Children.Add(contentBorder);

        // Legend title — floats above the border top (fieldset style)
        var legendContent = new Border
        {
            Background = Brush.Parse("#FAFAFA"),
            Padding = new Thickness(12, 4),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = _vis.Loc("Vis.Ingredients"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#555")
            }
        };
        var legendBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(20, 0, 0, 0),
            Child = legendContent
        };
        fieldset.Children.Add(legendBorder);

        // Ctrl+Click on legend to expand/collapse the content
        var expanded = true;
        legendContent.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                expanded = !expanded;
                contentBorder.IsVisible = expanded;
            }
        };

        var pattern = ReferencePattern.FromName("{mult}x{id}");

        void AddGroup(string label, string raw, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            hasAny = true;

            // Group sub-heading
            contentStack.Children.Add(new TextBlock
            {
                Text = label, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse(fg), Margin = new Thickness(4, 0, 0, 0)
            });

            // Vertical list of ingredients, each in its own Card
            var list = new StackPanel { Spacing = 6 };

            foreach (var part in raw.Split('+'))
            {
                var seg = part.Trim();
                var ing = _vis.Resolver.LookupRef<Ingredient>(r, propName, seg);
                var extra = pattern.FormatExtraInfo(seg);
                // FormatExtraInfo returns "x{N}" for {mult}x{id} pattern — strip the "x" for quantity
                var qty = string.IsNullOrEmpty(extra) ? "1" : extra.TrimStart('x');

                var cardStack = new StackPanel { Spacing = 4 };

                // Row 1: type badge + name + quantity
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (ing is not null)
                {
                    nameRow.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(3), Background = Brush.Parse(bg),
                        Padding = new Thickness(5, 1),
                        Child = new TextBlock { Text = "Ingredient", FontSize = 9, Foreground = Brush.Parse(fg) }
                    });
                    var nameBadge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Background = Brush.Parse("#0D000000"),
                        Padding = new Thickness(8, 3),
                        Child = new TextBlock { Text = ing.Name ?? seg, FontSize = 11, Foreground = Brush.Parse("#333") }
                    };
                    _refNode.WireNavigation(nameBadge, typeof(Ingredient), ing.EntityId, r, ing);
                    nameRow.Children.Add(nameBadge);
                }
                else
                {
                    nameRow.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(3), Background = Brush.Parse("#F5F5F5"),
                        Padding = new Thickness(5, 1),
                        Child = new TextBlock { Text = "?", FontSize = 9, Foreground = Brush.Parse("#999") }
                    });
                    nameRow.Children.Add(new TextBlock { Text = seg, FontSize = 11, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
                }
                // Quantity — only show when > 1
                if (qty != "1")
                    nameRow.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(3),
                        Background = Brush.Parse("#08000000"),
                        Padding = new Thickness(6, 1),
                        Child = new TextBlock { Text = $"×{qty}", FontSize = 10, Foreground = Brush.Parse("#666") }
                    });
                cardStack.Children.Add(nameRow);

                // Row 2: Required / Forbidden property badges — 2-col, Ctrl+Clickable
                if (ing is not null && (!string.IsNullOrWhiteSpace(ing.RequiredProps) ||
                                        !string.IsNullOrWhiteSpace(ing.ForbidProps)))
                {
                    var propGrid = new Grid
                    {
                        ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    propGrid.RowDefinitions.Add(new(GridLength.Auto));

                    if (!string.IsNullOrWhiteSpace(ing.RequiredProps))
                    {
                        var reqStack = new StackPanel { Spacing = 2 };
                        reqStack.Children.Add(new TextBlock { Text = _vis.Loc("Vis.Required"), FontSize = 9, Foreground = Brush.Parse("#2E7D32") });
                        var reqWp = new WrapPanel();
                        foreach (var pid in ing.RequiredProps.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0))
                        {
                            // Use unified LookupRef first — handles int IDs, prefixed IDs, MergedIds correctly
                            var prop = _vis.Resolver.LookupRef<ItemProp>(ing, nameof(Ingredient.RequiredProps), pid);
                            if (prop is null && int.TryParse(pid, out var pidi))
                            {
                                // Fallback: dictionary lookup by business key (Id)
                                itemProps.TryGetValue(pidi, out prop);
                            }
                            if (prop is not null)
                                reqWp.Children.Add(_refNode.BadgeForEntity(ing, prop,
                                    prop.PropertyName ?? $"#{pid}",
                                    "#E8F5E9", "#2E7D32"));
                            else
                                reqWp.Children.Add(_vis.MiniBadge($"#{pid}", "#F5F5F5", "#999"));
                        }
                        reqStack.Children.Add(reqWp);
                        Grid.SetColumn(reqStack, 0);
                        propGrid.Children.Add(reqStack);
                    }

                    if (!string.IsNullOrWhiteSpace(ing.ForbidProps))
                    {
                        var forbStack = new StackPanel { Spacing = 2 };
                        forbStack.Children.Add(new TextBlock { Text = _vis.Loc("Vis.Forbidden"), FontSize = 9, Foreground = Brush.Parse("#C62828") });
                        var forbWp = new WrapPanel();
                        foreach (var pid in ing.ForbidProps.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0))
                        {
                            // Use unified LookupRef first — handles int IDs, prefixed IDs, MergedIds correctly
                            var prop = _vis.Resolver.LookupRef<ItemProp>(ing, nameof(Ingredient.ForbidProps), pid);
                            if (prop is null && int.TryParse(pid, out var pidi))
                            {
                                // Fallback: dictionary lookup by business key (Id)
                                itemProps.TryGetValue(pidi, out prop);
                            }
                            if (prop is not null)
                                forbWp.Children.Add(_refNode.BadgeForEntity(ing, prop,
                                    prop.PropertyName ?? $"#{pid}",
                                    "#FFEBEE", "#C62828"));
                            else
                                forbWp.Children.Add(_vis.MiniBadge($"#{pid}", "#F5F5F5", "#999"));
                        }
                        forbStack.Children.Add(forbWp);
                        Grid.SetColumn(forbStack, 1);
                        propGrid.Children.Add(forbStack);
                    }

                    cardStack.Children.Add(propGrid);
                }

                list.Children.Add(_vis.Card(cardStack));
            }

            contentStack.Children.Add(list);
        }

        AddGroup(_vis.Loc("Vis.Tools"), r.Tools, nameof(Recipe.Tools), "#FFF3E0", "#E65100");
        AddGroup(_vis.Loc("Vis.Consumed"), r.Consumed, nameof(Recipe.Consumed), "#FFEBEE", "#C62828");
        AddGroup("Destroyed", r.Destroyed, nameof(Recipe.Destroyed), "#FCE4EC", "#880E4F");

        if (!hasAny)
            sp.Children.Add(
                new TextBlock { Text = "(No ingredients)", FontSize = 11, Foreground = Brush.Parse("#999") });
        else
            sp.Children.Add(fieldset);

        return sp;
    }

    private Control BuildProductPanel(Recipe r)
    {
        var sp = new StackPanel();
        var wp = new WrapPanel();
        // M3: TreasureTable badge → RefNode
        wp.Children.Add(_refNode.Badge<TreasureTable>(r, nameof(Recipe.TreasureId), r.TreasureId,
            resolvedBg: "#E8F5E9", resolvedFg: "#2E7D32",
            unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
        // Also show ItemType preview from the TT's treasure list
        var tt = _vis.Resolver.LookupRef<TreasureTable>(r, nameof(Recipe.TreasureId), r.TreasureId);
        if (tt is not null && !string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var itemTypes =
                _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);
            foreach (var seg in tt.Treasures.Split(',').Take(6))
            {
                var parts = seg.Trim().Split('x');
                if (parts.Length < 2) continue;
                var itemId = parts[0];
                var it = itemTypes.GetValueOrDefault(itemId);
                if (it is not null)
                    wp.Children.Add(_refNode.BadgeForEntity(r, it, it.Description!,
                        "#E0F2F1", "#00695C"));
            }
        }

        sp.Children.Add(_vis.Card(wp, _vis.Loc("Vis.Loot")));

        if (r.TempTreasureId != "3" && r.TempTreasureId != r.TreasureId)
        {
            var wp2 = new WrapPanel();
            wp2.Children.Add(_refNode.Badge<TreasureTable>(r, nameof(Recipe.TempTreasureId),
                r.TempTreasureId, "#E3F2FD", "#1565C0"));
            sp.Children.Add(_vis.Card(wp2, "Temp Product Preview"));
        }

        return sp;
    }

    private Control BuildAlsoTryPanel(Recipe r)
    {
        var wp = new WrapPanel();
        foreach (var seg in r.AlsoTry.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            wp.Children.Add(_refNode.Badge<Recipe>(r, nameof(Recipe.AlsoTry), seg,
                "#F3E5F5", "#6A1B9A"));
        }

        return _vis.Card(wp, "Also Try (Alternative Recipes)");
    }

    /// <summary>
    /// R30: nHiddenID → Recipe — unlock/related recipes (e.g. "reveal on identification").
    /// Previously only visible in the Raw Data table.
    /// </summary>
    private Control BuildHiddenPanel(Recipe r)
    {
        var wp = new WrapPanel();
        foreach (var seg in r.HiddenId.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            wp.Children.Add(_refNode.Badge<Recipe>(r, nameof(Recipe.HiddenId), seg,
                "#FFF3E0", "#E65100"));
        }

        return _vis.Card(wp, _vis.Loc("Vis.Hidden"));
    }

    private Control BuildReverseRefsPanel(Recipe r)
        => _vis.BuildReverseRefsPanel(r.EntityId);
}

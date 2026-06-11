using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls.Editors;

public class StoryTreeEditor : ICustomTableEditor
{
    public Type EntityType => typeof(Encounter);
    public string EditorName => "Encounter Editor";
    private TabControl? _tabs;
    private Encounter? _enc;
    private Dictionary<int, Encounter>? _allEnc;
    private double _maxX, _maxY;

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return;
        _tabs.Items.Clear();
        _enc = entity as Encounter;
        if (_enc is null) return;
        _allEnc = GenericDataGridHelper.GetEntities<Encounter>();

        _tabs.Items.Add(EditorHelper.MakeTab("Story Flow", BuildStoryFlow()));
        _tabs.Items.Add(EditorHelper.MakeTab("Text Editor", BuildTextEditor()));
        _tabs.Items.Add(EditorHelper.BuildOverviewTab(_enc));
        _tabs.Items.Add(EditorHelper.MakeTab("Flowchart", BuildFlowchart()));
    }

    // ==================== Story Flow (Twine-like split screen) ====================
    private Grid BuildStoryFlow()
    {
        if (_enc is null || _allEnc is null)
            return new Grid { Children = { new TextBlock { Text = "No encounter selected." } } };

        var e = _enc;

        // Left panel: encounter navigation tree
        var leftTree = BuildNavigationTree(e);

        // Right panel: encounter detail editor
        var rightDetail = BuildEncounterDetail(e);

        var leftScroll = new ScrollViewer
        {
            Content = leftTree,
            Width = 260,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var rightScroll = new ScrollViewer
        {
            Content = rightDetail,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,4,*") };
        Grid.SetColumn(leftScroll, 0);
        Grid.SetColumn(new GridSplitter { Width = 4, Background = Brushes.Transparent }, 1);
        Grid.SetColumn(rightScroll, 2);
        grid.Children.Add(leftScroll);
        grid.Children.Add(new GridSplitter { Width = 4, Background = Brushes.Transparent });
        grid.Children.Add(rightScroll);
        return grid;
    }

    private TreeView BuildNavigationTree(Encounter e)
    {
        var tree = new TreeView();
        var root = EditorHelper.NewNode(
            string.IsNullOrWhiteSpace(e.Name) ? $"Encounter #{e.Id}" : $"{e.Name} (id={e.Id})",
            Brushes.DodgerBlue, true);

        // Inbound (Leads From)
        var parents = _allEnc!.Values.Where(p =>
        {
            if (string.IsNullOrWhiteSpace(p.Responses)) return false;
            return ParseResp(p.Responses).Any(r => r.Id == e.Id);
        }).ToList();

        if (parents.Count > 0)
        {
            var lf = EditorHelper.NewNode($"Leads From ({parents.Count})", Brushes.DarkOrange, true);
            foreach (var p in parents)
            {
                var n = EditorHelper.NewNode($"{Trunc(p.Name, 30)}", Brushes.Orange);
                EditorHelper.NavOnCtrl(n, () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), p.EntityId));
                lf.Items.Add(n);
            }
            root.Items.Add(lf);
        }
        else
        {
            root.Items.Add(EditorHelper.NewNode("(Root — no parent)", Brushes.Gray));
        }

        // Self-reference
        var selfResp = ParseResp(e.Responses).Where(r => r.Id == e.Id).ToList();
        if (selfResp.Count > 0)
        {
            var selfNode = EditorHelper.NewNode("↺ Self-reference", Brushes.DeepPink, true);
            foreach (var (_, w) in selfResp)
                selfNode.Items.Add(EditorHelper.NewNode($"Weight: {w:F3}", Brushes.HotPink));
            root.Items.Add(selfNode);
        }

        // Outbound (Leads To)
        var resp = ParseResp(e.Responses).Where(r => r.Id != e.Id).ToList();
        if (resp.Count > 0)
        {
            var lt = EditorHelper.NewNode($"Leads To ({resp.Count})", Brushes.DarkGreen, true);
            foreach (var (cid, wgt) in resp)
            {
                var name = _allEnc.TryGetValue(cid, out var ce) ? ce.Name ?? "?" : "?";
                var n = EditorHelper.NewNode($"{Trunc(name, 25)} (w:{wgt:F2})", Brushes.Green);
                if (_allEnc.TryGetValue(cid, out var e2))
                    EditorHelper.NavOnCtrl(n, () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), e2.EntityId));
                lt.Items.Add(n);
            }
            root.Items.Add(lt);
        }
        else
        {
            root.Items.Add(EditorHelper.NewNode("(Leaf — no responses)", Brushes.Gray));
        }

        tree.Items.Add(root);
        return tree;
    }

    private StackPanel BuildEncounterDetail(Encounter e)
    {
        var sp = new StackPanel { Spacing = 10, Margin = new Thickness(8, 4) };

        // Name
        sp.Children.Add(new TextBlock { Text = "Name", FontWeight = FontWeight.SemiBold, FontSize = 11,
            Foreground = Brushes.Gray });
        var nameBox = new TextBox
        {
            Text = e.Name ?? "",
            FontSize = 14, FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        nameBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && e.Name != nameBox.Text)
                e.Name = nameBox.Text;
        };
        sp.Children.Add(nameBox);

        // Narrative (strDesc)
        sp.Children.Add(new TextBlock { Text = "Narrative (strDesc)", FontWeight = FontWeight.SemiBold, FontSize = 11,
            Foreground = Brushes.Gray });
        var descBox = new TextBox
        {
            Text = e.Description ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 200,
            MaxHeight = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 13
        };
        descBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && e.Description != descBox.Text)
                e.Description = descBox.Text;
        };
        sp.Children.Add(descBox);

        // Character count
        var charCount = new TextBlock
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = $"{e.Description?.Length ?? 0} characters"
        };
        descBox.TextChanged += (_, _) =>
            charCount.Text = $"{descBox.Text?.Length ?? 0} characters";
        sp.Children.Add(charCount);

        // Responses
        sp.Children.Add(new TextBlock { Text = "Responses", FontWeight = FontWeight.SemiBold, FontSize = 11,
            Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 0) });
        var resp = ParseResp(e.Responses).ToList();
        if (resp.Count > 0)
        {
            foreach (var (cid, wgt) in resp)
            {
                var name = _allEnc!.TryGetValue(cid, out var ce) ? ce.Name ?? $"[{cid}]" : $"[{cid}]";
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2) };
                row.Children.Add(new TextBlock { Text = $"→ {Trunc(name, 40)}", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = $"w:{wgt:F3}", FontSize = 10, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
                if (_allEnc.TryGetValue(cid, out var e2))
                {
                    var btn = new Button { Content = "Go", Padding = new Thickness(4, 0), FontSize = 10 };
                    btn.Click += (_, _) => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), e2.EntityId);
                    row.Children.Add(btn);
                }
                sp.Children.Add(row);
            }
        }
        else
        {
            sp.Children.Add(new TextBlock { Text = "(No responses)", FontSize = 11, Foreground = Brushes.Gray,
                FontStyle = FontStyle.Italic });
        }

        // Triggered By (EncounterTrigger)
        sp.Children.Add(new TextBlock { Text = "Triggered By", FontWeight = FontWeight.SemiBold, FontSize = 11,
            Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 0) });
        try
        {
            var triggers = GenericDataGridHelper.GetEntities<EncounterTrigger>();
            var myTriggers = triggers.Values
                .Where(t => t.EncounterId.ToString() == e.Id.ToString())
                .ToList();
            if (myTriggers.Count > 0)
            {
                foreach (var t in myTriggers)
                {
                    var triggerRow = new TextBlock
                    {
                        Text = $"▸ {Trunc(t.Name, 60)} (id={t.Id})",
                        FontSize = 12, Margin = new Thickness(0, 1)
                    };
                    sp.Children.Add(triggerRow);
                }
            }
            else
            {
                sp.Children.Add(new TextBlock { Text = "(Not triggered by any EncounterTrigger)",
                    FontSize = 11, Foreground = Brushes.Gray, FontStyle = FontStyle.Italic });
            }
        }
        catch
        {
            sp.Children.Add(new TextBlock { Text = "(Trigger data unavailable)", FontSize = 11,
                Foreground = Brushes.Gray, FontStyle = FontStyle.Italic });
        }

        return sp;
    }

    // ==================== Text Editor ====================
    private ScrollViewer BuildTextEditor()
    {
        if (_enc is null)
            return new ScrollViewer { Content = new TextBlock { Text = "No encounter selected." } };

        var e = _enc;
        var sp = new StackPanel { Spacing = 12, Margin = new Thickness(8) };

        sp.Children.Add(new TextBlock { Text = "Narrative Text Editor", FontSize = 14, FontWeight = FontWeight.Bold });

        // Name
        sp.Children.Add(new TextBlock { Text = "Encounter Name", FontWeight = FontWeight.SemiBold, FontSize = 11,
            Foreground = Brushes.Gray });
        var nameBox = new TextBox { Text = e.Name ?? "", FontSize = 14, FontWeight = FontWeight.SemiBold };
        nameBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && e.Name != nameBox.Text)
                e.Name = nameBox.Text;
        };
        sp.Children.Add(nameBox);

        // Description
        var descHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        descHeader.Children.Add(new TextBlock { Text = "Narrative Text (strDesc)", FontWeight = FontWeight.SemiBold,
            FontSize = 11, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
        var countLabel = new TextBlock { Text = $"{e.Description?.Length ?? 0} chars", FontSize = 10,
            Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
        descHeader.Children.Add(countLabel);
        sp.Children.Add(descHeader);

        var descBox = new TextBox
        {
            Text = e.Description ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 350,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 14,
            FontFamily = FontFamily.Parse("avares://Avalonia-Text-Diff-Tool/Assets/Sarasa-Mono-SC-Nerd.ttf#Sarasa Nerd,Consolas")
        };
        descBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && e.Description != descBox.Text)
                e.Description = descBox.Text;
        };
        descBox.TextChanged += (_, _) => countLabel.Text = $"{descBox.Text?.Length ?? 0} chars";
        sp.Children.Add(descBox);

        // Responses raw
        sp.Children.Add(new TextBlock { Text = "Responses (raw aResponses string)", FontWeight = FontWeight.SemiBold,
            FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 0) });
        var respBox = new TextBox
        {
            Text = e.Responses ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            MaxHeight = 200,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        respBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && e.Responses != respBox.Text)
                e.Responses = respBox.Text;
        };
        sp.Children.Add(respBox);

        // Image
        sp.Children.Add(new TextBlock { Text = "Image (strImg)", FontWeight = FontWeight.SemiBold, FontSize = 11,
            Foreground = Brushes.Gray });
        var imgBox = new TextBox { Text = e.Image ?? "", FontSize = 12 };
        imgBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty && e.Image != imgBox.Text)
                e.Image = imgBox.Text;
        };
        sp.Children.Add(imgBox);

        return new ScrollViewer { Content = sp };
    }

    // ==================== Flowchart (original canvas graph) ====================
    private ScrollViewer BuildFlowchart()
    {
        if (_enc is null || _allEnc is null)
            return new ScrollViewer { Content = new TextBlock { Text = "No data." } };

        var e = _enc;
        var canvas = new Canvas { Background = Brushes.Transparent, Width = 800, Height = 200 };
        var visited = new HashSet<int>();
        _maxX = 0; _maxY = 0;
        LayoutFlowNode(e, 20, 20, visited, 0, canvas);
        canvas.Height = Math.Max(canvas.Height, _maxY + 40);
        canvas.Width = Math.Max(canvas.Width, _maxX + 200);

        var tree = new TreeView();
        var root = EditorHelper.NewNode(
            string.IsNullOrWhiteSpace(e.Name) ? $"Encounter #{e.Id}" : $"{e.Name} (id={e.Id})",
            Brushes.DodgerBlue, true);

        var parents = _allEnc.Values.Where(p =>
        {
            if (string.IsNullOrWhiteSpace(p.Responses)) return false;
            return ParseResp(p.Responses).Any(r => r.Id == e.Id);
        }).ToList();

        var lf = EditorHelper.NewNode("Leads From", Brushes.DarkOrange, true);
        foreach (var p in parents)
        {
            var n = EditorHelper.NewNode($"{Trunc(p.Name, 35)}", Brushes.Orange);
            EditorHelper.NavOnCtrl(n, () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), p.EntityId));
            lf.Items.Add(n);
        }
        root.Items.Add(parents.Count > 0 ? lf : EditorHelper.NewNode("(Root)", Brushes.Gray));

        var selfResp = ParseResp(e.Responses).Where(r => r.Id == e.Id).ToList();
        if (selfResp.Count > 0)
        {
            var sn = EditorHelper.NewNode("↺ Self-reference", Brushes.DeepPink, true);
            foreach (var (_, w) in selfResp) sn.Items.Add(EditorHelper.NewNode($"Weight: {w:F3}", Brushes.HotPink));
            root.Items.Add(sn);
        }

        var resp = ParseResp(e.Responses).Where(r => r.Id != e.Id).ToList();
        if (resp.Count > 0)
        {
            var lt = EditorHelper.NewNode("Leads To", Brushes.DarkGreen, true);
            foreach (var (cid, wgt) in resp)
            {
                var name = _allEnc.TryGetValue(cid, out var ce) ? Trunc(ce.Name, 35) : $"Encounter #{cid}";
                var n = EditorHelper.NewNode($"{name} (w:{wgt:F3})", Brushes.Green);
                if (_allEnc.TryGetValue(cid, out var e2)) EditorHelper.NavOnCtrl(n, () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), e2.EntityId));
                lt.Items.Add(n);
            }
            root.Items.Add(lt);
        }
        else root.Items.Add(EditorHelper.NewNode("(Leaf)", Brushes.Gray));

        tree.Items.Add(root);

        var sp = new StackPanel();
        sp.Children.Add(new ScrollViewer { Content = canvas, Height = 240,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        sp.Children.Add(new TextBlock { Text = "─ Relationship Tree ─", FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8, 0, 4) });
        sp.Children.Add(new ScrollViewer { Content = tree,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });

        return new ScrollViewer { Content = sp,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    // ==================== Flowchart layout ====================
    private (double X, double W) LayoutFlowNode(Encounter enc, double x, double y, HashSet<int> vis, int depth, Canvas c)
    {
        if (depth > 6 || !vis.Add(enc.Id)) return (x, 0);
        const double nw = 140, nh = 36, hs = 12, vs = 40;
        bool selfRef = enc.Id == _enc?.Id && depth > 0;
        var children = ParseResp(enc.Responses).Where(r => r.Id != enc.Id).ToList();

        var color = selfRef ? "#FF1493" : depth == 0 ? "#4169E1" : "#888";
        var border = new Border
        {
            Width = nw, Height = nh, CornerRadius = new CornerRadius(4),
            BorderBrush = Brush.Parse(color), BorderThickness = new Thickness(depth == 0 || selfRef ? 2 : 1),
            Background = Brush.Parse(selfRef ? "#FFF0F5" : "#F0F0F0"),
            Child = new TextBlock
            {
                Text = selfRef ? $"↺ [{enc.Id}]" : $"[{enc.Id}] {Trunc(enc.Name, 16)}",
                FontSize = 10, TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(border, x); Canvas.SetTop(border, y); c.Children.Add(border);
        _maxX = Math.Max(_maxX, x + nw); _maxY = Math.Max(_maxY, y + nh);

        double totalW = nw;
        if (children.Count > 0)
        {
            var cx = x;
            var childY = y + nh + vs;
            var pcx = x + nw / 2; var pby = y + nh;
            foreach (var (cid, _) in children)
            {
                if (!_allEnc!.TryGetValue(cid, out var ce)) continue;
                var csx = cx;
                var r = LayoutFlowNode(ce, cx, childY, vis, depth + 1, c);
                cx = r.X + Math.Max(r.W, nw) + hs;

                c.Children.Add(new Line
                {
                    StartPoint = new Point(pcx, pby),
                    EndPoint = new Point(csx + nw / 2, childY),
                    Stroke = Brush.Parse("#999"), StrokeThickness = 1
                });
            }
            totalW = Math.Max(totalW, cx - x);
        }
        return (x, totalW);
    }

    // ==================== Helpers ====================
    private static List<(int Id, double Weight)> ParseResp(string raw)
    {
        var r = new List<(int, double)>();
        if (string.IsNullOrWhiteSpace(raw)) return r;
        foreach (var s in raw.Split(','))
        {
            var t = s.Trim().TrimStart('='); var xi = t.IndexOf('x');
            if (xi <= 0) continue;
            if (int.TryParse(t[..xi], out var id))
            {
                var rest = t[(xi + 1)..]; var x2 = rest.IndexOf('x');
                var w = double.TryParse(x2 > 0 ? rest[..x2] : rest,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var wv) ? wv : 0;
                r.Add((id, w));
            }
        }
        return r;
    }

    private static string Trunc(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? "?" : s!.Length <= max ? s : s[..max] + "...";
}

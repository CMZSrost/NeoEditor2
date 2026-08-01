using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Builds Avalonia DataGrid column templates (CellTemplate + CellEditingTemplate)
/// based on entity property metadata. Extracted from GenericDataGridHelper.ConfigureColumn.
///
/// R07: Receives dependencies via constructor injection.
/// </summary>
public class ColumnTemplateFactory
{
    private readonly DataTableService _data;
    private readonly IDataGridCellInteractionService _cellInteraction;
    private readonly DataGridInteractionState _state;

    public ColumnTemplateFactory(
        DataTableService data,
        IDataGridCellInteractionService cellInteraction,
        DataGridInteractionState state)
    {
        _data = data;
        _cellInteraction = cellInteraction;
        _state = state;
    }

    /// <summary>
    /// Field description provider for column tooltips. (tableName, propertyName) → description.
    /// Pluggable — defaults to null (no descriptions). Set by App startup.
    /// </summary>
    public Func<string, string, string?>? FieldDescriptionProvider { get; set; }

    /// <summary>
    /// Localizer function for header/description text.
    /// </summary>
    public Func<string, string>? Localizer { get; set; }

    /// <summary>
    /// Messenger for raising cell edit / clone / find-references events.
    /// </summary>
    public CommunityToolkit.Mvvm.Messaging.IMessenger? Messenger { get; set; }

    public void ConfigureColumn(DataGridAutoGeneratingColumnEventArgs e, Type modelType)
    {
        var localizer = Localizer ?? (s => s);
        var property = modelType.GetProperty(e.PropertyName);
        if (property == null) return;

        // 1. Skip properties without [Column] attribute (internal fields)
        var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
        if (columnAttr == null)
        {
            e.Cancel = true;
            return;
        }

        string headerText = property.Name;

        // 2. Tooltip: prefer .docx field description, then *Desc resource, fall back to display name
        var displayAttr = property.GetCustomAttribute<DisplayAttribute>();
        string comment = "";

        var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr?.Name ?? modelType.Name.ToLowerInvariant();
        var docxDesc = FieldDescriptionProvider?.Invoke(tableName, e.PropertyName);
        if (!string.IsNullOrWhiteSpace(docxDesc))
        {
            comment = docxDesc;
        }
        else if (displayAttr != null && !string.IsNullOrEmpty(displayAttr.Name))
        {
            var descKey = displayAttr.Name + "Desc";
            var descValue = localizer(descKey);
            comment = descValue != descKey ? descValue : localizer(displayAttr.Name);
        }

        // 3. Build custom column header with optional tooltip
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var headerTextBlock = new TextBlock { Text = headerText, VerticalAlignment = VerticalAlignment.Center };
        headerPanel.Children.Add(headerTextBlock);

        if (!string.IsNullOrEmpty(comment))
        {
            ToolTip.SetTip(headerPanel, comment);
        }

        // 4. ReferenceField columns → teal style, editable as ComboBox or TextBox
        var refAttr = property.GetCustomAttribute<ReferenceFieldAttribute>();
        if (refAttr != null)
        {
            BuildReferenceColumn(e, property, refAttr, headerPanel);
            return;
        }

        // 5. longtext → multi-line TextBox
        if (columnAttr.TypeName != null &&
            columnAttr.TypeName.Contains("longtext", StringComparison.OrdinalIgnoreCase))
        {
            BuildLongTextColumn(e, property, headerPanel);
            return;
        }

        // 6. bool → CheckBox
        if (property.PropertyType == typeof(bool))
        {
            e.Column = new DataGridCheckBoxColumn
            {
                Header = headerPanel,
                SortMemberPath = e.PropertyName,
                Width = new DataGridLength(70),
                Binding = new Binding(property.Name),
            };
            return;
        }

        // 7. Enum → ComboBox
        if (property.PropertyType.IsEnum)
        {
            BuildEnumColumn(e, property, headerPanel);
            return;
        }

        // 8. Default: retain original column type + header
        BuildDefaultColumn(e, property, headerPanel);
    }

    // ── Reference column builder ────────────────────────────────────────

    private void BuildReferenceColumn(DataGridAutoGeneratingColumnEventArgs e,
        PropertyInfo property, ReferenceFieldAttribute refAttr, StackPanel headerPanel)
    {
        var targetType = refAttr.TargetEntityType;
        var separator = refAttr.Separator;
        var pattern = refAttr.Pattern;
        var isMulti = separator is not null;

        e.Column = new DataGridTemplateColumn
        {
            Header = headerPanel,
            SortMemberPath = e.PropertyName,
            Width = new DataGridLength(160),
            CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
            {
                var raw = property.GetValue(item)?.ToString() ?? "";
                if (!isMulti)
                {
                    var rawId = ReferenceParser.ExtractRawId(raw, pattern);
                    var sourceEid = (item as IEntity)?.EntityId ?? "";
                    var subject = _data.LookupSubjectByRawId(targetType, rawId, sourceEid, e.PropertyName);
                    string display;
                    if (!string.IsNullOrEmpty(subject))
                    {
                        var parsed = ReferenceParser.ParseWithPattern(raw, pattern);
                        display = ReferencePattern.FromName(pattern).FormatDisplay(raw, subject, parsed.ModName);
                    }
                    else
                    {
                        display = raw;
                    }
                    var tb = new TextBlock
                    {
                        Text = display,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0),
                        TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = Brushes.Teal,
                        TextDecorations = TextDecorations.Underline,
                        Cursor = new Cursor(StandardCursorType.Hand),
                    };
                    var grid = new Grid
                    {
                        MinHeight = 20,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = Brushes.Transparent
                    };
                    grid.Children.Add(tb);

                    // Single-value: Ctrl+Hover, Ctrl+Click → IDataGridCellInteractionService
                    var refColNameSingle = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                    grid.Cursor = new Cursor(StandardCursorType.Hand);
                    _cellInteraction.AttachSingleRefHandlers(
                        grid, item, property, targetType, refAttr, pattern,
                        e.PropertyName, refColNameSingle);

                    grid.PointerExited += (_, _) =>
                    {
                        try
                        {
                            if (_data.FieldSources.Count > 0 && item is IEntity)
                                grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                                {
                                    Converter = new Converters.FieldSourceConverter(),
                                    ConverterParameter = refColNameSingle
                                });
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Logger.Verbose(ex,
                                "[CTF:PointerExited] Failed to bind FieldSource tooltip on grid for col={Col}",
                                refColNameSingle);
                        }
                    };

                    // Conflict background on the grid
                    if (_data.FieldSources.Count > 0 && item is IEntity)
                    {
                        var refColName2 = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                        grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                        {
                            Converter = new Converters.FieldConflictBackgroundConverter(),
                            ConverterParameter = refColName2
                        });
                    }
                    return grid;
                }
                else
                {
                    // Multi-value: split into individual elements
                    var wrapPanel = new WrapPanel();
                    var grid = new Grid
                    {
                        MinHeight = 20,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = Brushes.Transparent
                    };
                    grid.Children.Add(wrapPanel);

                    var refColName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                    var segChar = (separator?.Length > 0) ? separator[0] : ',';
                    var segments = raw.Split(segChar);
                    for (int si = 0; si < segments.Length; si++)
                    {
                        var segment = segments[si].Trim();
                        if (string.IsNullOrEmpty(segment)) continue;
                        if (si > 0)
                            wrapPanel.Children.Add(new TextBlock
                            {
                                Text = $" {segChar} ",
                                VerticalAlignment = VerticalAlignment.Center,
                                Foreground = Brushes.Teal
                            });

                        var secSep = segment.Contains('|') ? '|' : (segment.Contains(',') ? ',' : '\0');
                        var subParts = secSep != '\0'
                            ? segment.Split(secSep).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray()
                            : [segment];

                        for (int ai = 0; ai < subParts.Length; ai++)
                        {
                            var andPart = subParts[ai];
                            if (ai > 0)
                                wrapPanel.Children.Add(new TextBlock
                                {
                                    Text = secSep == '|' ? " or " : " + ",
                                    VerticalAlignment = VerticalAlignment.Center,
                                    FontSize = 10,
                                    Foreground = Brushes.Gray
                                });

                            var segDisplay = _cellInteraction.FormatSegmentDisplay(andPart, targetType, pattern,
                                (item as IEntity)?.EntityId ?? "", e.PropertyName, refAttr.TargetKey);
                            var segTb = new TextBlock
                            {
                                Tag = andPart,
                                Text = segDisplay,
                                VerticalAlignment = VerticalAlignment.Center,
                                Foreground = Brushes.Teal,
                                TextDecorations = TextDecorations.Underline
                            };

                            var segBorder = new Border
                            {
                                Background = Brushes.Transparent,
                                Cursor = new Cursor(StandardCursorType.Hand),
                                Child = segTb
                            };

                            if (_data.FieldSources.Count > 0)
                            {
                                segBorder.Bind(ToolTip.TipProperty, new Binding("EntityId")
                                {
                                    Converter = new Converters.FieldSourceConverter(),
                                    ConverterParameter = refColName
                                });
                            }

                            var capturedPart = andPart;
                            _cellInteraction.AttachMultiRefSegmentHandlers(
                                segBorder, capturedPart, item, targetType, refAttr,
                                pattern, e.PropertyName, refColName);

                            segBorder.PointerExited += (_, _) =>
                            {
                                try
                                {
                                    if (_data.FieldSources.Count > 0)
                                        segBorder.Bind(ToolTip.TipProperty, new Binding("EntityId")
                                        {
                                            Converter = new Converters.FieldSourceConverter(),
                                            ConverterParameter = refColName
                                        });
                                }
                                catch (Exception ex)
                                {
                                    Serilog.Log.Logger.Verbose(ex,
                                        "[CTF:PointerExited] Failed to bind FieldSource tooltip on segBorder for col={Col}",
                                        refColName);
                                }
                            };

                            wrapPanel.Children.Add(segBorder);
                        }
                    }

                    // Single Ctrl+Click handler on wrapPanel covering the entire cell
                    _cellInteraction.AttachMultiRefCellHandler(
                        wrapPanel, item, targetType, refAttr, pattern, e.PropertyName);
                    return grid;
                }
            }),
            CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
            {
                if (isMulti)
                {
                    var textBox = new TextBox
                    {
                        AcceptsReturn = false,
                        TextWrapping = TextWrapping.NoWrap
                    };
                    textBox.Bind(TextBox.TextProperty, new Binding(property.Name));
                    return textBox;
                }
                var comboBox = new ComboBox
                {
                    MaxDropDownHeight = 200,
                    IsEditable = true
                };
                comboBox.Bind(ComboBox.TextProperty, new Binding(property.Name)
                {
                    TargetNullValue = "",
                    FallbackValue = ""
                });
                if (_data.ReferenceLookups.TryGetValue(targetType, out var options) && options.Count > 0)
                {
                    var displayItems = options.OfType<IEntity>().Select(entity =>
                    {
                        var idProp = entity.GetType().GetProperties()
                            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>() != null &&
                                                 (p.PropertyType == typeof(int) || p.PropertyType == typeof(long)));
                        var nameProp = entity.GetType().GetProperty("Name") ??
                                       entity.GetType().GetProperty("strName");
                        var idVal = idProp?.GetValue(entity)?.ToString() ?? "?";
                        var nameVal = nameProp?.GetValue(entity)?.ToString() ?? "";
                        return (object)$"{idVal}: {nameVal}";
                    }).ToList();
                    comboBox.ItemsSource = displayItems;
                    comboBox.SelectionChanged += (_, _) =>
                    {
                        if (comboBox.SelectedItem is string s)
                        {
                            var colonIdx = s.IndexOf(':');
                            if (colonIdx > 0)
                                comboBox.Text = s[..colonIdx].Trim();
                        }
                    };
                }
                return comboBox;
            })
        };
    }

    // ── LongText column builder ──────────────────────────────────────────

    private void BuildLongTextColumn(DataGridAutoGeneratingColumnEventArgs e,
        PropertyInfo property, StackPanel headerPanel)
    {
        e.Column = new DataGridTemplateColumn
        {
            Header = headerPanel,
            SortMemberPath = e.PropertyName,
            Width = new DataGridLength(280),
            CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
            {
                var value = property.GetValue(item);
                var tb = new TextBlock
                {
                    Text = value?.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0)
                };
                if (_data.FieldSources.Count > 0 && item is IEntity)
                {
                    var ltColName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                    var grid = new Grid { MinHeight = 20 };
                    grid.Children.Add(tb);
                    grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                    {
                        Converter = new Converters.FieldSourceConverter(),
                        ConverterParameter = ltColName
                    });
                    grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                    {
                        Converter = new Converters.FieldConflictBackgroundConverter(),
                        ConverterParameter = ltColName
                    });
                    return grid;
                }
                return tb;
            }),
            CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
            {
                var textBox = new TextBox
                {
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 120
                };
                textBox.Bind(TextBox.TextProperty, new Binding(property.Name));
                return textBox;
            })
        };
    }

    // ── Enum column builder ──────────────────────────────────────────────

    private void BuildEnumColumn(DataGridAutoGeneratingColumnEventArgs e,
        PropertyInfo property, StackPanel headerPanel)
    {
        var enumValues = Enum.GetValues(property.PropertyType);
        e.Column = new DataGridTemplateColumn
        {
            Header = headerPanel,
            SortMemberPath = e.PropertyName,
            Width = new DataGridLength(120),
            CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
            {
                var value = property.GetValue(item);
                var tb = new TextBlock
                {
                    Text = value?.ToString() ?? "",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0)
                };
                if (_data.FieldSources.Count > 0 && item is IEntity)
                {
                    var enumColName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                    var grid = new Grid { MinHeight = 20 };
                    grid.Children.Add(tb);
                    grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                    {
                        Converter = new Converters.FieldSourceConverter(),
                        ConverterParameter = enumColName
                    });
                    grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                    {
                        Converter = new Converters.FieldConflictBackgroundConverter(),
                        ConverterParameter = enumColName
                    });
                    return grid;
                }
                return tb;
            }),
            CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, _) =>
            {
                var comboBox = new ComboBox
                {
                    ItemsSource = enumValues,
                };
                comboBox.Bind(ComboBox.SelectedValueProperty, new Binding(property.Name));
                return comboBox;
            })
        };
    }

    // ── Default column builder ───────────────────────────────────────────

    private void BuildDefaultColumn(DataGridAutoGeneratingColumnEventArgs e,
        PropertyInfo property, StackPanel headerPanel)
    {
        var colAttrForFs = property.GetCustomAttribute<ColumnAttribute>();
        var colNameForFs = colAttrForFs?.Name ?? e.PropertyName;
        var hasFieldSources = _data.FieldSources.Count > 0;

        var colWidth = property.PropertyType == typeof(int) || property.PropertyType == typeof(long) ? new DataGridLength(80)
            : property.PropertyType == typeof(float) || property.PropertyType == typeof(double) ? new DataGridLength(90)
            : new DataGridLength(160);

        var isNumeric = property.PropertyType == typeof(int) || property.PropertyType == typeof(long)
            || property.PropertyType == typeof(float) || property.PropertyType == typeof(double);

        if (hasFieldSources || isNumeric)
        {
            e.Column = new DataGridTemplateColumn
            {
                Header = headerPanel,
                SortMemberPath = e.PropertyName,
                Width = colWidth,
                IsReadOnly = false,
                CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) =>
                {
                    var tb = new TextBlock
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0)
                    };
                    tb.Bind(TextBlock.TextProperty, new Binding(property.Name));
                    var grid = new Grid
                    {
                        MinHeight = 20,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    grid.Children.Add(tb);
                    if (item is IEntity)
                    {
                        grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                        {
                            Converter = new Converters.FieldSourceConverter(),
                            ConverterParameter = colNameForFs
                        });
                        grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                        {
                            Converter = new Converters.FieldConflictBackgroundConverter(),
                            ConverterParameter = colNameForFs
                        });
                    }
                    return grid;
                }),
                CellEditingTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((_, _) => CreateEditControl(property))
            };
        }
        else
        {
            e.Column.Header = headerPanel;
            e.Column.IsReadOnly = false;
            e.Column.SortMemberPath ??= e.PropertyName;
            e.Column.Width = colWidth;
            if (e.Column is DataGridTextColumn tc && property.PropertyType == typeof(string))
                tc.Binding = new Binding(property.Name);
        }
    }

    private static Control CreateEditControl(PropertyInfo property)
    {
        if (property.PropertyType == typeof(int) || property.PropertyType == typeof(long))
        {
            var nud = new NumericUpDown { Increment = 1m, FormatString = "0" };
            nud.Bind(NumericUpDown.ValueProperty, new Binding(property.Name));
            return nud;
        }
        if (property.PropertyType == typeof(float) || property.PropertyType == typeof(double))
        {
            var nud = new NumericUpDown { Increment = 0.1m, FormatString = "0.##" };
            nud.Bind(NumericUpDown.ValueProperty, new Binding(property.Name));
            return nud;
        }
        var tb = new TextBox();
        tb.Bind(TextBox.TextProperty, new Binding(property.Name));
        return tb;
    }
}

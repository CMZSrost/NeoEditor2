using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Microsoft.EntityFrameworkCore;
using Avalonia.Controls.Templates;
using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Services;

namespace NeoEditor.Helper
{
    public static class GenericDataGridHelper
    {
        // ── Router bridge ────────────────────────────────────────────────────
        // Navigation is delegated to INavigationRouter (DI singleton).
        // Lazily resolved from App.ServiceProvider since GDH is static and
        // cannot use constructor injection.
        private static INavigationRouter? _router;
        private static INavigationRouter Router =>
            _router ??= App.ServiceProvider!.GetRequiredService<INavigationRouter>();

        // ── Instance store bridge ───────────────────────────────────────────
        // Stores are pushed by SearchableDataGrid.OnAttachedToVisualTree via SetActiveStores().
        // All public static properties delegate to the active stores for converter compatibility.
        private static EntityMergeStore? _activeMergeStore;
        private static EditTrackingStore? _activeEditStore;

        /// <summary>The currently active merge store (for ReferenceIndex access).</summary>
        public static EntityMergeStore? ActiveMergeStore => _activeMergeStore;

        /// <summary>Global persistent browser store. Built once from the "Game" profile data,
        /// reused across the entire editor session. Only rebuilt on profile change or mod save.</summary>
        private static EntityMergeStore? _browserStore;
        public static EntityMergeStore? BrowserStore
        {
            get => _browserStore;
            set { _browserStore = value; }
        }

        // ── Column metadata cache ──────────────────────────────────────────
        // Maps (DataGrid, propertyName) → ReferenceFieldAttribute.
        // Populated by ConfigureColumn, consumed by SearchableDataGrid navigation to
        // avoid the Bug 2 column-index mismatch (RowHeader offset in Children.IndexOf).
        internal static Dictionary<DataGrid, Dictionary<string, ReferenceFieldAttribute>> ColumnMetaCache { get; }
            = new();

        /// <summary>Set the active per-DataGrid stores. Called by SearchableDataGrid on attach/detach.</summary>
        public static void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore)
        {
            Serilog.Log.Logger.Debug("[GDH:SetActive] oldES={OldES:x} newES={NewES:x} oldMS={OldMS:x} newMS={NewMS:x} ec={EC} ov={OV}",
                _activeEditStore?.GetHashCode(), editStore?.GetHashCode(),
                _activeMergeStore?.GetHashCode(), mergeStore?.GetHashCode(),
                editStore?.EditedCells.Count ?? -1, mergeStore?.OverriddenEntityIds.Count ?? -1);
            _activeMergeStore = mergeStore;
            _activeEditStore = editStore;
        }

        // Empty collections returned when no store is active (never expected during normal operation)
        private static readonly Dictionary<Type, List<object>> _emptyRefLookups = new();
        private static readonly HashSet<(string, string)> _emptyEditedCells = new();
        private static readonly HashSet<string> _emptyNewIds = new();
        private static readonly HashSet<string> _emptyOverridden = new();
        private static readonly Dictionary<string, string> _emptyModNames = new();
        private static readonly Dictionary<string, string> _emptyNamespace = new();
        private static readonly Dictionary<string, int> _emptyMergedIds = new();
        private static readonly Dictionary<(string, string), string> _emptyFieldSources = new();
        private static readonly HashSet<(string, string)> _emptyFieldConflicts = new();
        private static readonly Dictionary<(Type, string), string?> _emptySubjectCache = new();
        private static readonly Dictionary<string, List<OverlayChainEntry>> _emptyOverlayChain = new();

        /// <summary>Populated by ModGameDataTabsView before rendering, used for ComboBox items in reference columns.</summary>
        public static Dictionary<Type, List<object>> ReferenceLookups =>
            _activeMergeStore?.ReferenceLookups ?? _browserStore?.ReferenceLookups ?? _emptyRefLookups;

        /// <summary>Field description service for column tooltips. Set by App startup.</summary>
        public static Services.FieldDescriptionService? FieldDescriptions { get; set; }

        /// <summary>Clear the Subject lookup cache (called on data reload).</summary>
        public static void ClearSubjectCache() => (_activeMergeStore?.SubjectCache ?? _emptySubjectCache).Clear();

        /// <summary>Build a deduped dictionary of entities by int key from the active ReferenceLookups. Highest ModId wins for dupes.</summary>
        public static Dictionary<int, T> GetEntities<T>() where T : IEntity
        {
            if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null) return [];
            var keyProp = EntityHelper.ResolveKeyProperty(typeof(T));
            if (keyProp is null) return [];
            return list.OfType<T>()
                .GroupBy(e => keyProp.GetValue(e) is int id ? id : 0)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.ModId).First());
        }

        /// <summary>Build a deduped dictionary keyed by a composite string selector.
        /// When sourceModId is provided, same-mod entities take priority over higher ModId ones.
        /// Otherwise highest ModId wins for dupes.</summary>
        public static Dictionary<string, T> GetCompositeEntities<T>(Func<T, string> keySelector,
            int sourceModId = int.MaxValue) where T : IEntity
        {
            if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null) return [];
            return list.OfType<T>()
                .GroupBy(keySelector)
                .ToDictionary(g => g.Key, g =>
                {
                    var ordered = g.OrderByDescending(e => e.ModId).ToList();
                    if (sourceModId < int.MaxValue)
                    {
                        var sameMod = ordered.FirstOrDefault(e => e.ModId == sourceModId);
                        if (sameMod is not null) return sameMod;
                    }
                    return ordered[0];
                });
        }

        /// <summary>Get all entities of a type deduped by int key. Highest ModId wins for dupes.</summary>
        public static List<T> GetDedupedEntities<T>() where T : IEntity
        {
            if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null) return [];
            var keyProp = EntityHelper.ResolveKeyProperty(typeof(T));
            return list.OfType<T>()
                .GroupBy(e => keyProp?.GetValue(e)?.ToString() ?? e.EntityId)
                .Select(g => g.OrderByDescending(e => e.ModId).First())
                .ToList();
        }

        /// <summary>Format a single segment of a multi-value reference field with Subject name.</summary>
        private static string FormatSegmentDisplay(string segment, Type targetType, string? pattern,
            string sourceEntityId, string propertyName, string? targetKey = null)
        {
            if (string.IsNullOrWhiteSpace(segment)) return segment;

            var pat = ReferencePattern.FromName(pattern);
            var rawId = pat.ExtractRawId(segment);
            var parsed = ReferenceParser.ParseWithPattern(segment, pattern);

            var subject = LookupSubjectByRawId(targetType, rawId, sourceEntityId, propertyName, targetKey);
            if (string.IsNullOrEmpty(subject)) return segment;

            return pat.FormatDisplay(segment, subject, parsed.ModName);
        }

        /// <summary>Set by Ctrl+PointerPressed, checked by ContextRequested to suppress right-click menu.</summary>
        private static bool _ctrlWasPressed;

        /// <summary>Set after a Tunnel handler has processed navigation, to prevent double-navigation from nested handlers.</summary>
        internal static bool NavigationHandled { get; set; }

        public static void RaiseCellEditCommitted(IEntity entity, string propertyName, object? oldValue, object? newValue)
            => App.ServiceProvider!.GetRequiredService<CommunityToolkit.Mvvm.Messaging.IMessenger>()
                .Send(new Data.Messages.CellEditCommittedMessage(entity, propertyName, oldValue, newValue));

        public static void RaiseCloneRowRequested(IEntity entity)
            => App.ServiceProvider!.GetRequiredService<CommunityToolkit.Mvvm.Messaging.IMessenger>()
                .Send(new Data.Messages.CloneRowRequestedMessage(entity));

        public static void RaiseFindReferencesRequested(IEntity entity)
            => App.ServiceProvider!.GetRequiredService<CommunityToolkit.Mvvm.Messaging.IMessenger>()
                .Send(new Data.Messages.FindReferencesRequestedMessage(entity));

        // ── Delegating properties ───────────────────────────────────────────

        public static HashSet<(string EntityId, string ColumnName)> EditedCells =>
            _activeEditStore?.EditedCells ?? _emptyEditedCells;

        public static HashSet<string> NewEntityIds =>
            _activeEditStore?.NewEntityIds ?? _emptyNewIds;

        public static HashSet<string> OverriddenEntityIds =>
            _activeMergeStore?.OverriddenEntityIds ?? _emptyOverridden;

        public static Dictionary<string, string> EntityModNames =>
            _activeMergeStore?.EntityModNames ?? _browserStore?.EntityModNames ?? _emptyModNames;

        /// <summary>Entity EntityId → strModName (namespace). Base game = "0".</summary>
        public static Dictionary<string, string> EntityNamespaces =>
            _activeMergeStore?.EntityNamespaces ?? _browserStore?.EntityNamespaces ?? _emptyModNames;

        public static Dictionary<string, string> NamespaceToModName =>
            _activeMergeStore?.NamespaceToModName ?? _emptyNamespace;

        public static Dictionary<string, int> EntityMergedIds =>
            _activeMergeStore?.EntityMergedIds ?? _browserStore?.EntityMergedIds ?? _emptyMergedIds;

        public static Dictionary<(string, string), string> FieldSources =>
            _activeMergeStore?.FieldSources ?? _emptyFieldSources;

        public static HashSet<(string, string)> FieldConflicts =>
            _activeMergeStore?.FieldConflicts ?? _emptyFieldConflicts;

        public static Dictionary<string, List<OverlayChainEntry>> OverlayChainDisplay =>
            _activeMergeStore?.OverlayChainDisplay ?? _emptyOverlayChain;

        public static string GetEntityModName(IEntity entity) =>
            EntityModNames.TryGetValue(entity.EntityId, out var name) ? name : "";

        public static int GetEntityMergedId(IEntity entity) =>
            EntityMergedIds.TryGetValue(entity.EntityId, out var id) ? id : 0;

        public static string? GetFieldSource(string entityId, string colName) =>
            FieldSources.TryGetValue((entityId, colName), out var name) ? name : null;

        /// <summary>Captures current helper state for later restoration (tab switch).</summary>
        public static object TakeSnapshot()
        {
            var mergeStore = _activeMergeStore;
            var editStore = _activeEditStore;
            return (mergeStore, editStore);
        }

        /// <summary>Restores helper state from a snapshot.</summary>
        public static void RestoreSnapshot(object snapshot)
        {
            if (snapshot is not (EntityMergeStore mergeStore, EditTrackingStore editStore)) return;
            SetActiveStores(mergeStore, editStore);
        }

        private static Dictionary<(Type, string), string?> SubjectCache =>
            _activeMergeStore?.SubjectCache ?? _emptySubjectCache;

        /// <summary>Look up an entity's Subject by type and business key id from ReferenceLookups.</summary>
        private static string? LookupSubject(Type entityType, int id)
        {
            return LookupSubjectByRawId(entityType, id.ToString(), "", "");
        }

        /// <summary>Look up an entity's Subject using TargetKey to decompose the raw ID value.
        /// Prefers the match with the highest ModId (overlay chain winner).
        /// Falls back to secondaryEntityType if primary lookup fails.</summary>
        private static string? LookupSubjectByRawId(Type entityType, string rawId,
            string sourceEntityId, string propertyName,
            string? targetKey = null,
            Type? secondaryEntityType = null, string? secondaryTargetKey = null)
        {
            return ReferenceResolver.Instance.LookupSubject(sourceEntityId, propertyName, entityType, rawId, secondaryEntityType);
        }

        /// <summary>Resolve an entity's EntityId using TargetKey decomposition.
        /// Prefers the match with the highest ModId (overlay chain winner).</summary>
        public static string? ResolveEntityIdByTargetKey(Type entityType, string rawId, string? targetKey,
            string sourceEntityId = "", string propertyName = "")
        {
            // Phase 2: try O(1) index lookup first, fall through to FindBestMatch on miss
            var index = _activeMergeStore?.Index;
            if (index is not null)
            {
                var entityId = index.Lookup(sourceEntityId, propertyName, entityType, rawId);
                if (entityId is not null) return entityId;
                // Index miss — fall through to FindBestMatch
            }

            var best = FindBestMatch(entityType, rawId, targetKey, sourceEntityId, propertyName);
            return best?.EntityId;
        }

        internal static IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey)
            => FindBestMatch(entityType, rawId, targetKey, "", "");

        /// <summary>Find the best entity match with source context for context-aware index lookup.</summary>
        internal static IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
            string sourceEntityId, string propertyName)
        {
            // Try index lookup first — both active merge store AND browser store
            var index = _activeMergeStore?.Index ?? _browserStore?.Index;
            if (index is not null)
            {
                var entityId = index.Lookup(sourceEntityId, propertyName, entityType, rawId);
                if (entityId is not null
                    && ReferenceLookups.TryGetValue(entityType, out var indexedEntities))
                {
                    foreach (var obj in indexedEntities)
                    {
                        if (obj is IEntity e && e.EntityId == entityId)
                            return e;
                    }
                }
                // Index miss — fall through to O(n) scan below (don't return null early)
            }

            // Index miss — fall through to shared O(n) fallback in ReferenceResolver
            var sourceNs = !string.IsNullOrWhiteSpace(sourceEntityId) && EntityNamespaces.TryGetValue(sourceEntityId, out var sn)
                ? sn : null;
            // Compute sourceModId for same-mod priority + ModId cap
            var sourceModId = int.MaxValue;
            if (!string.IsNullOrWhiteSpace(sourceEntityId))
            {
                foreach (var (_, entities) in ReferenceLookups)
                {
                    foreach (var obj in entities)
                    {
                        if (obj is IEntity e && e.EntityId == sourceEntityId)
                        {
                            sourceModId = e.ModId;
                            break;
                        }
                    }
                    if (sourceModId < int.MaxValue) break;
                }
            }
            return ReferenceResolver.Instance.FallbackLookup(entityType, rawId, sourceNs, sourceModId);
        }

        public static void NavigateTo(Type entityType, int id)
        {
            // Try to resolve business key → EntityId via the active index
            var entityId = _activeMergeStore?.Index.LookupGlobal(entityType, id.ToString());
            if (entityId is not null)
            {
                Router.Navigate(entityType, entityId);
                return;
            }
            Serilog.Log.Logger.Warning("[GDH:Nav] Could not resolve {EntityType} id={Id} to EntityId", entityType.Name, id);
        }

        public static void PeekEntity(Type entityType, string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return;
            // Find the actual entity for the ValueEditor panel
            IEntity? entity = null;
            if (ReferenceLookups.TryGetValue(entityType, out var list) && list is not null)
                entity = list.OfType<IEntity>().FirstOrDefault(e => e.EntityId == entityId);
            App.ServiceProvider!.GetRequiredService<IMessenger>()
                .Send(new VisualEditorRequestedMessage(entityType, entity));
        }

        public static void NavigateToByEntityId(Type entityType, string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return;
            Router.Navigate(entityType, entityId);
        }

        public static List<OverlayChainEntry> GetOverlayChain(IEntity entity)
        {
            return OverlayChainDisplay.TryGetValue(entity.EntityId, out var chain) ? chain : [];
        }

        /// <summary>Navigate to reference ALWAYS (even if peek handler is active). Used by Ctrl+LeftClick.</summary>
        internal static void NavigateToReferenceForce(Type targetType, string rawId, string? targetKey,
            Type? secondaryTargetType = null, string? secondaryTargetKey = null,
            string sourceEntityId = "", string propertyName = "")
        {
            if (rawId == "0") return;

            // Resolve entity
            var (resolvedType, targetEntity) = ResolveWithSecondary(targetType, rawId, targetKey,
                secondaryTargetType, secondaryTargetKey, sourceEntityId, propertyName);

            // Peek via router
            Router.Peek(resolvedType, targetEntity?.EntityId ?? rawId, targetEntity);

            // Navigate via router (using resolved EntityId when available)
            var entityId = ResolveEntityIdByTargetKey(resolvedType, rawId, targetKey, sourceEntityId, propertyName);
            bool navigated = false;
            if (entityId is not null)
                navigated = Router.Navigate(resolvedType, entityId);

            // Fallback: if router didn't find a target, try numeric-only lookup
            if (!navigated)
            {
                var colonIdx = rawId.IndexOf(':');
                var numericPart = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;
                if (int.TryParse(numericPart, out var intId) && intId >= 0)
                    NavigateTo(resolvedType, intId);
            }
        }

        /// <summary>Resolve a reference trying primary then secondary target.</summary>
        private static (Type resolvedType, IEntity? entity) ResolveWithSecondary(
            Type targetType, string rawId, string? targetKey,
            Type? secondaryTargetType, string? secondaryTargetKey,
            string sourceEntityId = "", string propertyName = "")
        {
            var entity = FindBestMatch(targetType, rawId, targetKey, sourceEntityId, propertyName);
            if (entity is not null) return (targetType, entity);

            if (secondaryTargetType is not null)
            {
                entity = FindBestMatch(secondaryTargetType, rawId, secondaryTargetKey, sourceEntityId, propertyName);
                if (entity is not null) return (secondaryTargetType, entity);
            }

            var colonIdx = rawId.IndexOf(':');
            var numericPart = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;
            if (int.TryParse(numericPart, out var intId) && intId >= 0)
                entity = FindBestMatch(targetType, intId.ToString(), null, sourceEntityId, propertyName);

            return (targetType, entity);
        }

        public static void ConfigureColumn<T>(DataGridAutoGeneratingColumnEventArgs e, Func<string, string> localizer)
        {
            ConfigureColumn(e, localizer, typeof(T));
        }

        /// <summary>
        /// 根据模型上的特性配置自动生成的列
        /// </summary>
        /// <typeparam name="T">模型类型</typeparam>
        /// <param name="e">事件参数</param>
        /// <param name="localizer">本地化函数：接收资源键，返回本地化字符串</param>
        public static void ConfigureColumn(DataGridAutoGeneratingColumnEventArgs e, Func<string, string> localizer,
            Type modelType)
        {
            var property = modelType.GetProperty(e.PropertyName);
            if (property == null) return;

            // 1. 如果没有 [Column] 特性，则不生成该列（视为内部字段）
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
            if (columnAttr == null)
            {
                e.Cancel = true;
                return;
            }

            string headerText = property.Name; // 默认用属性名

            // 2. Tooltip: prefer .docx field description, then *Desc resource, fall back to short display name
            var displayAttr = property.GetCustomAttribute<DisplayAttribute>();
            string comment = "";

            // Try field description from the .docx document first
            var tableAttr = modelType.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
            var tableName = tableAttr?.Name ?? modelType.Name.ToLowerInvariant();
            var docxDesc = FieldDescriptions?.GetDescription(tableName, e.PropertyName);
            if (!string.IsNullOrWhiteSpace(docxDesc))
            {
                comment = docxDesc;
            }
            else if (displayAttr != null && !string.IsNullOrEmpty(displayAttr.Name))
            {
                var descKey = displayAttr.Name + "Desc";
                var descValue = localizer(descKey);
                // Localizer returns the key itself when resource not found
                comment = descValue != descKey ? descValue : localizer(displayAttr.Name);
            }

            // 4. 构建自定义列头（包含文本和可选的工具提示）
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4};
            var headerTextBlock = new TextBlock { Text = headerText, VerticalAlignment = VerticalAlignment.Center };
            headerPanel.Children.Add(headerTextBlock);
            
            if (!string.IsNullOrEmpty(comment))
            {
                // 为文本块附加工具提示，也可以添加一个信息图标
                ToolTip.SetTip(headerPanel, comment);
                // 可选：添加一个信息图标（需引入 FluentIcons 或其它图标库）
                // var icon = new PathIcon { Data = (StreamGeometry)Application.Current.FindResource("InfoIcon") };
                // ToolTip.SetTip(icon, comment);
                // headerPanel.Children.Add(icon);
            }

            // 5. ReferenceField columns → display with teal style, editable as ComboBox or TextBox
            var refAttr = property.GetCustomAttribute<ReferenceFieldAttribute>();
            if (refAttr != null)
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
                    CellTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        var raw = property.GetValue(item)?.ToString() ?? "";
                        string display;
                        if (!isMulti)
                        {
                            var rawId = ReferenceParser.ExtractRawId(raw, pattern);
                            var sourceEid = (item as IEntity)?.EntityId ?? "";
                            var subject = LookupSubjectByRawId(targetType, rawId, sourceEid, e.PropertyName,
                                refAttr.TargetKey, refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey);
                            if (!string.IsNullOrEmpty(subject))
                            {
                                var parsed = ReferenceParser.ParseWithPattern(raw, pattern);
                                display = ReferencePattern.FromName(pattern).FormatDisplay(raw, subject, parsed.ModName);
                            }
                            else
                            {
                                display = raw;
                            }
                        }
                        else
                        {
                            display = raw;
                        }
                        var tb = new TextBlock
                        {
                            Text = display,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Avalonia.Thickness(4, 0),
                            TextWrapping = isMulti ? TextWrapping.Wrap : TextWrapping.NoWrap,
                            TextTrimming = isMulti ? TextTrimming.CharacterEllipsis : TextTrimming.CharacterEllipsis,
                            Foreground = Avalonia.Media.Brushes.Teal,
                            TextDecorations = isMulti ? null : Avalonia.Media.TextDecorations.Underline,
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                            MaxHeight = isMulti ? 60 : double.PositiveInfinity
                        };
                        var grid = new Grid
                        {
                            MinHeight = 20,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Center,
                            Background = Avalonia.Media.Brushes.Transparent
                        };
                        grid.Children.Add(tb);

                        if (isMulti)
                        {
                            // Multi-value: split into individual elements for visual clarity.
                            // Navigation via right-click context menu on the grid (reliable event routing).
                            var wrapPanel = new WrapPanel();
                            grid.Children.Clear();
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
                                        Foreground = Avalonia.Media.Brushes.Teal
                                    });

                                // Detect secondary separator within the segment
                                // If primary is ',' → secondary is '|' (OR); if primary is '|' → secondary is ',' (AND)
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
                                            Foreground = Avalonia.Media.Brushes.Gray
                                        });

                                    var segDisplay = FormatSegmentDisplay(andPart, targetType, pattern,
                                        (item as IEntity)?.EntityId ?? "", e.PropertyName, refAttr.TargetKey);
                                    var segTb = new TextBlock
                                    {
                                        Tag = andPart, // raw segment for navigation lookup
                                        Text = segDisplay,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Foreground = Avalonia.Media.Brushes.Teal,
                                        TextDecorations = Avalonia.Media.TextDecorations.Underline
                                    };

                                    var segBorder = new Border
                                    {
                                        Background = Avalonia.Media.Brushes.Transparent,
                                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                                        Child = segTb
                                    };

                                    if (FieldSources.Count > 0)
                                    {
                                        segBorder.Bind(ToolTip.TipProperty, new Binding("EntityId")
                                        {
                                            Converter = new Converter.FieldSourceConverter(),
                                            ConverterParameter = refColName
                                        });
                                    }

                                    var capturedPart = andPart;
                                    // Ctrl+Hover
                                    segBorder.AddHandler(InputElement.PointerMovedEvent, (_, pmArgs) =>
                                    {
                                        try
                                        {
                                            if (pmArgs is PointerEventArgs pe && (pe.KeyModifiers & KeyModifiers.Control) != 0)
                                            {
                                                var partRawId = ReferenceParser.ExtractRawId(capturedPart, pattern);
                                                var subject = LookupSubjectByRawId(targetType, partRawId,
                                                    (item as IEntity)?.EntityId ?? "", e.PropertyName,
                                                    refAttr.TargetKey, refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey);
                                                var label = string.IsNullOrEmpty(subject) ? capturedPart : $"{subject} ({partRawId})";
                                                ToolTip.SetTip(segBorder, $"{targetType.Name}: {label}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Serilog.Log.Logger.Verbose(ex, "[GDH:Hover] Multi-seg hover threw for {TargetType}", targetType.Name);
                                        }
                                    }, RoutingStrategies.Bubble, true);

                                    segBorder.PointerExited += (_, _) =>
                                    {
                                        try
                                        {
                                            if (FieldSources.Count > 0)
                                                segBorder.Bind(ToolTip.TipProperty, new Binding("EntityId")
                                                {
                                                    Converter = new Converter.FieldSourceConverter(),
                                                    ConverterParameter = refColName
                                                });
                                        }
                                        catch { }
                                    };

                                    // Ctrl+LeftClick = navigate  |  Ctrl+RightClick = peek
                                    segBorder.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
                                    {
                                        try
                                        {
                                            if (args is PointerPressedEventArgs pp && (pp.KeyModifiers & KeyModifiers.Control) != 0)
                                            {
                                                if (NavigationHandled) { NavigationHandled = false; return; }
                                                _ctrlWasPressed = true;
                                                var clickedRawId = ReferenceParser.ExtractRawId(capturedPart, pattern);
                                                if (string.IsNullOrWhiteSpace(clickedRawId)) return;
                                                pp.Handled = true;

                                                var point = pp.GetCurrentPoint(segBorder);
                                                if (point.Properties.IsRightButtonPressed)
                                                {
                                                    var srcEid = (item as IEntity)?.EntityId ?? "";
                                                    var target = FindBestMatch(targetType, clickedRawId, refAttr.TargetKey, srcEid, e.PropertyName)
                                                        ?? (refAttr.SecondaryTargetEntityType is not null
                                                            ? FindBestMatch(refAttr.SecondaryTargetEntityType, clickedRawId, refAttr.SecondaryTargetKey, srcEid, e.PropertyName)
                                                            : null)
                                                        ?? (int.TryParse(clickedRawId, out var intId) && intId >= 0
                                                            ? FindBestMatch(targetType, intId.ToString(), null, srcEid, e.PropertyName)
                                                            : null);
                                                    try
                                                    {
                                                        Router.Peek(targetType, target?.EntityId ?? clickedRawId, target);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Serilog.Log.Logger.Warning(ex, "[GDH:Peek] Multi-seg peek threw for {TargetType} rawId={RawId}", targetType.Name, clickedRawId);
                                                    }
                                                }
                                                else
                                                {
                                                    // Jump – peek AND navigate (with secondary target support)
                                                    NavigateToReferenceForce(targetType, clickedRawId, refAttr.TargetKey,
                                                        refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey,
                                                        (item as IEntity)?.EntityId ?? "", e.PropertyName);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Serilog.Log.Logger.Error(ex, "[GDH:PtrPressed] Multi-seg handler threw for {TargetType}", targetType.Name);
                                        }
                                    }, RoutingStrategies.Tunnel, true);

                                    // Suppress right-click context menu after Ctrl+Click
                                    segBorder.AddHandler(Control.ContextRequestedEvent, (_, ctxArgs) =>
                                    {
                                        if (_ctrlWasPressed) { ctxArgs.Handled = true; _ctrlWasPressed = false; }
                                    }, RoutingStrategies.Bubble, true);

                                    wrapPanel.Children.Add(segBorder);
                                }
                            }

                            // Context menu removed — use Ctrl+LeftClick (jump) or Ctrl+RightClick (peek)
                        }
                        else
                        {
                            // Single-value: Ctrl+Hover tooltip, Ctrl+Click to navigate
                            var singleParsed = ReferenceParser.ParseReference(raw);
                            var refColNameSingle = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                            grid.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);

                            // Ctrl+Hover: show jump target
                            grid.AddHandler(InputElement.PointerMovedEvent, (_, pmArgs) =>
                            {
                                try
                                {
                                    if (pmArgs is PointerEventArgs pe && (pe.KeyModifiers & KeyModifiers.Control) != 0)
                                    {
                                        var currentRaw = property.GetValue(item)?.ToString() ?? "";
                                        var rawId = ReferenceParser.ExtractRawId(currentRaw, pattern);
                                        var subject = LookupSubjectByRawId(targetType, rawId,
                                            (item as IEntity)?.EntityId ?? "", e.PropertyName,
                                            refAttr.TargetKey, refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey);
                                        if (!string.IsNullOrEmpty(subject))
                                        {
                                            ToolTip.SetTip(grid, $"{targetType.Name}: {subject} ({rawId})");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Serilog.Log.Logger.Verbose(ex, "[GDH:Hover] Single-val hover threw for {TargetType}", targetType.Name);
                                }
                            }, RoutingStrategies.Bubble, true);

                            grid.PointerExited += (_, _) =>
                            {
                                try
                                {
                                    if (FieldSources.Count > 0 && item is Data.Model.Game.IEntity)
                                        grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                                        {
                                            Converter = new Converter.FieldSourceConverter(),
                                            ConverterParameter = refColNameSingle
                                        });
                                }
                                catch { }
                            };

                            // Ctrl+LeftClick = navigate  |  Ctrl+RightClick = peek
                            grid.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
                            {
                                try
                                {
                                    if (args is PointerPressedEventArgs pp && (pp.KeyModifiers & KeyModifiers.Control) != 0)
                                    {
                                        if (NavigationHandled) { NavigationHandled = false; return; }
                                        _ctrlWasPressed = true;
                                        var currentRaw = property.GetValue(item)?.ToString() ?? "";
                                        var rawId = ReferenceParser.ExtractRawId(currentRaw, pattern);
                                        if (string.IsNullOrWhiteSpace(rawId)) return;
                                        pp.Handled = true;

                                        var point = pp.GetCurrentPoint(grid);
                                        if (point.Properties.IsRightButtonPressed)
                                        {
                                            var srcEid = (item as IEntity)?.EntityId ?? "";
                                            var target = FindBestMatch(targetType, rawId, refAttr.TargetKey, srcEid, e.PropertyName)
                                                ?? (refAttr.SecondaryTargetEntityType is not null
                                                    ? FindBestMatch(refAttr.SecondaryTargetEntityType, rawId, refAttr.SecondaryTargetKey, srcEid, e.PropertyName)
                                                    : null)
                                                ?? (int.TryParse(rawId, out var intId) && intId >= 0
                                                    ? FindBestMatch(targetType, intId.ToString(), null, srcEid, e.PropertyName)
                                                    : null);
                                            try
                                            {
                                                Router.Peek(targetType, target?.EntityId ?? rawId, target);
                                            }
                                            catch (Exception ex)
                                            {
                                                Serilog.Log.Logger.Warning(ex, "[GDH:Peek] Single-val peek threw for {TargetType} rawId={RawId}", targetType.Name, rawId);
                                            }
                                        }
                                        else
                                        {
                                            // Jump – navigate + peek (with secondary target support)
                                            NavigateToReferenceForce(targetType, rawId, refAttr.TargetKey,
                                                refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey,
                                                (item as IEntity)?.EntityId ?? "", e.PropertyName);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Serilog.Log.Logger.Error(ex, "[GDH:PtrPressed] Single-val handler threw for {TargetType}", targetType.Name);
                                }
                            }, RoutingStrategies.Tunnel, true);

                            // Suppress right-click context menu after Ctrl+Click
                            grid.AddHandler(Control.ContextRequestedEvent, (_, ctxArgs) =>
                            {
                                if (_ctrlWasPressed) { ctxArgs.Handled = true; _ctrlWasPressed = false; }
                            }, RoutingStrategies.Bubble, true);

                            // Context menu removed — use Ctrl+LeftClick (jump) or Ctrl+RightClick (peek)
                        }

                        // Conflict background on the grid (tooltip handled per-branch above)
                        if (FieldSources.Count > 0 && item is Data.Model.Game.IEntity)
                        {
                            var refColName2 = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                            grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                            {
                                Converter = new Converter.FieldConflictBackgroundConverter(),
                                ConverterParameter = refColName2
                            });
                        }
                        return grid;
                    }),
                    CellEditingTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        if (isMulti)
                        {
                            // Multi-value: plain TextBox for free-form editing
                            var textBox = new TextBox
                            {
                                AcceptsReturn = false,
                                TextWrapping = TextWrapping.NoWrap
                            };
                            textBox.Bind(TextBox.TextProperty, new Binding(property.Name));
                            return textBox;
                        }
                        // Single-value: ComboBox with lookup
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
                        if (ReferenceLookups.TryGetValue(targetType, out var options) && options.Count > 0)
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
                return;
            }

            // 6. 根据属性类型或 Column.TypeName 决定是否替换为自定义模板列
            if (e.Column is DataGridTextColumn textColumn)
            {
                // 数值类型设置默认格式
                if (property.PropertyType == typeof(double) || property.PropertyType == typeof(float) ||
                    property.PropertyType == typeof(int))
                {
                    // 保留两位小数（可根据需要调整）
                    textColumn.Binding = new Binding(property.Name) { StringFormat = "0.##" };
                }
            }

            // 根据属性类型生成合适的编辑模板
            if (columnAttr.TypeName != null &&
                columnAttr.TypeName.Contains("longtext", StringComparison.OrdinalIgnoreCase))
            {
                // longtext → 多行文本框
                e.Column = new DataGridTemplateColumn
                {
                    Header = headerPanel,
                    SortMemberPath = e.PropertyName,
                    Width = new DataGridLength(280),
                    CellTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        var value = property.GetValue(item);
                        var tb = new TextBlock
                        {
                            Text = value?.ToString(),
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Avalonia.Thickness(4, 0)
                        };
                        if (FieldSources.Count > 0 && item is Data.Model.Game.IEntity)
                        {
                            var ltColName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                            var grid = new Grid { MinHeight = 20 };
                            grid.Children.Add(tb);
                            grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                            {
                                Converter = new Converter.FieldSourceConverter(),
                                ConverterParameter = ltColName
                            });
                            grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                            {
                                Converter = new Converter.FieldConflictBackgroundConverter(),
                                ConverterParameter = ltColName
                            });
                            return grid;
                        }
                        return tb;
                    }),
                    CellEditingTemplate = new FuncDataTemplate<object>((item, _) =>
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
                return;
            }

            // bool → CheckBox
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

            // Enum → ComboBox
            if (property.PropertyType.IsEnum)
            {
                var enumValues = Enum.GetValues(property.PropertyType);
                e.Column = new DataGridTemplateColumn
                {
                    Header = headerPanel,
                    SortMemberPath = e.PropertyName,
                    Width = new DataGridLength(120),
                    CellTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        var value = property.GetValue(item);
                        var tb = new TextBlock
                        {
                            Text = value?.ToString() ?? "",
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Avalonia.Thickness(4, 0)
                        };
                        if (FieldSources.Count > 0 && item is Data.Model.Game.IEntity)
                        {
                            var enumColName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                            var grid = new Grid { MinHeight = 20 };
                            grid.Children.Add(tb);
                            grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                            {
                                Converter = new Converter.FieldSourceConverter(),
                                ConverterParameter = enumColName
                            });
                            grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                            {
                                Converter = new Converter.FieldConflictBackgroundConverter(),
                                ConverterParameter = enumColName
                            });
                            return grid;
                        }
                        return tb;
                    }),
                    CellEditingTemplate = new FuncDataTemplate<object>((_, _) =>
                    {
                        var comboBox = new ComboBox
                        {
                            ItemsSource = enumValues,
                        };
                        comboBox.Bind(ComboBox.SelectedValueProperty, new Binding(property.Name));
                        return comboBox;
                    })
                };
                return;
            }

            // 默认：保留原列类型 + Header
            var colAttrForFs = property.GetCustomAttribute<ColumnAttribute>();
            var colNameForFs = colAttrForFs?.Name ?? e.PropertyName;
            var hasFieldSources = FieldSources.Count > 0;

            var colWidth = property.PropertyType == typeof(int) || property.PropertyType == typeof(long) ? new DataGridLength(80)
                : property.PropertyType == typeof(float) || property.PropertyType == typeof(double) ? new DataGridLength(90)
                : new DataGridLength(160);

            var isInt = property.PropertyType == typeof(int) || property.PropertyType == typeof(long);
            var isNumeric = isInt || property.PropertyType == typeof(float) || property.PropertyType == typeof(double);

            if (hasFieldSources || isNumeric)
            {
                // Template column with field-source tooltip on cell (merge view) or NumericUpDown editing (numeric types)
                e.Column = new DataGridTemplateColumn
                {
                    Header = headerPanel,
                    SortMemberPath = e.PropertyName,
                    Width = colWidth,
                    IsReadOnly = false,
                    CellTemplate = new FuncDataTemplate<object>((item, _) =>
                    {
                        var tb = new TextBlock
                        {
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Avalonia.Thickness(4, 0)
                        };
                        tb.Bind(TextBlock.TextProperty, new Binding(property.Name));
                        var grid = new Grid
                        {
                            MinHeight = 20,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        grid.Children.Add(tb);
                        if (item is Data.Model.Game.IEntity)
                        {
                            grid.Bind(ToolTip.TipProperty, new Binding("EntityId")
                        {
                            Converter = new Converter.FieldSourceConverter(),
                            ConverterParameter = colNameForFs
                        });
                        grid.Bind(Grid.BackgroundProperty, new Binding("EntityId")
                        {
                            Converter = new Converter.FieldConflictBackgroundConverter(),
                            ConverterParameter = colNameForFs
                        });
                        }
                        return grid;
                    }),
                    CellEditingTemplate = new FuncDataTemplate<object>((_, _) => CreateEditControl(property))
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
}
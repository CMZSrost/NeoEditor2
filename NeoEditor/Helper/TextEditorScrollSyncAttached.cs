using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;

namespace NeoEditor.Helper;

public enum ScrollSyncMode
{
    Vertical,
    Horizontal,
    Both
}

public sealed class TextEditorScrollSyncAttached
{
    private TextEditorScrollSyncAttached()
    {
    }

    private sealed class SyncState
    {
        public ScrollViewer? ScrollViewer { get; set; }
        public EventHandler? ScrollHandler { get; set; }
        public bool IsSyncing { get; set; }
    }

    private static readonly ConditionalWeakTable<TextEditor, SyncState> States = new();

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TextEditorScrollSyncAttached, TextEditor, bool>("IsEnabled");

    public static readonly AttachedProperty<TextEditor?> PartnerProperty =
        AvaloniaProperty.RegisterAttached<TextEditorScrollSyncAttached, TextEditor, TextEditor?>("Partner");

    public static readonly AttachedProperty<ScrollSyncMode> ModeProperty =
        AvaloniaProperty.RegisterAttached<TextEditorScrollSyncAttached, TextEditor, ScrollSyncMode>("Mode");

    static TextEditorScrollSyncAttached()
    {
        IsEnabledProperty.Changed.AddClassHandler<TextEditor>(OnConfigurationChanged);
        PartnerProperty.Changed.AddClassHandler<TextEditor>(OnConfigurationChanged);
        ModeProperty.Changed.AddClassHandler<TextEditor>(OnConfigurationChanged);
    }

    public static bool GetIsEnabled(AvaloniaObject element)
    {
        return element.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(AvaloniaObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    public static TextEditor? GetPartner(AvaloniaObject element)
    {
        return element.GetValue(PartnerProperty);
    }

    public static void SetPartner(AvaloniaObject element, TextEditor? value)
    {
        element.SetValue(PartnerProperty, value);
    }

    public static ScrollSyncMode GetMode(AvaloniaObject element)
    {
        return element.GetValue(ModeProperty);
    }

    public static void SetMode(AvaloniaObject element, ScrollSyncMode value)
    {
        element.SetValue(ModeProperty, value);
    }

    private static void OnConfigurationChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        if (!GetIsEnabled(editor) || GetPartner(editor) is null || ReferenceEquals(GetPartner(editor), editor))
        {
            Unsubscribe(editor);
            return;
        }

        Subscribe(editor);
    }

    private static void Subscribe(TextEditor editor)
    {
        var state = States.GetOrCreateValue(editor);
        if (state.ScrollHandler != null)
        {
            return;
        }

        state.ScrollHandler = (_, _) => Sync(editor);
        editor.TextArea.TextView.ScrollOffsetChanged += state.ScrollHandler;
    }

    private static void Unsubscribe(TextEditor editor)
    {
        if (!States.TryGetValue(editor, out var state) || state.ScrollHandler is null)
        {
            return;
        }

        editor.TextArea.TextView.ScrollOffsetChanged -= state.ScrollHandler;
        state.ScrollHandler = null;
        state.ScrollViewer = null;
        state.IsSyncing = false;
    }

    private static void Sync(TextEditor sourceEditor)
    {
        if (!GetIsEnabled(sourceEditor))
        {
            return;
        }

        var targetEditor = GetPartner(sourceEditor);
        if (targetEditor == null || !GetIsEnabled(targetEditor) || ReferenceEquals(sourceEditor, targetEditor))
        {
            return;
        }

        var sourceState = States.GetOrCreateValue(sourceEditor);
        if (sourceState.IsSyncing)
        {
            return;
        }

        var targetState = States.GetOrCreateValue(targetEditor);
        if (targetState.IsSyncing)
        {
            return;
        }

        sourceState.ScrollViewer ??= GetScrollViewer(sourceEditor);
        targetState.ScrollViewer ??= GetScrollViewer(targetEditor);
        if (sourceState.ScrollViewer == null || targetState.ScrollViewer == null)
        {
            return;
        }

        var nextOffset = GetSyncedOffset(sourceState.ScrollViewer.Offset, targetState.ScrollViewer.Offset,
            GetMode(sourceEditor));
        if (nextOffset == targetState.ScrollViewer.Offset)
        {
            return;
        }

        targetState.IsSyncing = true;
        try
        {
            targetState.ScrollViewer.Offset = nextOffset;
        }
        finally
        {
            targetState.IsSyncing = false;
        }
    }

    private static Vector GetSyncedOffset(Vector sourceOffset, Vector targetOffset, ScrollSyncMode mode)
    {
        return mode switch
        {
            ScrollSyncMode.Horizontal => new Vector(sourceOffset.X, targetOffset.Y),
            ScrollSyncMode.Both => new Vector(sourceOffset.X, sourceOffset.Y),
            _ => new Vector(targetOffset.X, sourceOffset.Y)
        };
    }

    private static ScrollViewer? GetScrollViewer(TextEditor editor)
    {
        return typeof(TextEditor).GetProperty("ScrollViewer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(editor) as ScrollViewer;
    }
}
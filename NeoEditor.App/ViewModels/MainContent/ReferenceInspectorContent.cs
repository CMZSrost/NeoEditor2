using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.ViewModels;

namespace NeoEditor.ViewModels.MainContent;

public partial class ReferenceInspectorContent : LocalizedObservableObject
{
    [ObservableProperty] public partial string TargetType { get; set; } = "";
    [ObservableProperty] public partial string TargetSubject { get; set; } = "";
    [ObservableProperty] public partial string TargetRawId { get; set; } = "";
    [ObservableProperty] public partial string TargetEntityId { get; set; } = "";
    [ObservableProperty] public partial int TargetModId { get; set; }
    [ObservableProperty] public partial bool HasContent { get; set; }
    [ObservableProperty] public partial bool IsPinned { get; set; }

    [ObservableProperty] public partial bool CanGoBack { get; set; }
    [ObservableProperty] public partial bool CanGoForward { get; set; }

    public ObservableCollection<PropEntry> Properties { get; } = [];

    private readonly Stack<PeekSnapshot> _backStack = new();
    private readonly Stack<PeekSnapshot> _forwardStack = new();
    private PeekSnapshot? _current;

    /// <summary>Always pushes to history. Auto-displays only if not pinned.</summary>
    public void ShowEntity(Type entityType, string rawId, IEntity? entity)
    {
        var newEntityId = entity?.EntityId ?? "";

        // Same as current → just refresh display
        if (_current is not null && _current.TargetEntityId == newEntityId && newEntityId != "")
        {
            if (!IsPinned)
                ApplySnapshot(TakeSnapshot(entityType, entity));
            return;
        }

        // If entity already in history, pull it to front
        if (newEntityId != "" && TryPopFromHistory(newEntityId))
        {
            _current = TakeSnapshot(entityType, entity);
            UpdateNavButtons();
            if (!IsPinned)
                ApplySnapshot(_current);
            return;
        }

        // New entity: push current to back
        if (_current is not null)
        {
            _backStack.Push(_current);
            _forwardStack.Clear();
        }

        _current = TakeSnapshot(entityType, entity);
        UpdateNavButtons();
        if (!IsPinned)
            ApplySnapshot(_current);
    }

    private PeekSnapshot TakeSnapshot(Type entityType, IEntity? entity)
    {
        var props = new List<PropEntry>();
        if (entity is not null)
        {
            foreach (var prop in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.DeclaringType != typeof(IEntity) && p.GetCustomAttribute<ColumnAttribute>() != null))
            {
                var val = prop.GetValue(entity)?.ToString() ?? "(null)";
                props.Add(new PropEntry(prop.Name, val));
            }
        }
        return new PeekSnapshot(
            entityType.Name,
            entity?.Subject ?? "(target not found)",
            entity?.EntityId ?? "",
            entity?.ModId ?? 0,
            props
        );
    }

    private void ApplySnapshot(PeekSnapshot s)
    {
        TargetType = s.EntityType;
        TargetSubject = s.Subject;
        TargetRawId = s.TargetEntityId;
        TargetEntityId = s.TargetEntityId;
        TargetModId = s.ModId;
        HasContent = s.SavedProperties.Count > 0 || s.TargetEntityId != "";
        Properties.Clear();
        foreach (var p in s.SavedProperties)
            Properties.Add(p);
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    /// <summary>If entityId exists in back or forward stack, pop it out.</summary>
    private bool TryPopFromHistory(string entityId)
    {
        var tmp = new Stack<PeekSnapshot>();
        // Search back
        while (_backStack.Count > 0)
        {
            var e = _backStack.Pop();
            if (e.TargetEntityId == entityId)
            {
                if (_current is not null) _backStack.Push(_current);
                while (tmp.Count > 0) _backStack.Push(tmp.Pop());
                _forwardStack.Clear();
                return true;
            }
            tmp.Push(e);
        }
        while (tmp.Count > 0) _backStack.Push(tmp.Pop());
        // Search forward
        while (_forwardStack.Count > 0)
        {
            var e = _forwardStack.Pop();
            if (e.TargetEntityId == entityId)
            {
                if (_current is not null) _backStack.Push(_current);
                while (tmp.Count > 0) _forwardStack.Push(tmp.Pop());
                return true;
            }
            tmp.Push(e);
        }
        while (tmp.Count > 0) _forwardStack.Push(tmp.Pop());
        return false;
    }

    public void TogglePin()
    {
        IsPinned = !IsPinned;
        // On unpin, sync display to current history entry
        if (!IsPinned && _current is not null)
            ApplySnapshot(_current);
    }

    public void GoBack()
    {
        if (!CanGoBack || _backStack.Count == 0) return;
        if (_current is not null) _forwardStack.Push(_current);
        _current = _backStack.Pop();
        UpdateNavButtons();
        ApplySnapshot(_current); // always update — pin only blocks auto-peek, not manual nav
    }

    public void GoForward()
    {
        if (!CanGoForward || _forwardStack.Count == 0) return;
        if (_current is not null) _backStack.Push(_current);
        _current = _forwardStack.Pop();
        UpdateNavButtons();
        ApplySnapshot(_current);
    }

    public bool ShowEmptyState => !HasContent;

    private void UpdateNavButtons()
    {
        CanGoBack = _backStack.Count > 0;
        CanGoForward = _forwardStack.Count > 0;
    }

    private record PeekSnapshot(
        string EntityType, string Subject, string TargetEntityId, int ModId,
        List<PropEntry> SavedProperties);
}

public record PropEntry(string Name, string Value);

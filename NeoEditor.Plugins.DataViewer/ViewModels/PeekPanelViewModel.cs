using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.DataViewer.ViewModels;

public partial class PeekPanelViewModel : ObservableRecipient
{
    private readonly ILocalizationService _loc;

    public ObservableCollection<PeekBreadcrumb> Breadcrumbs { get; } = [];

    [ObservableProperty] public partial IEntity? CurrentEntity { get; set; }

    [ObservableProperty] public partial bool IsPinned { get; set; }

    [ObservableProperty] public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty] public partial string InfoText { get; set; } = "";

    public PeekPanelViewModel(ILocalizationService localizationService)
    {
        _loc = localizationService;
        IsActive = true;
        InfoText = _loc["EP.EmptyHint"];
    }

    public ILocalizationService Loc => _loc;

    public void Peek(IEntity? targetEntity, IEntity? sourceEntity, string? propertyName)
    {
        if (IsPinned) return;

        SetEntity(targetEntity);

        if (targetEntity != null)
        {
            // Deduplicate consecutive same-entity breadcrumbs
            var last = Breadcrumbs.LastOrDefault();
            if (last?.EntityId != targetEntity.EntityId)
            {
                Breadcrumbs.Add(new PeekBreadcrumb(
                    targetEntity.GetType().Name,
                    targetEntity.Subject ?? targetEntity.EntityId,
                    targetEntity.EntityId,
                    targetEntity));
            }
            while (Breadcrumbs.Count > 10)
                Breadcrumbs.RemoveAt(0);
            IsEmpty = false;
            InfoText = "";
        }
        else
        {
            IsEmpty = false;
            InfoText = "";
        }
    }

    public void Clear()
    {
        if (IsPinned) return;
        CurrentEntity = null;
        Breadcrumbs.Clear();
        IsEmpty = true;
        InfoText = Loc["EP.EmptyHint"];
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void OpenFull()
    {
        if (CurrentEntity != null)
        {
            Messenger.Send(new NavigateToEntityRequestedMessage(
                CurrentEntity.GetType().Name, CurrentEntity.EntityId));
        }
    }

    [RelayCommand]
    private void OpenInSplitView()
    {
        if (CurrentEntity != null)
        {
            Messenger.Send(new OpenInSplitViewMessage(CurrentEntity));
        }
    }

    [RelayCommand]
    private void NavigateBreadcrumb(PeekBreadcrumb? crumb)
    {
        if (crumb == null) return;

        var idx = Breadcrumbs.IndexOf(crumb);
        if (idx < 0) return;

        // Remove all breadcrumbs after this one
        for (int i = Breadcrumbs.Count - 1; i > idx; i--)
            Breadcrumbs.RemoveAt(i);

        // Restore the entity from the breadcrumb
        SetEntity(crumb.Entity);
    }

    private void SetEntity(IEntity? entity)
    {
        CurrentEntity = entity;
    }
}

public record PeekBreadcrumb(
    string TypeName,
    string DisplayName,
    string EntityId,
    IEntity? Entity
)
{
    public string DisplayText => $"{TypeName}: {DisplayName}";
}

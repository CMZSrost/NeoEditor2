using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Enum;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels.Controls;

public partial class TabViewModel<T> : ObservableRecipient where T : class
{
    protected readonly DbSet<T> DbSet;

    public TabViewModel(DbSet<T> dbSet)
    {
        DbSet = dbSet;
        IsActive = true;
        DataView = DbSet.Local;
        DbSet.Local.CollectionChanged += (sender, args) =>
        {
            Console.WriteLine($"{args.OldStartingIndex} -> {args.NewStartingIndex}");
            if (args.NewStartingIndex > 0) DocumentState = DocumentEnum.Edited;
        };
        CollectionSource = new CollectionViewSource { Source = DbSet.Local.ToObservableCollection() };
        Name = typeof(T).Name;
        Application.Current.Dispatcher.BeginInvoke(async () => { await DbSet.LoadAsync(); });
    }

    public CollectionViewSource CollectionSource { get; set; }

    [ObservableProperty] public partial LocalView<T> DataView { get; set; }
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial DocumentEnum DocumentState { get; set; } = DocumentEnum.Default;

    public SolidColorBrush DocumentColor
    {
        get
        {
            return new SolidColorBrush
            {
                Color =
                    DocumentState switch
                    {
                        DocumentEnum.Default => Colors.Green,
                        DocumentEnum.Edited => Colors.Yellow,
                        _ => Colors.Black
                    }
            };
        }
    }

    [RelayCommand]
    public async Task LoadData()
    {
        try
        {
            if (DocumentState != DocumentEnum.Edited)
                await DbSet.LoadAsync();
            else
                Messenger.Send(new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = $"{Name} is edited, please save it first",
                    MsgBox = true
                });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [RelayCommand]
    public void ToggleLast()
    {
        CollectionSource.View.Filter = (dynamic o) => o.isLast_;
        CollectionSource.View.Refresh();
    }


    [RelayCommand]
    public void Save()
    {
        Console.WriteLine("Save!");
        DocumentState = DocumentEnum.Default;
    }
}
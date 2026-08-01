using System;
using System.Collections.ObjectModel;
using AutoMapper;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.DTO;
using NeoEditor.ViewModels;

namespace NeoEditor.Helper.DragDropHandler;

public abstract class WrapDataGridDropHandler<T> : BaseDataGridDropHandler<T> where T : class
{
    protected IMapper _mapper;

    public WrapDataGridDropHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext,
        bool execute)
    {
        try
        {
            // Validate that we are dragging a WrapEntry and dropping onto an ObservableCollection
            if (sourceContext is T sourceItem &&
                dg.ItemsSource is ObservableCollection<T> items)
            {
                // If we are just validating (execute=false), return true to indicate drop is allowed
                if (!execute) return true;
                if (items.Count == 1) return false;

                // If executing, perform the move
                // targetContext is the item we are dropping onto (or null if empty/not on a row)

                var targetItem = TargetItem(dg, e);
                // Helper method from BaseDataGridDropHandler to handle Move/Copy logic
                // It calculates indices and moves the item in the collection
                return RunDropAction(dg, e, execute, sourceItem, targetItem, items);
            }

            Serilog.Log.Logger.Debug("[ModEntryDrop] Validate failed: source={SrcType} target={TgtType}",
                sourceContext?.GetType(), targetContext?.GetType());
            return false;
        }
        catch (Exception exception)
        {
            Serilog.Log.Logger.Error(exception, "[ModEntryDrop] Execute failed");
            return false;
        }
    }

    private static T? TargetItem(DataGrid dg, DragEventArgs e)
    {
        T? targetItem = default;
        var hit = dg.InputHitTest(e.GetPosition(dg)) as StyledElement;
        while (hit != null)
        {
            if (hit is DataGridRow row)
            {
                targetItem = row.DataContext as T;
                break;
            }

            hit = hit.Parent;
        }

        return targetItem;
    }
}

public class ModEntryDropHandler : WrapDataGridDropHandler<ModEntry>
{
    public ModEntryDropHandler(IMapper mapper) : base(mapper) { }

    protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext,
        bool execute)
    {
        var result = base.Validate(dg, e, sourceContext, targetContext, execute);
        if (dg.DataContext is EditProfileViewModel editProfileViewModel)
            editProfileViewModel.NeedNotifyWhenClose = true;

        return result;
    }

    protected override ModEntry MakeCopy(ObservableCollection<ModEntry> parentCollection, ModEntry item)
    {
        return _mapper.Map<ModEntry>(item,
            options => options.AfterMap((o, entry) => entry.Name = $"{entry.Name} Copy")
        );
    }
}
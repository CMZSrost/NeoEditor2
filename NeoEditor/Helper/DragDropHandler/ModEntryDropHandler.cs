using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactions.DragAndDrop;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Model;
using Newtonsoft.Json;

namespace NeoEditor.Helper.DragDropHandler;

public class ModEntryDropHandler : BaseDataGridDropHandler<ModEntry>
{
    protected override bool Validate(DataGrid dg, DragEventArgs e, object? sourceContext, object? targetContext,
        bool execute)
    {
        try
        {
            // Validate that we are dragging an ModEntry and dropping onto an ObservableCollection
            if (sourceContext is ModEntry sourceItem &&
                dg.ItemsSource is ObservableCollection<ModEntry> items)
            {
                // If we are just validating (execute=false), return true to indicate drop is allowed
                if (!execute) return true;

                // If executing, perform the move
                // targetContext is the item we are dropping onto (or null if empty/not on a row)
                var targetItem = targetContext as ModEntry;

                // Helper method from BaseDataGridDropHandler to handle Move/Copy logic
                // It calculates indices and moves the item in the collection
                var option = new JsonSerializerSettings()
                    { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Formatting = Formatting.Indented };
                // Console.WriteLine($"drop: Args: {JsonConvert.SerializeObject(e, option)}");
                Console.WriteLine($"execute: {execute}");
                Console.WriteLine($"source: {JsonConvert.SerializeObject(sourceContext, option)}");
                Console.WriteLine($"target: {JsonConvert.SerializeObject(targetItem, option)}");
                return RunDropAction(dg, e, execute, sourceItem, targetItem, items);
            }

            Console.WriteLine(
                $"Validata failed: sourceContext={JsonConvert.SerializeObject(sourceContext)}, targetContext={JsonConvert.SerializeObject(targetContext)}");
            return false;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            return false;
        }
    }

    protected override ModEntry MakeCopy(ObservableCollection<ModEntry> parentCollection, ModEntry item)
    {
        // Return a clone of the item if you support Copy operations
        return new ModEntry { Name = item.Name + " (Copy)" };
    }
}
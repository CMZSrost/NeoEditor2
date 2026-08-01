using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using NeoEditor.Data.DTO;
using NeoEditor.ViewModels;

namespace NeoEditor;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        if (param is string s)
        {
            return new TextBlock { Text = s };
        }

        var name = param.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        if (data is null)
        {
            return false;
        }

        if (data is IDockable)
            return true;
        // ViewModelBase covers all real VMs; raw INotifyPropertyChanged is too broad
        // and catches data models like GameDataTypeTabItem that aren't controls.
        return data is ViewModelBase;
    }
}
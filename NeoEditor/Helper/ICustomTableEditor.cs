using System;
using Avalonia.Controls;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

public interface ICustomTableEditor
{
    Type EntityType { get; }
    string EditorName { get; }
    Control CreateEditor();
    void UpdateEntity(IEntity? entity);
}

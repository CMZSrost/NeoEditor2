using System.Windows;
using System.Windows.Controls;
using NeoEditor.ViewModels;

namespace NeoEditor.Views;

public class TabTemplateSelector : DataTemplateSelector
{
    public DataTemplate? AttackModeTemplate { get; set; }
    public DataTemplate? BattleMoveTemplate { get; set; } 
    public DataTemplate? RecipeTemplate { get; set; }
    public DataTemplate? ConditionTemplate { get; set; }
    public DataTemplate? CreatureTemplate { get; set; }
    public DataTemplate? ItemTypeTemplate { get; set; }
    public DataTemplate? GenericTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not DtoTabItem dto) return base.SelectTemplate(item, container);
        var name = dto.Name;
        return name.ToLower() switch
        {
            "attackmode" => AttackModeTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "battlemove" => BattleMoveTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "recipe" => RecipeTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "condition" => ConditionTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "creature" => CreatureTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "itemtype" => ItemTypeTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            _ => GenericTemplate ?? base.SelectTemplate(item, container)
        };
    }
}
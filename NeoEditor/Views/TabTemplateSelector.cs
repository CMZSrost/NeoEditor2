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
    public DataTemplate? IngredientTemplate { get; set; }
    public DataTemplate? EncounterTemplate { get; set; }
    public DataTemplate? HextypeTemplate { get; set; }
    public DataTemplate? FactionTemplate { get; set; }
    public DataTemplate? TreasuretableTemplate { get; set; }
    public DataTemplate? MapTemplate { get; set; }
    public DataTemplate? BarterhexTemplate { get; set; }
    public DataTemplate? CamptypeTemplate { get; set; }
    public DataTemplate? ChargeprofileTemplate { get; set; }
    public DataTemplate? ContainertypeTemplate { get; set; }
    public DataTemplate? CreaturesourceTemplate { get; set; }
    public DataTemplate? DatafileTemplate { get; set; }
    public DataTemplate? DmcplaceTemplate { get; set; }
    public DataTemplate? EncountertriggerTemplate { get; set; }
    public DataTemplate? ForbiddenhexTemplate { get; set; }
    public DataTemplate? GamevarTemplate { get; set; }
    public DataTemplate? HeadlineTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }
    public DataTemplate? ItempropTemplate { get; set; }
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
            "ingredient" => IngredientTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "encounter" => EncounterTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "hextype" => HextypeTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "faction" => FactionTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "treasuretable" => TreasuretableTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "map" => MapTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "barterhex" => BarterhexTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "camptype" => CamptypeTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "chargeprofile" => ChargeprofileTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "containertype" => ContainertypeTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "creaturesource" => CreaturesourceTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "datafile" => DatafileTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "dmcplace" => DmcplaceTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "encountertrigger" => EncountertriggerTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "forbiddenhex" => ForbiddenhexTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "gamevar" => GamevarTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "headline" => HeadlineTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "image" => ImageTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            "itemprop" => ItempropTemplate ?? GenericTemplate ?? base.SelectTemplate(item, container),
            _ => GenericTemplate ?? base.SelectTemplate(item, container)
        };
    }
}
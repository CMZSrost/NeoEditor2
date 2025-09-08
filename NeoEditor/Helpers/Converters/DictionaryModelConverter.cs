using System.Dynamic;
using NeoEditor.Data.Models;
using attackmode = NeoEditor.Data.Models.Dto.attackmode;
using battlemove = NeoEditor.Data.Models.Dto.battlemove;
using camptype = NeoEditor.Data.Models.Dto.camptype;
using chargeprofile = NeoEditor.Data.Models.Dto.chargeprofile;
using condition = NeoEditor.Data.Models.Dto.condition;
using containertype = NeoEditor.Data.Models.Dto.containertype;
using creature = NeoEditor.Data.Models.Dto.creature;
using creaturesource = NeoEditor.Data.Models.Dto.creaturesource;
using datafile = NeoEditor.Data.Models.Dto.datafile;
using dmcplace = NeoEditor.Data.Models.Dto.dmcplace;
using encounter = NeoEditor.Data.Models.Dto.encounter;
using encountertrigger = NeoEditor.Data.Models.Dto.encountertrigger;
using faction = NeoEditor.Data.Models.Dto.faction;
using forbiddenhex = NeoEditor.Data.Models.Dto.forbiddenhex;
using gamevar = NeoEditor.Data.Models.Dto.gamevar;
using headline = NeoEditor.Data.Models.Dto.headline;
using hextype = NeoEditor.Data.Models.Dto.hextype;
using image = NeoEditor.Data.Models.Dto.image;
using ingredient = NeoEditor.Data.Models.Dto.ingredient;
using itemprop = NeoEditor.Data.Models.Dto.itemprop;
using itemtype = NeoEditor.Data.Models.Dto.itemtype;
using map = NeoEditor.Data.Models.Dto.map;
using recipe = NeoEditor.Data.Models.Dto.recipe;
using treasuretable = NeoEditor.Data.Models.Dto.treasuretable;

namespace NeoEditor.Helpers.Converters;

public static class DictionaryModelConverter
{
    public static dynamic? Convert(IDictionary<string, dynamic?> dictionary, string name)
    {
        // if (dictionary.Values.Count == 0) return Activator.CreateInstance(GetType(name));
        // return Activator.CreateInstance(GetType(name), dictionary.Values);

        dynamic dyn = new ExpandoObject();
        var typ = GetType(name);
        var expandoDic = (IDictionary<string, object>)dyn;

        dictionary.ToList()
            .ForEach(keyValue => expandoDic.Add(keyValue.Key, keyValue.Value));
        var instance = Activator.CreateInstance(typ);
        foreach (var prop in typ.GetProperties())
            if (expandoDic.TryGetValue(prop.Name, out var value))
                prop.SetValue(instance, value);

        return instance;
    }

    public static Type GetType(string name)
    {
        return name.ToLower() switch
        {
            "attackmodes" => typeof(attackmode),
            "barterhexes" => typeof(barterhex),
            "battlemoves" => typeof(battlemove),
            "camptypes" => typeof(camptype),
            "chargeprofiles" => typeof(chargeprofile),
            "conditions" => typeof(condition),
            "containertypes" => typeof(containertype),
            "creatures" => typeof(creature),
            "creaturesources" => typeof(creaturesource),
            "datafiles" => typeof(datafile),
            "dmcplaces" => typeof(dmcplace),
            "encounters" => typeof(encounter),
            "encountertriggers" => typeof(encountertrigger),
            "factions" => typeof(faction),
            "forbiddenhexes" => typeof(forbiddenhex),
            "gamevars" => typeof(gamevar),
            "headlines" => typeof(headline),
            "hextypes" => typeof(hextype),
            "images" => typeof(image),
            "ingredients" => typeof(ingredient),
            "itemprops" => typeof(itemprop),
            "itemtypes" => typeof(itemtype),
            "maps" => typeof(map),
            "recipes" => typeof(recipe),
            "treasuretable" => typeof(treasuretable),
            _ => throw new ArgumentOutOfRangeException(name)
        };
    }
}
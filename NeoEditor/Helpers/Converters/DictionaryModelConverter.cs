using System.Dynamic;
using NeoEditor.Data.Models;

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
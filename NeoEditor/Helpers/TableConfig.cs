using Microsoft.Extensions.Configuration;

namespace NeoEditor.Helpers;

public class TableConfig(IConfiguration configuration)
{
    public Dictionary<string, string> GetTableKeys()
    {
        return configuration.GetSection("tableKey")
            .GetChildren()
            .Select(tableName =>
                {
                    var lst = tableName.GetChildren().Select(c => c.Value ?? string.Empty)
                        .ToList();
                    return new
                    {
                        key = tableName.Key,
                        value = tableName.Value ?? string.Empty
                    };
                }
            ).ToDictionary(arg => arg.key, arg => arg.value);
    }

    public Dictionary<string, List<string>> GetTableAttribs()
    {
        return configuration.GetSection("tableAttibute")
            .GetChildren()
            .Select(tableName =>
                {
                    var lst = tableName.GetChildren().Select(c => c.Value ?? string.Empty)
                        .ToList();
                    return new
                    {
                        key = tableName.Key,
                        value = lst.Concat(["idx", "modName", "modIndex", "serialId_", "overId_"]).ToList()
                    };
                }
            ).ToDictionary(arg => arg.key, arg => arg.value);
    }
}
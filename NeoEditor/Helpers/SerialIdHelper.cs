using System.Data;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Helpers;

public class SerialIdHelper(
    string keyColumnName,
    string idxColumnName = "idx",
    string modNameColumnName = "modName",
    string modIndexColumnName = "modIndex",
    string serialIdColumnName = "serialId_",
    string overIdColumnName = "overId_")
{
    private readonly Dictionary<object, int> _idMapper = new();
    private int _idx;

    public void GetSerialId(DataRow row, bool isOverride)
    {
        var keyId = row[keyColumnName] as string ?? string.Empty;

        int serialId;
        int overId;

        if (isOverride)
        {
            if (int.TryParse(keyId, out var i))
                serialId = i;
            else
                serialId = -1;
            overId = _idMapper.GetValueOrDefault(serialId, -1);
        }
        else
        {
            overId = -1;
            serialId = ++_idx;
            _idMapper[serialId] = _idx;
        }

        row[idxColumnName] = _idx;
        row[serialIdColumnName] = serialId;
        row[overIdColumnName] = overId;
    }

    public void SerialTable(DataTable dt, ModXmlData modData)
    {
        foreach (DataRow row in dt.Rows)
        {
            row[modNameColumnName] = modData.ModName;
            row[modIndexColumnName] = modData.ModIndex;
            GetSerialId(row, (string)row[modNameColumnName] == "0");
        }
    }

    public void Reset()
    {
        _idx = 0;
        _idMapper.Clear();
    }
}
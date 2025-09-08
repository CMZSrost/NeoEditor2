using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.Helpers;

public class SerialIdHelper
{
    private readonly string _connectionString;
    private readonly Dictionary<string, List<string>> _tableAttribs;
    private readonly Dictionary<string, string> _tableKey;
    private readonly List<string> _tableNames;

    public SerialIdHelper(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
                            throw new ArgumentNullException(nameof(configuration));
        _tableAttribs = GetTableAttribs(configuration);
        _tableKey = GetTableKey(configuration);
        _tableNames = _tableAttribs.Keys.ToList();
    }

    public async Task ReorderAll(string? tableName = null)
    {
        using IDbConnection connection = new SqliteConnection(_connectionString);
        connection.Open();
        ICollection<string> tables;
        if (tableName == null) tables = _tableNames;
        else tables = [tableName];
        foreach (var table in tables)
        {
            ICollection<SerialRecord> records = (
                    await connection.QueryAsync<SerialRecord>(
                        $"SELECT {_tableKey[table]}, idx, modName, modIndex, overId_, serialId_ FROM {table}"))
                .ToArray();

            var transaction = connection.BeginTransaction();
            // foreach (var record in )
            await connection.ExecuteAsync(
                $"UPDATE {table} SET serialId_=@serialId_, overId_=@overId_, isLast_=@isLast_ WHERE idx=@idx",
                Reorder(records), transaction);
            transaction.Commit();
        }
    }

    public static Dictionary<string, List<string>> GetTableAttribs(IConfiguration configuration)
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

    public static Dictionary<string, string> GetTableKey(IConfiguration configuration)
    {
        return configuration.GetSection("tableKey")
            .GetChildren()
            .Select(section => new
            {
                key = section.Key,
                value = section.Value ?? "id"
            }).ToDictionary(arg => arg.key, arg => arg.value);
    }

    public static IEnumerable<SerialRecord> Reorder(ICollection<SerialRecord> records)
    {
        var appended = records.Where(record => record.modName != "0")
            .OrderBy(record => record.modIndex).ThenBy(record =>
                record.id ?? record.nID).ToList();
        var overrided = records.Where(record => record.modName == "0")
            .OrderBy(record => record.modIndex).ThenBy(record =>
                record.id ?? record.nID).ToList();
        Dictionary<string, int> idxMap = new(); // Id(0) -> <serialId(!0) -> Idx>
        var results = new List<SerialRecord>();
        var id = 1;
        foreach (var aRecord in appended)
        {
            aRecord.serialId_ = (id++).ToString();
            aRecord.overId_ = -1;
            idxMap.Add(aRecord.serialId_, aRecord.idx);
            results.Add(aRecord);
        }

        foreach (var oRecord in overrided)
        {
            oRecord.serialId_ = oRecord.id?.ToString() ?? oRecord.nID?.ToString() ?? oRecord.strName ?? string.Empty;
            oRecord.overId_ = idxMap.GetValueOrDefault(oRecord.serialId_, -1);
            results.Add(oRecord);
        }

        foreach (var serialRecord in results.GroupBy(o => o.serialId_)
                     .Select(p => p.OrderBy(r => r.modIndex).LastOrDefault()))
            if (serialRecord != null)
                serialRecord.isLast_ = true;

        return results;
    }
}
using System.Collections.ObjectModel;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Xml;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.Helpers;

public class DapperCollection
{
    private readonly IList<string> _attributes;
    private readonly string _connectionString;

    private readonly string _insertString;
    private readonly string _tableName;
    private readonly string _upsertString;
    public readonly Collection<ExpandoObject> EntitiesList;

    public DapperCollection(string connectionString, string tableName, List<string> attributes)
    {
        _connectionString = connectionString;
        _tableName = tableName;
        _attributes = attributes;
        EntitiesList = [];
        var insertStringKey = $"({string.Join(", ", attributes)})";
        var insertStringValue = $"(@{string.Join(", @", attributes)})";
        _insertString = $"INSERT INTO {_tableName} {insertStringKey} VALUES {insertStringValue};";
        _upsertString = $"INSERT INTO {_tableName} {insertStringKey} VALUES {insertStringValue};";
        // _insertString = $"INSERT INTO {_tableName} {insertStringKey} VALUES ";
    }

    public async Task Truncate()
    {
        try
        {
            using IDbConnection connection = new SqliteConnection(_connectionString);

            await connection.ExecuteAsync($"DELETE FROM {_tableName};");
            await connection.ExecuteAsync($"UPDATE sqlite_sequence SET seq = 0 WHERE name = '{_tableName}';");
        }
        catch (Exception ex)
        {
        }
    }


    public async Task BulkInsert()
    {
        try
        {
            using IDbConnection connection = new SqliteConnection(_connectionString);
            if (connection.State == ConnectionState.Closed) connection.Open();
            var transaction = connection.BeginTransaction();
            foreach (var dictionary in EntitiesList)
                await connection.ExecuteAsync(_insertString, dictionary, transaction);
            transaction.Commit();
            EntitiesList.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}

public class XmlLoader
{
    private readonly Dictionary<string, int> _cntDictory;
    private readonly string _connectionString;
    private readonly Dictionary<string, DapperCollection> _dapperCollections;
    private readonly SerialIdHelper _serialIdHelper;
    private readonly Dictionary<string, List<string>> _tableAttribs;
    private readonly List<string> _tableNames;

    public XmlLoader(IConfiguration configuration)
    {
        _cntDictory = new Dictionary<string, int>();
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
                            throw new ArgumentNullException(nameof(configuration));
        _dapperCollections = new Dictionary<string, DapperCollection>();
        _tableAttribs = GetTableAttribs(configuration);
        _tableNames = _tableAttribs.Keys.ToList();
    }

    public int Idx { get; set; }

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

    public async Task Clean()
    {
        Idx = 0;
        _cntDictory.Clear();
        _dapperCollections.Clear();
        await Truncate();
    }

    public async Task Truncate()
    {
        foreach (var tableName in _tableNames)
        {
            var dapperCollection = new DapperCollection(_connectionString, tableName, _tableAttribs[tableName]);
            _dapperCollections.Add(tableName, dapperCollection);
            await dapperCollection.Truncate();
        }
    }

    public async Task CleanEncodingHeader(string xmlFilePath)
    {
        // 读取文件第一行，替换utf8为utf-8，保存
        await using (var f2 = new StreamWriter(xmlFilePath + ".tmp"))
        {
            using var f = new StreamReader(xmlFilePath);
            var firstLine = await f.ReadLineAsync();
            var newFirstLine = firstLine;
            if (firstLine?.StartsWith("<?xml") == true) newFirstLine = firstLine.Replace("utf8", "utf-8");
            await f2.WriteLineAsync(newFirstLine);
            await f2.WriteLineAsync(await f.ReadToEndAsync());
        }

        File.Move(xmlFilePath + ".tmp", xmlFilePath, true);
    }

    private object? GuessType(string attrib, dynamic? obj = null)
    {
        try
        {
            if (attrib.StartsWith('v') || attrib.StartsWith('a') || attrib.StartsWith("str"))
                return obj == null ? string.Empty : Convert.ChangeType(obj, TypeCode.String);

            if (attrib.StartsWith('n') || attrib.StartsWith('i') || attrib.StartsWith('b'))
                return obj == null ? -1 : Convert.ChangeType(obj, TypeCode.Int32);

            if (attrib.StartsWith('f') || attrib.StartsWith("m_f"))
                return obj == null ? double.NaN : Convert.ChangeType(obj, TypeCode.Double);
        }
        catch (FormatException)
        {
        }

        return obj;
    }

    public async Task LoadXml(string xmlFilePath, ModData modData)
    {
        await CleanEncodingHeader(xmlFilePath);
        await Task.Run(async () =>
            {
                Console.WriteLine($"loading {xmlFilePath}...");
                // var reader = XmlReader.Create(xmlFilePath,new XmlReaderSettings { Async = true });
                var doc = new XmlDocument();
                doc.Load(xmlFilePath);
                foreach (XmlNode selectTable in doc.SelectNodes("//table")!)
                {
                    var name = selectTable.Attributes?["name"]?.Value ?? string.Empty;
                    var columns = selectTable.SelectNodes("./column")?.Cast<XmlNode>().ToList();
                    ArgumentNullException.ThrowIfNull(columns);
                    var eo = new ExpandoObject();
                    try
                    {
                        var cnt = 0;
                        _cntDictory.TryGetValue(name, out cnt);
                        _cntDictory[name] = cnt + 1;
                        Idx += 1;
                        eo.TryAdd("idx", cnt);

                        eo.TryAdd("modName", modData.ModName);
                        eo.TryAdd("modIndex", modData.ModIndex);
                        eo.TryAdd("serialId_", -1);
                        eo.TryAdd("overId_", -1);

                        foreach (var xmlNode in columns)
                        {
                            var attrib = xmlNode.Attributes["name"].Value;
                            eo.TryAdd(attrib, GuessType(attrib, xmlNode.InnerText));
                        }

                        foreach (var attrib in _tableAttribs[name])
                            eo.TryAdd(attrib, GuessType(attrib));

                        _dapperCollections[name].EntitiesList.Add((dynamic)eo);
                    }
                    catch (ArgumentException e)
                    {
                        // Console.WriteLine("ArgumentException!");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("==============");
                        foreach (var xmlNode in columns)
                            Console.WriteLine($"{xmlNode.Attributes?["name"]?.Value}: {xmlNode.InnerText}");
                        Console.WriteLine("==============");

                        Console.WriteLine($"{e}");
                    }
                }

                foreach (var dapperCollection in _dapperCollections.Values) await dapperCollection.BulkInsert();
            }
        );
    }
}
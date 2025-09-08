using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Xml;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace NeoEditor.Helpers;

public class XmlWriter
{
    private const string _templateHeader = """
                                           <?xml version="1.0" encoding="utf-8"?>
                                           <pma_xml_export version="1.0">
                                               <database name="neogame">
                                               </database>
                                           </pma_xml_export>
                                           """;

    private readonly Dictionary<string, int> _cntDictory;
    private readonly string _connectionString;
    private readonly Dictionary<string, List<string>> _tableAttribs;
    private readonly List<string> _tableNames;

    public XmlWriter(IConfiguration configuration)
    {
        _cntDictory = new Dictionary<string, int>();
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
                            throw new ArgumentNullException(nameof(configuration));
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
                    return new
                    {
                        key = tableName.Key,
                        value = tableName.GetChildren().Select(c => c.Value ?? string.Empty)
                            .ToList()
                    };
                }
            ).ToDictionary(arg => arg.key, arg => arg.value);
    }


    public XmlElement FormatXML(Dictionary<string, string> table, string tableName, XmlDocument doc)
    {
        var tableNode = doc.CreateElement("table");
        tableNode.SetAttribute("name", tableName);
        foreach (var attrib in table)
            if (_tableAttribs[tableName].Contains(attrib.Key))
            {
                var node = doc.CreateElement("column");
                node.SetAttribute("name", attrib.Key);
                node.InnerText = attrib.Value;
                tableNode.AppendChild(node);
            }

        return tableNode;
    }

    public async Task<Dictionary<string, IEnumerable<Dictionary<string, string>>>> LoadAll()
    {
        using IDbConnection connection = new SqliteConnection(_connectionString);
        if (connection.State == ConnectionState.Closed) connection.Open();
        Dictionary<string, IEnumerable<Dictionary<string, string>>> tables = new();
        foreach (var tableName in _tableNames)
        {
            var sql = $"SELECT * FROM {tableName}";
            var entities = await connection.QueryAsync<Dictionary<string, string>>(sql);
            tables.Add(tableName, entities.OrderBy(dictionary => dictionary["serialId_"]));
        }

        return tables;
    }

    public async Task LoadXml(string xmlFilePath)
    {
        if (Directory.Exists(xmlFilePath))
            await Task.WhenAll(await SaveToDirectory(xmlFilePath));

        else if (File.Exists(xmlFilePath))
            await SaveToFile(xmlFilePath);
    }

    private async Task<Collection<Task>> SaveToDirectory(string xmlFilePath)
    {
        var entitiesDict = await Task.Run(async () => await LoadAll());
        Collection<Task> tasks = [];
        foreach (var keyValuePair in entitiesDict)
        {
            var tableName = keyValuePair.Key;
            var tables = keyValuePair.Value;
            tasks.Add(
                Task.Run(async () =>
                    {
                        var filePath = Path.Join(xmlFilePath, $"{tableName}.xml");
                        Console.WriteLine($"loading {tableName}... to {xmlFilePath}");
                        var doc = new XmlDocument();
                        doc.LoadXml(_templateHeader);
                        var database = doc.SelectSingleNode("//databse");
                        ArgumentNullException.ThrowIfNull(database);
                        foreach (var node in tables.Select(table => FormatXML(table, tableName, doc)))
                            database.AppendChild(node);
                        File.Delete(filePath);
                        await using (var fileStream = new FileStream(filePath, FileMode.CreateNew))
                        {
                            doc.Save(fileStream);
                        }

                        Console.WriteLine($"{filePath} saved.");
                    }
                )
            );
        }

        return tasks;
    }

    private async Task SaveToFile(string xmlFilePath)
    {
        var entitiesDict = await Task.Run(async () => await LoadAll());
        var filePath = Path.Join(xmlFilePath, "neogame.xml");
        Console.WriteLine($"loading {xmlFilePath}...");
        var doc = new XmlDocument();
        doc.LoadXml(_templateHeader);
        var database = doc.SelectSingleNode("//databse");
        foreach (var keyValuePair in entitiesDict)
        {
            var tableName = keyValuePair.Key;
            var tables = keyValuePair.Value;
            {
                Console.WriteLine($"loading {tableName}...");
                ArgumentNullException.ThrowIfNull(database);
                foreach (var node in tables.Select(table => FormatXML(table, tableName, doc)))
                    database.AppendChild(node);
            }
        }

        File.Delete(filePath);
        await using (var fileStream = new FileStream(filePath, FileMode.CreateNew))
        {
            doc.Save(fileStream);
        }

        Console.WriteLine($"{filePath} saved.");
    }
}
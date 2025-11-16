using System.Data;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using NeoEditor.Data.Models;

namespace NeoEditor.Helpers;

public static class GameXmlLoader
{
    private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
    private static readonly SemaphoreSlim _dictionaryLock = new(1, 1);

    private static async Task<SemaphoreSlim> GetFileLock(string filePath)
    {
        await _dictionaryLock.WaitAsync();
        try
        {
            if (!_fileLocks.TryGetValue(filePath, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _fileLocks[filePath] = semaphore;
            }
            return semaphore;
        }
        finally
        {
            _dictionaryLock.Release();
        }
    }

    private static async Task<string> FixEncodingHeader(string xmlFilePath)
    {
        var fileLock = await GetFileLock(xmlFilePath);
        await fileLock.WaitAsync();
        try
        {
            // 读取整个文件内容
            string content;
            using (var reader = new StreamReader(xmlFilePath, Encoding.UTF8, true))
            {
                content = await reader.ReadToEndAsync();
            }

            // 只在内存中修复编码声明，不修改原文件
            // 查找 encoding='utf8' 或 encoding="utf8" 并替换为 utf-8
            if (content.Contains("encoding='utf8'"))
            {
                content = content.Replace("encoding='utf8'", "encoding='utf-8'");
            }
            else if (content.Contains("encoding=\"utf8\""))
            {
                content = content.Replace("encoding=\"utf8\"", "encoding=\"utf-8\"");
            }

            return content;
        }
        finally
        {
            fileLock.Release();
        }
    }
    public static async Task<DataSet> LoadXmlToDataSet(string xmlFilePath)
    {
        return await Task.Run(async () =>
        {
            var content = await FixEncodingHeader(xmlFilePath);
            Console.WriteLine($"loading {xmlFilePath}...");
            
            var serializer = new XmlSerializer(typeof(PmaXmlExport));
            using var reader = new StringReader(content);
            var doc = (PmaXmlExport)serializer.Deserialize(reader);
            var ds = new DataSet();
            
            foreach (var rows in doc.Database.Table.GroupBy(table => table.Name))
            {
                var dt = ds.Tables.Add(rows.Key);
                dt.Columns.AddRange([
                    new DataColumn("idx"), new DataColumn("modName"), new DataColumn("modIndex"),
                    new DataColumn("serialId_"), new DataColumn("overId_")
                ]);
                
                foreach (var dataRow in rows)
                {
                    var row = dt.NewRow();
                    foreach (var column in dataRow.Column)
                    {
                        if (!dt.Columns.Contains(column.Name)) dt.Columns.Add(column.Name);
                        row[column.Name] = column.Text;
                    }
                    dt.Rows.Add(row);
                }
            }
            
            return ds;
        });
    }

    public static async Task<XDocument> LoadXmlToDom(string xmlFilePath)
    {
        return await Task.Run(async () =>
        {
            var content = await FixEncodingHeader(xmlFilePath);
            Console.WriteLine($"loading {xmlFilePath} to DOM...");
            return XDocument.Parse(content);
        });
    }
}
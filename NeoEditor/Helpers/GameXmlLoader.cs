using System.Data;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using NeoEditor.Data.Models;

namespace NeoEditor.Helpers;

public static class GameXmlLoader
{
    private static async Task FixEncodingHeader(string xmlFilePath)
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


    public static async Task<DataSet> LoadXml(string xmlFilePath)
    {
        return await Task.Run(async () =>
            {
                await FixEncodingHeader(xmlFilePath);
                Console.WriteLine($"loading {xmlFilePath}...");
                var serializer = new XmlSerializer(typeof(PmaXmlExport));
                using (var reader = new StreamReader(xmlFilePath, Encoding.UTF8, true))
                {
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
                }
            }
        );
    }
}
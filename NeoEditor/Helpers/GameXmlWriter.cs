using System.Data;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NeoEditor.Data.Models;

namespace NeoEditor.Helpers;

public static class GameXmlWriter
{
    private const string TemplateHeader = """
                                          <?xml version="1.0" encoding="utf-8"?>
                                          <pma_xml_export version="1.0">
                                              <database name="neogame">
                                              </database>
                                          </pma_xml_export>
                                          """;

    public static Task WriteXml(DataSet ds, string xmlFilePath)
    {
        return Task.Run(() =>
            {
                if (Directory.Exists(xmlFilePath))
                    SaveToDirectory(ds, xmlFilePath);
                else if (File.Exists(xmlFilePath) && xmlFilePath.EndsWith(".xml")) SaveToFile(ds, xmlFilePath);
            }
        );
    }

    private static void SaveToDirectory(DataSet ds, string xmlFilePath)
    {
        var serializer = new XmlSerializer(typeof(PmaXmlExport));
        foreach (DataTable dt in ds.Tables)
        {
            var tableName = dt.TableName;
            var filePath = Path.Join(xmlFilePath, $"{tableName}.xml");

            Console.WriteLine($"loading {tableName}... to {xmlFilePath}");

            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                Encoding = Encoding.UTF8
            };

            var xmlParams = new PmaXmlExport();
            foreach (DataRow row in dt.Rows)
            {
                var table = new Table
                {
                    Name = tableName
                };
                foreach (DataColumn column in dt.Columns)
                    table.Column.Add(new Column
                    {
                        Name = column.ColumnName,
                        Text = row[column].ToString() ?? ""
                    });
                xmlParams.Database.Table.Add(table);
            }

            using (TextWriter writer = new StreamWriter(filePath))
            {
                using (var xmlWriter = XmlWriter.Create(writer, settings))
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    serializer.Serialize(xmlWriter, xmlParams);
                }
            }
            // var doc = new XmlDocument();
            // doc.LoadXml(TemplateHeader);
            // var database = doc.SelectSingleNode("//databse");

            Console.WriteLine($"{filePath} saved.");
        }
    }

    private static void SaveToFile(DataSet ds, string xmlFilePath)
    {
        var serializer = new XmlSerializer(typeof(PmaXmlExport));

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = Encoding.UTF8
        };

        using TextWriter writer = new StreamWriter(xmlFilePath);
        using (var xmlWriter = XmlWriter.Create(writer, settings))
        {
            foreach (DataTable dt in ds.Tables)
            {
                var tableName = dt.TableName;

                Console.WriteLine($"loading {tableName}... to {xmlFilePath}");


                var xmlParams = new PmaXmlExport();
                foreach (DataRow row in dt.Rows)
                {
                    var table = new Table
                    {
                        Name = tableName
                    };
                    foreach (DataColumn column in dt.Columns)
                        table.Column.Add(new Column
                        {
                            Name = column.ColumnName,
                            Text = row[column].ToString() ?? ""
                        });
                    xmlParams.Database.Table.Add(table);
                }

                if (File.Exists(xmlFilePath))
                    File.Delete(xmlFilePath);
                serializer.Serialize(xmlWriter, xmlParams);
            }
        }

        Console.WriteLine($"{xmlFilePath} saved.");
    }
}
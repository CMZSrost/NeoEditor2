using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.XmlDiffPatch;

namespace NeoEditor.Plugins.EntityEditor.Helper;

/// <summary>
/// XML comparison helper using Microsoft.XmlDiffPatch.
/// Produces a patched XML showing differences between old and new versions.
/// Migrated from NeoEditor.App.Helper during M10 Phase 5.
/// </summary>
public static class XmlCompareHelper
{
    public static string Compare(string oldXmlPath, string newXmlPath)
    {
        if (!File.Exists(oldXmlPath) || !File.Exists(newXmlPath))
        {
            throw new FileNotFoundException("The XML files are different.", oldXmlPath);
        }

        var xmlDiff = new XmlDiff(
            XmlDiffOptions.IgnoreComments |
            XmlDiffOptions.IgnoreWhitespace |
            XmlDiffOptions.IgnoreNamespaces
            );
        var xmlPatch = new XmlPatch();

        using var diffStream = new MemoryStream();
        using var patchStream = new MemoryStream();

        using var diffWriter = XmlWriter.Create(diffStream);
        xmlDiff.Compare(oldXmlPath, newXmlPath, false, diffWriter);
        diffWriter.Flush();
        diffStream.Position = 0;

        using var diffReader = XmlReader.Create(diffStream);
        xmlPatch.Patch(oldXmlPath, patchStream, diffReader);
        patchStream.Position = 0;

        using var reader = new StreamReader(patchStream);
        return NormalizePatchedXml(reader.ReadToEnd());
    }

    private static string NormalizePatchedXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return xml;
        }

        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        foreach (var columnElement in document.Descendants("column").Where(element => !element.HasElements))
        {
            var value = columnElement.Value;
            if (!IsIndentationOnlyEmptyValue(value))
            {
                continue;
            }

            columnElement.Value = string.Empty;
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineChars = Environment.NewLine,
            OmitXmlDeclaration = false
        };

        using var writer = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(writer, settings);
        document.Save(xmlWriter);
        xmlWriter.Flush();
        return writer.ToString();
    }

    private static bool IsIndentationOnlyEmptyValue(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               string.IsNullOrWhiteSpace(value) &&
               value.IndexOfAny(['\r', '\n']) >= 0;
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}

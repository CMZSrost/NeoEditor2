using System;
using System.IO;
using System.Xml;
using Microsoft.XmlDiffPatch;

namespace NeoEditor.Helper;

public static class XmlCompareHelper
{
    public static string Compare(string oldXmlPath, string newXmlPath)
    {
        if (!File.Exists(oldXmlPath) || !File.Exists(newXmlPath))
        {
            throw new FileNotFoundException("The XML files are different.", oldXmlPath);
        }

        var xmlDiff = new XmlDiff(XmlDiffOptions.IgnoreComments | XmlDiffOptions.IgnoreWhitespace |
                                  XmlDiffOptions.IgnoreNamespaces);
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
        return reader.ReadToEnd();
    }
}
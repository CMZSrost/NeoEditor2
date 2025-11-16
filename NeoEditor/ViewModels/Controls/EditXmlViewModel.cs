using System.Collections.ObjectModel;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Helpers;

namespace NeoEditor.ViewModels.Controls;

public partial class EditXmlViewModel : ObservableObject
{
    [ObservableProperty] public partial ObservableCollection<XmlNodeItem> Nodes { get; set; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial ObservableCollection<XmlNodeItem> FilteredNodes { get; set; } = [];

    partial void OnSearchTextChanged(string value)
    {
        FilterNodes();
    }

    public async Task LoadXmlAsync(string xmlFilePath)
    {
        var xDoc = await GameXmlLoader.LoadXmlToDom(xmlFilePath);
        Nodes.Clear();

        // 解析 XML 树形结构
        ParseElement(xDoc.Root, null, 0);
        FilterNodes();
    }

    private void ParseElement(XElement? element, XmlNodeItem? parent, int level)
    {
        if (element == null) return;

        var item = new XmlNodeItem
        {
            Name = element.Name.LocalName,
            Value = element.HasElements ? string.Empty : element.Value,
            Level = level,
            Attributes = string.Join(", ", element.Attributes().Select(a => $"{a.Name}={a.Value}"))
        };

        Nodes.Add(item);

        foreach (var child in element.Elements())
            ParseElement(child, item, level + 1);
    }

    private void FilterNodes()
    {
        FilteredNodes.Clear();
        var query = Nodes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(n =>
                n.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                n.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                n.Attributes.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var node in query)
            FilteredNodes.Add(node);
    }
}

public class XmlNodeItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Attributes { get; set; } = string.Empty;
    public int Level { get; set; }
}
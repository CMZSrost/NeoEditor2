using System.Xml.Serialization;

namespace NeoEditor.Data;

// using System.Xml.Serialization;
// XmlSerializer serializer = new XmlSerializer(typeof(PmaXmlExport));
// using (StringReader reader = new StringReader(xml))
// {
//    var test = (PmaXmlExport)serializer.Deserialize(reader);
// }

[XmlRoot(ElementName="table")]
public class Table { 

    [XmlAttribute(AttributeName="name")] 
    public string Name { get; set; } 

    [XmlText] 
    public string Text { get; set; } 

    [XmlElement(ElementName="column")] 
    public List<Column> Column { get; set; } 
}

[XmlRoot(ElementName="database")]
public class Database { 

    [XmlElement(ElementName="table")] 
    public List<Table> Table { get; set; } 

    [XmlAttribute(AttributeName="name")] 
    public string Name { get; set; } 

    [XmlText] 
    public string Text { get; set; } 
}

[XmlRoot(ElementName="structure_schemas")]
public class StructureSchemas { 

    [XmlElement(ElementName="database")] 
    public Database Database { get; set; } 
}

[XmlRoot(ElementName="column")]
public class Column { 

    [XmlAttribute(AttributeName="name")] 
    public string Name { get; set; } 

    [XmlText] 
    public int Text { get; set; } 
}

[XmlRoot(ElementName="pma_xml_export")]
public class PmaXmlExport { 

    [XmlElement(ElementName="structure_schemas")] 
    public StructureSchemas? StructureSchemas { get; set; } 

    [XmlElement(ElementName="database")] 
    public Database Database { get; set; } 

    [XmlAttribute(AttributeName="ns0")] 
    public string Ns0 { get; set; } 

    [XmlAttribute(AttributeName="version")] 
    public double Version { get; set; } 

    [XmlText] 
    public string Text { get; set; } 
}


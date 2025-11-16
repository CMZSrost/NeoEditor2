using System.IO;

namespace NeoEditor.Data.Messages;

public class OpenXmlMessage
{
    public string FilePath { get; set; }
    public FileMode Mode { get; set; }
}

public class OpenXmlEvent : PubSubEvent<OpenXmlMessage>;
namespace NeoEditor.Data.Messages;

public class LoadFromXmlMessage
{
    public string FilePath { get; set; } = "";
}

public class LoadFromXmlEvent : PubSubEvent<LoadFromXmlMessage>;
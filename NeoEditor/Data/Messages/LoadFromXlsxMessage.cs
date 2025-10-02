namespace NeoEditor.Data.Messages;

public class LoadFromXlsxMessage
{
    public string TargetTable { get; set; } = "edit";
    public string FilePath { get; set; }
}

public class LoadFromXlsxEvent : PubSubEvent<LoadFromXlsxMessage>;
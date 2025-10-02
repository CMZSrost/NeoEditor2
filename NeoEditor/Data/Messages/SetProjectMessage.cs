namespace NeoEditor.Data.Messages;

public class SetProjectMessage
{
    public required string ModConfigFilePath;
}

public class SetProjectEvent : PubSubEvent<SetProjectMessage>;
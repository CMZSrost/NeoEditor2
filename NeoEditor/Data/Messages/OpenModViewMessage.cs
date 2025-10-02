namespace NeoEditor.Data.Messages;

public class OpenModViewMessage
{
    public required int ModIndex;
    public required string ModName;
}

public class OpenModViewEvent : PubSubEvent<OpenModViewMessage>;
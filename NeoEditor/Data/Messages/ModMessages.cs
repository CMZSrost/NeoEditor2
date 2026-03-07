namespace NeoEditor.Data.Messages;

public record InitModMessage();

public record RefreshModMessage();

public record OpenXmlDocumentMessage(string XmlPath, string Title);

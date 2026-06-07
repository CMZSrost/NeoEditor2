using System.Collections.Generic;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.Messages;

public record InitModMessage();

public record RefreshModMessage();

public record OpenXmlDocumentMessage(string XmlPath, string Title);

public record OpenModGameDataDocumentMessage(ModInfo ModInfo, bool ReadOnly = false);

public record OpenModImagesDocumentMessage(ModInfo ModInfo);

public record OpenMergeEditorMessage(ProfileInfo ProfileInfo);

public record OpenImageDocumentMessage(string Title, string ImagePath);
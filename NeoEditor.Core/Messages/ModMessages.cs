using System.Collections.Generic;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.Messages;

// Q10=A: InitModMessage deleted (dead message).
public record RefreshModMessage();

public record OpenXmlDocumentMessage(string XmlPath, string Title);

public record OpenModGameDataDocumentMessage(ModInfo ModInfo, bool ReadOnly = false);

public record OpenModImagesDocumentMessage(ModInfo ModInfo);

public record OpenMergeEditorMessage(ProfileInfo ProfileInfo);

public record OpenImageDocumentMessage(string Title, string ImagePath);

/// <summary>
/// Published when an AI-generated image is ready.
/// The App shell subscribes to open the image in ImageEditorDocument.
/// </summary>
public record ImageGeneratedMessage(string EntityType, string EntityId,
    string NormalImagePath, string X2ImagePath);
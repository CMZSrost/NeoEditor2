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
/// Opens a blank Image Editor workbench (with the AI generate panel) from the Image
/// Browser's "AI 生成图片" context action. The App shell creates the document.
/// </summary>
public record OpenAiImageWorkbenchMessage;
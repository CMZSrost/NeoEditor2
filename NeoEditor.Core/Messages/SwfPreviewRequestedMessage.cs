namespace NeoEditor.Data.Messages;

/// <summary>
/// Requests the in-app WebView preview (Docs/42): opens/activates the WebView tool and loads
/// the game SWF through the reverse proxy (live editor state). SwfPath is the resolved SWF
/// file (RuffleOptionsBuilder.FindSwfPath); null means "no SWF found — show guidance".
/// </summary>
public record SwfPreviewRequestedMessage(string? SwfPath);

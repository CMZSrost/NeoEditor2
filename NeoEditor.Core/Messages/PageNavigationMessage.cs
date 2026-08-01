namespace NeoEditor.Data.Messages;

public record NavigateToPageMessage(PageType Page);

public enum PageType
{
    Home,
    Workspace,
    Settings
}

namespace NeoEditor.Data.Messages;

public class OpenEditTableMessage
{
    public required string ModConfigFilePath;
    public required string ProjectDataFolder;
    public required string ProjectModFolder;
    public required string ProjectRootDirectory;
}
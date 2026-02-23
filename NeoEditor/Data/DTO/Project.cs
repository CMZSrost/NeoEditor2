using System.Collections.Generic;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.DTO;

public class Project
{
    public string Name { get; set; }
    public string ProjectFilePath { get; set; }
    public string DatabasePath { get; set; }
    public string BaseDataPath { get; set; }
    public List<ModReference> Mods { get; set; } = new();
    public int? DefaultMergeModId { get; set; }
    public int? DefaultInsertModId { get; set; }
}

public enum EditActionType
{
    UpdateField,
    CreateEntity
}

public class ModReference
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public ModType Type { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsBase { get; set; }
}
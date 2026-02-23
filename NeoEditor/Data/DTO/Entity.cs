using System.Collections.Generic;

namespace NeoEditor.Data.DTO;

public class ParsedModEntry
{
    public string Name { get; set; }
    public string Path { get; set; } // 相对路径，如 "Mods/NeoScavExtended/NSEgame"
}

// Models/MergedEntity.cs
public class MergedEntity<T>
{
    public T Entity { get; set; }
    public Dictionary<string, FieldSource> FieldSources { get; set; }
    public object[] PrimaryKeyValues { get; set; }
}

public class FieldSource
{
    public int? ModId { get; set; }
    public object? Value { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
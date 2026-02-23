using System.Collections.Generic;

namespace NeoEditor.Data.DTO;

public enum DiffType
{
    Added, // 仅在目标中存在（相对于源）
    Removed, // 仅在源中存在
    Modified // 两者都存在但值不同
}

public class FieldDiff
{
    public string FieldName { get; set; }
    public object SourceValue { get; set; }
    public object TargetValue { get; set; }
    public bool IsDifferent { get; set; }
}

public class EntityDiff
{
    public DiffType DiffType { get; set; }
    public object[] PrimaryKeyValues { get; set; }
    public List<FieldDiff> FieldDiffs { get; set; } = new();
}

public class ComparisonResult<TEntity> where TEntity : class
{
    public string SourceModName { get; set; }
    public string TargetModName { get; set; }
    public int SourceModId { get; set; }
    public int TargetModId { get; set; }
    public List<EntityDiff> EntityDiffs { get; set; } = new();
    public int TotalEntitiesSource { get; set; }
    public int TotalEntitiesTarget { get; set; }
}
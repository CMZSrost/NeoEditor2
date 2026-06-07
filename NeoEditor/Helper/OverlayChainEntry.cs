namespace NeoEditor.Helper;

public record OverlayChainEntry(string ModName, int Id, System.Type EntityType, string EntityId = "", string Subject = "")
{
    public string Display => string.IsNullOrWhiteSpace(Subject)
        ? $"[{ModName}] id={Id}"
        : $"[{ModName}] {Subject} (id={Id})";
    public override string ToString() => Display;
}

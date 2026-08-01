using System.Threading.Tasks;
using NeoEditor.Services;

namespace NeoEditor.Infra.Services;

/// <summary>Browser index service interface — extracted to Infra per M9 plugin migration.</summary>
public interface IBrowserIndexService
{
    ReferenceIndexService? Index { get; }
    bool IsBuilding { get; }
    System.Collections.Generic.Dictionary<string, string> GlobalModNames { get; }
    void Invalidate();
    Task EnsureBuiltAsync();
}

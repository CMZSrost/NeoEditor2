using NeoEditor.Infra.Services;
using NeoEditor.Plugins.DataViewer.ViewModels;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// Provides the two shared <see cref="IndexTableViewModel"/> singletons (Forward /
/// Reverse) so the ForwardIndex/ReverseIndex tool plugins and the App shell all
/// reference the same instances. Spec: D02-dynamic-dock-layout §五.
/// </summary>
public interface IIndexTableFactory
{
    IndexTableViewModel Forward { get; }
    IndexTableViewModel Reverse { get; }
}

/// <inheritdoc cref="IIndexTableFactory"/>
public sealed class IndexTableFactory : IIndexTableFactory
{
    public IndexTableViewModel Forward { get; }
    public IndexTableViewModel Reverse { get; }

    public IndexTableFactory(IWorkspaceSession session, IBrowserIndexService bis)
    {
        Forward = new IndexTableViewModel(session, bis) { Direction = IndexDirection.Forward };
        Reverse = new IndexTableViewModel(session, bis) { Direction = IndexDirection.Reverse };
    }
}

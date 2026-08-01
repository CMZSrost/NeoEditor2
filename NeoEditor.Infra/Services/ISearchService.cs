using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public interface ISearchService
{
    Task<(List<SearchResultGroup> Groups, string StatusText)> SearchAsync(string query, CancellationToken ct = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// RAG (Retrieval-Augmented Generation) service that indexes entity data
/// and retrieves relevant context for AI chat queries.
/// </summary>
public interface IRagService
{
    /// <summary>Whether RAG is usable. False when no API key is configured.</summary>
    bool IsAvailable { get; }

    /// <summary>Whether an index has been built.</summary>
    bool HasIndex { get; }

    /// <summary>Total number of indexed entities.</summary>
    int IndexedCount { get; }

    /// <summary>
    /// Build the search index from all entities across all types.
    /// This iterates every entity type in <see cref="Data.Constants.GameTypes"/>,
    /// generates text summaries, embeds them, and stores the vectors.
    /// </summary>
    Task BuildIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Search the index for entities relevant to the query.
    /// Returns top-K text summaries sorted by relevance.
    /// </summary>
    Task<IReadOnlyList<RagResult>> SearchAsync(string query, int topK = 5, CancellationToken ct = default);

    /// <summary>Clear the index.</summary>
    void Clear();
}

/// <summary>
/// A single RAG search result: entity summary text and relevance score.
/// </summary>
/// <param name="Summary">Entity text summary.</param>
/// <param name="Score">Cosine similarity score (0..1, higher is more relevant).</param>
public sealed record RagResult(string Summary, float Score);

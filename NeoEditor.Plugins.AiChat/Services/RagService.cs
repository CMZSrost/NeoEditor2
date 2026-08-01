using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Model.Game;
using OpenAI.Embeddings;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// RAG service that indexes game entity data into an in-memory vector store
/// using OpenAI-compatible embeddings, then retrieves relevant entities
/// for AI chat context augmentation.
/// </summary>
public class RagService : IRagService
{
    private readonly EmbeddingClient? _embeddingClient;
    private readonly IHostService _hostService;
    private readonly EntitySummaryBuilder _summaryBuilder;

    private readonly List<IndexEntry> _index = new();
    private bool _hasIndex;

    public RagService(EmbeddingClient? embeddingClient, IHostService hostService,
        EntitySummaryBuilder summaryBuilder)
    {
        _embeddingClient = embeddingClient;
        _hostService = hostService;
        _summaryBuilder = summaryBuilder;
    }

    /// <inheritdoc />
    public bool IsAvailable => _embeddingClient is not null;

    public bool HasIndex => _hasIndex;
    public int IndexedCount => _index.Count;

    public async Task BuildIndexAsync(CancellationToken ct = default)
    {
        if (_embeddingClient is null) return; // disabled — no API key

        _index.Clear();
        _hasIndex = false;

        var entries = new List<IndexEntry>();

        foreach (var kvp in Constants.GameTypes.OrderBy(k => k.Key))
        {
            ct.ThrowIfCancellationRequested();
            var entities = (await GetAllByTypeAsync(kvp.Value)).ToList();
            if (entities.Count == 0) continue;

            // Build summaries first, then batch embed
            var summaries = entities.Select(e => _summaryBuilder.BuildSummary(e)).ToList();

            for (var i = 0; i < summaries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var summary = summaries[i];

                try
                {
                    var embedding = await _embeddingClient.GenerateEmbeddingAsync(summary,
                        new EmbeddingGenerationOptions(), ct);
                    var vector = embedding.Value.ToFloats().ToArray();
                    entries.Add(new IndexEntry(summary, vector));
                }
                catch (Exception)
                {
                    // Skip entities that fail to embed (e.g. too long text)
                    // Add without embedding for future improvement
                }
            }
        }

        _index.AddRange(entries);
        _hasIndex = _index.Count > 0;
    }

    public async Task<IReadOnlyList<RagResult>> SearchAsync(string query, int topK = 5,
        CancellationToken ct = default)
    {
        if (_embeddingClient is null || !_hasIndex || _index.Count == 0)
            return Array.Empty<RagResult>();

        var queryEmbedding = await _embeddingClient.GenerateEmbeddingAsync(query,
            new EmbeddingGenerationOptions(), ct);
        var queryVector = queryEmbedding.Value.ToFloats().ToArray();

        // Cosine similarity search
        var scored = _index
            .Select(e => new RagResult(e.Summary, CosineSimilarity(queryVector, e.Vector)))
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return scored;
    }

    public void Clear()
    {
        _index.Clear();
        _hasIndex = false;
    }

    private async Task<IReadOnlyList<IEntity>> GetAllByTypeAsync(Type entityType)
    {
        try
        {
            var method = typeof(IHostService).GetMethod(nameof(IHostService.Repository))
                ?.MakeGenericMethod(entityType);
            var repo = method?.Invoke(_hostService, null);
            if (repo is null) return Array.Empty<IEntity>();

            var getAllMethod = repo.GetType().GetMethod("GetAllAsync");
            var task = (Task?)getAllMethod?.Invoke(repo, null);
            if (task is null) return Array.Empty<IEntity>();

            await task.ConfigureAwait(false);
            var result = task.GetType().GetProperty("Result")?.GetValue(task) as System.Collections.IEnumerable;
            return result?.Cast<IEntity>().ToList() ?? (IReadOnlyList<IEntity>)Array.Empty<IEntity>();
        }
        catch
        {
            return Array.Empty<IEntity>();
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length && i < b.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return (normA <= 0 || normB <= 0) ? 0 : dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private sealed record IndexEntry(string Summary, float[] Vector);
}

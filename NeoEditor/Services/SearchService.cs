using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public class SearchService : ISearchService
{
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;

    public SearchService(IDbContextFactory<GameDbContext> gameDbFactory)
    {
        _gameDbFactory = gameDbFactory;
    }

    public async Task<(List<SearchResultGroup> Groups, string StatusText)> SearchAsync(string query, CancellationToken ct = default)
    {
        query = query?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(query))
            return ([], "");

        try
        {
            return await Task.Run(async () =>
            {
                await using var db = await _gameDbFactory.CreateDbContextAsync();
                var groups = new List<SearchResultGroup>();

                string? colFilter = null;
                var searchTerms = query;
                var colonIdx = query.IndexOf(':');
                if (colonIdx > 0 && !query.Contains(' '))
                {
                    colFilter = query[..colonIdx];
                    searchTerms = query[(colonIdx + 1)..];
                }

                foreach (var (typeName, type) in Constants.GameTypes)
                {
                    ct.ThrowIfCancellationRequested();
                    var method = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                        .MakeGenericMethod(type);
                    var dbSet = (System.Collections.IEnumerable)method.Invoke(db, null)!;

                    var stringProps = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => p.DeclaringType != typeof(IEntity)
                            && p.GetCustomAttribute<ColumnAttribute>() != null
                            && p.PropertyType == typeof(string))
                        .ToList();

                    var matches = new List<(IEntity Entity, string Field, string MatchText)>();

                    foreach (var entity in dbSet)
                    {
                        if (entity is not IEntity ie) continue;

                        if (colFilter is not null)
                        {
                            var prop = stringProps.FirstOrDefault(p =>
                            {
                                var col = p.GetCustomAttribute<ColumnAttribute>();
                                return col?.Name == colFilter || p.Name == colFilter;
                            });
                            if (prop is null) continue;
                            var val = prop.GetValue(ie)?.ToString() ?? "";
                            if (val.Contains(searchTerms, StringComparison.OrdinalIgnoreCase))
                                matches.Add((ie, prop.Name, val));
                        }
                        else
                        {
                            foreach (var prop in stringProps)
                            {
                                var val = prop.GetValue(ie)?.ToString() ?? "";
                                if (string.IsNullOrEmpty(val)) continue;
                                if (val.Contains(searchTerms, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches.Add((ie, prop.Name, val));
                                    break;
                                }
                            }
                        }

                        if (matches.Count >= 50) break;
                    }

                    if (matches.Count > 0)
                        groups.Add(new SearchResultGroup(typeName, type, matches));
                }

                var statusText = groups.Count > 0
                    ? $"{groups.Sum(g => g.Items.Count)} results in {groups.Count} types"
                    : "No results found.";

                return (groups, statusText);
            });
        }
        catch (Exception ex)
        {
            return ([], $"Search error: {ex.Message}");
        }
    }
}

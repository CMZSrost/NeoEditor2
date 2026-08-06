using System;
using System.Collections.Generic;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>Comparison operator for a field-level search filter.</summary>
public enum FilterOperator
{
    Contains,
    Equals,
    NotEquals,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// A single field-level filter condition: field (column or property name),
/// operator, and the comparison value as text (parsed to the field's type).
/// Multiple filters combine with AND semantics.
/// </summary>
public record EntityFilter(string Field, FilterOperator Operator, string Value);

/// <summary>
/// Structured search request for <see cref="IHostService.SearchEntitiesAsync(EntitySearchRequest)"/>.
/// All qualifiers are optional; <see cref="Limit"/> is the page size and
/// <see cref="Offset"/> the page start. <see cref="SortBy"/> names a column or
/// property (e.g. "Subject", "Weight"); null = no sorting.
/// </summary>
public record EntitySearchRequest(
    string Query,
    IReadOnlyList<string>? EntityTypes = null,
    int? ModId = null,
    IReadOnlyList<EntityFilter>? Filters = null,
    int Limit = 50,
    int Offset = 0,
    string? SortBy = null,
    bool SortDescending = false);

/// <summary>
/// Result of a structured search. <see cref="Total"/> is the match count BEFORE
/// pagination; <see cref="Truncated"/> is true when more results exist beyond the page.
/// </summary>
public record EntitySearchResult(
    IReadOnlyList<IEntity> Items,
    int Total,
    bool Truncated);

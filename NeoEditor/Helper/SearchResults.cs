using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

public class SearchResultGroup
{
    public string TypeName { get; }
    public Type EntityType { get; }
    public ObservableCollection<SearchResultItem> Items { get; } = [];

    public SearchResultGroup(string typeName, Type entityType,
        List<(IEntity Entity, string Field, string MatchText)> matches)
    {
        TypeName = typeName;
        EntityType = entityType;
        foreach (var (entity, field, matchText) in matches)
            Items.Add(new SearchResultItem(entity, entityType, field, matchText));
    }
}

public class SearchResultItem
{
    public IEntity Entity { get; }
    public Type EntityType { get; }
    public string FieldName { get; }
    public string MatchText { get; }
    public string DisplayText =>
        $"{Entity.Subject}  [{FieldName}: {MatchText.Truncate(60)}]";

    public SearchResultItem(IEntity entity, Type entityType, string fieldName, string matchText)
    {
        Entity = entity;
        EntityType = entityType;
        FieldName = fieldName;
        MatchText = matchText;
    }
}

public static class StringExtensions
{
    public static string Truncate(this string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "...";
}

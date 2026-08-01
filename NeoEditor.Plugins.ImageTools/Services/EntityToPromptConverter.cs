using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Converts game entities to structured prompts for AI image generation.
/// Zero constructor dependencies (matches <see cref="ImageEditorProcessingService"/> pre-G1 pattern).
/// Uses reflection to read entity properties and builds pixel-art-specific prompts.
/// </summary>
public class EntityToPromptConverter
{
    private static readonly Dictionary<string, string> TypeCategoryMap = new()
    {
        ["ItemType"] = "item sprite",
        ["Creature"] = "creature sprite",
        ["Recipe"] = "crafting result icon",
        ["Encounter"] = "encounter scene",
        ["Condition"] = "status effect icon",
        ["AttackMode"] = "attack icon",
        ["BattleMove"] = "combat action icon",
        ["CampType"] = "camp site icon",
        ["ContainerType"] = "container icon",
        ["Faction"] = "faction icon",
        ["TreasureTable"] = "treasure icon",
        ["Map"] = "map icon",
        ["ChargeProfile"] = "charge icon",
        ["BarterHex"] = "hex icon",
        ["DmcPlace"] = "location icon",
    };

    /// <summary>
    /// Build a pixel art image generation prompt for the given entity.
    /// </summary>
    public string BuildPrompt(IEntity entity, ImageGenerationOptions options)
    {
        var type = entity.GetType();
        var typeName = type.Name;
        var category = TypeCategoryMap.GetValueOrDefault(typeName, "pixel art sprite");

        var sb = new StringBuilder();
        sb.Append("A pixel art ");
        sb.Append(category);
        sb.Append(" for a 2D post-apocalyptic game. ");

        // Build description from entity properties
        var description = BuildDescription(entity);
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.Append(description);
            sb.Append(". ");
        }
        else
        {
            // Fallback for data-poor entities
            var subject = entity.Subject;
            if (!string.IsNullOrWhiteSpace(subject) && subject != entity.EntityId)
                sb.Append($"A {subject.ToLowerInvariant()}. ");
            else
                sb.Append($"Based on the {typeName} \"{entity.EntityId}\". ");
        }

        sb.Append("Single object centered on transparent background, ");
        sb.Append(options.Width);
        sb.Append('x');
        sb.Append(options.Height);
        sb.Append(" pixels, limited color palette (16-32 colors), ");
        sb.Append("pixel-perfect edges, no anti-aliasing, clean pixel art style with clear outlines.");

        return sb.ToString();
    }

    /// <summary>
    /// Build a description string from the entity's properties.
    /// </summary>
    private static string BuildDescription(IEntity entity)
    {
        var parts = new List<string>();

        // Start with subject/name
        var subject = entity.Subject;
        if (!string.IsNullOrWhiteSpace(subject) && subject != entity.EntityId)
        {
            parts.Add(subject);
        }

        // Scan for descriptive properties (names, colors, materials)
        var type = entity.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                && p.Name is not ("EntityId" or "Subject" or "ModId" or "MergedId" or "IsDirty" or "FilePath"));

        foreach (var p in props)
        {
            var value = p.GetValue(entity);
            if (value is null) continue;

            // Skip reference fields for the description (they're just IDs)
            if (p.GetCustomAttribute<ReferenceFieldAttribute>() is not null) continue;

            var strValue = value.ToString();
            if (string.IsNullOrWhiteSpace(strValue)) continue;

            var propName = p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name;

            // Collect name-like properties
            if (IsNameProperty(propName) && strValue != subject)
            {
                parts.Add($"called \"{strValue}\"");
                continue;
            }

            // Collect color-like properties
            if (IsColorProperty(propName))
            {
                parts.Add($"with {strValue.ToLowerInvariant()} tones");
                continue;
            }

            // Collect material-like properties
            if (IsMaterialProperty(propName))
            {
                parts.Add($"made of {strValue.ToLowerInvariant()}");
                continue;
            }
        }

        // Limit to 3 parts to keep prompts concise
        if (parts.Count > 3)
            parts = parts.Take(3).ToList();

        return parts.Count > 0
            ? string.Join(", ", parts)
            : string.Empty;
    }

    private static bool IsNameProperty(string columnName)
    {
        var lower = columnName.ToLowerInvariant();
        return lower.Contains("name") || lower.Contains("label") || lower.Contains("title");
    }

    private static bool IsColorProperty(string columnName)
    {
        var lower = columnName.ToLowerInvariant();
        return lower.Contains("color") || lower.Contains("colour") || lower.Contains("hue")
            || lower.Contains("tint") || lower.Contains("shade");
    }

    private static bool IsMaterialProperty(string columnName)
    {
        var lower = columnName.ToLowerInvariant();
        return lower.Contains("material") || lower.Contains("mattype") || lower.Contains("texture");
    }
}

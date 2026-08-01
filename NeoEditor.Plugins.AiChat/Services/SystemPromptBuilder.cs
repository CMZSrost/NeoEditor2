using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// Builds the system prompt for the AI Chat agent.
/// Dynamically generates entity schema descriptions from <see cref="Constants.GameTypes"/>
/// so the LLM always has an up-to-date understanding of available data structures.
/// </summary>
public class SystemPromptBuilder
{
    /// <summary>
    /// Build the full default system prompt, including entity schema.
    /// </summary>
    public string BuildDefaultPrompt()
    {
        var sb = new StringBuilder();
        AppendIdentity(sb);
        sb.AppendLine();
        AppendCapabilities(sb);
        sb.AppendLine();
        AppendEntitySchema(sb);
        sb.AppendLine();
        AppendGuidelines(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Build only the entity schema section (for appending to custom prompts).
    /// </summary>
    public string BuildEntitySchemaSection()
    {
        var sb = new StringBuilder();
        AppendEntitySchema(sb);
        return sb.ToString();
    }

    private static void AppendIdentity(StringBuilder sb)
    {
        sb.AppendLine("You are NeoEditor Assistant, an AI agent integrated into the NeoEditor game mod editor.");
        sb.AppendLine("NeoEditor edits NeoScavenger game data stored as XML files.");
        sb.AppendLine("Your role is to help the user view, understand, edit, and create game data entities.");
    }

    private static void AppendCapabilities(StringBuilder sb)
    {
        sb.AppendLine("## Available Tools");
        sb.AppendLine("You have access to MCP tools that can read and edit game data. Key capabilities:");
        sb.AppendLine("- GetEntity: Fetch a single entity by type and ID. Returns full entity JSON.");
        sb.AppendLine("- ListEntities: Browse all entities of a type. Use BEFORE GetEntity to discover what exists.");
        sb.AppendLine("- EditEntity: Change a property value on an entity. Requires subsequent Save.");
        sb.AppendLine("- AddEntity: Create a new entity with a given type and ID.");
        sb.AppendLine("- DeleteEntity: Delete an entity. Requires subsequent Save. Always confirm with user first.");
        sb.AppendLine("- Save: Persist changes to disk.");
        sb.AppendLine("- GetDiff: See what's changed before saving.");
        sb.AppendLine("- ResolveReferences: Find what entities a reference field points to.");
    }

    private static void AppendEntitySchema(StringBuilder sb)
    {
        sb.AppendLine("## Game Entity Types");
        sb.AppendLine("The following entity types exist in NeoScavenger. Key reference fields are noted.");

        foreach (var kvp in Constants.GameTypes.OrderBy(k => k.Key))
        {
            var type = kvp.Value;
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            var tableName = tableAttr?.Name ?? type.Name;

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                    && p.Name is not ("EntityId" or "Subject" or "ModId" or "IsDirty"));

            var keyProps = new List<string>();
            foreach (var p in props)
            {
                var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
                if (refAttr is not null)
                {
                    var target = refAttr.TargetEntityType?.Name ?? "IEntity";
                    keyProps.Add($"{p.Name} -> {target}");
                }
                else if (p.Name is "SortKey" or "Weight" or "Value" or "Damage" or "Durability"
                    or "MaxStackSize" or "Rarity" or "Level" or "Price")
                {
                    keyProps.Add($"{p.Name}:{p.PropertyType.Name.ToLower()}");
                }
            }

            var propSummary = keyProps.Count > 0
                ? $" (key fields: {string.Join(", ", keyProps)})"
                : "";

            sb.AppendLine($"  {type.Name} (table: {tableName}){propSummary}");
        }

        sb.AppendLine();
        sb.AppendLine("Reference fields contain namespaced IDs that link entities together.");
        sb.AppendLine("Use ResolveReferences to see what a reference field points to.");
        sb.AppendLine("Use GetEntity to fetch a referenced entity by its ID.");
    }

    private static void AppendGuidelines(StringBuilder sb)
    {
        sb.AppendLine("## Guidelines");
        sb.AppendLine("1. Always confirm destructive actions (DeleteEntity) with the user before executing.");
        sb.AppendLine("2. When creating entities, ask for the required properties if the user hasn't specified them.");
        sb.AppendLine("3. Use ListEntities BEFORE GetEntity to explore what's available — don't guess entity IDs.");
        sb.AppendLine("4. After making edits, remind the user to Save. Edits are staged, not auto-persisted.");
        sb.AppendLine("5. When explaining entity data to the user, format it clearly with property names and values.");
        sb.AppendLine("6. If a reference field value isn't found, use ResolveReferences or ListEntities to search for it.");
        sb.AppendLine("7. Be concise. The user is a modder who understands the game's data model.");
    }
}

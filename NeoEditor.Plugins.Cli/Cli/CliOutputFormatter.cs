using System;
using System.Text;

namespace NeoEditor.Plugins.Cli.Cli;

/// <summary>
/// Formats command results as plain text or JSON.
/// </summary>
public class CliOutputFormatter
{
    public string Format(object? result, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => FormatJson(result),
            "text" => FormatText(result),
            "table" => FormatTable(result),
            _ => FormatText(result)
        };
    }

    public string FormatHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("NeoEditor CLI — Available Commands");
        sb.AppendLine("====================================");
        sb.AppendLine();
        sb.AppendLine("Usage: neoeditor-cli <command> [options]");
        sb.AppendLine();
        sb.AppendLine("Commands:");
        sb.AppendLine("  get-entity, get, show       Get entity by type and ID");
        sb.AppendLine("    --entity-type, -t <type>   Entity type name (e.g. ItemType)");
        sb.AppendLine("    --entity-id, -id <id>      Entity ID");
        sb.AppendLine();
        sb.AppendLine("  edit-entity, edit, set      Edit a property value");
        sb.AppendLine("    --entity-type, -t <type>   Entity type name");
        sb.AppendLine("    --entity-id, -id <id>      Entity ID");
        sb.AppendLine("    --property, -p <name>      Property name");
        sb.AppendLine("    --value, -v <value>        New value");
        sb.AppendLine();
        sb.AppendLine("  add-entity, add, create     Create a new entity");
        sb.AppendLine("    --entity-type, -t <type>   Entity type name");
        sb.AppendLine("    --entity-id, -id <id>      Unique entity ID");
        sb.AppendLine("    --property, -p <name>      Property to set (repeatable)");
        sb.AppendLine("    --value, -v <value>        Property value (repeatable)");
        sb.AppendLine();
        sb.AppendLine("  delete-entity, delete, rm   Delete an entity");
        sb.AppendLine("    --entity-type, -t <type>   Entity type name");
        sb.AppendLine("    --entity-id, -id <id>      Entity ID");
        sb.AppendLine();
        sb.AppendLine("  list-entities, list, ls     List entities of a type");
        sb.AppendLine("    --entity-type, -t <type>   Entity type name");
        sb.AppendLine("    --filter <text>            Optional substring filter");
        sb.AppendLine("    --limit, -n <num>          Max results (default 100)");
        sb.AppendLine();
        sb.AppendLine("  save, commit                Persist staged changes");
        sb.AppendLine("    --entity-id, -id <id>      Optional: save only this entity");
        sb.AppendLine();
        sb.AppendLine("  diff, changes               Show field-level diffs");
        sb.AppendLine("    --entity-id, -id <id>      Optional: diff for specific entity");
        sb.AppendLine();
        sb.AppendLine("  query-references, refs      Resolve reference values");
        sb.AppendLine("    --entity-type, -t <type>   Entity type name");
        sb.AppendLine("    --entity-id, -id <id>      Entity ID");
        sb.AppendLine("    --property, -p <name>      Reference property name");
        sb.AppendLine();
        sb.AppendLine("Global Options:");
        sb.AppendLine("  --format, -f <fmt>          Output format: text, json, table (default: text)");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatJson(object? result)
    {
        if (result is null) return "{}";
        return Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
    }

    private static string FormatText(object? result)
    {
        if (result is null) return "(no result)";
        if (result is string s) return s;
        // Use JSON as readable text fallback for objects
        return Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
    }

    private static string FormatTable(object? result)
    {
        // Table format is the same as text for now; could be enhanced later
        return FormatText(result);
    }
}

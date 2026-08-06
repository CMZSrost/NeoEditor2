using System;
using System.Linq;

namespace NeoEditor.Plugins.Cli.Cli;

/// <summary>
/// Parses command-line arguments into a <see cref="CliParsedCommand"/>.
/// </summary>
public class CliCommandParser
{
    /// <summary>
    /// Parse args into a structured command. The first positional arg is the command name.
    /// </summary>
    public CliParsedCommand Parse(string[] args)
    {
        if (args is null || args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
            return new CliParsedCommand { Command = CliCommandType.Help };

        var commandName = NormalizeCommand(args[0]);
        var cmd = new CliParsedCommand { Command = commandName };
        var remaining = args.Skip(1).ToArray();

        // Parse named options
        for (var i = 0; i < remaining.Length; i++)
        {
            switch (remaining[i].ToLowerInvariant())
            {
                case "--format":
                case "-f":
                    cmd.Format = GetNextArg(remaining, ref i)?.ToLowerInvariant() ?? "text";
                    break;
                case "--entity-type":
                case "-t":
                    cmd.EntityType = GetNextArg(remaining, ref i);
                    break;
                case "--entity-id":
                case "-id":
                    cmd.EntityId = GetNextArg(remaining, ref i);
                    break;
                case "--property":
                case "-p":
                    cmd.PropertyName = GetNextArg(remaining, ref i);
                    break;
                case "--value":
                case "-v":
                    cmd.PropertyValue = GetNextArg(remaining, ref i);
                    break;
                case "--filter":
                    cmd.Filter = GetNextArg(remaining, ref i);
                    break;
                case "--limit":
                case "-n":
                    if (int.TryParse(GetNextArg(remaining, ref i), out var limit))
                        cmd.Limit = limit;
                    break;
                case "--mod-id":
                    if (int.TryParse(GetNextArg(remaining, ref i), out var modId))
                        cmd.ModId = modId;
                    break;
                case "--commit":
                    cmd.Commit = true;
                    break;
                default:
                    // Treat unrecognized args as positional fallback
                    if (cmd.EntityType is null) cmd.EntityType = remaining[i];
                    else if (cmd.EntityId is null) cmd.EntityId = remaining[i];
                    else if (cmd.PropertyName is null) cmd.PropertyName = remaining[i];
                    else if (cmd.PropertyValue is null) cmd.PropertyValue = remaining[i];
                    break;
            }
        }

        // Validate required args per command
        Validate(cmd);
        return cmd;
    }

    private static CliCommandType NormalizeCommand(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "help" or "--help" or "-h" => CliCommandType.Help,
            "get-entity" or "get" or "show" => CliCommandType.GetEntity,
            "edit-entity" or "edit" or "set" => CliCommandType.EditEntity,
            "add-entity" or "add" or "create" => CliCommandType.AddEntity,
            "delete-entity" or "delete" or "remove" or "rm" => CliCommandType.DeleteEntity,
            "list-entities" or "list" or "ls" => CliCommandType.ListEntities,
            "save" or "commit" => CliCommandType.Save,
            "diff" or "changes" => CliCommandType.Diff,
            "query-references" or "refs" or "references" => CliCommandType.QueryReferences,
            "undo" => CliCommandType.Undo,
            "redo" => CliCommandType.Redo,
            "publish" => CliCommandType.Publish,
            "export-mod" or "export" => CliCommandType.ExportMod,
            _ => CliCommandType.Unknown
        };
    }

    private static string? GetNextArg(string[] args, ref int i)
    {
        if (i + 1 < args.Length) return args[++i];
        return null;
    }

    private static void Validate(CliParsedCommand cmd)
    {
        switch (cmd.Command)
        {
            case CliCommandType.Unknown:
                cmd.HasError = true;
                cmd.ErrorMessage = "Unknown command. Use 'help' to see available commands.";
                break;

            case CliCommandType.GetEntity:
            case CliCommandType.DeleteEntity:
                if (string.IsNullOrWhiteSpace(cmd.EntityType) || string.IsNullOrWhiteSpace(cmd.EntityId))
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = $"{cmd.Command} requires --entity-type and --entity-id.";
                }
                break;

            case CliCommandType.EditEntity:
                if (string.IsNullOrWhiteSpace(cmd.EntityType) || string.IsNullOrWhiteSpace(cmd.EntityId))
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = $"{cmd.Command} requires --entity-type and --entity-id.";
                }
                else if (string.IsNullOrWhiteSpace(cmd.PropertyName) || cmd.PropertyValue is null)
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = "edit-entity requires --property and --value.";
                }
                break;

            case CliCommandType.QueryReferences:
                if (string.IsNullOrWhiteSpace(cmd.EntityType) || string.IsNullOrWhiteSpace(cmd.EntityId))
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = $"{cmd.Command} requires --entity-type and --entity-id.";
                }
                else if (string.IsNullOrWhiteSpace(cmd.PropertyName))
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = "query-references requires --property.";
                }
                break;

            case CliCommandType.ListEntities:
                if (string.IsNullOrWhiteSpace(cmd.EntityType))
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = "list-entities requires --entity-type.";
                }
                break;

            case CliCommandType.AddEntity:
                if (string.IsNullOrWhiteSpace(cmd.EntityType) || string.IsNullOrWhiteSpace(cmd.EntityId))
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = "add-entity requires --entity-type and --entity-id.";
                }
                break;

            case CliCommandType.ExportMod:
                if (cmd.ModId is null)
                {
                    cmd.HasError = true;
                    cmd.ErrorMessage = "export-mod requires --mod-id <number>.";
                }
                break;
        }
    }
}

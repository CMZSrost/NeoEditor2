using NeoEditor.Plugins.Cli.Cli;
using Xunit;

namespace NeoEditor.Plugins.Cli.Tests;

public class CliCommandParserTests
{
    private readonly CliCommandParser _parser = new();

    [Fact]
    public void Parse_EmptyArgs_ReturnsHelp()
    {
        var cmd = _parser.Parse(new string[0]);
        Assert.Equal(CliCommandType.Help, cmd.Command);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_NullArgs_ReturnsHelp()
    {
        var cmd = _parser.Parse(null!);
        Assert.Equal(CliCommandType.Help, cmd.Command);
    }

    [Fact]
    public void Parse_ExplicitHelp_ReturnsHelp()
    {
        Assert.Equal(CliCommandType.Help, _parser.Parse(new[] { "help" }).Command);
        Assert.Equal(CliCommandType.Help, _parser.Parse(new[] { "--help" }).Command);
        Assert.Equal(CliCommandType.Help, _parser.Parse(new[] { "-h" }).Command);
    }

    [Fact]
    public void Parse_UnknownCommand_HasError()
    {
        var cmd = _parser.Parse(new[] { "foobar" });
        Assert.Equal(CliCommandType.Unknown, cmd.Command);
        Assert.True(cmd.HasError);
        Assert.Contains("Unknown command", cmd.ErrorMessage);
    }

    // ── GetEntity ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_GetEntity_WithFullOptions()
    {
        var cmd = _parser.Parse(new[] { "get", "-t", "ItemType", "-id", "item_01", "-f", "json" });

        Assert.Equal(CliCommandType.GetEntity, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("item_01", cmd.EntityId);
        Assert.Equal("json", cmd.Format);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_GetEntity_WithLongOptions()
    {
        var cmd = _parser.Parse(new[] { "get-entity", "--entity-type", "Creature", "--entity-id", "dog_01" });

        Assert.Equal(CliCommandType.GetEntity, cmd.Command);
        Assert.Equal("Creature", cmd.EntityType);
        Assert.Equal("dog_01", cmd.EntityId);
    }

    [Fact]
    public void Parse_GetEntity_MissingRequired_HasError()
    {
        var cmd = _parser.Parse(new[] { "get", "-t", "ItemType" }); // missing --entity-id
        Assert.Equal(CliCommandType.GetEntity, cmd.Command);
        Assert.True(cmd.HasError);
        Assert.Contains("entity-type", cmd.ErrorMessage);
    }

    [Fact]
    public void Parse_Show_Alias_MapsTo_GetEntity()
    {
        var cmd = _parser.Parse(new[] { "show", "-t", "Recipe", "-id", "rec_01" });
        Assert.Equal(CliCommandType.GetEntity, cmd.Command);
    }

    // ── EditEntity ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_EditEntity_WithAllOptions()
    {
        var cmd = _parser.Parse(new[] { "edit", "-t", "ItemType", "-id", "item_01", "-p", "Name", "-v", "Sword" });

        Assert.Equal(CliCommandType.EditEntity, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("item_01", cmd.EntityId);
        Assert.Equal("Name", cmd.PropertyName);
        Assert.Equal("Sword", cmd.PropertyValue);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_EditEntity_MissingProperty_HasError()
    {
        var cmd = _parser.Parse(new[] { "edit", "-t", "ItemType", "-id", "item_01" });
        Assert.True(cmd.HasError);
        Assert.Contains("property", cmd.ErrorMessage);
    }

    [Fact]
    public void Parse_Set_Alias_MapsTo_EditEntity()
    {
        var cmd = _parser.Parse(new[] { "set", "-t", "ItemType", "-id", "item_01", "-p", "val", "-v", "42" });
        Assert.Equal(CliCommandType.EditEntity, cmd.Command);
    }

    // ── AddEntity ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AddEntity_WithFullOptions()
    {
        var cmd = _parser.Parse(new[] { "add", "-t", "ItemType", "-id", "new_item" });

        Assert.Equal(CliCommandType.AddEntity, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("new_item", cmd.EntityId);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_Create_Alias_MapsTo_AddEntity()
    {
        var cmd = _parser.Parse(new[] { "create", "-t", "Creature", "-id", "monster_01" });
        Assert.Equal(CliCommandType.AddEntity, cmd.Command);
    }

    // ── DeleteEntity ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_DeleteEntity_WithOptions()
    {
        var cmd = _parser.Parse(new[] { "delete", "-t", "ItemType", "-id", "old_item" });

        Assert.Equal(CliCommandType.DeleteEntity, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("old_item", cmd.EntityId);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_Rm_Alias_MapsTo_DeleteEntity()
    {
        var cmd = _parser.Parse(new[] { "rm", "-t", "ItemType", "-id", "junk" });
        Assert.Equal(CliCommandType.DeleteEntity, cmd.Command);
    }

    // ── ListEntities ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_ListEntities_WithFilterAndLimit()
    {
        var cmd = _parser.Parse(new[] { "list", "-t", "ItemType", "--filter", "sword", "-n", "25" });

        Assert.Equal(CliCommandType.ListEntities, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("sword", cmd.Filter);
        Assert.Equal(25, cmd.Limit);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_ListEntities_MissingType_HasError()
    {
        var cmd = _parser.Parse(new[] { "ls" });
        Assert.True(cmd.HasError);
    }

    [Fact]
    public void Parse_ListEntities_DefaultFormat_IsText()
    {
        var cmd = _parser.Parse(new[] { "list", "-t", "ItemType" });
        Assert.Equal("text", cmd.Format);
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Save_NoEntityId()
    {
        var cmd = _parser.Parse(new[] { "save" });
        Assert.Equal(CliCommandType.Save, cmd.Command);
        Assert.Null(cmd.EntityId);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_Save_WithEntityId()
    {
        var cmd = _parser.Parse(new[] { "commit", "-id", "item_01" });
        Assert.Equal(CliCommandType.Save, cmd.Command);
        Assert.Equal("item_01", cmd.EntityId);
    }

    // ── Diff ─────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Diff_NoArgs()
    {
        var cmd = _parser.Parse(new[] { "diff" });
        Assert.Equal(CliCommandType.Diff, cmd.Command);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_Changes_Alias_MapsTo_Diff()
    {
        var cmd = _parser.Parse(new[] { "changes", "-id", "item_01" });
        Assert.Equal(CliCommandType.Diff, cmd.Command);
        Assert.Equal("item_01", cmd.EntityId);
    }

    // ── QueryReferences ──────────────────────────────────────────────────

    [Fact]
    public void Parse_QueryReferences_WithAllOptions()
    {
        var cmd = _parser.Parse(new[] { "refs", "-t", "ItemType", "-id", "item_01", "-p", "weaponRef" });

        Assert.Equal(CliCommandType.QueryReferences, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("item_01", cmd.EntityId);
        Assert.Equal("weaponRef", cmd.PropertyName);
        Assert.False(cmd.HasError);
    }

    [Fact]
    public void Parse_QueryReferences_MissingProperty_HasError()
    {
        var cmd = _parser.Parse(new[] { "references", "-t", "ItemType", "-id", "item_01" });
        Assert.True(cmd.HasError);
        Assert.Contains("property", cmd.ErrorMessage);
    }

    // ── Format ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Format_DefaultsToText()
    {
        var cmd = _parser.Parse(new[] { "list", "-t", "ItemType" });
        Assert.Equal("text", cmd.Format);
    }

    [Fact]
    public void Parse_Format_ShortFlag()
    {
        var cmd = _parser.Parse(new[] { "list", "-t", "ItemType", "-f", "json" });
        Assert.Equal("json", cmd.Format);
    }

    // ── Positional fallback ──────────────────────────────────────────────

    [Fact]
    public void Parse_PositionalArgs_AreMapped()
    {
        var cmd = _parser.Parse(new[] { "get", "ItemType", "item_01" });

        Assert.Equal(CliCommandType.GetEntity, cmd.Command);
        Assert.Equal("ItemType", cmd.EntityType);
        Assert.Equal("item_01", cmd.EntityId);
    }

    [Fact]
    public void Parse_Limit_Invalid_NotSet()
    {
        var cmd = _parser.Parse(new[] { "list", "-t", "ItemType", "-n", "not_a_number" });
        Assert.Null(cmd.Limit);
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using Xunit;

namespace NeoEditor.Core.Tests.Spec;

/// <summary>
/// Architecture compliance tests for spec rule R24:
/// "All data modifications must go through IHostService."
///
/// These tests verify that forbidden patterns are not present:
/// - ViewModels must not reference GameDbContext directly
/// - CRUD operations must flow through HostService scope registration
/// </summary>
public class R24HostServiceRuleTests
{
    /// <summary>
    /// Verify that the Core project does not reference GameDbContext (EF).
    /// Core must remain pure domain model with zero data access dependencies.
    /// </summary>
    [Fact]
    public void R24_Core_DoesNotReference_GameDbContext()
    {
        var coreAssembly = typeof(NeoEditor.Core.Abstractions.IHostService).Assembly;
        var referencedAssemblies = coreAssembly.GetReferencedAssemblies();

        // Core should not reference EF Core or SQLite
        var efRefs = referencedAssemblies
            .Where(a => a.Name != null && (
                a.Name.Contains("EntityFramework") ||
                a.Name.Contains("SQLite") ||
                a.Name.Contains("Sqlite")))
            .ToList();

        Assert.Empty(efRefs);
    }

    /// <summary>
    /// Verify that IEditorCommand is properly defined in Core/Abstractions,
    /// not leaked from Infra. This ensures the command interface contract
    /// is available to all layers without EF dependency.
    /// </summary>
    [Fact]
    public void R24_IEditorCommand_IsIn_CoreAbstractions()
    {
        var cmdType = typeof(IEditorCommand);
        Assert.Equal("NeoEditor.Core.Abstractions", cmdType.Namespace);
    }

    /// <summary>
    /// Verify that IHostService interface defines all required R24 members:
    /// ExecuteAsync, SaveAsync, DirtyTracking, Events, Scope management.
    /// </summary>
    [Fact]
    public void R24_IHostService_Has_RequiredMembers()
    {
        var iface = typeof(IHostService);

        // Command execution
        Assert.NotNull(iface.GetMethod("ExecuteAsync"));
        Assert.NotNull(iface.GetMethod("ExecuteBatchAsync"));
        Assert.NotNull(iface.GetMethod("UndoAsync"));
        Assert.NotNull(iface.GetMethod("RedoAsync"));

        // Dirty tracking
        Assert.NotNull(iface.GetProperty("DirtyEntities"));
        Assert.NotNull(iface.GetProperty("HasUnsavedChanges"));
        Assert.NotNull(iface.GetEvent("DirtyStateChanged"));
        Assert.NotNull(iface.GetMethod("MarkEntityDirty"));
        Assert.NotNull(iface.GetMethod("ClearDirtyEntities"));

        // Persistence
        Assert.NotNull(iface.GetMethod("SaveAsync"));
        Assert.NotNull(iface.GetMethod("SaveAllAsync"));
        Assert.NotNull(iface.GetMethod("DiscardAsync"));

        // Diff
        Assert.NotNull(iface.GetMethod("GetDiffAsync"));

        // Events
        Assert.NotNull(iface.GetProperty("Changes"));

        // Scope management
        Assert.NotNull(iface.GetMethod("RegisterCommandScope"));
        Assert.NotNull(iface.GetMethod("UnregisterCommandScope"));
        Assert.NotNull(iface.GetMethod("SetActiveScope"));
    }

    /// <summary>
    /// R24/R26: the data-table View must not contain GameDbContext WRITE paths
    /// (bulk upsert / delete-range / save). All entity persistence flows through IHostService.
    /// EditorDbContext bookkeeping (LastModified/LastImport) is allowed — it is not the entity store.
    /// </summary>
    [Fact]
    public void R24_DataTableView_DoesNotWrite_GameDbContext()
    {
        var root = FindRepoRoot();
        var files = Directory.GetFiles(
            Path.Combine(root, "NeoEditor.App", "Views", "UserControls"),
            "ModGameDataTabsView*.cs");

        Assert.NotEmpty(files);
        // These are the GameDbContext write markers that were removed in Phase 9B (B5).
        string[] forbidden = { "DbBulkInsertOrUpdate", "SaveToDatabaseAsync" };
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var pattern in forbidden)
                Assert.False(
                    text.Contains(pattern, StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} must not contain '{pattern}' " +
                    "(R24: all data writes flow through IHostService).");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NeoEditor.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root (NeoEditor.sln).");
    }
}

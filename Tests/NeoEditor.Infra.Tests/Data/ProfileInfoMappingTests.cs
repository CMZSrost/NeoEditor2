using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Infra.Tests.Data.Repository;
using Xunit;

namespace NeoEditor.Infra.Tests.Data;

/// <summary>
/// B4: <see cref="ProfileInfo.IncludeGame"/> and <see cref="ProfileInfo.SingleModId"/> are
/// real DB columns (persisted single-mod profiles) — verify they round-trip through EditorDbContext.
/// </summary>
public class ProfileInfoMappingTests
{
    private static EditorDbContext CreateDb(out SqliteConnection conn)
    {
        conn = RepositoryTestHelpers.OpenSqlite();
        conn.Open();
        var options = new DbContextOptionsBuilder<EditorDbContext>().UseSqlite(conn).Options;
        var db = new EditorDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task IncludeGame_Defaults_To_True()
    {
        using var db = CreateDb(out var conn);
        try
        {
            var profile = new ProfileInfo
            {
                Name = "MergeProfile",
                Path = "Profiles/getmods_x.php",
                Content = "nRows=0"
            };
            db.ProfileInfos.Add(profile);
            await db.SaveChangesAsync();

            Assert.True(profile.IncludeGame, "real merge profiles include game data by default");
        }
        finally
        {
            await db.DisposeAsync();
            conn.Dispose();
        }
    }

    [Fact]
    public async Task SingleModProfile_IncludeGame_False_And_SingleModId_Persist()
    {
        // In-memory SQLite is connection-scoped, so share ONE connection across the
        // write context and the read context via a factory (same pattern as XmlRepositoryTests).
        var conn = RepositoryTestHelpers.OpenSqlite();
        conn.Open();
        var options = new DbContextOptionsBuilder<EditorDbContext>().UseSqlite(conn).Options;
        var factory = new RepositoryTestHelpers.TestDbFactory<EditorDbContext>(options);

        int profileId;
        await using (var db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
            var profile = new ProfileInfo
            {
                Name = "MyMod",
                Path = "",
                Content = "nRows=1&strModName0=0&strModURL0=Mods/MyMod",
                IncludeGame = false,
                SingleModId = 42
            };
            db.ProfileInfos.Add(profile);
            await db.SaveChangesAsync();
            profileId = profile.ProfileId;
        }

        // Re-open a fresh context (same connection) to prove the columns are persisted.
        await using var db2 = factory.CreateDbContext();
        var reloaded = await db2.ProfileInfos.FirstOrDefaultAsync(p => p.ProfileId == profileId);
        Assert.NotNull(reloaded);
        Assert.Equal(42, reloaded.SingleModId);
        Assert.False(reloaded.IncludeGame);

        await conn.DisposeAsync();
    }

    [Fact]
    public async Task NormalProfile_Has_Null_SingleModId()
    {
        using var db = CreateDb(out var conn);
        try
        {
            var profile = new ProfileInfo
            {
                Name = "MergeProfile",
                Path = "Profiles/getmods_x.php",
                Content = "nRows=0",
                IncludeGame = true
            };
            db.ProfileInfos.Add(profile);
            await db.SaveChangesAsync();

            Assert.Null(profile.SingleModId);
        }
        finally
        {
            await db.DisposeAsync();
            conn.Dispose();
        }
    }
}

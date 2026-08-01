using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Infra.Tests.Data.Repository;

/// <summary>
/// Shared helpers for repository tests: in-memory SQLite context factories and DI stubs.
/// </summary>
internal static class RepositoryTestHelpers
{
    /// <summary>In-memory <see cref="IDbContextFactory{T}"/> that shares one open SQLite connection.</summary>
    public sealed class TestDbFactory<T>(DbContextOptions<T> options) : IDbContextFactory<T> where T : DbContext
    {
        public T CreateDbContext() => (T)Activator.CreateInstance(typeof(T), options)!;

        public Task<T> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    /// <summary>Config service stub rooted at a temp game directory.</summary>
    public sealed class StubConfigService(string gameRoot) : IConfigService
    {
        public AppConfig Config { get; } = new() { GameRootDir = gameRoot };

        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Reference-list serializer stub. Returns empty text on serialize and an empty list on
    /// deserialize — sufficient for EF model building / round-trip tests that do not care about
    /// the reference-list payload.
    /// </summary>
    public sealed class StubReferenceSerializer : IReferenceListSerializer
    {
        public ReferenceList<IReferenceEntry> Deserialize(string raw, ReferenceFieldAttribute metadata)
            => new();

        public string Serialize(ReferenceList<IReferenceEntry> list, ReferenceFieldAttribute metadata)
            => "";
    }

    public static SqliteConnection OpenSqlite() => new("DataSource=:memory:");

    /// <summary>
    /// IXmlParser stub: exports entities as <c>&lt;table id="..."/&gt;</c> elements and imports
    /// tables back as entities carrying the table id as <c>EntityId</c>. Sufficient for repository
    /// orchestration tests that do not exercise the real serializer.
    /// </summary>
    public sealed class StubXmlParser : IXmlParser
    {
        public IList<T> ImportEntities<T>(XDocument doc, int modId, string filePath) where T : IEntity, new()
        {
            var result = new List<T>();
            foreach (var table in doc.Descendants("table"))
            {
                var entity = new T
                {
                    ModId = modId,
                    FilePath = filePath,
                    EntityId = table.Attribute("id")?.Value ?? $"t{result.Count}",
                };
                result.Add(entity);
            }
            return result;
        }

        public XDocument Export(IEnumerable<IEntity> entities, string databaseName = "neogame")
        {
            var db = new XElement("database", new XAttribute("name", databaseName));
            foreach (var entity in entities)
                db.Add(new XElement("table", new XAttribute("id", entity.EntityId)));
            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("pma_xml_export", db));
        }
    }
}

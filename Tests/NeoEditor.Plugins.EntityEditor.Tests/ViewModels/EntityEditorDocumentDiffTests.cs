using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// 追修: the XML diff view must show changes for entities that were already dirty when the
/// document opened. The diff OLD side comes from the game XML on disk (the true original —
/// nothing was exported yet), while the XML tab content shows the CURRENT values. Previously
/// both were initialized from the disk original, so old == new and the diff had no reaction.
/// </summary>
public class EntityEditorDocumentDiffTests
{
    [Fact]
    public void DirtyEntity_XmlContentShowsCurrent_DiffOldShowsDiskOriginal()
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"neotest_{Guid.NewGuid():N}.xml");
        try
        {
            // A minimal game XML file holding the ORIGINAL (pre-edit) values.
            File.WriteAllText(xmlPath,
                "<table name=\"attackmodes\"><column name=\"id\">1</column>" +
                "<column name=\"strName\">Old</column><column name=\"fDamageBlunt\">1.1</column></table>");
            var parser = new FakeXmlParser();
            var original = parser.ImportEntities<AttackMode>(XDocument.Load(xmlPath), 4, xmlPath).Single();

            // The entity as it exists in the editor NOW: same id/source file, edited value.
            var current = new AttackMode
            {
                EntityId = original.EntityId,
                ModId = 4,
                FilePath = xmlPath,
                Id = 1,
                Name = "Old",
                DamageBlunt = 1.2,
            };

            var session = new StubWorkspaceSession();
            session.DirtyEntities.Add(current.EntityId); // edited before the document opened

            var doc = new EntityEditorDocument(
                current, session, null!,
                new StubEntityLookupService(),
                new StubLocalizationService(),
                new StubNotificationService(),
                new ReferenceListSerializer(),
                parser,
                new StubConfigService());

            // The XML tab shows the CURRENT (edited) values — not the disk original.
            Assert.Equal(EntityXmlHelper.GenerateXmlFragment(current), doc.XmlContent.Text);

            // The diff OLD side is the disk original, so the pair actually differs.
            doc.RefreshDiff();
            Assert.Equal(EntityXmlHelper.GenerateXmlFragment(original), doc.DiffOldDocument!.Text);
            Assert.NotEqual(doc.DiffOldDocument.Text, doc.DiffNewDocument!.Text);
            Assert.Contains("1.1", doc.DiffOldDocument.Text); // original value on the old side
            Assert.Contains("1.2", doc.DiffNewDocument.Text); // current value on the new side
        }
        finally
        {
            if (File.Exists(xmlPath)) File.Delete(xmlPath);
        }
    }

    [Fact]
    public void CleanEntity_DiffPairEmpty_UntilEdited()
    {
        var parser = new FakeXmlParser();
        var current = new AttackMode { EntityId = "c1", ModId = 4, FilePath = "", Id = 1, Name = "X" };
        var session = new StubWorkspaceSession(); // not dirty

        var doc = new EntityEditorDocument(
            current, session, null!,
            new StubEntityLookupService(),
            new StubLocalizationService(),
            new StubNotificationService(),
            new ReferenceListSerializer(),
            parser,
            new StubConfigService());

        doc.RefreshDiff();
        // Clean entity: both sides are the current state → empty diff (old behavior preserved).
        Assert.Equal(doc.DiffOldDocument!.Text, doc.DiffNewDocument!.Text);
    }

    [Fact]
    public void ToggleDiffView_HookRefreshesDiffDocuments()
    {
        // 追修: the ToggleButton binds IsChecked (TwoWay) only — setting IsDiffView must
        // drive RefreshDiff via the generated OnIsDiffViewChanged hook. If the hook does not
        // fire, the XmlDiffView binds null documents and renders BLANK (the reported bug).
        var parser = new FakeXmlParser();
        var current = new AttackMode { EntityId = "c1", ModId = 4, FilePath = "", Id = 1, Name = "X" };
        var doc = new EntityEditorDocument(
            current, new StubWorkspaceSession(), null!,
            new StubEntityLookupService(),
            new StubLocalizationService(),
            new StubNotificationService(),
            new ReferenceListSerializer(),
            parser,
            new StubConfigService());

        Assert.False(doc.IsDiffView);
        Assert.Null(doc.DiffOldDocument); // not initialized before first toggle

        doc.IsDiffView = true; // what the ToggleButton binding does on click

        Assert.NotNull(doc.DiffOldDocument);
        Assert.NotNull(doc.DiffNewDocument);
        Assert.Equal(EntityXmlHelper.GenerateXmlFragment(current), doc.DiffOldDocument!.Text);
        Assert.Equal(EntityXmlHelper.GenerateXmlFragment(current), doc.DiffNewDocument!.Text);
    }

    [Fact]
    public void XmlContent_InitializedWithCurrentFragment()
    {
        var parser = new FakeXmlParser();
        var current = new AttackMode { EntityId = "c1", ModId = 4, FilePath = "", Id = 1, Name = "X" };
        var doc = new EntityEditorDocument(
            current, new StubWorkspaceSession(), null!,
            new StubEntityLookupService(),
            new StubLocalizationService(),
            new StubNotificationService(),
            new ReferenceListSerializer(),
            parser,
            new StubConfigService());

        // The XML tab must never be blank: XmlContent is initialized from the entity.
        Assert.False(string.IsNullOrWhiteSpace(doc.XmlContent.Text));
        Assert.Equal(EntityXmlHelper.GenerateXmlFragment(current), doc.XmlContent.Text);
    }

    /// <summary>
    /// Test double for IXmlParser that imports single-entity pma_xml_export fragments
    /// (&lt;table name="..."&gt;&lt;column name="..."&gt;...). EntityId uses the REAL
    /// Sha256Helper so ids are stable and match across imports (like the production parser).
    /// </summary>
    private sealed class FakeXmlParser : IXmlParser
    {
        public IList<T> ImportEntities<T>(XDocument doc, int modId, string filePath) where T : IEntity, new()
        {
            var tableName = typeof(T).GetCustomAttribute<TableAttribute>()?.Name
                            ?? typeof(T).Name.ToLowerInvariant();
            var keyProp = typeof(T).GetProperties().FirstOrDefault(p =>
                p.GetCustomAttribute<ColumnAttribute>()?.Name is "id" or "nID")
                          ?? typeof(T).GetProperty("Id");

            var result = new List<T>();
            foreach (var tableEl in doc.Descendants("table")
                         .Where(t => (string?)t.Attribute("name") == tableName))
            {
                var entity = new T();
                foreach (var colEl in tableEl.Elements("column"))
                {
                    var name = colEl.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;
                    var prop = typeof(T).GetProperties().FirstOrDefault(p =>
                        p.GetCustomAttribute<ColumnAttribute>()?.Name == name);
                    if (prop is null || !prop.CanWrite) continue;

                    var text = colEl.Value;
                    object? value = text;
                    if (prop.PropertyType == typeof(int)) value = int.Parse(text, CultureInfo.InvariantCulture);
                    else if (prop.PropertyType == typeof(long)) value = long.Parse(text, CultureInfo.InvariantCulture);
                    else if (prop.PropertyType == typeof(double)) value = double.Parse(text, CultureInfo.InvariantCulture);
                    else if (prop.PropertyType == typeof(float)) value = float.Parse(text, CultureInfo.InvariantCulture);
                    else if (prop.PropertyType == typeof(bool)) value = bool.Parse(text);
                    else if (prop.PropertyType.IsEnum) value = Enum.ToObject(prop.PropertyType, int.Parse(text));
                    // ReferenceList columns are skipped — the diff tests do not use them.
                    prop.SetValue(entity, value);
                }

                var keyVal = keyProp?.GetValue(entity)?.ToString() ?? "";
                entity.EntityId = Sha256Helper.CreateEntityId(tableName, modId, keyVal);
                entity.ModId = modId;
                entity.FilePath = filePath;
                result.Add(entity);
            }

            return result;
        }

        public XDocument Export(IEnumerable<IEntity> entities, string databaseName = "neogame")
            => throw new NotSupportedException();
    }
}

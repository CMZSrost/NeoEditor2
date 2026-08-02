using System;
using System.IO;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>
/// R30: reference columns must export as their raw XML text ("16,46"),
/// not the damaged "[16, 46]" ReferenceList.ToString() format.
/// </summary>
public class CsvImportExportServiceTests
{
    [Fact]
    public void ExportEntitiesToCsv_ReferenceColumns_ExportRawText_NotBrokenBrackets()
    {
        var svc = new CsvImportExportService();
        var creature = new Creature { EntityId = "c1" };
        creature.EncounterIds = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var tmp = Path.Combine(Path.GetTempPath(), $"neo_csv_{Guid.NewGuid():N}.csv");
        try
        {
            svc.ExportEntitiesToCsv([creature], typeof(Creature), tmp);
            var text = File.ReadAllText(tmp);

            // The header row + data row must carry the raw "3,14", and never "[3, 14]".
            var dataLine = text.Split('\n').Last(l => l.Contains("3,14"));
            Assert.DoesNotContain("[3, 14]", dataLine);
            Assert.DoesNotContain("[", dataLine);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void ExportEntitiesToCsv_SingleRefValue_ExportsRawId()
    {
        var svc = new CsvImportExportService();
        var itemType = new ItemType { EntityId = "i1", TreasureId = new ReferenceList<IReferenceEntry> { new PureRefFormat { Entity = new EntityRef { Id = "735" } } } };

        var tmp = Path.Combine(Path.GetTempPath(), $"neo_csv_{Guid.NewGuid():N}.csv");
        try
        {
            svc.ExportEntitiesToCsv([itemType], typeof(ItemType), tmp);
            var text = File.ReadAllText(tmp);
            Assert.Contains("735", text);
            Assert.DoesNotContain("[735]", text);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}

using NeoEditor.Data.Command;

namespace NeoEditor.Infra.Tests;

public class CommandTests
{
    [Fact]
    public void EditRecord_ShouldStoreValues()
    {
        var entity = new DummyEntity();
        var prop = typeof(DummyEntity).GetProperty(nameof(DummyEntity.Name))!;
        var record = new EditRecord(entity, prop, "name", "old", "new");

        Assert.Equal("name", record.ColumnName);
        Assert.Equal("old", record.OldValue);
        Assert.Equal("new", record.NewValue);
    }

    [Fact]
    public void EditCellCommand_ShouldBeCreated()
    {
        var entity = new DummyEntity { Name = "old" };
        var prop = typeof(DummyEntity).GetProperty(nameof(DummyEntity.Name))!;
        var cmd = new EditCellCommand(entity, prop, "name", "old", "new", () => { });

        Assert.NotNull(cmd);
    }

    private class DummyEntity : NeoEditor.Data.Model.Game.IEntity
    {
        public string Name { get; set; } = "";
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Core.Tests;

public class EntityHelperTests
{
    [UIDKey(nameof(EntityId), nameof(Id))]
    private class TestEntity : Data.Model.Game.IEntity
    {
        [Column("id")]
        public int Id { get; set; }
    }

    [Fact]
    public void ResolveKeyProperty_WithUIDKeyAttribute_ShouldFindIdProperty()
    {
        var key = EntityHelper.ResolveKeyProperty(typeof(TestEntity));

        Assert.NotNull(key);
        Assert.Equal("Id", key!.Name);
    }

    [Fact]
    public void GetKeyValue_ShouldReturnKeyValue()
    {
        var entity = new TestEntity { Id = 42, EntityId = "test" };

        var value = EntityHelper.GetKeyValue(entity);

        Assert.Equal(42, value);
    }

    [Fact]
    public void ComputeEntityKeyString_ShouldReturnStringRepresentation()
    {
        var entity = new TestEntity { Id = 99, EntityId = "test" };

        var keyString = EntityHelper.ComputeEntityKeyString(entity);

        Assert.Equal("99", keyString);
    }
}

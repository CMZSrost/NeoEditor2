using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Core.Tests.Model;

/// <summary>
/// Asserts the [ReferenceField] annotations on game entity models match Doc 37/38 measured
/// semantics (target type, key shape, pattern, separators). Guards against mis-annotations.
/// </summary>
public class ReferenceFieldAnnotationTests
{
    private static ReferenceFieldAttribute Attr<T>(string prop)
        => typeof(T).GetProperty(prop)!.GetCustomAttribute<ReferenceFieldAttribute>()!;

    [Fact]
    public void Creature_EncounterIds_targets_Encounter()
        => Assert.Equal(typeof(Encounter), Attr<Creature>("EncounterIds").TargetEntityType);

    [Fact]
    public void Encounter_ItemsId_uses_primary_key()
        => Assert.Null(Attr<Encounter>("ItemsId").TargetKey);

    [Fact]
    public void ItemType_SwitchIds_uses_value_equals_id_pattern()
        => Assert.Equal("{value}={id}", Attr<ItemType>("SwitchIds").Pattern);

    [Fact]
    public void TreasureTable_aTreasures_three_segment_or()
    {
        var a = Attr<TreasureTable>("Treasures");
        Assert.Equal("{id}x{mult}x{qty}", a.Pattern);
        Assert.Equal("|", a.OrSeparator);
        Assert.Equal(",", a.Separator);
    }

    [Fact]
    public void Condition_Thresholds_marked_reference()
    {
        var a = Attr<Condition>("Thresholds");
        Assert.Equal(typeof(Condition), a.TargetEntityType);
        Assert.Equal(";", a.Separator);
        Assert.Equal("{value}={id}", a.Pattern);
    }

    [Theory]
    [InlineData(typeof(AttackMode), "Image")]
    [InlineData(typeof(Creature), "Image")]
    [InlineData(typeof(Encounter), "Image")]
    [InlineData(typeof(DataFile), "Image")]
    [InlineData(typeof(DmcPlace), "Image")]
    [InlineData(typeof(CampType), "ImageList")]
    [InlineData(typeof(ItemType), "ImageList")]
    [InlineData(typeof(ItemType), "SpriteList")]
    public void Image_columns_target_ImageAsset(Type type, string prop)
    {
        var propInfo = type.GetProperty(prop)!;
        var a = propInfo.GetCustomAttribute<ReferenceFieldAttribute>()!;
        Assert.Equal(typeof(ImageAsset), a.TargetEntityType);
        Assert.Equal(typeof(ReferenceList<IReferenceEntry>), propInfo.PropertyType);
    }
}

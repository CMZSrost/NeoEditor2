using System;
using System.Collections.Generic;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Model;

/// <summary>
/// Defines semantic field groupings for each entity type.
/// Used by the Key-Value editor to group fields into collapsible sections.
/// </summary>
public static class FieldGroupMetadata
{
    private static readonly Dictionary<Type, List<(string Section, string[] Fields)>> Groups = new()
    {
        [typeof(ItemType)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strDesc", "strDescAlt", "fWeight", "fMonetaryValue", "fMonetaryValueAlt", "nStackLimit" }),
            ("图片与外观", new[] { "vImageList", "vSpriteList", "vImageUsage", "bMirrored" }),
            ("耐久与消耗", new[] { "fDurability", "fDegradePerHour", "fEquipDegradePerHour", "fDegradePerUse", "vDegradeTreasureIDs" }),
            ("装备与容量", new[] { "vEquipSlots", "vUseSlots", "aCapacities", "nSlotDepth", "bSocketLocked" }),
            ("引用关联", new[] { "nCondID", "aEquipConditions", "aPossessConditions", "aUseConditions", "vProperties", "aContentIDs", "nFormatID", "nTreasureID", "nComponentID", "strChargeProfiles", "aAttackModes", "aSwitchIDs" }),
            ("音效", new[] { "aSounds" }),
        },
        [typeof(Recipe)] = new()
        {
            ("基本属性", new[] { "nID", "strName", "nHours", "bConsumed", "nTreasureID" }),
            ("工具与配方", new[] { "strTools", "strAlsoTry", "nParentRecipeID" }),
            ("材料", new[] { "aIngredients" }),
        },
        [typeof(AttackMode)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strId", "strVerb", "nAttackType", "bRanged", "nRange", "nNoise" }),
            ("伤害属性", new[] { "fBluntDmg", "fCutDmg", "fPenetration", "fDmgBonus", "nMoraleMod" }),
            ("弹药消耗", new[] { "strAmmo", "fPerUse", "fPerHour", "fPerHourEquipped", "fPerHex", "bDegrade" }),
            ("状态关联", new[] { "aAttackerConditions", "aTargetConditions" }),
        },
        [typeof(Creature)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strId", "nBodyType", "nSpecies" }),
            ("属性", new[] { "nHP", "nMoveCost", "nVisibility", "nStrength", "nToughness", "nAgility", "nPerception", "nMorale" }),
            ("战斗", new[] { "aAttackModes", "nFaction" }),
            ("战利品", new[] { "nTreasureID", "vCorpseID" }),
        },
        [typeof(Condition)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strId", "nSeverity", "bInstant", "nDuration", "bResetTimer", "bTransfer", "nColor" }),
            ("字段修饰", new[] { "aFieldNames", "aModifiers" }),
            ("条件链", new[] { "vIDNext", "fChanceNext", "nThresholds" }),
        },
        [typeof(Encounter)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strId", "nType", "strNoun" }),
            ("剧情文本", new[] { "strDesc", "aPhrases" }),
            ("回应选项", new[] { "aResponses" }),
        },
        [typeof(BattleMove)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strId", "nType", "bExpose" }),
            ("条件", new[] { "aUsPreConditions", "aThemPreConditions", "aUsPostConditions", "aThemPostConditions" }),
        },
    };

    /// <summary>
    /// Returns the section name for a given entity type and column property name.
    /// If the type is not explicitly mapped, all fields go into a default section.
    /// </summary>
    public static string GetSection(Type entityType, string propertyName)
    {
        if (Groups.TryGetValue(entityType, out var sections))
        {
            foreach (var (section, fields) in sections)
            {
                if (fields.Contains(propertyName))
                    return section;
            }
        }
        return "属性";
    }
}

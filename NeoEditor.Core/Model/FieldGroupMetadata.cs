using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Model;

/// <summary>
/// Defines semantic field groupings for each entity type.
/// Used by the Key-Value editor to group fields into collapsible sections.
/// R48: rewritten against the REAL model columns — the previous groups used
/// stale/imaginary column names (e.g. Creature "nHP/nStrength", Recipe "nHours")
/// that never existed in the game data, which silently dumped every field into
/// the default "属性" section.
/// </summary>
public static class FieldGroupMetadata
{
    private static readonly Dictionary<Type, List<(string Section, string[] Fields)>> Groups = new()
    {
        [typeof(ItemType)] = new()
        {
            ("基本属性", new[] { "id", "nGroupID", "nSubgroupID", "strName", "strDesc", "strDescAlt", "fWeight", "fMonetaryValue", "fMonetaryValueAlt", "nStackLimit" }),
            ("图片与外观", new[] { "vImageList", "vSpriteList", "vImageUsage", "bMirrored" }),
            ("耐久与消耗", new[] { "fDurability", "fDegradePerHour", "fEquipDegradePerHour", "fDegradePerUse", "vDegradeTreasureIDs" }),
            ("装备与容量", new[] { "vEquipSlots", "vUseSlots", "aCapacities", "nSlotDepth", "bSocketLocked" }),
            ("引用关联", new[] { "nCondID", "aEquipConditions", "aPossessConditions", "aUseConditions", "vProperties", "aContentIDs", "nFormatID", "nTreasureID", "nComponentID", "strChargeProfiles", "aAttackModes", "aSwitchIDs" }),
            ("音效", new[] { "aSounds" }),
        },
        [typeof(Recipe)] = new()
        {
            ("基本属性", new[] { "nID", "strName", "strSecretName", "fHours", "strType", "bScrap" }),
            ("工具与材料", new[] { "strTools", "strConsumed", "strDestroyed" }),
            ("产物与拆解", new[] { "nTreasureID", "nTempTreasureID", "nReverse", "bDegradeOutput", "bTransferComponents", "bIdentify" }),
            ("配方关联", new[] { "nHiddenID", "vAlsoTry" }),
        },
        [typeof(AttackMode)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strNotes", "nType", "nRange", "nPenetration", "strIMG" }),
            ("伤害属性", new[] { "fDamageCut", "fDamageBlunt", "fMorale" }),
            ("弹药与音效", new[] { "strChargeProfiles", "strSnd", "bTransfer" }),
            ("状态关联", new[] { "vAttackerConditions", "strWieldPhrase", "vAttackPhrases" }),
        },
        [typeof(Creature)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strNamePublic", "strNotes", "strImg", "nMovesPerTurn" }),
            ("战斗", new[] { "vAttackModes", "nFaction" }),
            ("出场状态", new[] { "vBaseConditions", "vActivities" }),
            ("战利品", new[] { "nTreasureID", "nCorpseID" }),
            ("遭遇", new[] { "vEncounterIDs" }),
        },
        [typeof(Condition)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strDesc", "nColor", "bDisplay", "bDisplayOther", "bDisplayGameOver" }),
            ("效果与修饰", new[] { "aFieldNames", "aModifiers", "aEffects" }),
            ("属性标志", new[] { "bFatal", "bPermanent", "bStackable", "bResetTimer", "bRemoveAll", "bRemovePostCombat" }),
            ("持续时间", new[] { "fDuration", "nTransferRange" }),
            ("条件链", new[] { "vIDNext", "vChanceNext", "aThresholds" }),
        },
        [typeof(Encounter)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strDesc", "strImg", "nType", "fPrice" }),
            ("条件与前置", new[] { "aConditions", "aPreConditions" }),
            ("搜刮概率", new[] { "fLootChance", "fAccidentChance", "fCreatureChance", "vAccidents", "vLoot" }),
            ("产出与移除", new[] { "nTreasureID", "nRemoveTreasureID", "nItemsID", "bRemoveCreatures", "bRemoveUsed" }),
            ("剧情行为", new[] { "aResponses", "aMinimapHexes", "nCreatureID", "ptCreatureHex", "ptTeleport", "ptEditor" }),
        },
        [typeof(BattleMove)] = new()
        {
            ("基本属性", new[] { "id", "strID", "strName", "strNotes", "nAttackModeType", "vHexTypes" }),
            ("条件", new[] { "vUsPreConditions", "vThemPreConditions", "vUsConditions", "vThemConditions", "vPairConditions", "vUsFailConditions", "vThemFailConditions", "vPairFailConditions" }),
            ("数值与AI", new[] { "fChance", "fPriority", "fDetect", "fOrder", "fFatigue", "nSeeThem", "nSeeUs", "bAllOutOfRange", "bInAttackRange", "nMinCharges", "nMinRange", "nMaxRange", "vChanceType" }),
            ("行为标志", new[] { "bApproach", "bOffense", "bFallBack", "bRetreat", "bPosition", "bPassive" }),
            ("文本", new[] { "strSuccess", "strFail", "strPopUp" }),
        },
        [typeof(EncounterTrigger)] = new()
        {
            ("基本属性", new[] { "id", "strName", "nEncounterID", "fChance" }),
            ("触发方式", new[] { "bLocBased", "bDateBased", "bHexBased", "bUnique", "bAIPassable", "aArea", "dateMin", "dateMax", "aHexTypes" }),
        },
        [typeof(ContainerType)] = new()
        {
            ("基本属性", new[] { "id", "strName" }),
        },
        [typeof(ChargeProfile)] = new()
        {
            ("基本属性", new[] { "nID", "strName", "strItemID", "bDegrade" }),
            ("消耗速率", new[] { "fPerUse", "fPerHour", "fPerHourEquipped", "fPerHex" }),
        },
        [typeof(CreatureSource)] = new()
        {
            ("基本属性", new[] { "id", "strName", "nCreatureID" }),
            ("刷新配置", new[] { "nX", "nY", "nMin", "nMax", "fWeight" }),
        },
        [typeof(BarterHex)] = new()
        {
            ("基本属性", new[] { "id", "nX", "nY", "bBuys", "nRestockTreasureID" }),
        },
        [typeof(CampType)] = new()
        {
            ("基本属性", new[] { "id", "strDesc", "vImageList", "aCapacities", "nTreasureID" }),
            ("环境属性", new[] { "m_fAlertness", "m_fVisibility", "WetTempAdjustMod", "m_fHealPerHourMod", "fSleepQuality" }),
        },
        [typeof(DataFile)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strDesc", "fValue", "strImg" }),
        },
        [typeof(DmcPlace)] = new()
        {
            ("基本属性", new[] { "id", "strImg", "nEncounterID", "nX", "nY" }),
        },
        [typeof(Faction)] = new()
        {
            ("基本属性", new[] { "id", "strName" }),
            ("外交关系", new[] { "dictFactions" }),
        },
        [typeof(ForbiddenHex)] = new()
        {
            ("基本属性", new[] { "id", "nX", "nY", "strName" }),
        },
        [typeof(GameVar)] = new()
        {
            ("基本属性", new[] { "strName", "strType", "strValue" }),
        },
        [typeof(Headline)] = new()
        {
            ("基本属性", new[] { "id", "strHeadline" }),
        },
        [typeof(HexType)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strDesc", "nTerrainCost", "bPassable" }),
            ("视野", new[] { "nVizLimiter", "nVizIncrease" }),
            ("搜刮", new[] { "nTreasureID", "nScavengeInitialID", "nScavengeItemsIDPerHour", "nCampItems" }),
            ("环境", new[] { "vLightLevels", "nDefaultCampID", "nMinRange", "nMaxRange", "vCondIDs" }),
        },
        [typeof(Ingredient)] = new()
        {
            ("基本属性", new[] { "nID", "strName" }),
            ("属性要求", new[] { "strRequiredProps", "strForbidProps" }),
        },
        [typeof(ItemProp)] = new()
        {
            ("基本属性", new[] { "nID", "strPropertyName" }),
        },
        [typeof(Map)] = new()
        {
            ("基本属性", new[] { "id", "strName", "strDef" }),
        },
        [typeof(TreasureTable)] = new()
        {
            ("基本属性", new[] { "id", "strName" }),
            ("掉落配置", new[] { "aTreasures", "bNested", "bSuppress", "bIdentify" }),
        },
    };

    /// <summary>
    /// Returns the ordered section names for an entity type (authoring order), or a
    /// single default section for unmapped types. Used by the raw-data audit view
    /// to render fields grouped exactly like the Key-Value editor.
    /// </summary>
    public static IReadOnlyList<string> GetSections(Type entityType)
    {
        return Groups.TryGetValue(entityType, out var sections)
            ? sections.Select(s => s.Section).ToArray()
            : new[] { "属性" };
    }

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

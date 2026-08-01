using System;
using System.Collections.Generic;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

public record EntityTypeGroup(string TypeName, Type EntityType, int EntityCount);

public static class GameDomain
{
    public static readonly Dictionary<string, Type[]> Domains = new()
    {
        ["DomainCoreItems"] = new[] { typeof(ItemType), typeof(ItemProp), typeof(ContainerType), typeof(ChargeProfile) },
        ["DomainCombat"] = new[] { typeof(AttackMode), typeof(BattleMove), typeof(Creature), typeof(Faction), typeof(Condition) },
        ["DomainCrafting"] = new[] { typeof(Recipe), typeof(Ingredient) },
        ["DomainLoot"] = new[] { typeof(TreasureTable) },
        ["DomainStory"] = new[] { typeof(Encounter), typeof(EncounterTrigger) },
        ["DomainMap"] = new[] { typeof(Map), typeof(HexType), typeof(ForbiddenHex), typeof(BarterHex), typeof(DmcPlace), typeof(CreatureSource) },
        ["DomainOther"] = new[] { typeof(GameVar), typeof(Headline), typeof(DataFile), typeof(CampType) },
    };
}

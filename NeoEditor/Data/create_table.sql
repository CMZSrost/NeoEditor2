drop table if exists main.attackmodes;
create table main.attackmodes
(
    idx                 integer primary key,
    modName             varchar(255) not null default '',
    modIndex            integer      not null,
    serialId_           integer      not null,
    overId_             integer,

    id                  integer,
    strName             varchar(255) not null default '',
    strNotes            varchar(255) not null default '',
    nRange              integer      not null default 1,
    fDamageCut          float        not null default 0,
    fDamageBlunt        float        not null default 0,
    strChargeProfiles   varchar(24)  not null default '',
    nPenetration        integer      not null default 0,
    nType               integer      not null default 0,
    strSnd              varchar(30)  not null default '',
    bTransfer           tinyint(1)   not null default 0,
    vAttackerConditions varchar(255) not null default '',
    strIMG              varchar(50)  not null default '',
    fMorale             float        not null default 0.25,
    strWieldPhrase      text         not null default '',
    vAttackPhrases      text         not null default ''
);

drop table if exists main.barterhexes;
create table main.barterhexes
(
    idx                integer primary key,
    modName            varchar(255) not null default '',
    modIndex           integer      not null,
    serialId_          integer      not null,
    overId_            integer,

    id                 integer,
    nX                 integer      not null default 0,
    nY                 integer      not null default 0,
    bBuys              tinyint(1)   not null default 0,
    nRestockTreasureID integer      not null default 3
);

create index main_barterhexes_nX_nY_index
    on barterhexes (nX, nY);
create index main_barterhexes_nRestockTreasureID_index
    on barterhexes (nRestockTreasureID);

drop table if exists main.battlemoves;
create table main.battlemoves
(
    idx                 integer primary key,
    modName             varchar(255) not null default '',
    modIndex            integer      not null,
    serialId_           integer      not null,
    overId_             integer,

    id                  integer,
    strID               varchar(255) not null default '',
    strName             varchar(255) not null default '',
    strNotes            varchar(255) not null default '',
    strSuccess          varchar(255) not null default '',
    strFail             varchar(255)          default null,
    strPopUp            text,
    vChanceType         varchar(255) not null default '0,0,0',
    vUsConditions       varchar(255)          default null,
    vThemConditions     varchar(255)          default null,
    vPairConditions     varchar(255)          default null,
    vUsFailConditions   varchar(255)          default null,
    vThemFailConditions varchar(255)          default null,
    vPairFailConditions varchar(255)          default null,
    vUsPreConditions    varchar(255)          default null,
    vThemPreConditions  varchar(255)          default null,
    nSeeThem            integer               default 2,
    nSeeUs              integer               default 2,
    bAllOutOfRange      tinyint(1)            default 0,
    bInAttackRange      tinyint(1)            default 0,
    nMinCharges         integer               default 0,
    nMinRange           integer               default -1,
    nMaxRange           integer               default -1,
    nAttackModeType     integer               default -1,
    vHexTypes           varchar(255) not null default '',
    fChance             float                 default 1,
    fPriority           float                 default 0,
    fDetect             float                 default 1,
    fOrder              float                 default 0.5,
    fFatigue            float                 default 0,
    bApproach           tinyint(1)            default 0,
    bOffense            tinyint(1)            default 0,
    bFallBack           tinyint(1)            default 0,
    bRetreat            tinyint(1)            default 0,
    bPosition           tinyint(1)            default 0,
    bPassive            tinyint(1)   not null default 0
);

create index main_battlemoves_strID_index
    on battlemoves (strID);
drop table if exists main.camptypes;
create table main.camptypes
(
    idx               integer primary key,
    modName           varchar(255) not null default '',
    modIndex          integer      not null,
    serialId_         integer      not null,
    overId_           integer,

    id                integer,
    strDesc           varchar(255) not null default '',
    vImageList        varchar(255) not null default '',
    aCapacities       varchar(255) not null default '',
    nTreasureID       integer      not null default 3,
    m_fAlertness      float        not null default 0,
    m_fVisibility     float        not null default -0.05,
    WetTempAdjustMod  float        not null default 0,
    m_fHealPerHourMod float        not null default 0,
    fSleepQuality     float        not null default 0
);
create index main_camptypes_nTreasureID_index
    on camptypes (nTreasureID);
drop table if exists main.chargeprofiles;
create table main.chargeprofiles
(
    idx              integer primary key,
    modName          varchar(255) not null default '',
    modIndex         integer      not null,
    serialId_        integer      not null,
    overId_          integer,

    nID              integer,
    strName          varchar(255) not null default '',
    strItemID        varchar(12)  not null default '',
    fPerUse          float        not null default 0,
    fPerHour         float        not null default 0,
    fPerHourEquipped float        not null default 0,
    fPerHex          float        not null default 0,
    bDegrade         tinyint(1)   not null default 0
);

create index main_chargeprofiles_strItemID_index
    on chargeprofiles (strItemID);
drop table if exists main.conditions;
create table main.conditions
(
    idx               integer primary key,
    modName           varchar(255) not null default '',
    modIndex          integer      not null,
    serialId_         integer      not null,
    overId_           integer,

    id                integer,
    strName           varchar(255) not null default '',
    strDesc           text         not null default '',
    aFieldNames       varchar(255) not null default '',
    aModifiers        varchar(100) not null default '',
    aEffects          text         not null default '',
    bFatal            tinyint(1)   not null default 0,
    vIDNext           varchar(255) not null default '0',
    fDuration         float        not null default 0,
    bPermanent        tinyint(1)   not null default 0,
    vChanceNext       varchar(255) not null default '0',
    bStackable        tinyint(1)   not null default 0,
    bDisplay          tinyint(1)   not null default 1,
    bDisplayOther     tinyint(1)   not null default 0,
    bDisplayGameOver  tinyint(1)   not null default 1,
    nColor            integer      not null default 0,
    bResetTimer       tinyint(1)   not null default 1,
    bRemoveAll        tinyint(1)   not null default 0,
    bRemovePostCombat tinyint(1)   not null default 0,
    nTransferRange    integer      not null default -1,
    aThresholds       varchar(255) not null default ''
);

drop table if exists main.containertypes;
create table main.containertypes
(

    idx       integer primary key,
    modName   varchar(255) not null default '',
    modIndex  integer      not null,
    serialId_ integer      not null,
    overId_   integer,

    id        integer,
    strName   varchar(255) not null default ''
);

drop table if exists main.creatures;
create table main.creatures
(
    idx             integer primary key,
    modName         varchar(255) not null default '',
    modIndex        integer      not null,
    serialId_       integer      not null,
    overId_         integer,

    id              integer,
    strName         varchar(255) not null default '',
    strNamePublic   varchar(255) not null default '',
    strNotes        varchar(255) not null default '',
    strImg          varchar(255) not null default '',
    vEncounterIDs   varchar(255) not null default '',
    nMovesPerTurn   integer      not null,
    nTreasureID     integer      not null default 3,
    nFaction        integer      not null default 0,
    vAttackModes    varchar(25)  not null default '',
    vBaseConditions text         not null default '',
    nCorpseID       integer      not null default 3,
    vActivities     text         not null default ''
);
drop table if exists main.creatures_dg_tmp;
create index main_creatures_nTreasureID_index
    on creatures (nTreasureID);
drop table if exists main.creatures_nTreasureID_index;
create index main_creatures_nCorpseID_index
    on creatures (nCorpseID);
drop table if exists main.creaturesources;
create table main.creaturesources
(
    idx         integer primary key,
    modName     varchar(255) not null default '',
    modIndex    integer      not null,
    serialId_   integer      not null,
    overId_     integer,

    id          integer,
    strName     varchar(255) not null default '',
    nX          integer      not null default -1,
    nY          integer      not null default -1,
    nCreatureID integer      not null default 0,
    nMin        integer      not null default 0,
    nMax        integer      not null default 0,
    fWeight     float        not null default 1
);


create index main_creaturesources_nCreatureID_index
    on creaturesources (nCreatureID);

create index main_creaturesources_nX_nY_index
    on creaturesources (nX, nY);
drop table if exists main.datafiles;
create table main.datafiles
(
    idx       integer primary key,
    modName   varchar(255) not null default '',
    modIndex  integer      not null,
    serialId_ integer      not null,
    overId_   integer,

    id        integer,
    strName   varchar(255) not null default '',
    strDesc   text         not null default '',
    fValue    float        not null default 0,
    strImg    varchar(255) not null default ''
);

drop table if exists main.dmcplaces;
create table main.dmcplaces
(
    idx          integer primary key,
    modName      varchar(255) not null default '',
    modIndex     integer      not null,
    serialId_    integer      not null,
    overId_      integer,

    id           integer,
    strImg       varchar(255) not null default '',
    nEncounterID integer      not null default 1,
    nX           integer      not null default 0,
    nY           integer      not null default 0
);

create index main_dmcplaces_nEncounterID_index
    on dmcplaces (nEncounterID);

create index main_dmcplaces_nX_nY_index
    on dmcplaces (nX, nY);
drop table if exists main.encounters;
create table main.encounters
(
    idx               integer primary key,
    modName           varchar(255) not null default '',
    modIndex          integer      not null,
    serialId_         integer      not null,
    overId_           integer,

    id                integer,
    strName           varchar(255) not null default '',
    strDesc           text         not null default '',
    strImg            varchar(255) not null default 'EncBlank.png',
    nTreasureID       integer      not null default 3,
    nRemoveTreasureID integer      not null default 3,
    aConditions       varchar(255) not null default '1',
    aPreConditions    varchar(255) not null default '',
    fPrice            float        not null default 0,
    aResponses        text         not null default '',
    aMinimapHexes     varchar(255) not null default '',
    bRemoveCreatures  tinyint(1)   not null default 0,
    bRemoveUsed       tinyint(1)   not null default 0,
    nItemsID          integer      not null default 3,
    nCreatureID       integer      not null default 0,
    ptCreatureHex     varchar(9)   not null default '0,0',
    ptTeleport        varchar(9)   not null default '0,0',
    ptEditor          varchar(24)  not null default '0,0',
    nType             integer      not null default 0,
    fLootChance       float        not null default 0,
    fAccidentChance   float        not null default 0,
    fCreatureChance   float        not null default 0,
    vAccidents        varchar(255) not null default '1',
    vLoot             varchar(255) not null default '3'
);


create index main_encounters_nTreasureID_index
    on encounters (nTreasureID);

create index main_encounters_nRemoveTreasureID_index
    on encounters (nRemoveTreasureID);

create index main_encounters_nItemsID_index
    on encounters (nItemsID);

create index main_encounters_nCreatureID_index
    on encounters (nCreatureID);

drop table if exists main.encountertriggers;
create table main.encountertriggers
(
    idx          integer primary key,
    modName      varchar(255) not null default '',
    modIndex     integer      not null,
    serialId_    integer      not null,
    overId_      integer,

    id           integer,
    strName      varchar(255) not null default '',
    nEncounterID integer      not null,
    fChance      float        not null,
    bLocBased    tinyint(1)   not null,
    bDateBased   tinyint(1)   not null,
    bHexBased    tinyint(1)   not null,
    bUnique      tinyint(1)   not null,
    bAIPassable  tinyint(1)   not null default '1',
    aArea        varchar(25)  not null default '',
    dateMin      varchar(15)  not null default '',
    dateMax      varchar(15)  not null default '',
    aHexTypes    text         not null default ''
);

create index main_encountertriggers_nEncounterID_index
    on encountertriggers (nEncounterID);
drop table if exists main.factions;
create table main.factions
(
    idx          integer primary key,
    modName      varchar(255) not null default '',
    modIndex     integer      not null,
    serialId_    integer      not null,
    overId_      integer,

    id           integer,
    strName      varchar(255) not null default '',
    dictFactions text         not null default ''
);
drop table if exists main.forbiddenhexes;
create table main.forbiddenhexes
(
    idx       integer primary key,
    modName   varchar(255) not null default '',
    modIndex  integer      not null,
    serialId_ integer      not null,
    overId_   integer,

    id        integer,
    nX        integer      not null,
    nY        integer      not null,
    strName   varchar(255) not null default ''
);

create index main_forbiddenhexes_nX_nY_index
    on forbiddenhexes (nX, nY);
drop table if exists main.gamevars;
create table main.gamevars
(
    idx       integer primary key,
    modName   varchar(255) not null default '',
    modIndex  integer      not null,
    serialId_ integer      not null,
    overId_   integer,

    strName   varchar(255) not null default '',
    strType   varchar(255) not null default '',
    strValue  varchar(255) not null default ''
);
drop table if exists main.headlines;
create table main.headlines
(
    idx         integer primary key,
    modName     varchar(255) not null default '',
    modIndex    integer      not null,
    serialId_   integer      not null,
    overId_     integer,

    id          integer,
    strHeadline text         not null default ''
);
drop table if exists main.hextypes;
create table main.hextypes
(
    idx                     integer primary key,
    modName                 varchar(255) not null default '',
    modIndex                integer      not null,
    serialId_               integer      not null,
    overId_                 integer,

    id                      integer,
    strName                 varchar(255) not null default '',
    strDesc                 text         not null default '',
    nTerrainCost            integer      not null,
    nVizLimiter             integer      not null,
    nVizIncrease            integer      not null,
    nTreasureID             integer      not null,
    bPassable               tinyint(1)   not null,
    nScavengeInitialID      integer      not null default 3,
    nScavengeItemsIDPerHour integer      not null default 25,
    nCampItems              integer      not null default 5,
    vLightLevels            varchar(255) not null default '0.57,1.0,0.57,0.15',
    nDefaultCampID          integer      not null default 517,
    nMinRange               integer      not null default 3,
    nMaxRange               integer      not null default 6,
    vCondIDs                varchar(255) not null default ''
);

create index main_hextypes_nTreasureID_index
    on hextypes (nTreasureID);

create index main_hextypes_nScavengeInitialID_index
    on hextypes (nScavengeInitialID);

create index main_hextypes_nScavengeItemsIDPerHour_index
    on hextypes (nScavengeItemsIDPerHour);

create index main_hextypes_nCampItems_index
    on hextypes (nCampItems);
drop table if exists main.ingredients;
create table main.ingredients
(
    idx               integer primary key,
    modName           varchar(255) not null default '',
    modIndex          integer      not null,
    serialId_         integer      not null,
    overId_           integer,

    nID               integer,
    strName           varchar(255) not null default '0',
    strRequiredProps  varchar(255) not null default '0',
    strForbiddenProps varchar(255) not null default ''
);
drop table if exists main.itemprops;
create table main.itemprops
(
    idx             integer primary key,
    modName         varchar(255) not null default '',
    modIndex        integer      not null,
    serialId_       integer      not null,
    overId_         integer,

    nID             integer,
    strPropertyName varchar(255) not null default ''
);
drop table if exists main.itemtypes;
create table main.itemtypes
(
    idx                  integer primary key,
    modName              varchar(255) not null default '',
    modIndex             integer      not null,
    serialId_            integer      not null,
    overId_              integer,

    id                   integer,
    nGroupID             integer      not null, -- 索引
    nSubgroupID          integer      not null,
    strName              varchar(255) not null default '',
    strDesc              varchar(255) not null default '',
    strDescAlt           varchar(255) not null default '',
    nCondID              integer      not null default 1,
    vImageList           text         not null default '',
    vSpriteList          varchar(255) not null default '',
    vImageUsage          varchar(25)  not null default '',
    fWeight              float        not null default 0,
    fMonetaryValue       float        not null default 0,
    fMonetaryValueAlt    float        not null default 0,
    fDurability          float        not null default 1,
    fDegradePerHour      float        not null default 0,
    fEquipDegradePerHour float        not null default 0,
    fDegradePerUse       float        not null default 0,
    vDegradeTreasureIDs  varchar(255) not null default '3,3',
    aEquipConditions     text         not null default '',
    aPossessConditions   text         not null default '',
    aUseConditions       text         not null default '',
    aCapacities          varchar(255) not null default '',
    vEquipSlots          varchar(255) not null default '',
    vUseSlots            varchar(255) not null default '',
    bSocketLocked        tinyint(1)   not null default 0,
    vProperties          varchar(255) not null default '',
    aContentIDs          varchar(255) not null default '',
    nFormatID            integer      not null default '3',
    nTreasureID          integer      not null default '3',
    nComponentID         integer      not null default '3',
    bMirrored            tinyint(1)   not null default '0',
    nSlotDepth           integer      not null default '0',
    strChargeProfiles    varchar(255) not null default '',
    aAttackModes         varchar(255) not null default '',
    nStackLimit          integer      not null default '1',
    aSwitchIDs           varchar(255) not null default '',
    aSounds              varchar(255) not null default 'cuePickup,cuePutdown'
);

create index main_itemtypes_nGroupID_index
    on itemtypes (nGroupID);

create index main_itemtypes_nSubgroupID_index
    on itemtypes (nSubgroupID);

create index main_itemtypes_nGroupID_nSubgroupID_index
    on itemtypes (nGroupID, nSubgroupID);

create index main_itemtypes_nCondID_index
    on itemtypes (nCondID);

create index main_itemtypes_nTreasureID_index
    on itemtypes (nTreasureID);

create index main_itemtypes_nComponentID_index
    on itemtypes (nComponentID);

drop table if exists main.maps;
create table main.maps
(
    idx       integer primary key,
    modName   varchar(255) not null default '',
    modIndex  integer      not null,
    serialId_ integer      not null,
    overId_   integer,

    id        integer,
    strName   varchar(255) not null default '',
    strDef    text         not null default ''
);
drop table if exists main.recipes;
create table main.recipes
(
    idx                 integer primary key,
    modName             varchar(255) not null default '',
    modIndex            integer      not null,
    serialId_           integer      not null,
    overId_             integer,

    nID                 integer,
    strName             varchar(255) not null default '',
    strSecretName       varchar(255) not null default '',
    strTools            varchar(255) not null default '',
    strConsumed         varchar(255) not null default '',
    strDestroyed        varchar(255) not null default '',
    nTreasureID         integer      not null default 3,
    fHours              float        not null default 0,
    nReverse            integer      not null default 0,
    nHiddenID           integer      not null default 0,
    bIdentify           tinyint(1)   not null default 0,
    bTransferComponents tinyint(1)   not null default 0,
    vAlsoTry            varchar(255) not null default '',
    nTempTreasureID     integer      not null default 3,
    bDegradeOutput      tinyint(1)   not null default 1,
    strType             varchar(255) not null default '',
    bScrap              tinyint(1)   not null default 1
);

create index main_recipes_nTreasureID_index
    on recipes (nTreasureID);

create index main_recipes_nTempTreasureID_index
    on recipes (nTempTreasureID);
drop table if exists main.treasuretable;
create table main.treasuretable
(
    idx        integer primary key,
    modName    varchar(255) not null default '',
    modIndex   integer      not null,
    serialId_  integer      not null,
    overId_    integer,

    id         integer,
    strName    varchar(255) not null default '',
    aTreasures text         not null default '',
    bNested    tinyint(1)   not null default 0,
    bSuppress  tinyint(1)   not null default 0,
    bIdentify  tinyint(1)   not null default 0
);
drop table if exists main.images;
create table main.images
(
    idx       integer primary key,
    modName   varchar(255) not null default '',
    modIndex  integer      not null,
    serialId_ integer      not null,
    overId_   integer,

    id        integer,
    imagePath text         not null default ''
);
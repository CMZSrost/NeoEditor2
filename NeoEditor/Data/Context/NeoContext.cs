using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Models;

namespace NeoEditor.Data.Context;

public partial class NeoContext : DbContext
{
    public NeoContext(DbContextOptions<NeoContext> options)
        : base(options)
    {
    }

    public virtual DbSet<attackmode> attackmodes { get; set; }

    public virtual DbSet<barterhex> barterhexes { get; set; }

    public virtual DbSet<battlemove> battlemoves { get; set; }

    public virtual DbSet<camptype> camptypes { get; set; }

    public virtual DbSet<chargeprofile> chargeprofiles { get; set; }

    public virtual DbSet<condition> conditions { get; set; }

    public virtual DbSet<containertype> containertypes { get; set; }

    public virtual DbSet<creature> creatures { get; set; }

    public virtual DbSet<creaturesource> creaturesources { get; set; }

    public virtual DbSet<datafile> datafiles { get; set; }

    public virtual DbSet<dmcplace> dmcplaces { get; set; }

    public virtual DbSet<encounter> encounters { get; set; }

    public virtual DbSet<encountertrigger> encountertriggers { get; set; }

    public virtual DbSet<faction> factions { get; set; }

    public virtual DbSet<forbiddenhex> forbiddenhexes { get; set; }

    public virtual DbSet<gamevar> gamevars { get; set; }

    public virtual DbSet<headline> headlines { get; set; }

    public virtual DbSet<hextype> hextypes { get; set; }

    public virtual DbSet<image> images { get; set; }

    public virtual DbSet<ingredient> ingredients { get; set; }

    public virtual DbSet<itemprop> itemprops { get; set; }

    public virtual DbSet<itemtype> itemtypes { get; set; }

    public virtual DbSet<map> maps { get; set; }

    public virtual DbSet<recipe> recipes { get; set; }

    public virtual DbSet<treasuretable> treasuretables { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<attackmode>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.fMorale).HasDefaultValue(0.25);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nRange).HasDefaultValue(1);
            entity.Property(e => e.strChargeProfiles).HasDefaultValue("");
            entity.Property(e => e.strIMG).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.strNotes).HasDefaultValue("");
            entity.Property(e => e.strSnd).HasDefaultValue("");
            entity.Property(e => e.strWieldPhrase).HasDefaultValue("");
            entity.Property(e => e.vAttackPhrases).HasDefaultValue("");
            entity.Property(e => e.vAttackerConditions).HasDefaultValue("");
        });

        modelBuilder.Entity<barterhex>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nRestockTreasureID).HasDefaultValue(3);
        });

        modelBuilder.Entity<battlemove>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.bAllOutOfRange).HasDefaultValue((byte)0);
            entity.Property(e => e.bApproach).HasDefaultValue((byte)0);
            entity.Property(e => e.bFallBack).HasDefaultValue((byte)0);
            entity.Property(e => e.bInAttackRange).HasDefaultValue((byte)0);
            entity.Property(e => e.bOffense).HasDefaultValue((byte)0);
            entity.Property(e => e.bPosition).HasDefaultValue((byte)0);
            entity.Property(e => e.bRetreat).HasDefaultValue((byte)0);
            entity.Property(e => e.fChance).HasDefaultValue(1.0);
            entity.Property(e => e.fDetect).HasDefaultValue(1.0);
            entity.Property(e => e.fFatigue).HasDefaultValue(0.0);
            entity.Property(e => e.fOrder).HasDefaultValue(0.5);
            entity.Property(e => e.fPriority).HasDefaultValue(0.0);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nAttackModeType).HasDefaultValue(-1);
            entity.Property(e => e.nMaxRange).HasDefaultValue(-1);
            entity.Property(e => e.nMinCharges).HasDefaultValue(0);
            entity.Property(e => e.nMinRange).HasDefaultValue(-1);
            entity.Property(e => e.nSeeThem).HasDefaultValue(2);
            entity.Property(e => e.nSeeUs).HasDefaultValue(2);
            entity.Property(e => e.strFail).HasDefaultValueSql("null");
            entity.Property(e => e.strID).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.strNotes).HasDefaultValue("");
            entity.Property(e => e.strSuccess).HasDefaultValue("");
            entity.Property(e => e.vChanceType).HasDefaultValue("0,0,0");
            entity.Property(e => e.vHexTypes).HasDefaultValue("");
            entity.Property(e => e.vPairConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vPairFailConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vThemConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vThemFailConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vThemPreConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vUsConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vUsFailConditions).HasDefaultValueSql("null");
            entity.Property(e => e.vUsPreConditions).HasDefaultValueSql("null");
        });

        modelBuilder.Entity<camptype>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.aCapacities).HasDefaultValue("");
            entity.Property(e => e.m_fVisibility).HasDefaultValue(-0.050000000000000003);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nTreasureID).HasDefaultValue(3);
            entity.Property(e => e.strDesc).HasDefaultValue("");
            entity.Property(e => e.vImageList).HasDefaultValue("");
        });

        modelBuilder.Entity<chargeprofile>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strItemID).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<condition>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.aEffects).HasDefaultValue("");
            entity.Property(e => e.aFieldNames).HasDefaultValue("");
            entity.Property(e => e.aModifiers).HasDefaultValue("");
            entity.Property(e => e.aThresholds).HasDefaultValue("");
            entity.Property(e => e.bDisplay).HasDefaultValue((byte)1);
            entity.Property(e => e.bDisplayGameOver).HasDefaultValue((byte)1);
            entity.Property(e => e.bResetTimer).HasDefaultValue((byte)1);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nTransferRange).HasDefaultValue(-1);
            entity.Property(e => e.strDesc).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.vChanceNext).HasDefaultValue("0");
            entity.Property(e => e.vIDNext).HasDefaultValue("0");
        });

        modelBuilder.Entity<containertype>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<creature>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nCorpseID).HasDefaultValue(3);
            entity.Property(e => e.nTreasureID).HasDefaultValue(3);
            entity.Property(e => e.strImg).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.strNamePublic).HasDefaultValue("");
            entity.Property(e => e.strNotes).HasDefaultValue("");
            entity.Property(e => e.vActivities).HasDefaultValue("");
            entity.Property(e => e.vAttackModes).HasDefaultValue("");
            entity.Property(e => e.vBaseConditions).HasDefaultValue("");
            entity.Property(e => e.vEncounterIDs).HasDefaultValue("");
        });

        modelBuilder.Entity<creaturesource>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.fWeight).HasDefaultValue(1.0);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nX).HasDefaultValue(-1);
            entity.Property(e => e.nY).HasDefaultValue(-1);
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<datafile>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strDesc).HasDefaultValue("");
            entity.Property(e => e.strImg).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<dmcplace>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nEncounterID).HasDefaultValue(1);
            entity.Property(e => e.strImg).HasDefaultValue("");
        });

        modelBuilder.Entity<encounter>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.aConditions).HasDefaultValue("1");
            entity.Property(e => e.aMinimapHexes).HasDefaultValue("");
            entity.Property(e => e.aPreConditions).HasDefaultValue("");
            entity.Property(e => e.aResponses).HasDefaultValue("");
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nItemsID).HasDefaultValue(3);
            entity.Property(e => e.nRemoveTreasureID).HasDefaultValue(3);
            entity.Property(e => e.nTreasureID).HasDefaultValue(3);
            entity.Property(e => e.ptCreatureHex).HasDefaultValue("0,0");
            entity.Property(e => e.ptEditor).HasDefaultValue("0,0");
            entity.Property(e => e.ptTeleport).HasDefaultValue("0,0");
            entity.Property(e => e.strDesc).HasDefaultValue("");
            entity.Property(e => e.strImg).HasDefaultValue("EncBlank.png");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.vAccidents).HasDefaultValue("1");
            entity.Property(e => e.vLoot).HasDefaultValue("3");
        });

        modelBuilder.Entity<encountertrigger>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.aArea).HasDefaultValue("");
            entity.Property(e => e.aHexTypes).HasDefaultValue("");
            entity.Property(e => e.bAIPassable).HasDefaultValueSql("'1'");
            entity.Property(e => e.dateMax).HasDefaultValue("");
            entity.Property(e => e.dateMin).HasDefaultValue("");
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<faction>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.dictFactions).HasDefaultValue("");
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<forbiddenhex>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<gamevar>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.strType).HasDefaultValue("");
            entity.Property(e => e.strValue).HasDefaultValue("");
        });

        modelBuilder.Entity<headline>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strHeadline).HasDefaultValue("");
        });

        modelBuilder.Entity<hextype>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nCampItems).HasDefaultValue(5);
            entity.Property(e => e.nDefaultCampID).HasDefaultValue(517);
            entity.Property(e => e.nMaxRange).HasDefaultValue(6);
            entity.Property(e => e.nMinRange).HasDefaultValue(3);
            entity.Property(e => e.nScavengeInitialID).HasDefaultValue(3);
            entity.Property(e => e.nScavengeItemsIDPerHour).HasDefaultValue(25);
            entity.Property(e => e.strDesc).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.vCondIDs).HasDefaultValue("");
            entity.Property(e => e.vLightLevels).HasDefaultValue("0.57,1.0,0.57,0.15");
        });

        modelBuilder.Entity<image>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.imagePath).HasDefaultValue("");
            entity.Property(e => e.modName).HasDefaultValue("");
        });

        modelBuilder.Entity<ingredient>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strForbiddenProps).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("0");
            entity.Property(e => e.strRequiredProps).HasDefaultValue("0");
        });

        modelBuilder.Entity<itemprop>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strPropertyName).HasDefaultValue("");
        });

        modelBuilder.Entity<itemtype>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.aAttackModes).HasDefaultValue("");
            entity.Property(e => e.aCapacities).HasDefaultValue("");
            entity.Property(e => e.aContentIDs).HasDefaultValue("");
            entity.Property(e => e.aEquipConditions).HasDefaultValue("");
            entity.Property(e => e.aPossessConditions).HasDefaultValue("");
            entity.Property(e => e.aSounds).HasDefaultValue("cuePickup,cuePutdown");
            entity.Property(e => e.aSwitchIDs).HasDefaultValue("");
            entity.Property(e => e.aUseConditions).HasDefaultValue("");
            entity.Property(e => e.bMirrored).HasDefaultValueSql("'0'");
            entity.Property(e => e.fDurability).HasDefaultValue(1.0);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nComponentID).HasDefaultValueSql("'3'");
            entity.Property(e => e.nCondID).HasDefaultValue(1);
            entity.Property(e => e.nFormatID).HasDefaultValueSql("'3'");
            entity.Property(e => e.nSlotDepth).HasDefaultValueSql("'0'");
            entity.Property(e => e.nStackLimit).HasDefaultValueSql("'1'");
            entity.Property(e => e.nTreasureID).HasDefaultValueSql("'3'");
            entity.Property(e => e.strChargeProfiles).HasDefaultValue("");
            entity.Property(e => e.strDesc).HasDefaultValue("");
            entity.Property(e => e.strDescAlt).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.vDegradeTreasureIDs).HasDefaultValue("3,3");
            entity.Property(e => e.vEquipSlots).HasDefaultValue("");
            entity.Property(e => e.vImageList).HasDefaultValue("");
            entity.Property(e => e.vImageUsage).HasDefaultValue("");
            entity.Property(e => e.vProperties).HasDefaultValue("");
            entity.Property(e => e.vSpriteList).HasDefaultValue("");
            entity.Property(e => e.vUseSlots).HasDefaultValue("");
        });

        modelBuilder.Entity<map>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strDef).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        modelBuilder.Entity<recipe>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.bDegradeOutput).HasDefaultValue((byte)1);
            entity.Property(e => e.bScrap).HasDefaultValue((byte)1);
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.nTempTreasureID).HasDefaultValue(3);
            entity.Property(e => e.nTreasureID).HasDefaultValue(3);
            entity.Property(e => e.strConsumed).HasDefaultValue("");
            entity.Property(e => e.strDestroyed).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
            entity.Property(e => e.strSecretName).HasDefaultValue("");
            entity.Property(e => e.strTools).HasDefaultValue("");
            entity.Property(e => e.strType).HasDefaultValue("");
            entity.Property(e => e.vAlsoTry).HasDefaultValue("");
        });

        modelBuilder.Entity<treasuretable>(entity =>
        {
            entity.Property(e => e.idx).ValueGeneratedNever();
            entity.Property(e => e.aTreasures).HasDefaultValue("");
            entity.Property(e => e.modName).HasDefaultValue("");
            entity.Property(e => e.strName).HasDefaultValue("");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
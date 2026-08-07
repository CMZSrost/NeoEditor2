package
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.events.*;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import flash.ui.Mouse;
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class Creature extends FlxGroup implements IConditionOwner
   {
       
      
      public var m_strNamePrivate:String;
      
      public var m_strNamePublic:String;
      
      public var m_nMovesPerTurn:uint;
      
      public var m_fScent:Number;
      
      public var m_strImage:String;
      
      public var m_sprImage:FlxSprite;
      
      public var m_nFaction:uint;
      
      public var fFoodConsumptionRate:Number;
      
      public var fWaterConsumptionRate:Number;
      
      public var fNormalBodyTemp:Number;
      
      public var fPassiveRewarmPerHour:Number;
      
      public var fWetTempAdjust:Number;
      
      public var fHoursToStarve:Number;
      
      public var fHoursToDehydrate:Number;
      
      public var fHoursToBlackout:Number;
      
      public var grpGroundSlot:GUIInventorySlot;
      
      public var grpCampSlot:GUIInventorySlot;
      
      public var m_fTrackingThreshold:Number;
      
      public var m_fEncumberanceLimit:Number;
      
      public var m_vBaseAttackModes:Vector.<AttackMode>;
      
      protected var nVisionRange:Number;
      
      protected var fMinLightLevel:Number;
      
      protected var fMinSafeTemp:Number;
      
      protected var fMaxSafeTemp:Number;
      
      protected var fBodyInsulation:Number;
      
      public var fFullyRested:Number;
      
      protected var m_fHealPerHour:Number;
      
      public var m_vBaseConditions:Vector.<Vector.<Number>>;
      
      private var m_fBaseDetectionLevel:Number;
      
      public var m_fMoveReserve:Number;
      
      public var m_vBluntWoundSlots:Vector.<GUIInventoryWound>;
      
      public var m_vCutWoundSlots:Vector.<GUIInventoryWound>;
      
      public var m_vAllWoundSlots:Vector.<GUIInventoryWound>;
      
      public var vInvCategories:Vector.<GUIInventorySlot>;
      
      public var m_dictSlots:Dictionary;
      
      public var m_tilCurrentHex:FlxHexTile;
      
      private var m_bAlive:Boolean;
      
      protected var m_bKillQueue:Boolean;
      
      private var m_bDespawnQueue:Boolean;
      
      public var m_bCondQueue:Boolean;
      
      public var m_dictCondQueue:Dictionary;
      
      private var m_strPopUp:String;
      
      private var m_strPopUpDetail:String;
      
      public var m_fMovesLeft:Number;
      
      public var aCurrentStates:Array;
      
      public var fMovesPerTurnModifier:Number;
      
      public var fSleepDebt:Number;
      
      public var fFoodDebt:Number;
      
      public var fWaterDebt:Number;
      
      public var fCoreTemp:Number;
      
      public var aLoadStates:Array;
      
      public var aHungerStates:Array;
      
      public var aThirstStates:Array;
      
      public var aRestedStates:Array;
      
      public var aCoreTempStates:Array;
      
      public var aBloodStates:Array;
      
      public var aInfectionStates:Array;
      
      public var aPainStates:Array;
      
      public var aMoralityStates:Array;
      
      public var fHoursSlept:Number;
      
      public var fAdjMinSafeTemp:Number;
      
      public var fAdjMaxSafeTemp:Number;
      
      public var fAdjBodyInsulation:Number;
      
      public var m_fFatigueModifier:Number;
      
      public var m_fHealPerHourMod:Number;
      
      private var m_fEncumberance:Number;
      
      public var m_objCurrentHungerCond:PlayerCondition;
      
      public var m_objCurrentThirstCond:PlayerCondition;
      
      public var m_objCurrentRestCond:PlayerCondition;
      
      public var m_objCurrentLoadCond:PlayerCondition;
      
      public var m_objCurrentTempCond:PlayerCondition;
      
      public var m_objCurrentBloodCond:PlayerCondition;
      
      public var m_objCurrentPainCond:PlayerCondition;
      
      public var m_objCurrentInfectionCond:PlayerCondition;
      
      public var m_objCurrentMoralityCond:PlayerCondition;
      
      protected var bAsleep:Boolean;
      
      protected var m_bResting:Boolean;
      
      public var m_nAttackMode:uint;
      
      public var m_vAttackModes:Vector.<AttackMode>;
      
      public var m_fSleepAwareness:Number;
      
      public var m_fVisibility:Number;
      
      private var fWetTempAdjustMod:Number;
      
      private var m_fSleepQuality:Number;
      
      public var m_dictCamps:Dictionary;
      
      public var m_fDetectionLevel:Number;
      
      protected var m_fLightLevel:Number;
      
      public var m_fMoveReserveRemaining:Number;
      
      public var m_fMoveCost:Number;
      
      public var m_fCover:Number;
      
      public var m_dictCombatPairs:Dictionary;
      
      public var m_fMorale:Number;
      
      public var m_fMoraleHidden:Number;
      
      public var m_fMoraleSitu:Number;
      
      public var m_fMoraleSituHidden:Number;
      
      public var m_fDefense:Number;
      
      public var m_fOrder:Number;
      
      public var m_objPair:CombatPair;
      
      public var m_objMove:BattleMove;
      
      public var m_objMoveLast:BattleMove;
      
      public var m_objTargetLast:Creature;
      
      public var m_fBloodLeft:Number;
      
      public var m_fImmuneLeft:Number;
      
      public var m_fPainLeft:Number;
      
      public var m_fBloodLeftBase:Number;
      
      public var m_fImmuneLeftBase:Number;
      
      public var m_fPainLeftBase:Number;
      
      public var m_fBloodRestoreRate:Number;
      
      public var m_fImmuneRestoreRate:Number;
      
      public var m_dictCrippled:Dictionary;
      
      public var m_dictFactions:Dictionary;
      
      public var m_nMorality:int;
      
      public var m_fLeader:Number;
      
      public var m_bLeader:Boolean;
      
      private var m_fAttDmgMult:Number;
      
      private var m_fDefDmgMult:Number;
      
      protected var m_vImmunities:Vector.<int>;
      
      protected var m_objLocker:ItemInstance;
      
      public var m_bVisibleBefore:Boolean;
      
      public const CRIPPLED_LEFTARM:uint = 1;
      
      public const CRIPPLED_RIGHTARM:uint = 2;
      
      public const CRIPPLED_LEFTLEG:uint = 3;
      
      public const CRIPPLED_RIGHTLEG:uint = 4;
      
      public var m_bSpied:Boolean = false;
      
      public function Creature(param1:String, param2:String, param3:String, param4:uint, param5:uint, param6:Vector.<AttackMode>, param7:Vector.<Vector.<Number>>, param8:Dictionary)
      {
         var _loc9_:Object = null;
         super();
         this.m_sprImage = new FlxSprite(0,0);
         this.m_sprImage.pixels = DataHandler.GetImage(param3);
         add(this.m_sprImage);
         this.m_strNamePublic = param2;
         this.m_strNamePrivate = param1;
         this.m_strImage = param3;
         this.m_nMovesPerTurn = param4;
         this.m_vBaseAttackModes = param6.concat();
         this.m_vBaseConditions = param7;
         this.m_vBluntWoundSlots = new Vector.<GUIInventoryWound>();
         this.m_vCutWoundSlots = new Vector.<GUIInventoryWound>();
         this.m_vAllWoundSlots = new Vector.<GUIInventoryWound>();
         this.m_nFaction = param5;
         this.m_bResting = false;
         this.m_dictCamps = new Dictionary();
         this.m_dictCrippled = new Dictionary();
         this.m_dictFactions = new Dictionary();
         this.m_dictCombatPairs = new Dictionary();
         this.m_dictCondQueue = new Dictionary();
         this.m_vImmunities = new Vector.<int>();
         for(_loc9_ in param8)
         {
            this.m_dictFactions[_loc9_] = param8[_loc9_];
         }
      }
      
      public function Initialize(param1:Vector.<Vector.<Number>> = null, param2:Boolean = false) : void
      {
         var _loc4_:Vector.<Vector.<Number>> = null;
         this.m_bAlive = true;
         this.m_bKillQueue = false;
         this.m_bDespawnQueue = false;
         this.m_bCondQueue = false;
         this.m_dictCondQueue = new Dictionary();
         this.fSleepDebt = 0;
         this.fFoodDebt = 0;
         this.fWaterDebt = 0;
         this.m_fMovesLeft = this.m_nMovesPerTurn;
         this.fMovesPerTurnModifier = 0;
         this.bAsleep = false;
         this.fNormalBodyTemp = 98.6;
         this.fMinSafeTemp = 82;
         this.fMaxSafeTemp = 110;
         this.fBodyInsulation = 4;
         this.fAdjMinSafeTemp = this.fMinSafeTemp;
         this.fAdjMaxSafeTemp = this.fMaxSafeTemp;
         this.fAdjBodyInsulation = this.fBodyInsulation;
         this.fPassiveRewarmPerHour = 2.16;
         this.fWetTempAdjust = 15;
         this.fWetTempAdjustMod = 0;
         this.fCoreTemp = this.fNormalBodyTemp;
         this.fHoursToStarve = 168;
         this.fHoursToDehydrate = 72;
         this.fHoursToBlackout = 288;
         this.fFullyRested = 7;
         this.m_fSleepQuality = 1;
         this.m_fScent = 4;
         this.m_fTrackingThreshold = 3;
         this.m_fFatigueModifier = 1;
         this.m_fHealPerHour = 0.1;
         this.m_fHealPerHourMod = 0;
         this.m_fEncumberance = 0;
         this.m_fEncumberanceLimit = 100;
         this.nVisionRange = 2;
         this.fMinLightLevel = 0.5;
         this.fFoodConsumptionRate = 1;
         this.fWaterConsumptionRate = 1;
         this.m_fSleepAwareness = 0.1;
         this.m_fVisibility = 0.5;
         this.m_fBaseDetectionLevel = 0.5;
         this.m_fDetectionLevel = DM.Rand(DM.RAND_LOW);
         this.m_fLightLevel = 0;
         this.m_fMoveReserve = 1;
         this.m_fMoveReserveRemaining = this.m_fMoveReserve;
         this.m_fMoveCost = 1;
         this.m_fMorale = 0;
         this.m_fMoraleHidden = 0;
         this.m_fMoraleSitu = 0;
         this.m_fMoraleSituHidden = 0;
         this.m_fDefense = 0;
         this.m_fOrder = 0.5;
         this.m_fBloodLeftBase = 1;
         this.m_fImmuneLeftBase = 1;
         this.m_fPainLeftBase = 1;
         this.m_fBloodLeft = this.m_fBloodLeftBase;
         this.m_fImmuneLeft = this.m_fImmuneLeftBase;
         this.m_fPainLeft = this.m_fPainLeftBase;
         this.m_fBloodRestoreRate = 0.00078125;
         this.m_fImmuneRestoreRate = 0.006;
         this.m_nMorality = 0;
         this.m_fLeader = Math.random();
         this.m_bLeader = false;
         this.m_fAttDmgMult = 0.5;
         this.m_fDefDmgMult = 1;
         this.m_bVisibleBefore = false;
         this.aCurrentStates = new Array();
         this.vInvCategories = new Vector.<GUIInventorySlot>();
         this.m_dictSlots = new Dictionary();
         this.m_vAttackModes = new Vector.<AttackMode>();
         this.m_dictCrippled[this.CRIPPLED_LEFTARM] = false;
         this.m_dictCrippled[this.CRIPPLED_RIGHTARM] = false;
         this.m_dictCrippled[this.CRIPPLED_LEFTLEG] = false;
         this.m_dictCrippled[this.CRIPPLED_RIGHTLEG] = false;
         this.aHungerStates = new Array();
         this.aHungerStates.push(new Array(this.fHoursToStarve,2));
         this.aHungerStates.push(new Array(this.fHoursToStarve / 2,3));
         this.aHungerStates.push(new Array(this.fHoursToStarve / 3,4));
         this.aHungerStates.push(new Array(this.fHoursToStarve / 4,5));
         this.aHungerStates.push(new Array(0,104));
         this.aThirstStates = new Array();
         this.aThirstStates.push(new Array(this.fHoursToDehydrate,32));
         this.aThirstStates.push(new Array(this.fHoursToDehydrate / 2,31));
         this.aThirstStates.push(new Array(this.fHoursToDehydrate / 3,30));
         this.aThirstStates.push(new Array(this.fHoursToDehydrate / 4,62));
         this.aThirstStates.push(new Array(0,105));
         this.aRestedStates = new Array();
         this.aRestedStates.push(new Array(this.fHoursToBlackout,6));
         this.aRestedStates.push(new Array(this.fHoursToBlackout / 2,7));
         this.aRestedStates.push(new Array(this.fHoursToBlackout / 3,8));
         this.aRestedStates.push(new Array(0,103));
         this.aCoreTempStates = new Array();
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp + 10,15));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp + 7.4,14));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp + 3.4,13));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp + 1.4,570));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp - 2.5,63));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp - 8.5,12));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp - 16.5,11));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp - 30.5,10));
         this.aCoreTempStates.push(new Array(this.fNormalBodyTemp - 98.5,9));
         this.aLoadStates = new Array();
         this.aLoadStates.push(new Array(1,56));
         this.aLoadStates.push(new Array(2,55));
         this.aLoadStates.push(new Array(4,54));
         this.aLoadStates.push(new Array(Number.POSITIVE_INFINITY,102));
         this.aBloodStates = new Array();
         this.aBloodStates.push(new Array(0.66,173));
         this.aBloodStates.push(new Array(0.33,174));
         this.aBloodStates.push(new Array(0.1,175));
         this.aBloodStates.push(new Array(-10,176));
         this.aPainStates = new Array();
         this.aPainStates.push(new Array(0.75,177));
         this.aPainStates.push(new Array(0.5,178));
         this.aPainStates.push(new Array(0.25,179));
         this.aPainStates.push(new Array(-10,180));
         this.aInfectionStates = new Array();
         this.aInfectionStates.push(new Array(0.65,181));
         this.aInfectionStates.push(new Array(0.35,182));
         this.aInfectionStates.push(new Array(0.05,183));
         this.aInfectionStates.push(new Array(-10,184));
         this.aMoralityStates = new Array();
         this.aMoralityStates.push(new Array(3,862));
         this.aMoralityStates.push(new Array(-3,861));
         this.aMoralityStates.push(new Array(-500,863));
         this.m_objCurrentHungerCond = this.GetCondition(this.aHungerStates[this.aHungerStates.length - 1][1]);
         this.m_objCurrentThirstCond = this.GetCondition(this.aThirstStates[this.aThirstStates.length - 1][1]);
         this.m_objCurrentRestCond = this.GetCondition(this.aRestedStates[this.aRestedStates.length - 1][1]);
         this.m_objCurrentLoadCond = this.GetCondition(this.aLoadStates[this.aLoadStates.length - 1][1]);
         this.m_objCurrentTempCond = this.GetCondition(this.aCoreTempStates[4][1]);
         this.m_objCurrentBloodCond = this.GetCondition(this.aBloodStates[0][1]);
         this.m_objCurrentPainCond = this.GetCondition(this.aPainStates[0][1]);
         this.m_objCurrentInfectionCond = this.GetCondition(this.aInfectionStates[0][1]);
         this.m_objCurrentMoralityCond = this.GetCondition(this.aMoralityStates[1][1]);
         var _loc3_:int = 0;
         while(_loc3_ < this.m_vBaseAttackModes.length)
         {
            this.AddAttackMode(this.m_vBaseAttackModes[_loc3_]);
            _loc3_++;
         }
         if(param2 == false)
         {
            _loc4_ = this.m_vBaseConditions.concat();
            if(param1 != null)
            {
               _loc4_ = this.m_vBaseConditions.concat(param1);
            }
            if(_loc4_ != null)
            {
               _loc3_ = 0;
               while(_loc3_ < _loc4_.length)
               {
                  if(Math.random() <= _loc4_[_loc3_][1])
                  {
                     if(_loc4_[_loc3_][0] > 0)
                     {
                        this.AddCondition(this.GetCondition(_loc4_[_loc3_][0]));
                     }
                     else
                     {
                        this.RemoveCondition(this.GetCondition(_loc4_[_loc3_][0]));
                     }
                  }
                  _loc3_++;
               }
            }
         }
         this.UpdatePopUp();
      }
      
      override public function destroy() : void
      {
         var _loc1_:Object = null;
         var _loc2_:int = 0;
         this.m_strNamePrivate = null;
         this.m_strNamePublic = null;
         this.m_strImage = null;
         if(this.m_vBaseAttackModes != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.m_vBaseAttackModes.length)
            {
               this.m_vBaseAttackModes[_loc2_] = null;
               _loc2_++;
            }
            this.m_vBaseAttackModes = null;
         }
         this.m_vBaseConditions = null;
         if(this.m_vAllWoundSlots != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.m_vAllWoundSlots.length)
            {
               this.m_vAllWoundSlots[_loc2_].destroy();
               this.m_vAllWoundSlots[_loc2_] = null;
               _loc2_++;
            }
            this.m_vAllWoundSlots = null;
         }
         this.m_vBluntWoundSlots = null;
         this.m_vCutWoundSlots = null;
         if(this.vInvCategories != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.vInvCategories.length)
            {
               this.vInvCategories[_loc2_].destroy();
               this.vInvCategories[_loc2_] = null;
               _loc2_++;
            }
            this.vInvCategories = null;
         }
         for each(_loc2_ in this.m_dictSlots)
         {
            if(this.m_dictSlots[_loc2_] != null)
            {
               GUIInventorySlot(this.m_dictSlots[_loc2_]).destroy();
            }
            delete this.m_dictSlots[_loc2_];
         }
         this.m_dictSlots = null;
         for each(_loc2_ in this.m_dictCondQueue)
         {
            delete this.m_dictCondQueue[_loc2_];
         }
         this.m_dictCondQueue = null;
         this.m_tilCurrentHex = null;
         this.m_strPopUp = null;
         this.m_strPopUpDetail = null;
         if(this.aCurrentStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aCurrentStates.length)
            {
               PlayerCondition(this.aCurrentStates[_loc2_]).destroy();
               this.aCurrentStates[_loc2_] = null;
               _loc2_++;
            }
            this.aCurrentStates = null;
         }
         if(this.aLoadStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aLoadStates.length)
            {
               this.aLoadStates[_loc2_] = null;
               _loc2_++;
            }
            this.aLoadStates = null;
         }
         if(this.aHungerStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aHungerStates.length)
            {
               this.aHungerStates[_loc2_] = null;
               _loc2_++;
            }
            this.aHungerStates = null;
         }
         if(this.aThirstStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aThirstStates.length)
            {
               this.aThirstStates[_loc2_] = null;
               _loc2_++;
            }
            this.aThirstStates = null;
         }
         if(this.aRestedStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aRestedStates.length)
            {
               this.aRestedStates[_loc2_] = null;
               _loc2_++;
            }
            this.aRestedStates = null;
         }
         if(this.aCoreTempStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aCoreTempStates.length)
            {
               this.aCoreTempStates[_loc2_] = null;
               _loc2_++;
            }
            this.aCoreTempStates = null;
         }
         if(this.aBloodStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aBloodStates.length)
            {
               this.aBloodStates[_loc2_] = null;
               _loc2_++;
            }
            this.aBloodStates = null;
         }
         if(this.aInfectionStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aInfectionStates.length)
            {
               this.aInfectionStates[_loc2_] = null;
               _loc2_++;
            }
            this.aInfectionStates = null;
         }
         if(this.aPainStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aPainStates.length)
            {
               this.aPainStates[_loc2_] = null;
               _loc2_++;
            }
            this.aPainStates = null;
         }
         if(this.aMoralityStates != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.aMoralityStates.length)
            {
               this.aMoralityStates[_loc2_] = null;
               _loc2_++;
            }
            this.aMoralityStates = null;
         }
         this.m_objCurrentHungerCond = DataHandler.DestroyObject(this.m_objCurrentHungerCond);
         this.m_objCurrentThirstCond = DataHandler.DestroyObject(this.m_objCurrentThirstCond);
         this.m_objCurrentRestCond = DataHandler.DestroyObject(this.m_objCurrentRestCond);
         this.m_objCurrentLoadCond = DataHandler.DestroyObject(this.m_objCurrentLoadCond);
         this.m_objCurrentTempCond = DataHandler.DestroyObject(this.m_objCurrentTempCond);
         this.m_objCurrentBloodCond = DataHandler.DestroyObject(this.m_objCurrentBloodCond);
         this.m_objCurrentPainCond = DataHandler.DestroyObject(this.m_objCurrentPainCond);
         this.m_objCurrentInfectionCond = DataHandler.DestroyObject(this.m_objCurrentInfectionCond);
         this.m_objCurrentMoralityCond = DataHandler.DestroyObject(this.m_objCurrentMoralityCond);
         if(this.m_vAttackModes != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.m_vAttackModes.length)
            {
               this.m_vAttackModes[_loc2_] = null;
               _loc2_++;
            }
            this.m_vAttackModes = null;
         }
         for each(_loc2_ in this.m_dictCamps)
         {
            this.m_dictCamps[_loc2_] = null;
         }
         this.m_dictCamps = null;
         for(_loc1_ in this.m_dictCombatPairs)
         {
            CombatPair(this.m_dictCombatPairs[_loc1_]).destroy();
            delete this.m_dictCombatPairs[_loc1_];
         }
         this.m_dictCombatPairs = null;
         this.m_objPair = DataHandler.DestroyObject(this.m_objPair);
         this.m_objMove = null;
         this.m_objMoveLast = null;
         this.m_objTargetLast = null;
         this.m_dictCrippled = null;
         this.m_dictFactions = null;
         super.destroy();
      }
      
      public function AddSlot(param1:FlxGroup, param2:Vector.<GUIInventorySlot>, param3:uint, param4:String, param5:String, param6:String, param7:FlxPoint, param8:int, param9:Boolean, param10:Boolean = false, param11:FlxPoint = null, param12:Vector.<int> = null, param13:Boolean = false) : GUIInventorySlot
      {
         var _loc14_:GUIInventorySlot = new GUIInventorySlot(this,param4,param3,param7,param5,param6,param8,param9,param10,param11,param12,param13);
         if(param2 != null)
         {
            param2.push(_loc14_);
         }
         if(param1 != null)
         {
            param1.add(_loc14_);
            param1.setAll("cameras",[FlxG.camera]);
         }
         this.m_dictSlots[param3] = _loc14_;
         return _loc14_;
      }
      
      public function AddWound(param1:FlxGroup, param2:uint, param3:String, param4:Number, param5:FlxPoint, param6:int, param7:Vector.<String>, param8:Vector.<String>, param9:Vector.<String>, param10:Vector.<String>, param11:Vector.<String>, param12:Vector.<String>, param13:String, param14:Boolean, param15:Array, param16:Array, param17:Boolean = false, param18:Number = 1) : GUIInventoryWound
      {
         var _loc19_:GUIInventoryWound = new GUIInventoryWound(this,param3,param4,param2,param5,param6,param7,param8,param9,param10,param11,param12,param13,param14,param15,param16,param17,param18);
         if(param12.length > 0)
         {
            this.m_vCutWoundSlots.push(_loc19_);
         }
         if(param11.length > 0)
         {
            this.m_vBluntWoundSlots.push(_loc19_);
         }
         this.m_vAllWoundSlots.push(_loc19_);
         if(param1 != null)
         {
            param1.add(_loc19_);
            param1.setAll("cameras",[FlxG.camera]);
         }
         this.m_dictSlots[param2] = _loc19_;
         return _loc19_;
      }
      
      public function UpdatePopUp() : void
      {
         this.m_strPopUp = this.Name;
         this.m_strPopUpDetail = "\n当前状态:  " + this.GetStatus();
         this.m_strPopUpDetail += "\n当前武器: ";
         if(this.CurrentAttackMode.m_objItem != null)
         {
            this.m_strPopUpDetail += this.CurrentAttackMode.m_objItem.strDesc;
         }
         else
         {
            this.m_strPopUpDetail += this.CurrentAttackMode.m_strName;
         }
      }
      
      public function GetPopUpText(param1:Boolean = false) : String
      {
         var _loc2_:String = this.m_strPopUp;
         if(this.m_bSpied && param1)
         {
            _loc2_ += this.m_strPopUpDetail;
         }
         return _loc2_;
      }
      
      public function get Name() : String
      {
         var _loc1_:* = this.m_strNamePublic;
         if(this.HasCondition(504))
         {
            _loc1_ = this.m_strNamePrivate;
         }
         if(!this.HasCondition(495) && this.m_bLeader)
         {
            _loc1_ += " 首领";
         }
         return _loc1_;
      }
      
      public function GetCreatureImage(param1:Boolean) : BitmapData
      {
         var _loc2_:BitmapData = null;
         var _loc3_:FlxSprite = null;
         if(param1)
         {
            for each(_loc3_ in members)
            {
               if(_loc2_ == null)
               {
                  _loc2_ = new BitmapData(_loc3_.width,_loc3_.height,true,0);
               }
               if(_loc3_.visible)
               {
                  _loc2_.copyPixels(_loc3_.pixels,_loc3_.pixels.rect,new Point(),null,null,true);
               }
            }
            if(_loc2_ == null)
            {
               _loc2_ = DataHandler.GetImage("blank.png");
            }
            return _loc2_;
         }
         return DataHandler.GetImage("CreUnknown.png");
      }
      
      public function KillCreature(param1:String, param2:String, param3:String = "") : void
      {
         var _loc5_:Creature = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:ItemInstance = null;
         var _loc4_:* = this.Name + " 死亡";
         if(param1 != "")
         {
            _loc4_ += " 被 " + param1 + " 用 " + param2;
         }
         if(param3 != "")
         {
            _loc4_ += ", " + param3 + ".";
         }
         _loc4_ += ".";
         this.m_bAlive = false;
         this.m_bKillQueue = false;
         visible = false;
         if(this.m_tilCurrentHex.m_objBattle != null)
         {
            this.m_tilCurrentHex.m_objBattle.RemoveCreature(this);
         }
         this.m_tilCurrentHex.RemoveCreature(this);
         if(this.m_tilCurrentHex.m_vOccupants.length > 0)
         {
            _loc5_ = this.m_tilCurrentHex.m_vOccupants[0];
         }
         var _loc6_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         if(this.m_tilCurrentHex.GroundObject != PlayState.m_objInstance.tilCurrentHex.GroundObject)
         {
            _loc6_ = this.grpGroundSlot.GetAllSocketedItems();
            for each(_loc8_ in _loc6_)
            {
               _loc7_ = this.grpGroundSlot.UnSocketItem(true,_loc8_);
               if(_loc5_ != null && _loc7_ != null)
               {
                  _loc5_.grpGroundSlot.SocketItem(_loc7_);
                  _loc7_ = null;
               }
            }
            _loc6_ = this.grpCampSlot.GetAllSocketedItems();
         }
         for each(_loc8_ in _loc6_)
         {
            _loc7_ = this.grpCampSlot.UnSocketItem(true,_loc8_);
            if(_loc5_ != null && _loc7_ != null)
            {
               _loc5_.grpCampSlot.SocketItem(_loc7_);
               _loc7_ = null;
            }
         }
      }
      
      public function SortWounds(param1:Vector.<GUIInventoryWound>) : Vector.<GUIInventoryWound>
      {
         var _loc3_:GUIInventoryWound = null;
         var _loc2_:Array = new Array();
         for each(_loc3_ in param1)
         {
            _loc2_.push(_loc3_);
         }
         _loc2_.sortOn("m_fProbability");
         return Vector.<GUIInventoryWound>(_loc2_);
      }
      
      public function GetWoundLocation(param1:Number, param2:Number) : GUIInventoryWound
      {
         var _loc5_:int = 0;
         var _loc6_:int = 0;
         var _loc3_:int = -1;
         var _loc4_:GUIInventoryWound = this.m_vAllWoundSlots[Math.floor(Math.random() * this.m_vAllWoundSlots.length)];
         if(param2 > 0 && param2 >= param1 && this.m_vCutWoundSlots.length > 0)
         {
            _loc5_ = int(this.m_vCutWoundSlots.length);
            _loc6_ = 0;
            while(_loc6_ < _loc5_)
            {
               if(Math.random() <= this.m_vCutWoundSlots[_loc6_].m_fProbability)
               {
                  _loc4_ = this.m_vCutWoundSlots[_loc6_];
                  break;
               }
               if(_loc6_ >= _loc5_ - 1)
               {
                  _loc4_ = this.m_vCutWoundSlots[_loc6_];
               }
               _loc6_++;
            }
         }
         if(param1 > 0 && param1 > param2 && this.m_vBluntWoundSlots.length > 0)
         {
            _loc5_ = int(this.m_vBluntWoundSlots.length);
            _loc6_ = 0;
            while(_loc6_ < _loc5_)
            {
               if(Math.random() <= this.m_vBluntWoundSlots[_loc6_].m_fProbability)
               {
                  _loc4_ = this.m_vBluntWoundSlots[_loc6_];
                  break;
               }
               if(_loc6_ >= _loc5_ - 1)
               {
                  _loc4_ = this.m_vBluntWoundSlots[_loc6_];
               }
               _loc6_++;
            }
         }
         if(this.HasCondition(18) && !_loc4_.m_bVital)
         {
            _loc5_ = int(this.m_vCutWoundSlots.length + this.m_vBluntWoundSlots.length);
            _loc6_ = 0;
            while(_loc6_ < _loc5_)
            {
               if((_loc4_ = this.GetWoundLocation(param1,param2)).m_bVital)
               {
                  break;
               }
               _loc6_++;
            }
         }
         if(_loc4_ == null)
         {
            _loc4_ = this.m_vAllWoundSlots[Math.floor(Math.random() * this.m_vAllWoundSlots.length)];
         }
         return _loc4_;
      }
      
      public function CauseWound(param1:Number, param2:Number, param3:Number, param4:String, param5:String) : GUIInventoryWound
      {
         var _loc6_:int = -1;
         var _loc7_:GUIInventoryWound = this.GetWoundLocation(param1,param2);
         var _loc8_:String = "未知目标";
         if(PlayState.m_objInstance.sprPlayer.PlayerCanSee(this))
         {
            _loc8_ = this.Name;
         }
         _loc7_.Damage(param1,param2,param3,param4,param5);
         if(this.m_bAlive && this.Asleep)
         {
            this.ForceAwake();
         }
         this.UpdateStatus();
         return _loc7_;
      }
      
      public function Spawn(param1:FlxPoint) : void
      {
         this.m_tilCurrentHex = MapUtils.GetTileByCoords(param1);
         PlayState.m_objInstance.AlignCreatureToHex(this,this.m_tilCurrentHex);
      }
      
      public function CanEnterHex(param1:FlxHexTile) : Boolean
      {
         if(param1 == null || !param1.bPassable)
         {
            return false;
         }
         return true;
      }
      
      public function CanMove() : Boolean
      {
         return this.m_bAlive && this.m_fMovesLeft > 0 && (this.m_tilCurrentHex.m_objBattle == null || !this.m_tilCurrentHex.m_objBattle.IsCombatant(this));
      }
      
      public function GetItems(param1:Boolean = true, param2:Boolean = true, param3:Boolean = true, param4:Boolean = true, param5:int = -1, param6:int = -1) : Vector.<ItemInstance>
      {
         var _loc9_:GUIInventorySlot = null;
         var _loc10_:Vector.<ItemInstance> = null;
         var _loc11_:ItemInstance = null;
         var _loc12_:Boolean = false;
         var _loc13_:Vector.<ItemInstance> = null;
         var _loc7_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc8_:Vector.<GUIInventorySlot> = this.vInvCategories.concat();
         if(param4)
         {
            _loc8_ = _loc8_.concat(this.m_vAllWoundSlots);
         }
         for each(_loc9_ in _loc8_)
         {
            _loc10_ = _loc9_.GetAllSocketedItems();
            for each(_loc11_ in _loc10_)
            {
               if(_loc11_ != null)
               {
                  _loc12_ = true;
                  if(param5 > 0 && _loc11_.ItemDefinition.nGroupID != param5 || param6 >= 0 && _loc11_.ItemDefinition.nSubgroupID != param6)
                  {
                     _loc12_ = false;
                  }
                  if(param2 == false && _loc9_.bHoldSlot)
                  {
                     _loc12_ = false;
                  }
                  if(_loc12_)
                  {
                     _loc7_.push(_loc11_);
                  }
                  if(param3)
                  {
                     if((_loc13_ = _loc11_.GetItems(param5,param6)).length > 0)
                     {
                        _loc7_ = _loc7_.concat(_loc13_);
                     }
                  }
               }
            }
         }
         return _loc7_;
      }
      
      public function RemoveItem(param1:ItemInstance, param2:Boolean) : ItemInstance
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         if(param1.Slot != null)
         {
            _loc3_ = this.m_dictSlots[param1.Slot.nSlotIndex];
            _loc4_ = _loc3_.GetAllSocketedItems();
            for each(_loc5_ in _loc4_)
            {
               if(_loc5_ != null)
               {
                  if(_loc5_ == param1 || _loc5_.m_vStack.indexOf(param1) >= 0)
                  {
                     return _loc3_.UnSocketItem(false,param1,param2);
                  }
                  if((_loc6_ = _loc3_.RemoveItem(param1,param2)) != null)
                  {
                     return _loc6_;
                  }
               }
            }
         }
         return null;
      }
      
      public function EndTurn(param1:Number, param2:Weather) : void
      {
         var _loc3_:Date = null;
         var _loc4_:Array = null;
         var _loc5_:PlayerCondition = null;
         var _loc6_:GUIInventoryWound = null;
         var _loc7_:Vector.<ItemInstance> = null;
         var _loc8_:ItemInstance = null;
         var _loc9_:* = false;
         var _loc10_:Number = NaN;
         if(param1 > 0)
         {
            _loc3_ = PlayState.m_objInstance.objDate;
            _loc4_ = this.aCurrentStates.concat();
            for each(_loc5_ in _loc4_)
            {
               _loc5_.Update(this,_loc3_);
            }
            this.RemoveCondition(this.GetCondition(106));
            for each(_loc6_ in this.m_vAllWoundSlots)
            {
               _loc6_.EndTurn(param1,_loc3_);
               if(!_loc6_.m_bStaunched && _loc6_.m_fBleedRate > 0 && !this.HasCondition(106))
               {
                  this.AddCondition(this.GetCondition(106),true,false);
               }
            }
            _loc7_ = this.GetItems(true,true,false,true);
            for each(_loc8_ in _loc7_)
            {
               if(_loc8_.fDurability <= 0)
               {
                  _loc9_ = _loc8_.ItemDefinition.m_vProperties.indexOf(82) >= 0;
                  _loc8_.ReplaceDegradedItem(1,_loc9_);
               }
               else
               {
                  _loc8_.EquipDegrade(param1);
                  _loc9_ = _loc8_.fDurability <= 0 && _loc8_.ItemDefinition.m_vProperties.indexOf(83) >= 0;
                  _loc8_.TimeDegrade(_loc3_);
                  if(_loc8_.fDurability <= 0)
                  {
                     _loc8_.ReplaceDegradedItem(0,_loc9_);
                  }
               }
            }
            if(this.fFoodConsumptionRate > 0)
            {
               this.fFoodDebt += param1 * this.fFoodConsumptionRate;
            }
            if(this.fWaterConsumptionRate > 0)
            {
               this.fWaterDebt += param1 * this.fWaterConsumptionRate;
            }
            if(this.bAsleep)
            {
               this.fHoursSlept += param1;
               this.fSleepDebt = this.fSleepDebt * (this.fFullyRested - this.fHoursSlept * this.m_fSleepQuality) / this.fFullyRested;
               if(this.fSleepDebt <= 0)
               {
                  this.fSleepDebt = 0;
                  this.Asleep = false;
               }
               else if(this.m_tilCurrentHex.m_objBattle != null)
               {
                  this.AddCondition(this.GetCondition(147));
               }
            }
            else
            {
               if(this.fSleepDebt <= 0)
               {
                  this.fSleepDebt = 0;
               }
               this.fSleepDebt += param1;
               this.fHoursSlept = 0;
            }
            if(param2 != null)
            {
               this.Exposure(param1,param2);
            }
            if(this.Resting)
            {
               this.MessageFloaty("恢复中...");
            }
            if(this.m_fMovesLeft < 0)
            {
               this.m_fMovesLeft = 0;
            }
            if(this.m_fBloodLeft < this.m_fBloodLeftBase)
            {
               if((_loc10_ = 10 - 10 * (1 - (this.m_fBloodLeftBase - this.m_fBloodLeft) / this.m_fBloodLeftBase)) < 1)
               {
                  _loc10_ = 1;
               }
               this.m_fBloodLeft += param1 * this.m_fBloodRestoreRate * _loc10_;
            }
            if(this.m_fImmuneLeft < this.m_fImmuneLeftBase)
            {
               this.m_fImmuneLeft += param1 * this.m_fImmuneRestoreRate;
            }
            if(this.m_fBloodLeft > this.m_fBloodLeftBase)
            {
               this.m_fBloodLeft = this.m_fBloodLeftBase;
            }
            if(this.m_tilCurrentHex.m_objBattle == null && !this.HasCondition(376))
            {
               this.m_fMoveReserveRemaining += this.m_fMovesLeft;
               this.m_fMoveReserveRemaining = Math.min(this.m_fMoveReserve,this.m_fMoveReserveRemaining);
               this.m_fMovesLeft += (this.m_nMovesPerTurn + this.fMovesPerTurnModifier) * param1 / PlayState.HOURS_PER_TURN;
               if(this.m_fMovesLeft > this.m_nMovesPerTurn + this.fMovesPerTurnModifier)
               {
                  this.m_fMovesLeft = this.m_nMovesPerTurn + this.fMovesPerTurnModifier;
               }
               if(this.m_fMovesLeft < -50)
               {
                  this.m_fMovesLeft = 0;
               }
               else if(this.m_fMovesLeft < 1)
               {
                  this.m_fMovesLeft = 1;
               }
               if(this.m_fMoveReserveRemaining < 0)
               {
                  this.m_fMoveReserveRemaining = 0;
               }
            }
         }
         this.UpdateStatus();
         this.m_fDetectionLevel = 0.25;
         if(this.fFoodDebt < 0)
         {
            this.fFoodDebt = 0;
         }
         if(this.fWaterDebt < 0)
         {
            this.fWaterDebt = 0;
         }
         if(this.m_tilCurrentHex.m_objBattle == null && this.HasCondition(143) && !this.bAsleep && !this.m_dictCrippled[this.CRIPPLED_LEFTLEG] && !this.m_dictCrippled[this.CRIPPLED_RIGHTLEG])
         {
            this.RemoveCondition(this.GetCondition(143));
         }
         if(this.m_bKillQueue)
         {
            this.KillCreature("","","");
         }
         if(this.m_bDespawnQueue)
         {
            this.Despawn([1],true);
         }
      }
      
      protected function Exposure(param1:Number, param2:Weather) : void
      {
         this.MinSafeTemp = this.MinSafeTemp;
         this.MaxSafeTemp = this.MaxSafeTemp;
         this.fAdjBodyInsulation = Math.max(this.fBodyInsulation,0);
         var _loc3_:Number = Math.min(this.fAdjMinSafeTemp,this.fCoreTemp);
         if(param2.fTemp < _loc3_)
         {
            this.fCoreTemp += (-this.fBodyInsulation / Math.exp((param2.fTemp - _loc3_) / 40) + this.fBodyInsulation) * param1;
         }
         else
         {
            this.fCoreTemp += this.fPassiveRewarmPerHour * param1;
            if(param2.fTemp < this.fAdjMaxSafeTemp)
            {
               if(this.fCoreTemp > this.fNormalBodyTemp)
               {
                  this.fCoreTemp = this.fNormalBodyTemp;
               }
            }
         }
      }
      
      public function GetStatus() : String
      {
         var _loc2_:PlayerCondition = null;
         var _loc1_:* = "";
         for each(_loc2_ in this.aCurrentStates)
         {
            if(_loc2_.m_bDisplayOther)
            {
               if(_loc1_ != "")
               {
                  _loc1_ += "\n";
               }
               _loc1_ += _loc2_.strName;
            }
         }
         return _loc1_;
      }
      
      public function GetCondition(param1:uint) : PlayerCondition
      {
         var _loc2_:PlayerCondition = null;
         for each(_loc2_ in this.aCurrentStates)
         {
            if(_loc2_.m_nID == param1)
            {
               return _loc2_;
            }
         }
         return DataHandler.GetCondition(param1);
      }
      
      public function HasCondition(param1:uint) : Boolean
      {
         var _loc2_:PlayerCondition = null;
         for each(_loc2_ in this.aCurrentStates)
         {
            if(_loc2_.m_nID == param1)
            {
               return true;
            }
         }
         return false;
      }
      
      public function AddCondition(param1:PlayerCondition, param2:Boolean = true, param3:Boolean = true) : void
      {
         if(this.m_bCondQueue)
         {
            if(this.m_dictCondQueue[param1.m_nID] == undefined)
            {
               this.m_dictCondQueue[param1.m_nID] = 1;
            }
            else
            {
               ++this.m_dictCondQueue[param1.m_nID];
            }
            return;
         }
         param1.ApplyConditionEffects(this);
      }
      
      public function RemoveCondition(param1:PlayerCondition) : void
      {
         if(param1 == null)
         {
            return;
         }
         if(this.m_bCondQueue)
         {
            if(this.m_dictCondQueue[param1.m_nID] == undefined)
            {
               this.m_dictCondQueue[param1.m_nID] = -1;
            }
            else
            {
               --this.m_dictCondQueue[param1.m_nID];
            }
            return;
         }
         param1.RemoveConditionEffects(this);
      }
      
      public function ProcessConditionQueue() : void
      {
         var _loc1_:PlayerCondition = null;
         var _loc2_:String = null;
         if(this.Alive == false)
         {
            return;
         }
         this.m_bCondQueue = false;
         for(_loc2_ in this.m_dictCondQueue)
         {
            while(this.m_dictCondQueue[_loc2_] > 0)
            {
               this.AddCondition(this.GetCondition(int(_loc2_)));
               --this.m_dictCondQueue[_loc2_];
            }
         }
         for(_loc2_ in this.m_dictCondQueue)
         {
            while(this.m_dictCondQueue[_loc2_] < 0)
            {
               this.RemoveCondition(this.GetCondition(int(_loc2_)));
               ++this.m_dictCondQueue[_loc2_];
            }
         }
         this.m_dictCondQueue = new Dictionary();
      }
      
      public function UpdateStatus() : void
      {
         var _loc1_:PlayerCondition = null;
         var _loc2_:Array = null;
         var _loc3_:uint = 0;
         if(this.m_bCondQueue)
         {
            return;
         }
         for each(_loc2_ in this.aRestedStates)
         {
            if(this.fSleepDebt >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentRestCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentRestCond);
                  this.m_objCurrentRestCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         for each(_loc2_ in this.aHungerStates)
         {
            if(this.fFoodDebt >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentHungerCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentHungerCond);
                  this.m_objCurrentHungerCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         for each(_loc2_ in this.aThirstStates)
         {
            if(this.fWaterDebt >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentThirstCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentThirstCond);
                  this.m_objCurrentThirstCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         for each(_loc2_ in this.aLoadStates)
         {
            if(this.m_fEncumberance >= this.m_fEncumberanceLimit / _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentLoadCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentLoadCond);
                  this.m_objCurrentLoadCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         _loc3_ = 0;
         for each(_loc2_ in this.aCoreTempStates)
         {
            if(this.fCoreTemp >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentTempCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentTempCond);
                  this.m_objCurrentTempCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
            _loc3_++;
         }
         for each(_loc2_ in this.aBloodStates)
         {
            if(this.m_fBloodLeft / this.m_fBloodLeftBase >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentBloodCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentBloodCond);
                  this.m_objCurrentBloodCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         for each(_loc2_ in this.aPainStates)
         {
            if(this.m_fPainLeft / this.m_fPainLeftBase >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentPainCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentPainCond);
                  this.m_objCurrentPainCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         for each(_loc2_ in this.aInfectionStates)
         {
            if(this.m_fImmuneLeft / this.m_fImmuneLeftBase >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentInfectionCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentInfectionCond);
                  this.m_objCurrentInfectionCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
                  this.Resting = false;
               }
               break;
            }
         }
         for each(_loc2_ in this.aMoralityStates)
         {
            if(this.m_nMorality >= _loc2_[0])
            {
               _loc1_ = this.GetCondition(_loc2_[1]);
               if(this.m_objCurrentMoralityCond != _loc1_)
               {
                  this.RemoveCondition(this.m_objCurrentMoralityCond);
                  this.m_objCurrentMoralityCond = _loc1_;
                  if(_loc1_.m_nStacked < 1)
                  {
                     this.AddCondition(_loc1_);
                  }
               }
               break;
            }
         }
         this.UpdatePopUp();
      }
      
      public function AddAttackMode(param1:AttackMode) : void
      {
         if(param1 == null)
         {
            return;
         }
         this.m_vAttackModes.push(param1);
      }
      
      public function RemoveAttackMode(param1:AttackMode) : void
      {
         if(param1 == null)
         {
            return;
         }
         var _loc2_:int = int(this.m_vAttackModes.indexOf(param1));
         if(_loc2_ >= 0)
         {
            this.m_vAttackModes.splice(_loc2_,1);
            if(_loc2_ <= this.m_nAttackMode)
            {
               this.ChangeAttackMode(this.m_nAttackMode - 1);
            }
         }
      }
      
      public function get CurrentAttackMode() : AttackMode
      {
         if(this.m_nAttackMode >= this.m_vAttackModes.length)
         {
            this.ChangeAttackMode(this.m_vAttackModes.length - 1);
         }
         return this.m_vAttackModes[this.m_nAttackMode];
      }
      
      public function ChangeAttackMode(param1:int) : void
      {
         var _loc2_:int = 0;
         if(param1 < this.m_vAttackModes.length && param1 >= 0)
         {
            _loc2_ = param1;
         }
         else if(param1 < 0)
         {
            _loc2_ = int(this.m_vAttackModes.length - 1);
         }
         else
         {
            _loc2_ = 0;
         }
         if(this.m_nAttackMode != _loc2_)
         {
            this.m_nAttackMode = _loc2_;
            if(this.m_tilCurrentHex != null && this.m_tilCurrentHex.m_objBattle != null)
            {
               this.AddCondition(this.GetCondition(157));
               this.m_tilCurrentHex.m_objBattle.TrimMoves(this);
            }
         }
         this.UpdatePopUp();
      }
      
      public function ForceAwake() : void
      {
         var _loc1_:Number = this.fSleepDebt;
         this.fSleepDebt = 0;
         this.Asleep = false;
         this.fSleepDebt = _loc1_;
      }
      
      public function get LightLevel() : Number
      {
         return this.m_fLightLevel;
      }
      
      public function set LightLevel(param1:Number) : void
      {
         this.m_fLightLevel = param1;
      }
      
      public function CanSeeCreature(param1:Creature) : Boolean
      {
         if(param1 == this)
         {
            return true;
         }
         if(this.bAsleep || param1 == null || param1.m_tilCurrentHex == null)
         {
            return false;
         }
         var _loc2_:Number = param1.m_tilCurrentHex.m_vLightLevels[PlayState.m_objInstance.nTimeOfDay] + param1.LightLevel - this.MinLightLevel;
         var _loc3_:Number = this.m_fBaseDetectionLevel + this.m_fDetectionLevel;
         var _loc4_:Number = param1.m_fVisibility + _loc2_;
         if(_loc3_ < _loc4_)
         {
            return true;
         }
         return false;
      }
      
      public function get Asleep() : Boolean
      {
         return this.bAsleep;
      }
      
      public function set Asleep(param1:Boolean) : void
      {
         var _loc2_:Number = NaN;
         var _loc3_:Number = NaN;
         var _loc4_:int = 0;
         if(!param1)
         {
            _loc2_ = 1 - this.m_fSleepQuality + Math.max(0,(this.fFullyRested + 8 - this.fSleepDebt) / this.fFullyRested);
            _loc3_ = Math.random();
            if(_loc3_ < _loc2_)
            {
               this.bAsleep = false;
               if((_loc4_ = int(this.aCurrentStates.indexOf(this.GetCondition(18)))) >= 0)
               {
                  this.RemoveCondition(this.aCurrentStates[_loc4_]);
                  this.m_fMovesLeft = this.m_nMovesPerTurn + this.fMovesPerTurnModifier;
                  if(PlayState.m_objInstance.sprPlayer.PlayerCanSee(this))
                  {
                     if(this.fHoursSlept <= 1 && _loc3_ <= 1 - this.m_fSleepQuality)
                     {
                        this.MessageFloaty(this.Name + " 难以入睡.");
                     }
                     else
                     {
                        this.MessageFloaty(this.Name + " 醒来.");
                     }
                  }
               }
            }
            else if(PlayState.m_objInstance.sprPlayer.PlayerCanSee(this) && PlayState.m_objInstance.sprPlayer.Alive)
            {
               this.MessageFloaty(this.Name + ": ...zzz...");
            }
         }
         else if(!this.bAsleep)
         {
            this.bAsleep = true;
            this.fHoursSlept = 0;
            this.AddCondition(this.GetCondition(18));
            this.KnockDown = 1;
            this.UpdateStatus();
         }
      }
      
      public function PassTime(param1:Array, param2:Boolean) : void
      {
         if(param1 == null || param1.length < 2 || param2 == false)
         {
            return;
         }
         var _loc3_:Number = Number(param1[0]);
         var _loc4_:Boolean = DataHandler.StrToBoolean(param1[1]);
         PlayState.m_objInstance.EndDMTurn(_loc3_,_loc4_,false);
      }
      
      public function get ExitBattle() : int
      {
         return 0;
      }
      
      public function set ExitBattle(param1:int) : void
      {
         var _loc3_:PlayerCondition = null;
         if(this.m_tilCurrentHex.m_objBattle != null)
         {
            this.m_tilCurrentHex.m_objBattle.RemoveCreature(this);
         }
         this.m_objPair = null;
         this.m_objMove = null;
         this.m_objMoveLast = null;
         this.m_objTargetLast = null;
         DM.TeleportRange(this,this.m_tilCurrentHex,param1);
         this.AddCondition(this.GetCondition(376));
         var _loc2_:Array = this.aCurrentStates.concat();
         for each(_loc3_ in _loc2_)
         {
            if(_loc3_.m_bRemovePostCombat)
            {
               this.RemoveCondition(_loc3_);
            }
         }
         if(this is AICreature)
         {
            this.RemoveCondition(this.GetCondition(506));
         }
         _loc2_.length = 0;
         _loc2_ = null;
      }
      
      public function get KnockDown() : int
      {
         return 0;
      }
      
      public function set KnockDown(param1:int) : void
      {
         this.AddCondition(this.GetCondition(143),true,false);
         this.AddCondition(this.GetCondition(147),true,false);
      }
      
      public function get Resting() : Boolean
      {
         return this.m_bResting;
      }
      
      public function set Resting(param1:Boolean) : void
      {
         this.m_bResting = param1;
         PlayState.m_objInstance.btnRest.on = this.m_bResting;
         if(this.m_bResting)
         {
            this.AddCondition(this.GetCondition(185));
         }
         else
         {
            this.RemoveCondition(this.GetCondition(185));
         }
      }
      
      public function set MinSafeTemp(param1:Number) : void
      {
         this.fMinSafeTemp = param1;
         this.fAdjMinSafeTemp = this.fMinSafeTemp;
         var _loc2_:Weather = PlayState.m_objInstance.grpWeatherNode.objWeatherLast;
         if(_loc2_ != null)
         {
            this.fAdjMinSafeTemp += int(_loc2_.bPrecip) * (this.fWetTempAdjust + this.fWetTempAdjustMod);
         }
      }
      
      public function get MinSafeTemp() : Number
      {
         return this.fMinSafeTemp;
      }
      
      public function set MaxSafeTemp(param1:Number) : void
      {
         this.fMaxSafeTemp = param1;
         this.fAdjMaxSafeTemp = this.fMaxSafeTemp;
         var _loc2_:Weather = PlayState.m_objInstance.grpWeatherNode.objWeatherLast;
         if(_loc2_ != null)
         {
            this.fAdjMaxSafeTemp += int(_loc2_.bPrecip) * (this.fWetTempAdjust + this.fWetTempAdjustMod);
         }
      }
      
      public function get MaxSafeTemp() : Number
      {
         return this.fMaxSafeTemp;
      }
      
      public function set BodyInsulation(param1:Number) : void
      {
         this.fBodyInsulation = param1;
         this.fAdjBodyInsulation = Math.max(this.fBodyInsulation,0);
      }
      
      public function get BodyInsulation() : Number
      {
         return this.fBodyInsulation;
      }
      
      public function get WetTempAdjustMod() : Number
      {
         return this.fWetTempAdjustMod;
      }
      
      public function set WetTempAdjustMod(param1:Number) : void
      {
         this.fWetTempAdjustMod = param1;
         this.MinSafeTemp = this.MinSafeTemp;
         this.MaxSafeTemp = this.MaxSafeTemp;
      }
      
      public function set VisionRange(param1:Number) : void
      {
         this.nVisionRange = param1;
      }
      
      public function get VisionRange() : Number
      {
         return this.nVisionRange;
      }
      
      public function set BaseDetectionLevel(param1:Number) : void
      {
         this.m_fBaseDetectionLevel = param1;
      }
      
      public function get BaseDetectionLevel() : Number
      {
         return this.m_fBaseDetectionLevel;
      }
      
      public function set MinLightLevel(param1:Number) : void
      {
         this.fMinLightLevel = param1;
      }
      
      public function get MinLightLevel() : Number
      {
         return this.fMinLightLevel;
      }
      
      public function MessageFloaty(param1:String, param2:Boolean = true, param3:FlxPoint = null, param4:int = -1) : void
      {
         if(param4 == -1)
         {
            param4 = int(GUIMessageWindow.COLOR_DEFAULT);
         }
         if(param3 == null && members.length > 0)
         {
            param3 = FlxSprite(members[0]).getMidpoint();
            param3.y -= FlxSprite(members[0]).height / 2;
         }
         if(PlayState.m_objInstance.sprPlayer.PlayerCanSee(this))
         {
            PlayState.m_objInstance.grpMsg.MessageFloaty(param1,param2,param3,param4);
         }
      }
      
      public function set Encumberance(param1:Number) : void
      {
         this.m_fEncumberance = param1;
         this.UpdateStatus();
      }
      
      public function get Encumberance() : Number
      {
         return this.m_fEncumberance;
      }
      
      public function get WoundCut() : Number
      {
         return 0;
      }
      
      public function set WoundCut(param1:Number) : void
      {
         this.CauseWound(0,param1,0,"","");
      }
      
      public function get WoundBruise() : Number
      {
         return 0;
      }
      
      public function set WoundBruise(param1:Number) : void
      {
         this.CauseWound(param1,0,0,"","");
      }
      
      public function get AddRecipe() : int
      {
         return 0;
      }
      
      public function set AddRecipe(param1:int) : void
      {
      }
      
      public function GetCamp(param1:FlxHexTile = null) : ItemCamp
      {
         if(param1 == null)
         {
            param1 = this.m_tilCurrentHex;
         }
         var _loc2_:ItemInstance = this.grpCampSlot.SocketedItem();
         if(param1 != null && param1.m_vCampItems != null && param1.m_vCampItems.indexOf(_loc2_) >= 0)
         {
            return ItemCamp(_loc2_);
         }
         var _loc3_:Vector.<int> = this.m_dictCamps[param1.nMapIndex];
         if(_loc3_ == null || _loc3_.length == 0)
         {
            return param1.GetCampObject();
         }
         return param1.GetCampObject(_loc3_[_loc3_.length - 1]);
      }
      
      public function GetCampList(param1:FlxHexTile = null) : Vector.<ItemCamp>
      {
         var _loc4_:int = 0;
         if(param1 == null)
         {
            param1 = this.m_tilCurrentHex;
         }
         var _loc2_:Vector.<int> = this.m_dictCamps[param1.nMapIndex];
         var _loc3_:Vector.<ItemCamp> = new Vector.<ItemCamp>();
         if(_loc2_ == null)
         {
            _loc3_ = _loc3_.concat(param1.m_vCampItems);
            _loc2_ = new Vector.<int>();
            _loc4_ = 0;
            while(_loc4_ < _loc3_.length)
            {
               _loc2_.push(_loc4_);
               _loc4_++;
            }
            this.m_dictCamps[param1.nMapIndex] = _loc2_;
         }
         else
         {
            _loc4_ = 0;
            while(_loc4_ < _loc2_.length)
            {
               _loc3_.push(param1.GetCampObject(_loc2_[_loc4_]));
               _loc4_++;
            }
         }
         return _loc3_;
      }
      
      public function RememberCamp(param1:FlxHexTile, param2:ItemCamp, param3:Boolean = false) : void
      {
         var _loc8_:Vector.<int> = null;
         var _loc9_:int = 0;
         var _loc4_:int = int(param1.nMapIndex);
         var _loc5_:int = int(param1.m_vCampItems.indexOf(param2));
         if(this.m_dictCamps[_loc4_] == null)
         {
            _loc8_ = new Vector.<int>();
            _loc9_ = 0;
            while(_loc9_ < param1.m_vCampItems.length)
            {
               _loc8_.push(_loc9_);
               _loc9_++;
            }
            this.m_dictCamps[_loc4_] = _loc8_;
         }
         var _loc7_:int;
         var _loc6_:Vector.<int>;
         if((_loc7_ = int((_loc6_ = this.m_dictCamps[_loc4_]).indexOf(_loc5_))) < 0)
         {
            _loc6_.push(_loc5_);
         }
         else if(param3)
         {
            _loc6_.splice(_loc7_,1);
         }
      }
      
      public function SetCamp(param1:FlxHexTile, param2:ItemCamp) : void
      {
         var _loc7_:Vector.<int> = null;
         var _loc8_:int = 0;
         var _loc3_:int = int(param1.nMapIndex);
         var _loc4_:int = int(param1.m_vCampItems.indexOf(param2));
         if(this.m_dictCamps[_loc3_] == null)
         {
            _loc7_ = new Vector.<int>();
            _loc8_ = 0;
            while(_loc8_ < param1.m_vCampItems.length)
            {
               _loc7_.push(_loc8_);
               _loc8_++;
            }
            this.m_dictCamps[_loc3_] = _loc7_;
         }
         var _loc5_:Vector.<int> = this.m_dictCamps[_loc3_];
         this.m_tilCurrentHex.IsCampTile = true;
         var _loc6_:int = int(_loc5_.indexOf(_loc4_));
         _loc5_.push(_loc4_);
         if(_loc6_ >= 0)
         {
            _loc5_.splice(_loc6_,1);
         }
      }
      
      public function DropItem(param1:ItemInstance, param2:Boolean, param3:Boolean, param4:Vector.<int> = null) : ItemInstance
      {
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemCamp = null;
         if(param1 == null)
         {
            return param1;
         }
         if(param1.bSocketed)
         {
            param1 = param1.grpItemPanelSlot.UnSocketItem(param2,param1,param3);
            if(param1 == null)
            {
               return param1;
            }
         }
         else if(param1.Slot != null)
         {
            param1 = param1.Slot.RemoveItem(param1,param3);
            if(param1 == null)
            {
               return param1;
            }
         }
         else
         {
            param1.m_bEquipped = false;
            param1.SwapImage(false);
         }
         if(param4 == null)
         {
            param4 = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT]);
         }
         if(this.m_tilCurrentHex != null)
         {
            _loc5_ = PlayState.m_objInstance.grpInventoryUI.AddItemToCapBox(param1,this.m_tilCurrentHex.GroundObject,param4,true);
            for each(_loc6_ in this.m_tilCurrentHex.m_vCampItems)
            {
               if(_loc5_ == null)
               {
                  break;
               }
               _loc5_ = PlayState.m_objInstance.grpInventoryUI.AddItemToCapBox(_loc5_,_loc6_,param4,true);
            }
         }
         return _loc5_;
      }
      
      public function SetRes() : void
      {
         var _loc1_:int = GUIValues.GetInt("Item.zoom");
         if(GUIInventorySlot(this.m_dictSlots[2]).m_fZoom != _loc1_ || true)
         {
            GUIInventorySlot(this.m_dictSlots[2]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[3]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[4]).SetRes(_loc1_,"GUIInventory.Body","GUIInventory.Body.CapLegs");
            GUIInventorySlot(this.m_dictSlots[5]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[6]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[7]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[8]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[11]).SetRes(_loc1_,"GUIInventory.Body","GUIInventory.Body.CapTorso");
            GUIInventorySlot(this.m_dictSlots[12]).SetRes(_loc1_,"GUIInventory.Body","GUIInventory.Body.CapBelt");
            GUIInventorySlot(this.m_dictSlots[13]).SetRes(_loc1_,"GUIInventory.Body","GUIInventory.Body.CapLeftShoulder");
            GUIInventorySlot(this.m_dictSlots[14]).SetRes(_loc1_,"GUIInventory.Body","GUIInventory.Body.CapRightShoulder");
            GUIInventorySlot(this.m_dictSlots[17]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[22]).SetRes(_loc1_,"GUIInventory.Backpack","GUIInventory.Backpack.Cap");
            GUIInventorySlot(this.m_dictSlots[23]).SetRes(_loc1_,"GUIInventory.Neck");
            GUIInventorySlot(this.m_dictSlots[20]).SetRes(_loc1_,"GUIInventory.HoldLeft","GUIInventory.HoldLeft.Cap");
            GUIInventorySlot(this.m_dictSlots[21]).SetRes(_loc1_,"GUIInventory.HoldRight","GUIInventory.HoldRight.Cap");
            GUIInventorySlot(this.m_dictSlots[200]).SetRes(_loc1_,"GUIInventory.grpGroundSlot.Cap","GUIInventory.grpGroundSlot.Cap");
            GUIInventorySlot(this.m_dictSlots[208]).SetRes(_loc1_,"GUIInventory.grpCampSlot","GUIInventory.grpCampSlot.Cap");
            GUIInventorySlot(this.m_dictSlots[100]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[101]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[102]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[103]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[104]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[105]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[106]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[107]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[108]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[109]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[110]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[111]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[112]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[113]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[114]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[115]).SetRes(_loc1_,"GUIInventory.Body");
            GUIInventorySlot(this.m_dictSlots[116]).SetRes(_loc1_,"GUIInventory.Body");
         }
      }
      
      public function Despawn(param1:Array, param2:Boolean) : void
      {
         var _loc4_:Creature = null;
         var _loc6_:ItemInstance = null;
         var _loc7_:ItemInstance = null;
         var _loc3_:int = 0;
         if(param1 != null && param1.length == 1)
         {
            _loc3_ = int(param1[0]);
         }
         if(_loc3_ == 0 && (this.Alive == false || this.m_tilCurrentHex != null && this.m_tilCurrentHex.m_objBattle != null || this.m_tilCurrentHex == PlayState.m_objInstance.tilCurrentHex || PlayState.m_objInstance.sprPlayer.PlayerCanSee(this)))
         {
            return;
         }
         if(this.m_bDespawnQueue == false)
         {
            this.m_bDespawnQueue = true;
            return;
         }
         this.m_bDespawnQueue = false;
         if(this.m_tilCurrentHex != null && this.m_tilCurrentHex.m_objBattle != null)
         {
            this.m_tilCurrentHex.m_objBattle.RemoveCreature(this);
         }
         this.m_tilCurrentHex.RemoveCreature(this);
         if(this.m_tilCurrentHex.m_vOccupants.length > 0)
         {
            _loc4_ = this.m_tilCurrentHex.m_vOccupants[0];
         }
         var _loc5_:Vector.<ItemInstance> = this.grpGroundSlot.GetAllSocketedItems();
         for each(_loc7_ in _loc5_)
         {
            _loc6_ = this.grpGroundSlot.UnSocketItem(true,_loc7_);
            if(_loc4_ != null && _loc6_ != null)
            {
               _loc4_.grpGroundSlot.SocketItem(_loc6_);
               _loc6_ = null;
            }
         }
         _loc5_ = this.grpCampSlot.GetAllSocketedItems();
         for each(_loc7_ in _loc5_)
         {
            _loc6_ = this.grpCampSlot.UnSocketItem(true,_loc7_);
            if(_loc4_ != null && _loc6_ != null)
            {
               _loc4_.grpCampSlot.SocketItem(_loc6_);
               _loc6_ = null;
            }
         }
         PlayState.m_objInstance.RemoveCreature(AICreature(this));
      }
      
      public function ArmorWound(param1:Array, param2:Boolean) : void
      {
         if(param1 == null || param1.length < 3)
         {
            return;
         }
         var _loc3_:int = int(param1[0]);
         var _loc4_:Number = Number(param1[1]);
         var _loc5_:Number = Number(param1[2]);
         if(this.m_dictSlots[_loc3_] == undefined)
         {
            return;
         }
         var _loc6_:Number = -1;
         if(param2)
         {
            _loc6_ = 1;
         }
         GUIInventoryWound(this.m_dictSlots[_loc3_]).m_fCutArmor = GUIInventoryWound(this.m_dictSlots[_loc3_]).m_fCutArmor + _loc6_ * parseFloat(param1[1]);
         GUIInventoryWound(this.m_dictSlots[_loc3_]).m_fBluntArmor = GUIInventoryWound(this.m_dictSlots[_loc3_]).m_fBluntArmor + _loc6_ * parseFloat(param1[2]);
      }
      
      public function ChangeFactionRep(param1:Array, param2:Boolean, param3:Boolean = true) : void
      {
         var _loc7_:Creature = null;
         var _loc4_:Number;
         if((_loc4_ = Number(param1[1])) == 0)
         {
            return;
         }
         var _loc5_:int = int(param1[0]);
         var _loc6_:Number = -1;
         if(param2)
         {
            _loc6_ = 1;
         }
         if(param3)
         {
            for each(_loc7_ in this.m_tilCurrentHex.m_vOccupants)
            {
               if(_loc7_.m_nFaction == this.m_nFaction)
               {
                  if(_loc7_.m_dictFactions[_loc5_] == undefined)
                  {
                     _loc7_.m_dictFactions[_loc5_] = _loc7_.GetFactionRep(_loc5_);
                  }
                  _loc7_.m_dictFactions[_loc5_] += _loc6_ * _loc4_;
               }
            }
         }
         else
         {
            if(this.m_dictFactions[_loc5_] == undefined)
            {
               this.m_dictFactions[_loc5_] = this.GetFactionRep(_loc5_);
            }
            this.m_dictFactions[_loc5_] += _loc6_ * _loc4_;
         }
      }
      
      public function AddItemGround(param1:Array, param2:Boolean) : void
      {
         var _loc9_:ItemInstance = null;
         if(param2 == false || param1 == null || param1.length <= 3)
         {
            return;
         }
         var _loc3_:int = int(param1[0]);
         var _loc4_:Boolean = DataHandler.StrToBoolean(param1[1]);
         var _loc5_:Boolean = DataHandler.StrToBoolean(param1[2]);
         var _loc6_:Boolean = DataHandler.StrToBoolean(param1[3]);
         var _loc7_:Vector.<ItemInstance> = DataHandler.GetTreasure(_loc3_).GenerateTreasure(_loc4_,_loc5_,_loc6_);
         var _loc8_:int = 0;
         while(_loc8_ < _loc7_.length)
         {
            _loc9_ = _loc7_[_loc8_];
            this.DropItem(_loc9_,false,true);
            _loc9_.CreateAppearance();
            _loc8_++;
         }
      }
      
      public function SetImmunity(param1:Array, param2:Boolean) : void
      {
         var _loc3_:int = 0;
         if(param1 == null || param1.length < 1)
         {
            return;
         }
         for each(_loc3_ in param1)
         {
            if(_loc3_ != 0)
            {
               if(param2 == false)
               {
                  _loc3_ = -_loc3_;
               }
               if(_loc3_ > 0 && this.m_vImmunities.indexOf(_loc3_) < 0)
               {
                  this.m_vImmunities.push(_loc3_);
               }
               else if(_loc3_ < 0 && this.m_vImmunities.indexOf(-_loc3_) >= 0)
               {
                  this.m_vImmunities.splice(this.m_vImmunities.indexOf(-_loc3_),1);
               }
            }
         }
      }
      
      public function ChainCondition(param1:Array, param2:Boolean) : void
      {
         var _loc3_:int = 0;
         if(param1 == null || param1.length < 1)
         {
            return;
         }
         for each(_loc3_ in param1)
         {
            if(_loc3_ != 0)
            {
               if(param2 == false)
               {
                  _loc3_ = -_loc3_;
               }
               if(_loc3_ > 0)
               {
                  this.AddCondition(this.GetCondition(_loc3_));
               }
               else
               {
                  this.RemoveCondition(this.GetCondition(-_loc3_));
               }
            }
         }
      }
      
      public function GetFactionRep(param1:int) : Number
      {
         if(this.m_dictFactions[param1] != undefined)
         {
            return this.m_dictFactions[param1];
         }
         if(this.HasCondition(151) == false)
         {
            return -100;
         }
         return 0;
      }
      
      public function get HealPerHour() : Number
      {
         return this.m_fHealPerHour + this.m_fHealPerHourMod;
      }
      
      public function get JustMoved() : int
      {
         return 0;
      }
      
      public function set JustMoved(param1:int) : void
      {
         this.AddCondition(this.GetCondition(98),false);
      }
      
      public function get fSleepQuality() : Number
      {
         return this.m_fSleepQuality;
      }
      
      public function set fSleepQuality(param1:Number) : void
      {
         this.m_fSleepQuality = param1;
      }
      
      public function get AttDmgMult() : Number
      {
         return this.m_fAttDmgMult;
      }
      
      public function set AttDmgMult(param1:Number) : void
      {
         this.m_fAttDmgMult = param1;
      }
      
      public function get DefDmgMult() : Number
      {
         return this.m_fDefDmgMult;
      }
      
      public function set DefDmgMult(param1:Number) : void
      {
         this.m_fDefDmgMult = param1;
      }
      
      public function get Alive() : Boolean
      {
         return this.m_bAlive;
      }
      
      public function set Alive(param1:Boolean) : void
      {
         if(this.m_bAlive)
         {
            if(this.m_tilCurrentHex != null && this.m_tilCurrentHex.m_objBattle != null)
            {
               this.KillCreature("","","");
            }
            else
            {
               this.m_bKillQueue = true;
            }
         }
      }
      
      public function get Crippled() : int
      {
         return 0;
      }
      
      public function set Crippled(param1:int) : void
      {
         var _loc2_:GUIInventorySlot = null;
         var _loc3_:ItemInstance = null;
         this.m_dictCrippled[Math.abs(param1)] = param1 > 0;
         for each(_loc2_ in this.vInvCategories)
         {
            if(_loc2_.bHoldSlot)
            {
               if(Math.abs(param1) == this.CRIPPLED_LEFTARM && _loc2_.nSlotIndex == 20)
               {
                  _loc3_ = _loc2_.SocketedItem();
                  this.DropItem(_loc3_,true,true);
                  if(this.m_dictCrippled[this.CRIPPLED_LEFTARM])
                  {
                     _loc2_.SocketItem(DataHandler.GetItem("20.1"));
                  }
               }
               else if(Math.abs(param1) == this.CRIPPLED_RIGHTARM && _loc2_.nSlotIndex == 21)
               {
                  _loc3_ = _loc2_.SocketedItem();
                  this.DropItem(_loc3_,true,true);
                  if(this.m_dictCrippled[this.CRIPPLED_RIGHTARM])
                  {
                     _loc2_.SocketItem(DataHandler.GetItem("20.1"));
                  }
               }
            }
         }
         if(Boolean(this.m_dictCrippled[this.CRIPPLED_LEFTARM]) && Boolean(this.m_dictCrippled[this.CRIPPLED_RIGHTARM]) && !this.HasCondition(188))
         {
            this.AddCondition(this.GetCondition(188));
         }
         else if((!this.m_dictCrippled[this.CRIPPLED_LEFTARM] || !this.m_dictCrippled[this.CRIPPLED_RIGHTARM]) && this.HasCondition(188))
         {
            this.RemoveCondition(this.GetCondition(188));
         }
         if(Boolean(this.m_dictCrippled[this.CRIPPLED_LEFTLEG]) && Boolean(this.m_dictCrippled[this.CRIPPLED_RIGHTLEG]) && !this.HasCondition(192))
         {
            this.AddCondition(this.GetCondition(192));
         }
         else if((!this.m_dictCrippled[this.CRIPPLED_LEFTLEG] || !this.m_dictCrippled[this.CRIPPLED_RIGHTLEG]) && this.HasCondition(192))
         {
            this.RemoveCondition(this.GetCondition(192));
         }
         if((Boolean(this.m_dictCrippled[this.CRIPPLED_LEFTLEG]) || Boolean(this.m_dictCrippled[this.CRIPPLED_RIGHTLEG])) && !this.HasCondition(477))
         {
            this.AddCondition(this.GetCondition(477));
         }
         else if(!this.m_dictCrippled[this.CRIPPLED_LEFTLEG] && !this.m_dictCrippled[this.CRIPPLED_RIGHTLEG] && this.HasCondition(477))
         {
            this.RemoveCondition(this.GetCondition(477));
         }
      }
      
      public function get CleanAndDress() : int
      {
         return 0;
      }
      
      public function set CleanAndDress(param1:int) : void
      {
         var _loc3_:Number = NaN;
         var _loc4_:GUIInventoryWound = null;
         var _loc5_:ItemInstance = null;
         var _loc2_:Vector.<GUIInventoryWound> = this.m_vAllWoundSlots.concat();
         if(param1 >= 0)
         {
            _loc2_ = Vector.<GUIInventoryWound>([this.m_dictSlots[param1]]);
         }
         for each(_loc4_ in _loc2_)
         {
            _loc3_ = Number(_loc4_.m_fCutSeverity);
            _loc4_.m_fCutSeverity = 0;
            _loc4_.UnSocketItem();
            _loc4_.m_fBleedRate = 0;
            _loc4_.m_fInfectRate = 0;
            _loc4_.m_fCutSeverity = _loc3_;
            if(_loc4_.m_fBluntSeverity >= _loc4_.m_fFractureMin)
            {
               _loc5_ = DataHandler.GetItem("22.0");
               _loc4_.SocketItem(_loc5_);
            }
            _loc4_.UpdateImage();
         }
      }
      
      public function get Threat() : int
      {
         return 0;
      }
      
      public function set Threat(param1:int) : void
      {
         this.m_fMorale += param1 * this.CurrentAttackMode.m_fMorale;
      }
      
      public function get TriggerEncounter() : int
      {
         return 0;
      }
      
      public function set TriggerEncounter(param1:int) : void
      {
      }
      
      public function get LoseRandomItem() : int
      {
         return 0;
      }
      
      public function set LoseRandomItem(param1:int) : void
      {
         var _loc5_:GUIInventorySlot = null;
         var _loc6_:ItemInstance = null;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:int = 0;
         var _loc11_:ItemInstance = null;
         var _loc2_:Vector.<int> = Vector.<int>([4,11,13,14,20,21,22,207]);
         var _loc3_:uint = _loc2_.length;
         var _loc4_:uint = 0;
         while(_loc4_ < _loc3_)
         {
            _loc9_ = _loc2_[_loc4_];
            _loc10_ = int(uint(Math.random() * (_loc3_ - _loc4_)));
            _loc2_[_loc4_] = _loc2_[_loc4_ + _loc10_];
            _loc2_[_loc10_] = _loc9_;
            _loc4_++;
         }
         var _loc7_:Boolean = false;
         for each(_loc8_ in _loc2_)
         {
            if((_loc6_ = (_loc5_ = this.m_dictSlots[_loc8_]).SocketedItem()) != null)
            {
               if(_loc6_.vItems.length > 0)
               {
                  _loc11_ = _loc6_.vItems[Math.floor(Math.random() * _loc6_.vItems.length)];
                  if(param1 < 0 || _loc6_.ItemDefinition.m_vProperties.indexOf(param1) >= 0)
                  {
                     _loc5_.RemoveItem(_loc11_);
                     _loc7_ = true;
                     this.MessageFloaty(this.Name + " 丢失了 " + _loc11_.strDesc + "!");
                  }
               }
               else if(_loc8_ != 4 && _loc8_ != 11)
               {
                  if(param1 < 0 || _loc6_.ItemDefinition.m_vProperties.indexOf(param1) >= 0)
                  {
                     _loc5_.UnSocketItem();
                     _loc7_ = true;
                     this.MessageFloaty(this.Name + " 丢失了 " + _loc6_.strDesc + "!");
                  }
               }
               if(_loc7_)
               {
                  return;
               }
            }
         }
      }
      
      public function get SpawnNewCreature() : int
      {
         return 0;
      }
      
      public function set SpawnNewCreature(param1:int) : void
      {
         var _loc4_:FlxHexTile = null;
         var _loc5_:Creature = null;
         var _loc6_:AICreature = null;
         var _loc2_:int = 0;
         var _loc3_:Vector.<FlxHexTile> = MapUtils.GetHexRing(this.m_tilCurrentHex.GetHexCoords(),3);
         _loc3_.push(this.m_tilCurrentHex);
         for each(_loc4_ in _loc3_)
         {
            if(_loc4_ != null)
            {
               for each(_loc5_ in _loc4_.m_vOccupants)
               {
                  if(_loc5_.m_nFaction == this.m_nFaction)
                  {
                     _loc2_++;
                  }
               }
            }
         }
         if(_loc2_ < DM.m_nLocalFactionPopCap && PlayState.m_objInstance.m_aCreatures.length < DM.m_nGlobalPopCap)
         {
            if((_loc6_ = DataHandler.GetCreature(param1)) != null)
            {
               PlayState.m_objInstance.AddCreature(_loc6_,this.m_tilCurrentHex.GetHexCoords());
            }
         }
      }
      
      public function get LoseAllItems() : int
      {
         return 0;
      }
      
      public function set LoseAllItems(param1:int) : void
      {
         var _loc2_:GUIInventorySlot = null;
         var _loc3_:ItemInstance = null;
         for each(_loc2_ in this.vInvCategories)
         {
            if(!(param1 >= 0 && _loc2_.nSlotIndex != param1))
            {
               _loc3_ = _loc2_.SocketedItem();
               if(_loc3_ != null)
               {
                  _loc2_.UnSocketItem();
                  if(this is Player)
                  {
                     this.MessageFloaty(this.Name + " 丢失了 " + _loc3_.strDesc + "!");
                  }
               }
            }
         }
      }
      
      public function get EmptyGroundSlot() : int
      {
         return 0;
      }
      
      public function set EmptyGroundSlot(param1:int) : void
      {
         var _loc2_:uint = this.m_tilCurrentHex.m_nBarterTile;
         this.m_tilCurrentHex.m_nBarterTile = BarterHex.BARTER_NONE;
         var _loc3_:ItemInstance = this.grpGroundSlot.SocketedItem();
         if(_loc3_ != null)
         {
            PlayState.m_objInstance.grpInventoryUI.TransferItemContents(_loc3_);
         }
         _loc3_ = null;
         this.m_tilCurrentHex.m_nBarterTile = _loc2_;
      }
      
      public function get DropAllItems() : int
      {
         return 0;
      }
      
      public function set DropAllItems(param1:int) : void
      {
         var _loc2_:GUIInventorySlot = null;
         var _loc3_:ItemInstance = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:Vector.<GUIInventorySlot> = this.vInvCategories.concat(this.m_vAllWoundSlots);
         var _loc6_:Boolean = false;
         for each(_loc2_ in _loc5_)
         {
            if(!(param1 >= 0 && _loc2_.nSlotIndex != param1))
            {
               _loc4_ = (_loc4_ = _loc2_.GetAllSocketedItems()).reverse();
               for each(_loc3_ in _loc4_)
               {
                  if(_loc3_.ItemDefinition.m_vProperties.indexOf(85) < 0)
                  {
                     if(this.DropItem(_loc3_,true,true) == null)
                     {
                        _loc6_ = true;
                     }
                  }
               }
            }
         }
         if(_loc6_)
         {
            this.m_tilCurrentHex.CalculateValue();
         }
      }
      
      public function get LootTarget() : int
      {
         return 0;
      }
      
      public function set LootTarget(param1:int) : void
      {
         var _loc2_:Creature = null;
         if(this.m_objPair != null)
         {
            _loc2_ = this.m_objPair.sprThem;
         }
         if(_loc2_ == null)
         {
            return;
         }
         _loc2_.DropAllItems = -1;
         this.m_tilCurrentHex.CalculateValue();
      }
      
      public function get BattleRange() : Number
      {
         return 0;
      }
      
      public function set BattleRange(param1:Number) : void
      {
         var _loc4_:CombatPair = null;
         var _loc5_:int = 0;
         if(this.m_tilCurrentHex.m_objBattle == null)
         {
            return;
         }
         var _loc2_:Number = DM.Rand(DM.RAND_FLAT);
         if(param1 > 0.66)
         {
            _loc2_ = DM.Rand(DM.RAND_HIGH);
         }
         else if(param1 > 0.33)
         {
            _loc2_ = DM.Rand(DM.RAND_MID);
         }
         else if(param1 > 0)
         {
            _loc2_ = DM.Rand(DM.RAND_LOW);
         }
         var _loc3_:int = this.m_tilCurrentHex.m_nMinRange + _loc2_ * (this.m_tilCurrentHex.m_nMaxRange - this.m_tilCurrentHex.m_nMinRange);
         for each(_loc4_ in this.m_dictCombatPairs)
         {
            _loc5_ = _loc3_ - _loc4_.nRange;
            _loc4_.ChangeRange += _loc5_;
         }
      }
      
      public function get x() : Number
      {
         if(members.length > 0)
         {
            return FlxObject(members[0]).x;
         }
         return 0;
      }
      
      public function set x(param1:Number) : void
      {
         var _loc2_:FlxSprite = null;
         for each(_loc2_ in members)
         {
            _loc2_.x = param1;
         }
      }
      
      public function get y() : Number
      {
         if(members.length > 0)
         {
            return FlxObject(members[0]).y;
         }
         return 0;
      }
      
      public function set y(param1:Number) : void
      {
         var _loc2_:FlxSprite = null;
         for each(_loc2_ in members)
         {
            _loc2_.y = param1;
         }
      }
      
      public function get SaveData() : SaveGameCreature
      {
         var _loc3_:PlayerCondition = null;
         var _loc4_:GUIInventoryWound = null;
         var _loc5_:Vector.<GUIInventorySlot> = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc7_:Vector.<int> = null;
         var _loc8_:String = null;
         var _loc9_:Object = null;
         var _loc10_:SaveGameCondition = null;
         var _loc11_:SaveGameWound = null;
         var _loc12_:Vector.<ItemInstance> = null;
         var _loc13_:ItemInstance = null;
         var _loc1_:SaveGameCreature = new SaveGameCreature();
         _loc1_.bAsleep = this.bAsleep;
         _loc1_.fCoreTemp = this.fCoreTemp;
         _loc1_.fFoodDebt = this.fFoodDebt;
         _loc1_.fSleepDebt = this.fSleepDebt;
         _loc1_.fWaterDebt = this.fWaterDebt;
         _loc1_.m_fBloodLeft = this.m_fBloodLeft;
         _loc1_.m_fImmuneLeft = this.m_fImmuneLeft;
         _loc1_.m_fPainLeft = this.m_fPainLeft;
         _loc1_.m_nAttackMode = this.m_nAttackMode;
         _loc1_.m_fMovesLeft = this.m_fMovesLeft;
         _loc1_.m_fMoveReserveRemaining = this.m_fMoveReserveRemaining;
         _loc1_.m_fMoveCost = this.m_fMoveCost;
         _loc1_.fHoursSlept = this.fHoursSlept;
         _loc1_.m_dictCamps = new Dictionary();
         _loc1_.m_nMorality = this.m_nMorality;
         _loc1_.m_fLeader = this.m_fLeader;
         if(this.m_objLocker != null)
         {
            _loc1_.m_objLocker = this.m_objLocker.SaveData;
         }
         var _loc2_:ItemCamp = this.GetCamp();
         this.grpCampSlot.UnSocketItem(true);
         if(this.aCurrentStates.indexOf(_loc2_.Condition) >= 0)
         {
            this.RemoveCondition(_loc2_.Condition);
         }
         _loc1_.vCurrentStates = new Vector.<SaveGameCondition>();
         for each(_loc3_ in this.aCurrentStates)
         {
            (_loc10_ = new SaveGameCondition()).m_fDate = _loc3_.m_objDate.getTime();
            _loc10_.m_nID = _loc3_.m_nID;
            _loc10_.m_nStacked = _loc3_.m_nStacked;
            _loc1_.vCurrentStates.push(_loc10_);
         }
         _loc1_.vAllWoundSlots = new Vector.<SaveGameWound>();
         for each(_loc4_ in this.m_vAllWoundSlots)
         {
            _loc11_ = new SaveGameWound(_loc4_);
            _loc1_.vAllWoundSlots.push(_loc11_);
         }
         _loc1_.m_vItems = new Vector.<SaveGameItem>();
         _loc5_ = this.vInvCategories.concat(this.m_vAllWoundSlots);
         for each(_loc6_ in _loc5_)
         {
            _loc12_ = _loc6_.GetAllSocketedItems();
            for each(_loc13_ in _loc12_)
            {
               _loc1_.m_vItems.push(_loc13_.SaveData);
            }
         }
         for(_loc9_ in this.m_dictCamps)
         {
            _loc8_ = String(_loc9_);
            _loc7_ = this.m_dictCamps[_loc9_];
            _loc1_.m_dictCamps[_loc8_] = _loc7_.concat();
         }
         for(_loc9_ in this.m_dictFactions)
         {
            _loc1_.m_vFactions.push(this.m_dictFactions[_loc9_]);
         }
         this.grpCampSlot.SocketItem(_loc2_);
         return _loc1_;
      }
      
      public function set SaveData(param1:SaveGameCreature) : void
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc4_:SaveGameItem = null;
         var _loc5_:GUIInventoryWound = null;
         var _loc6_:PlayerCondition = null;
         var _loc8_:SaveGameCondition = null;
         var _loc9_:Vector.<ItemInstance> = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:int = 0;
         var _loc12_:String = null;
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = true;
         var _loc2_:Vector.<GUIInventorySlot> = this.vInvCategories.concat(this.m_vAllWoundSlots);
         for each(_loc3_ in _loc2_)
         {
            _loc9_ = _loc3_.GetAllSocketedItems();
            for each(_loc10_ in _loc9_)
            {
               _loc3_.UnSocketItem(true,_loc10_);
            }
         }
         for each(_loc4_ in param1.m_vItems)
         {
            if((_loc10_ = DataHandler.GetItem(_loc4_.strID,false)) != null)
            {
               _loc10_.SaveData = _loc4_;
               _loc3_ = this.m_dictSlots[_loc4_.m_nSlotIndex];
               _loc3_.SocketItem(_loc10_);
            }
         }
         _loc11_ = 0;
         while(_loc11_ < this.m_vAllWoundSlots.length)
         {
            (_loc5_ = this.m_vAllWoundSlots[_loc11_]).SaveData = param1.vAllWoundSlots[_loc11_];
            _loc5_.AlignImageToSocket(_loc5_.SocketedItem());
            _loc11_++;
         }
         var _loc7_:Array = this.aCurrentStates.concat();
         for each(_loc6_ in _loc7_)
         {
            _loc11_ = int(_loc6_.m_nStacked);
            while(_loc11_ > 0)
            {
               this.RemoveCondition(_loc6_);
               _loc11_--;
            }
         }
         for each(_loc8_ in param1.vCurrentStates)
         {
            _loc6_ = this.GetCondition(_loc8_.m_nID);
            if(this.Name == "Player")
            {
            }
            _loc6_.m_objDate = new Date();
            _loc11_ = _loc6_.m_nStacked;
            while(_loc11_ < _loc8_.m_nStacked)
            {
               this.AddCondition(_loc6_);
               _loc11_++;
            }
            _loc6_.m_objDate.setTime(_loc8_.m_fDate);
         }
         this.bAsleep = param1.bAsleep;
         this.fCoreTemp = param1.fCoreTemp;
         this.fFoodDebt = param1.fFoodDebt;
         this.fSleepDebt = param1.fSleepDebt;
         this.fWaterDebt = param1.fWaterDebt;
         this.m_fBloodLeft = param1.m_fBloodLeft;
         this.m_fImmuneLeft = param1.m_fImmuneLeft;
         this.m_fPainLeft = param1.m_fPainLeft;
         this.m_nAttackMode = param1.m_nAttackMode;
         this.m_fMovesLeft = param1.m_fMovesLeft;
         this.m_fMoveReserveRemaining = param1.m_fMoveReserveRemaining;
         this.m_fMoveCost = param1.m_fMoveCost;
         this.fHoursSlept = param1.fHoursSlept;
         if(param1.m_dictCamps != null)
         {
            for(_loc12_ in param1.m_dictCamps)
            {
               this.m_dictCamps[parseInt(_loc12_)] = param1.m_dictCamps[_loc12_];
            }
         }
         if(param1.m_vFactions != null)
         {
            _loc11_ = 0;
            while(_loc11_ < param1.m_vFactions.length)
            {
               this.m_dictFactions[_loc11_] = param1.m_vFactions[_loc11_];
               _loc11_++;
            }
         }
         this.m_nMorality = param1.m_nMorality;
         this.m_fLeader = param1.m_fLeader;
         if(param1.m_objLocker != null)
         {
            this.m_objLocker = DataHandler.GetItem(param1.m_objLocker.strID,false);
            this.m_objLocker.SaveData = param1.m_objLocker;
         }
         this.UpdateStatus();
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = false;
      }
   }
}

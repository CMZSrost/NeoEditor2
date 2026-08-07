package
{
   import org.flixel.*;
   
   public class BattleMove
   {
      
      public static const BOOL_FALSE:uint = 0;
      
      public static const BOOL_TRUE:uint = 1;
      
      public static const BOOL_NA:uint = 2;
      
      public static const CHANCE_ALWAYS:uint = 0;
      
      public static const CHANCE_ATTACK_MELEE:uint = 1;
      
      public static const CHANCE_ATTACK_RANGED:uint = 2;
      
      public static const CHANCE_TRIP_NORMAL:uint = 3;
      
      public static const CHANCE_TRIP_CUSTOM:uint = 4;
      
      public static const CHANCE_MORALE:uint = 5;
      
      public static const CHANCE_FLAT:uint = 6;
      
      public static const CHANCE_RETREAT:uint = 7;
       
      
      private var m_objItem:ItemInstance;
      
      public var m_strID:String;
      
      public var m_strName:String;
      
      public var m_strSuccess:String;
      
      public var m_strFail:String;
      
      public var m_vChanceType:Vector.<Number>;
      
      public var m_vUsConditions:Vector.<Vector.<Number>>;
      
      public var m_vThemConditions:Vector.<Vector.<Number>>;
      
      public var m_vPairConditions:Vector.<Vector.<Number>>;
      
      public var m_vUsFailConditions:Vector.<Vector.<Number>>;
      
      public var m_vThemFailConditions:Vector.<Vector.<Number>>;
      
      public var m_vPairFailConditions:Vector.<Vector.<Number>>;
      
      public var m_vUsPreConditions:Vector.<int>;
      
      public var m_vThemPreConditions:Vector.<int>;
      
      public var m_nSeeThem:uint;
      
      public var m_nSeeUs:uint;
      
      public var m_bAllOutOfRange:Boolean;
      
      public var m_bInAttackRange:Boolean;
      
      public var m_nMinCharges:int;
      
      public var m_nMinRange:int;
      
      public var m_nMaxRange:int;
      
      public var m_nAttackModeType:int;
      
      public var m_vHexTypes:Vector.<int>;
      
      public var m_fChance:Number;
      
      public var m_fPriority:Number;
      
      public var m_fDetect:Number;
      
      public var m_fOrder:Number;
      
      public var m_fFatigue:Number;
      
      public var m_bApproach:Boolean;
      
      public var m_bOffense:Boolean;
      
      public var m_bFallBack:Boolean;
      
      public var m_bRetreat:Boolean;
      
      public var m_bPosition:Boolean;
      
      public var m_bPassive:Boolean;
      
      public var m_strPopUp:String;
      
      public function BattleMove(param1:String, param2:String, param3:String, param4:Vector.<Number>)
      {
         super();
         this.m_strID = param1;
         this.m_strName = param2;
         this.m_strSuccess = param3;
         this.m_strFail = "";
         this.m_vChanceType = param4;
         this.m_vUsConditions = new Vector.<Vector.<Number>>();
         this.m_vThemConditions = new Vector.<Vector.<Number>>();
         this.m_vPairConditions = new Vector.<Vector.<Number>>();
         this.m_vUsFailConditions = new Vector.<Vector.<Number>>();
         this.m_vThemFailConditions = new Vector.<Vector.<Number>>();
         this.m_vPairFailConditions = new Vector.<Vector.<Number>>();
         this.m_vUsPreConditions = new Vector.<int>();
         this.m_vThemPreConditions = new Vector.<int>();
         this.m_nSeeThem = BOOL_NA;
         this.m_nSeeUs = BOOL_NA;
         this.m_bAllOutOfRange = false;
         this.m_bInAttackRange = false;
         this.m_nMinCharges = 0;
         this.m_nMinRange = -1;
         this.m_nMaxRange = -1;
         this.m_nAttackModeType = -1;
         this.m_vHexTypes = new Vector.<int>();
         this.m_fChance = 1;
         this.m_fPriority = 0;
         this.m_fDetect = 1;
         this.m_fOrder = 0.5;
         this.m_fFatigue = 0;
         this.m_bApproach = false;
         this.m_bOffense = false;
         this.m_bFallBack = false;
         this.m_bRetreat = false;
         this.m_bPosition = false;
         this.m_bPassive = false;
         this.m_strPopUp = "";
      }
      
      public function Execute(param1:Battle, param2:CombatPair) : void
      {
         var _loc6_:Vector.<Vector.<Number>> = null;
         var _loc7_:Vector.<Vector.<Number>> = null;
         var _loc8_:Vector.<Vector.<Number>> = null;
         var _loc12_:Vector.<Number> = null;
         var _loc13_:PlayerCondition = null;
         var _loc14_:Number = NaN;
         var _loc15_:* = null;
         var _loc3_:uint = GUIMessageWindow.COLOR_DEFAULT;
         if(this.m_bOffense)
         {
            param2.sprThem.ForceAwake();
         }
         var _loc4_:Boolean = PlayState.m_objInstance.sprPlayer.PlayerCanSee(param2.sprUs);
         var _loc5_:Boolean = PlayState.m_objInstance.sprPlayer.PlayerCanSee(param2.sprThem);
         var _loc9_:String = "未知敌人";
         if(_loc4_)
         {
            _loc9_ = param2.sprUs.Name;
         }
         var _loc10_:String = "未知目标";
         if(_loc5_)
         {
            _loc10_ = param2.sprThem.Name;
         }
         param2.sprUs.m_objMoveLast = this;
         param2.sprUs.m_objTargetLast = param2.sprThem;
         if(this.Succeeds(this.m_vChanceType,param2))
         {
            param2.m_strVerb = this.m_strSuccess;
            _loc6_ = this.m_vUsConditions;
            _loc7_ = this.m_vThemConditions;
            _loc8_ = this.m_vPairConditions;
         }
         else
         {
            if(this.m_strFail != "")
            {
               param2.m_strVerb = this.m_strFail;
            }
            else
            {
               param2.m_strVerb = this.m_strSuccess;
            }
            _loc6_ = this.m_vUsFailConditions;
            _loc7_ = this.m_vThemFailConditions;
            _loc8_ = this.m_vPairFailConditions;
         }
         param2.m_strVerb = param2.m_strVerb.replace(/<us>/gi,_loc9_);
         param2.m_strVerb = param2.m_strVerb.replace(/<them>/gi,_loc10_);
         if(_loc4_)
         {
            param2.sprUs.MessageFloaty(param2.m_strVerb,false,null,_loc3_);
         }
         else if(_loc5_ && this.m_bOffense)
         {
            param2.sprThem.MessageFloaty(param2.m_strVerb,false,null,_loc3_);
         }
         var _loc11_:Boolean = false;
         for each(_loc12_ in _loc6_)
         {
            if(this.Succeeds(_loc12_,param2))
            {
               if(_loc12_[0] > 0)
               {
                  param2.sprUs.AddCondition(param2.sprUs.GetCondition(_loc12_[0]),true,false);
               }
               else
               {
                  param2.sprUs.RemoveCondition(param2.sprUs.GetCondition(-_loc12_[0]));
               }
            }
         }
         for each(_loc12_ in _loc7_)
         {
            if(this.Succeeds(_loc12_,param2))
            {
               if(_loc12_[0] > 0)
               {
                  param2.sprThem.AddCondition(param2.sprThem.GetCondition(_loc12_[0]),true,false);
               }
               else
               {
                  param2.sprThem.RemoveCondition(param2.sprThem.GetCondition(-_loc12_[0]));
               }
            }
         }
         for each(_loc12_ in _loc8_)
         {
            if(this.Succeeds(_loc12_,param2))
            {
               _loc13_ = DataHandler.GetCondition(Math.abs(_loc12_[0]));
               if(_loc12_[0] > 0)
               {
                  _loc13_.ApplyConditionEffects(param2);
               }
               else
               {
                  _loc13_.RemoveConditionEffects(param2);
               }
            }
         }
         param2.sprUs.fSleepDebt += this.m_fFatigue * param2.sprUs.m_fFatigueModifier;
         if(!param2.UsSpotted)
         {
            _loc14_ = DM.Rand(DM.RAND_FLAT);
            if(param2.sprThem.Asleep && _loc14_ / this.m_fDetect <= param2.sprThem.m_fSleepAwareness)
            {
               _loc15_ = _loc9_ + "的行动把你 " + _loc10_ + " 惊醒了！";
               if(_loc4_)
               {
                  param2.sprUs.MessageFloaty(_loc15_,false,null,_loc3_);
               }
               else if(_loc5_)
               {
                  param2.sprThem.MessageFloaty(_loc15_,false,null,_loc3_);
               }
               param2.sprThem.ForceAwake();
            }
         }
      }
      
      public function Succeeds(param1:Vector.<Number>, param2:CombatPair) : Boolean
      {
         var _loc3_:* = false;
         switch(param1[1])
         {
            case CHANCE_ATTACK_MELEE:
               _loc3_ = param2.CheckAttack(AttackMode.ATTACK_TYPE_MELEE,param1[2]);
               break;
            case CHANCE_ATTACK_RANGED:
               _loc3_ = param2.CheckAttack(AttackMode.ATTACK_TYPE_RANGED,param1[2]);
               break;
            case CHANCE_ALWAYS:
               _loc3_ = true;
               break;
            case CHANCE_FLAT:
               _loc3_ = Math.random() <= param1[2];
               break;
            case CHANCE_MORALE:
               _loc3_ = true;
               break;
            case CHANCE_TRIP_NORMAL:
               _loc3_ = param2.CheckTrip(1);
               break;
            case CHANCE_TRIP_CUSTOM:
               _loc3_ = param2.CheckTrip(param1[2]);
               break;
            case CHANCE_RETREAT:
               _loc3_ = param2.CheckRetreat();
               break;
            default:
               _loc3_ = true;
         }
         return _loc3_;
      }
      
      public function IsAvailable(param1:Battle, param2:CombatPair, param3:Boolean = true) : Boolean
      {
         var _loc6_:int = 0;
         var _loc4_:* = true;
         if(this.m_vUsPreConditions.length > 0)
         {
            for each(_loc6_ in this.m_vUsPreConditions)
            {
               if(_loc6_ > 0)
               {
                  _loc4_ = param2.sprUs.HasCondition(_loc6_);
               }
               else if(_loc6_ < 0)
               {
                  _loc4_ = !param2.sprUs.HasCondition(-_loc6_);
               }
               if(!_loc4_)
               {
                  return false;
               }
            }
         }
         if(this.m_vThemPreConditions.length > 0)
         {
            for each(_loc6_ in this.m_vThemPreConditions)
            {
               if(_loc6_ > 0)
               {
                  _loc4_ = param2.sprThem.HasCondition(_loc6_);
               }
               else if(_loc6_ < 0)
               {
                  _loc4_ = !param2.sprThem.HasCondition(-_loc6_);
               }
               if(!_loc4_)
               {
                  return false;
               }
            }
         }
         if(this.m_nSeeThem != BOOL_NA && Boolean(this.m_nSeeThem) != param2.ThemSpotted)
         {
            return false;
         }
         if(this.m_nSeeUs != BOOL_NA && Boolean(this.m_nSeeUs) != param2.UsSpotted)
         {
            return false;
         }
         if(this.m_bAllOutOfRange && param2.nLongestAttackRange >= param2.nShortestRange)
         {
            return false;
         }
         if(this.m_bInAttackRange && !param2.InRange())
         {
            return false;
         }
         if(this.m_nMinCharges > 0 && param2.sprUs.CurrentAttackMode.m_vChargeProfiles.length > 0 && param2.sprUs.CurrentAttackMode.ChargesLeft() < this.m_nMinCharges)
         {
            return false;
         }
         if(this.m_nMinRange >= 0 && param2.nRange < this.m_nMinRange)
         {
            return false;
         }
         if(this.m_nMaxRange >= 0 && param2.nRange > this.m_nMaxRange)
         {
            return false;
         }
         if(this.m_nAttackModeType >= 0 && this.m_nAttackModeType != param2.sprUs.CurrentAttackMode.m_nType)
         {
            return false;
         }
         if(this.m_vHexTypes.length > 0 && this.m_vHexTypes.indexOf(param1.tilHex) < 0)
         {
            return false;
         }
         var _loc5_:Number = DM.Rand(DM.RAND_FLAT);
         if(param3 && _loc5_ > this.m_fChance)
         {
            return false;
         }
         return _loc4_;
      }
      
      public function get ItemRef() : ItemInstance
      {
         if(this.m_objItem == null)
         {
            this.m_objItem = DataHandler.GetItem(this.m_strID);
         }
         return this.m_objItem;
      }
      
      public function set ItemRef(param1:ItemInstance) : void
      {
         this.m_objItem = param1;
      }
   }
}

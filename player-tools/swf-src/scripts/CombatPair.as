package
{
   import org.flixel.*;
   
   public class CombatPair
   {
       
      
      public var m_objBattle:Battle;
      
      public var sprUs:Creature;
      
      public var sprThem:Creature;
      
      public var nRange:int;
      
      public var vAllItems:Vector.<ItemInstance>;
      
      public var vAllMoves:Vector.<BattleMove>;
      
      public var vApproachMoves:Vector.<BattleMove>;
      
      public var vFallBackMoves:Vector.<BattleMove>;
      
      public var vRetreatMoves:Vector.<BattleMove>;
      
      public var vOffenseMoves:Vector.<BattleMove>;
      
      public var vPositionMoves:Vector.<BattleMove>;
      
      public var vPassiveMoves:Vector.<BattleMove>;
      
      public var nShortestRange:int = -1;
      
      public var nLongestRange:int = -1;
      
      public var nLongestAttackRange:int = -1;
      
      public var m_strVerb:String;
      
      public var m_bUsSpotted:Boolean;
      
      public var m_bThemSpotted:Boolean;
      
      public function CombatPair(param1:Creature, param2:Creature, param3:int, param4:Battle)
      {
         super();
         this.m_objBattle = param4;
         this.sprUs = param1;
         this.sprThem = param2;
         this.nRange = param3;
         this.vAllItems = new Vector.<ItemInstance>();
         this.vAllMoves = new Vector.<BattleMove>();
         this.vApproachMoves = new Vector.<BattleMove>();
         this.vFallBackMoves = new Vector.<BattleMove>();
         this.vRetreatMoves = new Vector.<BattleMove>();
         this.vOffenseMoves = new Vector.<BattleMove>();
         this.vPositionMoves = new Vector.<BattleMove>();
         this.vPassiveMoves = new Vector.<BattleMove>();
         this.m_bThemSpotted = false;
         this.m_bUsSpotted = false;
      }
      
      public function destroy() : void
      {
         this.m_objBattle = null;
         this.sprUs = null;
         this.sprThem = null;
         this.vAllItems = null;
         this.vAllMoves = null;
         this.vApproachMoves = null;
         this.vFallBackMoves = null;
         this.vRetreatMoves = null;
         this.vOffenseMoves = null;
         this.vPositionMoves = null;
         this.vPassiveMoves = null;
         this.m_strVerb = null;
      }
      
      private function EffectiveRange() : int
      {
         var _loc1_:int = this.nRange;
         if(this.sprUs.CurrentAttackMode.m_nType == 1 && !this.sprUs.HasCondition(87))
         {
            _loc1_ *= 2;
         }
         return _loc1_;
      }
      
      public function InRange() : Boolean
      {
         return this.EffectiveRange() <= this.sprUs.CurrentAttackMode.m_nRange;
      }
      
      public function GetInverse() : CombatPair
      {
         return this.sprThem.m_dictCombatPairs[this.sprUs];
      }
      
      public function MakeInverse() : CombatPair
      {
         var _loc1_:CombatPair;
         return _loc1_ = new CombatPair(this.sprThem,this.sprUs,this.nRange,this.m_objBattle);
      }
      
      public function CheckAttack(param1:uint, param2:Number) : Boolean
      {
         var _loc4_:Number = NaN;
         var _loc5_:Number = NaN;
         var _loc6_:int = 0;
         var _loc7_:Number = NaN;
         var _loc3_:Boolean = false;
         if(this.sprThem.Asleep && Math.random() > this.sprThem.m_fSleepAwareness)
         {
            _loc3_ = true;
         }
         else if(this.UsSpotted == false)
         {
            _loc3_ = true;
         }
         else
         {
            _loc4_ = (_loc4_ = 1) - this.sprThem.m_fDefense;
            _loc5_ = this.sprThem.m_tilCurrentHex.m_vLightLevels[PlayState.m_objInstance.nTimeOfDay] + this.sprThem.LightLevel - this.sprUs.MinLightLevel;
            if(param1 == AttackMode.ATTACK_TYPE_MELEE)
            {
               _loc4_ += _loc5_ * 0.25;
            }
            else
            {
               if((_loc6_ = int(this.sprUs.CurrentAttackMode.m_nRange)) == 0)
               {
                  _loc6_ = 2;
               }
               _loc7_ = this.EffectiveRange() / (_loc6_ + 1);
               if(PlayState.m_objInstance.grpWeatherNode.objWeatherLast.bPrecip)
               {
                  _loc4_ -= _loc7_ * 0.15;
               }
               _loc4_ = (_loc4_ = (_loc4_ += _loc5_ * _loc7_) * (1 - _loc7_)) * (1 - this.sprThem.m_fCover);
            }
            if((_loc4_ *= param2) < 0.05)
            {
               _loc4_ = 0.05;
            }
            if(Math.random() <= _loc4_)
            {
               _loc3_ = true;
            }
         }
         return _loc3_;
      }
      
      public function CheckRetreat() : Boolean
      {
         var _loc4_:CombatPair = null;
         var _loc5_:Number = NaN;
         var _loc1_:Number = 0;
         var _loc2_:uint = 0;
         var _loc3_:uint = uint(this.sprUs.m_tilCurrentHex.m_objBattle.NumberCombatants());
         for each(_loc4_ in this.sprUs.m_dictCombatPairs)
         {
            if(_loc4_.sprThem.m_nFaction != this.sprUs.m_nFaction && _loc4_.UsSpotted && _loc4_.sprThem.m_objPair != null && _loc4_.sprThem.m_objPair.sprThem === this.sprUs && _loc4_.sprThem.m_objMove != null && (_loc4_.sprThem.m_objMove.m_bApproach || _loc4_.sprThem.m_objMove.m_bOffense || _loc4_.sprThem.m_objMove.m_bPosition))
            {
               _loc5_ = 0;
               if(this.nRange >= this.sprUs.m_tilCurrentHex.m_nMinRange)
               {
                  _loc5_ += (this.nRange - this.sprUs.m_tilCurrentHex.m_nMinRange) / (this.sprUs.m_tilCurrentHex.m_nMaxRange - this.sprUs.m_tilCurrentHex.m_nMinRange);
               }
               _loc5_ += 0.1 * (this.sprUs.m_nMovesPerTurn + this.sprUs.fMovesPerTurnModifier - (this.sprThem.m_nMovesPerTurn + this.sprThem.fMovesPerTurnModifier));
               if(_loc4_.sprThem.HasCondition(143))
               {
                  _loc5_ += 0.2;
               }
               if(_loc4_.sprThem.HasCondition(144))
               {
                  _loc5_ += 0.2;
               }
               if(_loc4_.sprThem.HasCondition(145))
               {
                  _loc5_ += 0.2;
               }
               if(_loc4_.sprThem.HasCondition(146))
               {
                  _loc5_ += 0.2;
               }
               if(_loc4_.sprUs.HasCondition(477))
               {
                  _loc5_ -= 0.2;
               }
               if(_loc4_.sprThem.HasCondition(477))
               {
                  _loc5_ += 0.2;
               }
               _loc1_ += _loc5_ / (_loc3_ - 1);
            }
            else
            {
               _loc1_ += 1 / (_loc3_ - 1);
            }
         }
         if(this.sprUs.HasCondition(144) || this.sprUs.HasCondition(145))
         {
            _loc1_ = 0;
         }
         if(Math.random() < _loc1_)
         {
            return true;
         }
         return false;
      }
      
      public function CanUseMove(param1:String, param2:Battle) : Boolean
      {
         return this.vAllMoves.indexOf(DataHandler.GetBattleMove(param1)) >= 0;
      }
      
      public function CheckTrip(param1:Number) : Boolean
      {
         if(this.sprUs.m_tilCurrentHex.m_objBattle == null)
         {
            return false;
         }
         var _loc2_:Number = this.sprUs.m_tilCurrentHex.m_objBattle.fGround / 16;
         if(this.sprUs.HasCondition(52))
         {
            param1 *= 0.5;
         }
         if(this.sprUs.HasCondition(370))
         {
            param1 *= 0;
         }
         _loc2_ *= param1;
         if(Math.random() < _loc2_)
         {
            return true;
         }
         return false;
      }
      
      public function ChangeFactionRep(param1:Array, param2:Boolean) : void
      {
         var _loc3_:Number = Number(param1[0]);
         if(_loc3_ == 0)
         {
            return;
         }
         this.sprThem.ChangeFactionRep([this.sprUs.m_nFaction,_loc3_],param2,true);
         if(this.sprThem is Player && PlayState.m_objInstance.sprPlayer.PlayerCanSee(this.sprUs))
         {
            this.sprUs.AddCondition(this.sprUs.GetCondition(504));
         }
      }
      
      public function get Trip() : int
      {
         return 0;
      }
      
      public function set Trip(param1:int) : void
      {
         if(PlayState.m_objInstance.sprPlayer.PlayerCanSee(this.sprUs))
         {
            this.sprUs.MessageFloaty(this.sprUs.Name + " 被绊倒了！",false,null,GUIMessageWindow.COLOR_BAD);
         }
         this.sprUs.KnockDown = 1;
      }
      
      public function get Attack() : int
      {
         return 0;
      }
      
      public function set Attack(param1:int) : void
      {
         var _loc9_:GUIInventoryWound = null;
         var _loc10_:GUIInventoryWound = null;
         var _loc11_:ItemInstance = null;
         var _loc12_:ItemInstance = null;
         var _loc13_:Vector.<ItemInstance> = null;
         var _loc14_:Boolean = false;
         var _loc2_:Boolean = this.UsSpotted;
         var _loc3_:* = this.sprUs.CurrentAttackMode.m_nType == AttackMode.ATTACK_TYPE_MELEE;
         if(!_loc3_)
         {
            _loc3_ = Math.random() < 0.1;
         }
         this.UsSpotted = _loc3_;
         var _loc4_:FlxPoint = this.sprUs.CurrentAttackMode.GetWoundLevel(this.sprUs);
         var _loc5_:Boolean = false;
         var _loc6_:Boolean = PlayState.m_objInstance.sprPlayer.PlayerCanSee(this.sprUs);
         var _loc7_:String = "Unknown Assailant";
         if(_loc6_)
         {
            _loc7_ = this.sprUs.Name;
         }
         var _loc8_:String = "";
         if(_loc6_)
         {
            _loc8_ = this.sprUs.CurrentAttackMode.m_strName;
         }
         if(this.sprThem.Asleep)
         {
            if(this.sprUs.CurrentAttackMode.m_nType == AttackMode.ATTACK_TYPE_RANGED || Math.random() > this.sprThem.m_fSleepAwareness)
            {
               _loc4_.x *= 3;
               _loc4_.y *= 3;
               _loc9_ = this.sprThem.CauseWound(_loc4_.y,_loc4_.x,this.sprUs.CurrentAttackMode.m_nPenetration,this.sprUs.Name,_loc8_);
               _loc5_ = true;
            }
            else
            {
               this.sprThem.ForceAwake();
            }
         }
         else if(_loc2_ == false)
         {
            _loc4_.x *= 2;
            _loc4_.y *= 2;
            _loc9_ = this.sprThem.CauseWound(_loc4_.y,_loc4_.x,this.sprUs.CurrentAttackMode.m_nPenetration,this.sprUs.Name,_loc8_);
            _loc5_ = true;
         }
         else
         {
            _loc9_ = this.sprThem.CauseWound(_loc4_.y,_loc4_.x,this.sprUs.CurrentAttackMode.m_nPenetration,_loc7_,_loc8_);
            _loc5_ = true;
         }
         if(_loc5_)
         {
            this.sprUs.CurrentAttackMode.ApplyConditions(this.sprUs,this.sprThem);
         }
         this.Discharge = 1;
         if(_loc5_)
         {
            _loc10_ = _loc9_;
            for each(_loc12_ in this.sprUs.CurrentAttackMode.m_vChargesUsed)
            {
               _loc12_.UseDegrade();
               if(_loc12_.fDurability <= 0)
               {
                  if((_loc13_ = _loc12_.GetDegradedItems(1)).length > 0)
                  {
                     _loc12_ = _loc13_[0];
                  }
                  else
                  {
                     _loc12_ = null;
                  }
               }
               if(_loc12_ != null && _loc4_.x > 0)
               {
                  if(this.sprThem.Alive)
                  {
                     _loc10_.UnSocketItem(true);
                     _loc11_ = _loc10_.SocketItem(_loc12_);
                  }
                  else
                  {
                     _loc14_ = false;
                     if(this.sprThem.DropItem(_loc12_,true,true) == null)
                     {
                        _loc14_ = true;
                     }
                     if(_loc14_)
                     {
                        this.sprThem.m_tilCurrentHex.CalculateValue();
                     }
                  }
               }
            }
         }
         this.sprUs.CurrentAttackMode.m_vChargesUsed.length = 0;
      }
      
      public function get ScatterMissile() : int
      {
         return 0;
      }
      
      public function set ScatterMissile(param1:int) : void
      {
         var _loc2_:ItemInstance = null;
         var _loc3_:ItemInstance = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:Vector.<FlxHexTile> = null;
         var _loc6_:int = 0;
         for each(_loc3_ in this.sprUs.CurrentAttackMode.m_vChargesUsed)
         {
            _loc3_.UseDegrade();
            if(_loc3_.fDurability <= 0)
            {
               if((_loc4_ = _loc3_.GetDegradedItems(1)).length > 0)
               {
                  _loc3_ = _loc4_[0];
               }
               else
               {
                  _loc3_ = null;
               }
            }
            if(_loc3_ != null)
            {
               _loc5_ = MapUtils.GetHexRing(this.sprUs.m_tilCurrentHex.GetHexCoords(),1);
               _loc6_ = Math.floor(DM.Rand(DM.RAND_FLAT) * _loc5_.length);
               if(_loc5_[_loc6_].GroundObject.Slot != null)
               {
                  PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(_loc3_,_loc5_[_loc6_].GroundObject.Slot,true);
               }
               else
               {
                  PlayState.m_objInstance.grpInventoryUI.AddItemToCapBox(_loc3_,_loc5_[_loc6_].GroundObject,null,true);
               }
            }
         }
         this.sprUs.CurrentAttackMode.m_vChargesUsed.length = 0;
      }
      
      public function get Discharge() : int
      {
         return 0;
      }
      
      public function set Discharge(param1:int) : void
      {
         this.sprUs.CurrentAttackMode.UseDegrade();
         this.sprUs.CurrentAttackMode.Discharge();
         if(this.sprUs.CurrentAttackMode.m_nType == 0 && !this.sprUs.HasCondition(50) || this.sprUs.CurrentAttackMode.m_nType == 1 && !this.sprUs.HasCondition(87))
         {
            this.sprUs.CurrentAttackMode.UseDegrade();
         }
      }
      
      public function get UsSpotted() : Boolean
      {
         if(this.m_bUsSpotted)
         {
            return true;
         }
         this.UsSpotted = this.sprThem.CanSeeCreature(this.sprUs);
         return this.m_bUsSpotted;
      }
      
      public function set UsSpotted(param1:Boolean) : void
      {
         this.m_bUsSpotted = param1;
         this.GetInverse().m_bThemSpotted = this.m_bUsSpotted;
      }
      
      public function get ThemSpotted() : Boolean
      {
         if(this.m_bThemSpotted)
         {
            return true;
         }
         this.ThemSpotted = this.sprUs.CanSeeCreature(this.sprThem);
         return this.m_bThemSpotted;
      }
      
      public function set ThemSpotted(param1:Boolean) : void
      {
         this.m_bThemSpotted = param1;
         this.GetInverse().m_bUsSpotted = this.m_bThemSpotted;
      }
      
      public function get ChangeRange() : int
      {
         return this.nRange;
      }
      
      public function set ChangeRange(param1:int) : void
      {
         this.nRange = param1;
         if(this.nRange <= 0)
         {
            this.nRange = 0;
            if(this.sprUs.m_nFaction != this.sprThem.m_nFaction)
            {
               this.sprUs.RemoveCondition(this.sprUs.GetCondition(137));
               this.sprThem.RemoveCondition(this.sprThem.GetCondition(137));
            }
         }
         var _loc2_:CombatPair = this.sprThem.m_dictCombatPairs[this.sprUs];
         if(_loc2_ != null)
         {
            _loc2_.nRange = this.nRange;
         }
      }
      
      public function get ChangeRangeAll() : int
      {
         return 0;
      }
      
      public function set ChangeRangeAll(param1:int) : void
      {
         var _loc2_:CombatPair = null;
         for each(_loc2_ in this.sprUs.m_dictCombatPairs)
         {
            ++_loc2_.ChangeRange;
         }
      }
      
      public function get ResetUsSpotted() : int
      {
         return 0;
      }
      
      public function set ResetUsSpotted(param1:int) : void
      {
         var _loc2_:CombatPair = null;
         for each(_loc2_ in this.sprUs.m_dictCombatPairs)
         {
            this.UsSpotted = _loc2_.sprThem.CanSeeCreature(_loc2_.sprUs);
         }
      }
   }
}

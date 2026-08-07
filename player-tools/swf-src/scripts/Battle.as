package
{
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class Battle
   {
       
      
      public var tilHex:FlxHexTile;
      
      public var fCover:Number;
      
      public var fGround:Number;
      
      private var m_dictFactions:Dictionary;
      
      public var vAddCreatures:Vector.<Creature>;
      
      private var vCombatants:Vector.<Creature>;
      
      private var vRemoveCreatures:Vector.<Creature>;
      
      public function Battle(param1:FlxHexTile)
      {
         var _loc2_:Creature = null;
         super();
         this.tilHex = param1;
         this.vCombatants = new Vector.<Creature>();
         this.vAddCreatures = new Vector.<Creature>();
         this.vRemoveCreatures = new Vector.<Creature>();
         this.fCover = Math.random() * 0.33 * (this.tilHex.nVizLimiter + this.tilHex.nTerrainCost);
         this.fCover = Math.min(1,this.fCover);
         this.fGround = 0;
         if(PlayState.m_objInstance.grpWeatherNode.objWeatherLast.bPrecip)
         {
            this.fGround += 0.25;
         }
         this.fGround = Math.min(1,this.fGround + Math.random());
         this.m_dictFactions = new Dictionary();
         for each(_loc2_ in this.tilHex.m_vOccupants)
         {
            this.AddCreature(_loc2_);
         }
         this.ProcessAddQueue();
      }
      
      public static function SortLeaders(param1:Vector.<Creature>) : Vector.<Creature>
      {
         var _loc3_:Creature = null;
         var _loc2_:Array = new Array();
         for each(_loc3_ in param1)
         {
            _loc2_.push(_loc3_);
         }
         _loc2_.sortOn("m_fLeader",Array.DESCENDING);
         return Vector.<Creature>(_loc2_);
      }
      
      public function destroy() : void
      {
         this.tilHex = null;
         this.m_dictFactions = null;
         this.vAddCreatures = null;
         this.vCombatants = null;
         this.vRemoveCreatures = null;
      }
      
      public function AddCreature(param1:Creature) : void
      {
         if(this.vCombatants.indexOf(param1) >= 0 || this.vAddCreatures.indexOf(param1) >= 0)
         {
            return;
         }
         this.vAddCreatures.push(param1);
      }
      
      public function RemoveCreature(param1:Creature) : void
      {
         if(this.vCombatants.indexOf(param1) < 0 || this.vRemoveCreatures.indexOf(param1) >= 0)
         {
            return;
         }
         this.vRemoveCreatures.push(param1);
      }
      
      public function IsCombatant(param1:Creature) : Boolean
      {
         if(this.vAddCreatures.indexOf(param1) >= 0 || this.vCombatants.indexOf(param1) >= 0)
         {
            return true;
         }
         return false;
      }
      
      public function NumberOpponents(param1:Creature) : int
      {
         var _loc2_:int = int(this.vCombatants.length);
         if(this.m_dictFactions[param1.m_nFaction] != undefined)
         {
            _loc2_ -= this.m_dictFactions[param1.m_nFaction];
         }
         return _loc2_;
      }
      
      public function NumberCombatants() : int
      {
         return this.vCombatants.length;
      }
      
      public function AdvanceTurns() : void
      {
         var _loc1_:Creature = null;
         for each(_loc1_ in this.vCombatants)
         {
            if(!(_loc1_ is Player))
            {
               _loc1_.EndTurn(PlayState.HOURS_PER_COMBAT_TURN,PlayState.m_objInstance.grpWeatherNode.objWeatherLast);
            }
         }
      }
      
      public function NextCombatant(param1:Creature, param2:Creature) : Creature
      {
         var _loc3_:int = int(this.vCombatants.indexOf(param1));
         var _loc4_:int = int(this.vCombatants.indexOf(param2));
         var _loc5_:int;
         if((_loc5_ = _loc3_ + 1) == _loc4_)
         {
            _loc5_++;
         }
         if(_loc5_ < this.vCombatants.length)
         {
            return this.vCombatants[_loc5_];
         }
         return null;
      }
      
      public function PrevCombatant(param1:Creature, param2:Creature) : Creature
      {
         var _loc3_:int = int(this.vCombatants.indexOf(param1));
         var _loc4_:int = int(this.vCombatants.indexOf(param2));
         var _loc5_:int;
         if((_loc5_ = _loc3_ - 1) == _loc4_)
         {
            _loc5_--;
         }
         if(_loc5_ >= 0)
         {
            return this.vCombatants[_loc5_];
         }
         return null;
      }
      
      public function ProcessAddQueue() : void
      {
         var _loc1_:Creature = null;
         var _loc2_:Creature = null;
         var _loc3_:Creature = null;
         var _loc4_:uint = 0;
         var _loc5_:Number = NaN;
         var _loc6_:int = 0;
         var _loc7_:CombatPair = null;
         for each(_loc1_ in this.vAddCreatures)
         {
            if(this.m_dictFactions[_loc1_.m_nFaction] == undefined)
            {
               this.m_dictFactions[_loc1_.m_nFaction] = 1;
            }
            else
            {
               ++this.m_dictFactions[_loc1_.m_nFaction];
            }
            _loc2_ = _loc1_;
            _loc2_.m_fCover = 0;
            _loc4_ = 0;
            while(_loc4_ < this.vCombatants.length)
            {
               _loc3_ = this.vCombatants[_loc4_];
               _loc5_ = DM.Rand(DM.RAND_FLAT);
               _loc6_ = this.tilHex.m_nMinRange + _loc5_ * (this.tilHex.m_nMaxRange - this.tilHex.m_nMinRange);
               _loc7_ = new CombatPair(_loc2_,_loc3_,_loc6_,this);
               _loc2_.m_dictCombatPairs[_loc3_] = _loc7_;
               _loc3_.m_dictCombatPairs[_loc2_] = _loc7_.MakeInverse();
               if(_loc1_ is Player && (_loc3_.visible || _loc3_.m_bVisibleBefore))
               {
                  _loc7_.ThemSpotted = true;
               }
               else if(_loc3_ is Player && (_loc2_.visible || _loc2_.m_bVisibleBefore))
               {
                  _loc7_.UsSpotted = true;
               }
               _loc4_++;
            }
            this.vCombatants.push(_loc1_);
         }
         this.vCombatants = SortLeaders(this.vCombatants);
         this.vAddCreatures.length = 0;
      }
      
      private function ProcessRemoveQueue() : void
      {
         var _loc1_:Creature = null;
         var _loc2_:Creature = null;
         var _loc3_:CombatPair = null;
         for each(_loc1_ in this.vRemoveCreatures)
         {
            for each(_loc2_ in this.vCombatants)
            {
               _loc3_ = _loc2_.m_dictCombatPairs[_loc1_];
               if(_loc3_ != null && _loc3_.m_objBattle == this)
               {
                  delete _loc2_.m_dictCombatPairs[_loc1_];
               }
               _loc3_ = _loc1_.m_dictCombatPairs[_loc2_];
               if(_loc3_ != null && _loc3_.m_objBattle == this)
               {
                  delete _loc1_.m_dictCombatPairs[_loc2_];
               }
            }
            this.vCombatants.splice(this.vCombatants.indexOf(_loc1_),1);
            --this.m_dictFactions[_loc1_.m_nFaction];
            if(this.m_dictFactions[_loc1_.m_nFaction] <= 0)
            {
               delete this.m_dictFactions[_loc1_.m_nFaction];
            }
         }
         this.vRemoveCreatures.length = 0;
      }
      
      public function HandleCombatResponse(param1:CombatPair, param2:ItemInstance, param3:Boolean = false) : Encounter
      {
         var _loc4_:BattleMove = null;
         var _loc8_:Array = null;
         var _loc9_:Creature = null;
         var _loc5_:Encounter = DataHandler.GetEncounter(236).Clone();
         this.ProcessAddQueue();
         var _loc6_:Boolean = true;
         var _loc7_:Boolean = false;
         if(this.vCombatants.length > 1)
         {
            if(param2 != null)
            {
               _loc4_ = BattleMove(DataHandler.GetBattleMove(param2.IDString));
               param1.sprUs.m_objMove = _loc4_;
               param1.sprUs.m_objPair = param1;
               param1.sprUs.m_fOrder = _loc4_.m_fOrder;
               _loc5_.m_strName = _loc4_.m_strName;
            }
            if(param3)
            {
               _loc8_ = new Array();
               for each(_loc9_ in this.vCombatants)
               {
                  _loc9_.m_objMoveLast = null;
                  _loc9_.m_objTargetLast = null;
                  if(_loc9_ is AICreature)
                  {
                     AICreature(_loc9_).GetBattleMove2(this);
                  }
                  _loc8_.push(_loc9_);
               }
               _loc8_.sortOn("m_fOrder");
               for each(_loc9_ in _loc8_)
               {
                  if(this.vRemoveCreatures.indexOf(_loc9_) < 0)
                  {
                     if(_loc9_ is AICreature && _loc9_.m_objMove != null)
                     {
                        _loc9_.m_objMove.Execute(this,_loc9_.m_objPair);
                     }
                     else if(_loc9_ is Player && _loc4_ != null)
                     {
                        _loc4_.Execute(this,param1);
                     }
                     if(_loc6_ && _loc9_.Asleep == false && _loc9_.Alive && this.vRemoveCreatures.indexOf(_loc9_) < 0)
                     {
                        if(_loc9_.HasCondition(500) == false)
                        {
                           _loc6_ = false;
                        }
                        if(_loc9_ is AICreature && _loc9_.HasCondition(506))
                        {
                           _loc7_ = true;
                        }
                     }
                     if(_loc9_.HasCondition(137))
                     {
                        _loc9_.m_fCover = this.fCover;
                     }
                     else
                     {
                        _loc9_.m_fCover = 0;
                     }
                  }
               }
               if(_loc6_)
               {
                  for each(_loc9_ in this.vCombatants)
                  {
                     _loc9_.ExitBattle = 0;
                     if(_loc9_ is Player && _loc7_ && _loc9_.HasCondition(506))
                     {
                        _loc9_.AddCondition(_loc9_.GetCondition(499));
                     }
                  }
               }
               if(param1 != null && this.vRemoveCreatures.indexOf(param1.sprUs) >= 0)
               {
                  _loc5_ = DataHandler.GetEncounter(DM.m_nNullEnc).Clone();
               }
               this.ProcessRemoveQueue();
            }
         }
         if(this.vCombatants.length <= 1)
         {
            _loc5_ = DataHandler.GetEncounter(DM.m_nNullEnc).Clone();
            this.CloseBattle();
         }
         return _loc5_;
      }
      
      private function CloseBattle() : void
      {
         var _loc1_:Creature = null;
         for each(_loc1_ in this.vCombatants)
         {
            if(this.vCombatants.length <= 1)
            {
            }
            _loc1_.ExitBattle = 0;
         }
         this.tilHex.m_objBattle = null;
      }
      
      public function GetOptions() : void
      {
         var _loc1_:Creature = null;
         for each(_loc1_ in this.vCombatants)
         {
            this.GetCreatureOptions(_loc1_);
         }
      }
      
      public function GetCreatureOptions(param1:Creature) : void
      {
         var _loc5_:CombatPair = null;
         var _loc6_:CombatPair = null;
         var _loc2_:int = -1;
         var _loc3_:int = -1;
         var _loc4_:int = -1;
         for each(_loc5_ in param1.m_dictCombatPairs)
         {
            if(!(_loc5_.m_objBattle != this || _loc5_.sprThem.m_nFaction == param1.m_nFaction))
            {
               if(_loc2_ < 0 || _loc5_.nRange < _loc2_)
               {
                  _loc2_ = _loc5_.nRange;
               }
               if(_loc3_ < 0 || _loc5_.nRange > _loc3_)
               {
                  _loc3_ = _loc5_.nRange;
               }
               if(_loc5_.sprThem.Asleep == false && _loc5_.sprThem.HasCondition(18) == false && (_loc4_ < 0 || _loc5_.sprThem.CurrentAttackMode.m_nRange > _loc4_))
               {
                  _loc4_ = int(_loc5_.sprThem.CurrentAttackMode.m_nRange);
               }
            }
         }
         for each(_loc6_ in param1.m_dictCombatPairs)
         {
            if(_loc5_.m_objBattle == this)
            {
               _loc6_.vAllItems.length = 0;
               _loc6_.vAllMoves.length = 0;
               _loc6_.vApproachMoves.length = 0;
               _loc6_.vFallBackMoves.length = 0;
               _loc6_.vOffenseMoves.length = 0;
               _loc6_.vRetreatMoves.length = 0;
               _loc6_.vPositionMoves.length = 0;
               _loc6_.vPassiveMoves.length = 0;
               _loc6_.nShortestRange = _loc2_;
               _loc6_.nLongestRange = _loc3_;
               _loc6_.nLongestAttackRange = _loc4_;
               if(!(param1.Asleep || param1.HasCondition(18)))
               {
                  DataHandler.GetBattleMovesAvail(this,_loc6_);
                  _loc6_.vAllMoves = this.SortMoves(_loc6_.vAllMoves);
                  _loc6_.vApproachMoves = this.SortMoves(_loc6_.vApproachMoves);
                  _loc6_.vFallBackMoves = this.SortMoves(_loc6_.vFallBackMoves);
                  _loc6_.vOffenseMoves = this.SortMoves(_loc6_.vOffenseMoves);
                  _loc6_.vRetreatMoves = this.SortMoves(_loc6_.vRetreatMoves);
                  _loc6_.vPositionMoves = this.SortMoves(_loc6_.vPositionMoves);
                  _loc6_.vPassiveMoves = this.SortMoves(_loc6_.vPassiveMoves);
                  this.SortItems(_loc6_);
               }
            }
         }
      }
      
      public function TrimMoves(param1:Creature) : void
      {
         var _loc2_:CombatPair = null;
         var _loc3_:Vector.<BattleMove> = null;
         var _loc4_:BattleMove = null;
         for each(_loc2_ in param1.m_dictCombatPairs)
         {
            if(_loc2_.m_objBattle == this)
            {
               _loc3_ = _loc2_.vAllMoves.concat();
               _loc2_.vAllItems.length = 0;
               _loc2_.vAllMoves.length = 0;
               _loc2_.vApproachMoves.length = 0;
               _loc2_.vFallBackMoves.length = 0;
               _loc2_.vOffenseMoves.length = 0;
               _loc2_.vRetreatMoves.length = 0;
               _loc2_.vPositionMoves.length = 0;
               _loc2_.vPassiveMoves.length = 0;
               for each(_loc4_ in _loc3_)
               {
                  if(_loc4_.IsAvailable(this,_loc2_,false))
                  {
                     _loc2_.vAllMoves.push(_loc4_);
                     if(_loc4_.m_bApproach)
                     {
                        _loc2_.vApproachMoves.push(_loc4_);
                     }
                     if(_loc4_.m_bFallBack)
                     {
                        _loc2_.vFallBackMoves.push(_loc4_);
                     }
                     if(_loc4_.m_bOffense)
                     {
                        _loc2_.vOffenseMoves.push(_loc4_);
                     }
                     if(_loc4_.m_bRetreat)
                     {
                        _loc2_.vRetreatMoves.push(_loc4_);
                     }
                     if(_loc4_.m_bPosition)
                     {
                        _loc2_.vPositionMoves.push(_loc4_);
                     }
                     if(_loc4_.m_bPassive)
                     {
                        _loc2_.vPassiveMoves.push(_loc4_);
                     }
                  }
               }
               _loc2_.vAllMoves = this.SortMoves(_loc2_.vAllMoves);
               _loc2_.vApproachMoves = this.SortMoves(_loc2_.vApproachMoves);
               _loc2_.vFallBackMoves = this.SortMoves(_loc2_.vFallBackMoves);
               _loc2_.vOffenseMoves = this.SortMoves(_loc2_.vOffenseMoves);
               _loc2_.vRetreatMoves = this.SortMoves(_loc2_.vRetreatMoves);
               _loc2_.vPositionMoves = this.SortMoves(_loc2_.vPositionMoves);
               _loc2_.vPassiveMoves = this.SortMoves(_loc2_.vPassiveMoves);
               this.SortItems(_loc2_);
            }
         }
      }
      
      private function SortItems(param1:CombatPair) : void
      {
         var _loc3_:BattleMove = null;
         var _loc2_:Vector.<BattleMove> = param1.vRetreatMoves.concat(param1.vFallBackMoves);
         _loc2_ = _loc2_.concat(param1.vPositionMoves);
         _loc2_ = _loc2_.concat(param1.vApproachMoves);
         _loc2_ = _loc2_.concat(param1.vOffenseMoves);
         _loc2_ = _loc2_.concat(param1.vPassiveMoves);
         for each(_loc3_ in _loc2_)
         {
            if(param1.vAllItems.indexOf(_loc3_.ItemRef) < 0)
            {
               param1.vAllItems.push(_loc3_.ItemRef);
            }
         }
      }
      
      private function SortMoves(param1:Vector.<BattleMove>) : Vector.<BattleMove>
      {
         var _loc3_:BattleMove = null;
         var _loc2_:Array = new Array();
         for each(_loc3_ in param1)
         {
            _loc2_.push(_loc3_);
         }
         _loc2_.sortOn("m_fPriority");
         return Vector.<BattleMove>(_loc2_);
      }
   }
}

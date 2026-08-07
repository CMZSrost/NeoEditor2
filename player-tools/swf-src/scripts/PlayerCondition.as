package
{
   import org.flixel.*;
   
   public class PlayerCondition
   {
      
      public static var m_aFieldNames:Array = ["TriggerEncounter","GetDiagnostic","RemoveSkill","AddSkill","RemoveTrait","AddTrait","AddRecipe","LoseRandomItem","SpawnNewCreature","ChangeFactionRep","ChangeGlobalFactionRep","AddItemGround","SetWaypoint","SetPlayerCondition","SetImmunity","ChainCondition","EndGame"];
      
      public static var m_aFieldRemaps:Array = [["m_aEncounters",0],["m_aEncounters",0],["m_dictItems",0],["m_dictItems",0],["m_dictItems",0],["m_dictItems",0],["m_vRecipes",0],["m_aItemProps",0],["m_aCreatures",0],["m_dictFactions",0],["m_dictFactions",0],["m_vTreasures",0],["m_aEncounters",2],["m_aConditions",0],["m_aConditions",-1],["m_aConditions",-1],["m_aConditions",0]];
       
      
      public var strName:String;
      
      public var strDesc:String;
      
      public var m_aFieldNames:Array;
      
      public var m_aModifiers:Array;
      
      public var m_aThresholds:Array;
      
      public var m_aEffects:Array;
      
      public var bFatal:Boolean;
      
      public var m_nID:uint;
      
      public var m_bStackable:Boolean;
      
      public var m_bRemoveAll:Boolean;
      
      public var m_bPermanent:Boolean;
      
      public var m_nStacked:int;
      
      public var m_vIDNext:Vector.<int>;
      
      public var m_vChanceNext:Vector.<Number>;
      
      public var m_fDuration:Number;
      
      public var m_bReset:Boolean;
      
      public var m_objDate:Date;
      
      public var m_bDisplay:Boolean;
      
      public var m_bDisplayOther:Boolean;
      
      public var m_bDisplayGameOver:Boolean;
      
      public var m_bRemovePostCombat:Boolean;
      
      public var m_nColor:uint;
      
      public var m_nTransferRange:int;
      
      public function PlayerCondition(param1:uint, param2:String, param3:String, param4:Array, param5:Array, param6:Array, param7:Array, param8:Boolean = false, param9:Vector.<int> = null, param10:Number = 0, param11:Vector.<Number> = null, param12:Boolean = false, param13:Boolean = false, param14:Boolean = true, param15:Boolean = false, param16:Boolean = false, param17:uint = 0, param18:Boolean = true, param19:Boolean = false, param20:Boolean = false, param21:int = -1)
      {
         super();
         this.m_nID = param1;
         this.strName = param2;
         this.strDesc = param3;
         this.m_aFieldNames = param4;
         this.m_aModifiers = param5;
         this.m_aEffects = param6;
         this.m_aThresholds = param7;
         this.bFatal = param8;
         this.m_nStacked = 0;
         this.m_objDate = new Date();
         if(param9 == null)
         {
            param9 = new Vector.<int>(0);
         }
         this.m_vIDNext = param9.concat();
         if(param11 == null)
         {
            param11 = new Vector.<Number>(0);
         }
         this.m_vChanceNext = param11.concat();
         this.m_fDuration = param10;
         this.m_bPermanent = param12;
         this.m_bStackable = param13;
         this.m_bDisplay = param14;
         this.m_bDisplayOther = param15;
         this.m_bDisplayGameOver = param16;
         this.m_nColor = GUIMessageWindow.m_vColors[param17];
         this.m_bReset = param18;
         this.m_bRemoveAll = param19;
         this.m_bRemovePostCombat = param20;
         this.m_nTransferRange = param21;
      }
      
      public function destroy() : void
      {
         var _loc1_:int = 0;
         this.strName = null;
         this.strDesc = null;
         if(this.m_aFieldNames != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aFieldNames.length)
            {
               this.m_aFieldNames[_loc1_] = null;
               _loc1_++;
            }
            this.m_aFieldNames = null;
         }
         this.m_aModifiers = null;
         this.m_aEffects = null;
         if(this.m_aEffects != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aEffects.length)
            {
               this.m_aEffects[_loc1_] = null;
               _loc1_++;
            }
            this.m_aEffects = null;
         }
         this.m_aThresholds = null;
         if(this.m_aThresholds != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aThresholds.length)
            {
               this.m_aThresholds[_loc1_] = null;
               _loc1_++;
            }
            this.m_aThresholds = null;
         }
         this.m_vIDNext = null;
         this.m_objDate = null;
      }
      
      public function Clone() : PlayerCondition
      {
         return new PlayerCondition(this.m_nID,this.strName,this.strDesc,this.m_aFieldNames.concat(),this.m_aModifiers.concat(),this.m_aEffects.concat(),this.m_aThresholds.concat(),this.bFatal,this.m_vIDNext,this.m_fDuration,this.m_vChanceNext,this.m_bPermanent,this.m_bStackable,this.m_bDisplay,this.m_bDisplayOther,this.m_bDisplayGameOver,GUIMessageWindow.m_vColors.indexOf(this.m_nColor),this.m_bReset,this.m_bRemoveAll,this.m_bRemovePostCombat,this.m_nTransferRange);
      }
      
      public function Update(param1:IConditionOwner, param2:Date) : void
      {
         var _loc6_:Creature = null;
         var _loc7_:CombatPair = null;
         var _loc8_:Number = NaN;
         var _loc9_:Number = NaN;
         var _loc10_:int = 0;
         var _loc11_:int = 0;
         var _loc12_:PlayerCondition = null;
         var _loc3_:Number = Number(this.m_objDate.getTime());
         var _loc4_:Number;
         var _loc5_:Number = (_loc4_ = Number(param2.getTime())) - _loc3_;
         if(this.m_nTransferRange >= 0 && param1 is Creature && Creature(param1).m_tilCurrentHex.m_objBattle != null)
         {
            _loc6_ = Creature(param1);
            for each(_loc7_ in _loc6_.m_dictCombatPairs)
            {
               _loc8_ = DM.Rand(DM.RAND_FLAT);
               _loc9_ = 0;
               if(this.m_nTransferRange == 0)
               {
                  if(_loc7_.nRange == 0)
                  {
                     _loc9_ = 1;
                  }
               }
               else
               {
                  _loc9_ = (this.m_nTransferRange - _loc7_.nRange) / this.m_nTransferRange;
               }
               if(_loc8_ < _loc9_ && _loc7_.sprThem.HasCondition(this.m_nID) == false)
               {
                  _loc7_.sprThem.AddCondition(_loc7_.sprThem.GetCondition(this.m_nID));
               }
            }
         }
         if(this.m_fDuration > 0 && _loc5_ >= this.m_fDuration * 1000 * 60 * 60)
         {
            if(this.m_bStackable)
            {
               while(this.m_nStacked > 1)
               {
                  this.RemoveConditionEffects(param1);
               }
            }
            else
            {
               this.m_nStacked = 1;
            }
            this.RemoveConditionEffects(param1);
            _loc10_ = 0;
            _loc9_ = 0;
            for each(_loc11_ in this.m_vIDNext)
            {
               _loc9_ = this.m_vChanceNext[_loc10_];
               if(_loc11_ != 0 && Math.random() < this.m_vChanceNext[_loc10_])
               {
                  _loc12_ = param1.GetCondition(Math.abs(_loc11_));
                  if(_loc11_ > 0)
                  {
                     param1.AddCondition(_loc12_);
                  }
                  else
                  {
                     param1.RemoveCondition(_loc12_);
                  }
               }
               if(this.m_vChanceNext.length > _loc10_ + 1)
               {
                  _loc10_++;
               }
            }
         }
      }
      
      public function ApplyConditionEffects(param1:Object) : void
      {
         var _loc3_:PlayerCondition = null;
         var _loc4_:IConditionOwner = null;
         var _loc6_:int = 0;
         ++this.m_nStacked;
         if(this.m_bReset)
         {
            this.m_objDate.setTime(PlayState.m_objInstance.objDate.getTime());
         }
         if(this.m_nStacked == 1 || this.m_bStackable)
         {
            if(this.strName != null && !this.m_bPermanent && this.m_nStacked == 1 && Boolean(param1.hasOwnProperty("aCurrentStates")))
            {
               (param1["aCurrentStates"] as Array).push(this);
               this.m_objDate.setTime(PlayState.m_objInstance.objDate.getTime());
               if(this.bFatal && Boolean(param1.hasOwnProperty("Alive")))
               {
                  param1["Alive"] = false;
               }
            }
            this.ApplyEffects(param1,true);
         }
         if(PlayState.m_objInstance.m_nGameState < PlayState.GAMESTATE_GAMEREADY)
         {
            return;
         }
         var _loc2_:int = 0;
         var _loc5_:int = 0;
         while(_loc5_ < this.m_aThresholds.length)
         {
            _loc2_ = int(this.m_aThresholds[_loc5_][0]);
            if(this.m_nStacked == _loc2_ && Boolean(param1.hasOwnProperty("aCurrentStates")))
            {
               _loc4_ = IConditionOwner(param1);
               for each(_loc6_ in this.m_aThresholds[_loc5_][1])
               {
                  _loc3_ = _loc4_.GetCondition(Math.abs(_loc6_));
                  if(_loc6_ > 0)
                  {
                     _loc4_.AddCondition(_loc3_);
                  }
                  else
                  {
                     _loc4_.RemoveCondition(_loc3_);
                  }
               }
            }
            _loc5_++;
         }
      }
      
      public function RemoveConditionEffects(param1:Object) : void
      {
         var _loc3_:PlayerCondition = null;
         var _loc4_:IConditionOwner = null;
         var _loc6_:int = 0;
         var _loc7_:int = 0;
         if(this.m_bRemoveAll && this.m_nStacked > 1)
         {
            this.m_nStacked = 1;
         }
         --this.m_nStacked;
         if(this.m_nStacked == 0 || this.m_bStackable)
         {
            if(this.m_nStacked >= 0)
            {
               this.ApplyEffects(param1,false);
            }
            if(this.m_nStacked <= 0 && Boolean(param1.hasOwnProperty("aCurrentStates")))
            {
               if((_loc6_ = int((param1["aCurrentStates"] as Array).indexOf(this))) >= 0)
               {
                  (param1["aCurrentStates"] as Array).splice(_loc6_,1);
               }
            }
         }
         if(PlayState.m_objInstance.m_nGameState < PlayState.GAMESTATE_GAMEREADY)
         {
            return;
         }
         var _loc2_:int = 0;
         var _loc5_:int = 0;
         while(_loc5_ < this.m_aThresholds.length)
         {
            _loc2_ = int(this.m_aThresholds[_loc5_][0]);
            if(this.m_nStacked == _loc2_ - 1 && Boolean(param1.hasOwnProperty("aCurrentStates")))
            {
               _loc4_ = IConditionOwner(param1);
               for each(_loc7_ in this.m_aThresholds[_loc5_][1])
               {
                  _loc3_ = _loc4_.GetCondition(Math.abs(_loc7_));
                  if(_loc7_ > 0)
                  {
                     _loc4_.RemoveCondition(_loc3_);
                  }
                  else
                  {
                     _loc4_.AddCondition(_loc3_);
                  }
               }
            }
            _loc5_++;
         }
      }
      
      private function ApplyEffects(param1:Object, param2:Boolean) : void
      {
         var _loc5_:String = null;
         var _loc6_:Number = NaN;
         var _loc3_:int = -1;
         if(param2)
         {
            _loc3_ = 1;
         }
         var _loc4_:int = 0;
         while(_loc4_ < this.m_aFieldNames.length)
         {
            _loc5_ = this.m_aFieldNames[_loc4_];
            _loc6_ = Number(this.m_aModifiers[_loc4_]);
            if(_loc5_ != "" && Boolean(param1.hasOwnProperty(_loc5_)))
            {
               if(param1[_loc5_] is Boolean)
               {
                  if(param2)
                  {
                     param1[_loc5_] = Boolean(this.m_aModifiers[_loc4_]);
                  }
                  else
                  {
                     param1[_loc5_] = !Boolean(this.m_aModifiers[_loc4_]);
                  }
               }
               else
               {
                  param1[_loc5_] += _loc3_ * _loc6_;
               }
            }
            _loc4_++;
         }
         _loc4_ = 0;
         while(_loc4_ < this.m_aEffects.length)
         {
            if((_loc5_ = this.m_aEffects[_loc4_][0]) != "" && _loc5_ in param1)
            {
               param1[_loc5_](this.m_aEffects[_loc4_][1],param2);
            }
            _loc4_++;
         }
      }
   }
}

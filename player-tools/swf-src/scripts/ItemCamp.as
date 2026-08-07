package
{
   import flash.events.*;
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class ItemCamp extends ItemInstance
   {
       
      
      public var m_fSleepAwareness:Number;
      
      public var m_fVisibility:Number;
      
      public var WetTempAdjustMod:Number;
      
      public var m_fHealPerHourMod:Number;
      
      public var fSleepQuality:Number;
      
      private var m_objCond:PlayerCondition;
      
      private var m_dictConds:Dictionary;
      
      public function ItemCamp(param1:Item, param2:int = 0, param3:int = 0)
      {
         this.m_fSleepAwareness = 0;
         this.m_fVisibility = 0;
         this.WetTempAdjustMod = 0;
         this.m_fHealPerHourMod = 0;
         this.fSleepQuality = 0;
         this.m_dictConds = new Dictionary();
         super(param1,param2,param3);
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         this.m_objCond = DataHandler.DestroyObject(this.m_objCond);
         for each(_loc1_ in this.m_dictConds)
         {
            this.m_dictConds[_loc1_] = DataHandler.DestroyObject(this.m_dictConds[_loc1_]);
            delete this.m_dictConds[_loc1_];
         }
         this.m_dictConds = null;
      }
      
      override public function Clone(param1:Boolean = true) : ItemInstance
      {
         var _loc2_:ItemCamp = super.Clone() as ItemCamp;
         var _loc3_:PlayerCondition = new PlayerCondition(this.m_objCond.m_nID,this.m_objCond.strName,this.m_objCond.strDesc,this.m_objCond.m_aFieldNames.concat(),this.m_objCond.m_aModifiers.concat(),this.m_objCond.m_aEffects.concat(),this.m_objCond.m_aThresholds.concat(),this.m_objCond.bFatal,this.m_objCond.m_vIDNext,this.m_objCond.m_fDuration,this.m_objCond.m_vChanceNext,this.m_objCond.m_bPermanent,this.m_objCond.m_bStackable,this.m_objCond.m_bDisplay,this.m_objCond.m_bDisplayOther,this.m_objCond.m_bDisplayGameOver,GUIMessageWindow.m_vColors.indexOf(this.m_objCond.m_nColor));
         _loc2_.Condition = _loc3_;
         return _loc2_;
      }
      
      public function Randomize() : void
      {
         var _loc1_:PlayerCondition = this.m_objCond.Clone();
         var _loc2_:uint = 0;
         while(_loc2_ < _loc1_.m_aModifiers.length)
         {
            _loc1_.m_aModifiers[_loc2_] = 0.5 * _loc1_.m_aModifiers[_loc2_] * (1 + Math.random());
            _loc2_++;
         }
         this.Condition = _loc1_;
      }
      
      private function UpdateCampStats(param1:Array, param2:Boolean = false) : void
      {
         var _loc3_:Array = null;
         var _loc4_:PlayerCondition = null;
         var _loc5_:Boolean = false;
         for each(_loc3_ in param1)
         {
            if(_loc3_[0] == "208")
            {
               _loc4_ = this.m_dictConds[_loc3_[1]];
               _loc5_ = true;
               if(_loc4_ == null)
               {
                  this.m_dictConds[_loc3_[1]] = DataHandler.GetCondition(_loc3_[1]);
                  _loc4_ = this.m_dictConds[_loc3_[1]];
               }
               this.GetStatsFromCondition(_loc4_,param2);
            }
         }
      }
      
      private function GetStatsFromCondition(param1:PlayerCondition, param2:Boolean = false) : void
      {
         var _loc3_:int = 0;
         if(param2)
         {
            _loc3_ = param1.m_nStacked;
            if(param1 == this.m_objCond)
            {
               param1.m_nStacked = 1;
            }
            param1.RemoveConditionEffects(this);
            if(param1 == this.m_objCond)
            {
               param1.m_nStacked = _loc3_;
            }
         }
         else
         {
            _loc3_ = param1.m_nStacked;
            param1.ApplyConditionEffects(this);
            if(param1 == this.m_objCond)
            {
               param1.m_nStacked = _loc3_;
            }
         }
      }
      
      public function IncrementStackingStats(param1:ItemInstance, param2:int) : void
      {
         var _loc3_:* = param2 < 0;
         param2 = Math.abs(param2);
         var _loc4_:int = 0;
         while(_loc4_ < param2)
         {
            this.UpdateCampStats(param1.ItemDefinition.m_aPossessConditions,_loc3_);
            _loc4_++;
         }
         if(bSocketed && grpItemPanelSlot.nSlotIndex == 208)
         {
            PlayState.m_objInstance.grpInventoryUI.UpdateCampStats(this);
            if(Slot.m_sprOwner == PlayState.m_objInstance.sprPlayer)
            {
               PlayState.m_objInstance.sprPlayer.m_tilCurrentHex.IsCampTile = true;
            }
         }
      }
      
      override public function RemoveItem(param1:ItemInstance, param2:Boolean = false) : ItemInstance
      {
         var _loc3_:ItemInstance = super.RemoveItem(param1,param2);
         if(_loc3_ != null)
         {
            if(bSocketed && grpItemPanelSlot.nSlotIndex == 208)
            {
               if(vItems.length == 0 && Slot.m_sprOwner == PlayState.m_objInstance.sprPlayer && PlayState.m_objInstance.sprPlayer.m_tilCurrentHex != null)
               {
                  PlayState.m_objInstance.sprPlayer.m_tilCurrentHex.IsCampTile = false;
               }
            }
         }
         return _loc3_;
      }
      
      override public function set ItemDefinition(param1:Item) : void
      {
         super.ItemDefinition = param1;
         this.m_fVisibility = 1;
         this.UpdateCampStats(ItemDefinition.m_aEquipConditions);
      }
      
      public function get Condition() : PlayerCondition
      {
         return this.m_objCond;
      }
      
      public function set Condition(param1:PlayerCondition) : void
      {
         if(this.m_objCond != null)
         {
            this.GetStatsFromCondition(this.m_objCond,true);
         }
         this.m_objCond = param1;
         this.GetStatsFromCondition(this.m_objCond);
      }
      
      override public function get SaveData() : SaveGameItem
      {
         var _loc1_:SaveGameItem = super.SaveData;
         _loc1_.m_fSleepAwareness = this.m_objCond.m_aModifiers[0];
         _loc1_.m_fVisibility = this.m_objCond.m_aModifiers[1];
         _loc1_.WetTempAdjustMod = this.m_objCond.m_aModifiers[2];
         _loc1_.m_fHealPerHourMod = this.m_objCond.m_aModifiers[3];
         _loc1_.fSleepQuality = this.m_objCond.m_aModifiers[4];
         return _loc1_;
      }
      
      override public function set SaveData(param1:SaveGameItem) : void
      {
         var _loc3_:ItemInstance = null;
         super.SaveData = param1;
         var _loc2_:PlayerCondition = new PlayerCondition(this.m_objCond.m_nID,this.m_objCond.strName,this.m_objCond.strDesc,this.m_objCond.m_aFieldNames.concat(),new Array(param1.m_fSleepAwareness,param1.m_fVisibility,param1.WetTempAdjustMod,param1.m_fHealPerHourMod,param1.fSleepQuality),new Array(),new Array(),this.m_objCond.bFatal,this.m_objCond.m_vIDNext,this.m_objCond.m_fDuration,this.m_objCond.m_vChanceNext,this.m_objCond.m_bPermanent,this.m_objCond.m_bStackable,this.m_objCond.m_bDisplay,this.m_objCond.m_bDisplayOther,this.m_objCond.m_bDisplayGameOver,GUIMessageWindow.m_vColors.indexOf(this.m_objCond.m_nColor));
         this.Condition = _loc2_;
         for each(_loc3_ in vItems)
         {
            _loc3_.UpdatePossessConditions(null,this);
         }
      }
   }
}

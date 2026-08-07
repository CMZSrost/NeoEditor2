package
{
   import flash.utils.Dictionary;
   
   public class SaveGameCreature
   {
       
      
      public var m_nID:int;
      
      public var m_fMovesLeft:Number;
      
      public var m_fMoveReserveRemaining:Number;
      
      public var m_fMoveCost:Number;
      
      public var vCurrentStates:Vector.<SaveGameCondition>;
      
      public var vAllWoundSlots:Vector.<SaveGameWound>;
      
      public var fSleepDebt:Number;
      
      public var fFoodDebt:Number;
      
      public var fWaterDebt:Number;
      
      public var fCoreTemp:Number;
      
      public var fHoursSlept:int;
      
      public var bAsleep:Boolean;
      
      public var Alive:Boolean;
      
      public var m_bHiding:Boolean;
      
      public var m_nAttackMode:uint;
      
      public var m_vItems:Vector.<SaveGameItem>;
      
      public var m_dictCamps:Dictionary;
      
      public var m_vFactions:Vector.<int>;
      
      public var m_fBloodLeft:Number;
      
      public var m_fImmuneLeft:Number;
      
      public var m_fPainLeft:Number;
      
      public var m_nMorality:Number;
      
      public var m_fLeader:Number;
      
      public var m_vWaypoints:Vector.<Vector.<int>>;
      
      public var m_vEncQueue:Vector.<int>;
      
      public var m_objLocker:SaveGameItem;
      
      public function SaveGameCreature(param1:Object = null)
      {
         var _loc2_:Object = null;
         var _loc3_:Object = null;
         var _loc4_:Object = null;
         var _loc5_:Object = null;
         var _loc6_:SaveGameCondition = null;
         var _loc7_:SaveGameWound = null;
         var _loc8_:int = 0;
         super();
         this.vCurrentStates = new Vector.<SaveGameCondition>();
         this.vAllWoundSlots = new Vector.<SaveGameWound>();
         this.m_vItems = new Vector.<SaveGameItem>();
         this.m_dictCamps = new Dictionary();
         this.m_vFactions = new Vector.<int>();
         this.m_vWaypoints = new Vector.<Vector.<int>>();
         this.m_vEncQueue = new Vector.<int>();
         if(param1 != null)
         {
            this.m_nID = param1.m_nID;
            this.m_fMovesLeft = param1.m_fMovesLeft;
            this.m_fMoveReserveRemaining = param1.m_fMoveReserveRemaining;
            this.m_fMoveCost = param1.m_fMoveCost;
            for each(_loc2_ in param1.vCurrentStates)
            {
               _loc6_ = new SaveGameCondition(_loc2_);
               this.vCurrentStates.push(_loc6_);
            }
            for each(_loc3_ in param1.vAllWoundSlots)
            {
               _loc7_ = new SaveGameWound(_loc3_);
               this.vAllWoundSlots.push(_loc7_);
            }
            this.fSleepDebt = param1.fSleepDebt;
            this.fFoodDebt = param1.fFoodDebt;
            this.fWaterDebt = param1.fWaterDebt;
            this.fCoreTemp = param1.fCoreTemp;
            this.m_fBloodLeft = param1.m_fBloodLeft;
            this.m_fImmuneLeft = param1.m_fImmuneLeft;
            this.m_fPainLeft = param1.m_fPainLeft;
            this.fHoursSlept = param1.fHoursSlept;
            this.bAsleep = param1.bAsleep;
            this.Alive = param1.Alive;
            this.m_bHiding = param1.m_bHiding;
            this.m_nAttackMode = param1.m_nAttackMode;
            if(param1.m_objLocker != null)
            {
               this.m_objLocker = new SaveGameItem(param1.m_objLocker);
            }
            if(param1.m_vFactions != null)
            {
               _loc8_ = 0;
               while(_loc8_ < param1.m_vFactions.length)
               {
                  this.m_vFactions[_loc8_] = param1.m_vFactions[_loc8_];
                  _loc8_++;
               }
            }
            for(_loc4_ in param1.m_dictCamps)
            {
               this.m_dictCamps[_loc4_] = param1.m_dictCamps[_loc4_];
            }
            this.m_nMorality = param1.m_nMorality;
            this.m_fLeader = param1.m_fLeader;
            for each(_loc5_ in param1.m_vItems)
            {
               this.m_vItems.push(new SaveGameItem(_loc5_));
            }
            if(param1.m_vWaypoints != null)
            {
               _loc8_ = 0;
               while(_loc8_ < param1.m_vWaypoints.length)
               {
                  this.m_vWaypoints[_loc8_] = param1.m_vWaypoints[_loc8_];
                  _loc8_++;
               }
            }
            if(param1.m_vEncQueue != null)
            {
               _loc8_ = 0;
               while(_loc8_ < param1.m_vEncQueue.length)
               {
                  this.m_vEncQueue[_loc8_] = param1.m_vEncQueue[_loc8_];
                  _loc8_++;
               }
            }
         }
      }
      
      public function destroy() : void
      {
         var _loc1_:Vector.<int> = null;
         var _loc2_:int = 0;
         this.vCurrentStates = null;
         if(this.vAllWoundSlots != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.vAllWoundSlots.length)
            {
               this.vAllWoundSlots[_loc2_].destroy();
               this.vAllWoundSlots[_loc2_] = null;
               _loc2_++;
            }
            this.vAllWoundSlots = null;
         }
         if(this.m_vItems != null)
         {
            _loc2_ = 0;
            while(_loc2_ < this.m_vItems.length)
            {
               this.m_vItems[_loc2_].destroy();
               this.m_vItems[_loc2_] = null;
               _loc2_++;
            }
            this.m_vItems = null;
         }
         for each(_loc2_ in this.m_dictCamps)
         {
            delete this.m_dictCamps[_loc2_];
         }
         this.m_dictCamps = null;
         this.m_vFactions.length = 0;
         this.m_vFactions = null;
         for each(_loc1_ in this.m_vWaypoints)
         {
            _loc1_.length = 0;
         }
         this.m_vWaypoints.length = 0;
         this.m_vWaypoints = null;
         this.m_vEncQueue.length = 0;
         this.m_vEncQueue = null;
         this.m_objLocker = DataHandler.DestroyObject(this.m_objLocker);
      }
   }
}

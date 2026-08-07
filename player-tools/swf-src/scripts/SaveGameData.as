package
{
   import flash.utils.Dictionary;
   import org.flixel.FlxPoint;
   
   public class SaveGameData
   {
       
      
      public var m_strUsername:String;
      
      public var m_vEventQueue:Vector.<int>;
      
      public var m_vEncounterTriggersRemaining:Vector.<int>;
      
      public var m_vVisibleHexes:Vector.<SaveGameHex>;
      
      public var m_vCreatures:Vector.<SaveGameCreature>;
      
      public var m_dictMapLabels:Dictionary;
      
      public var m_dictMinimapLabels:Dictionary;
      
      public var m_strVersion:String;
      
      public var m_strCharname:String;
      
      public var m_fHours:Number;
      
      public var m_fMoney:Number;
      
      public var m_vKnownRecipes:Vector.<int>;
      
      public var m_ptHex:FlxPoint;
      
      public var m_bAlive:Boolean;
      
      public var m_bScavTut:Boolean;
      
      public var m_vNonInventoryItems:Vector.<SaveGameItem>;
      
      public var m_objPlayer:SaveGameCreature;
      
      public function SaveGameData(param1:Object = null)
      {
         var _loc2_:Object = null;
         var _loc3_:Object = null;
         var _loc4_:String = null;
         var _loc5_:Object = null;
         var _loc6_:SaveGameCreature = null;
         var _loc7_:SaveGameHex = null;
         var _loc8_:SaveGameItem = null;
         super();
         this.m_vEncounterTriggersRemaining = new Vector.<int>();
         this.m_vEventQueue = new Vector.<int>();
         this.m_vCreatures = new Vector.<SaveGameCreature>();
         this.m_vVisibleHexes = new Vector.<SaveGameHex>();
         this.m_vKnownRecipes = new Vector.<int>();
         this.m_vNonInventoryItems = new Vector.<SaveGameItem>();
         this.m_dictMapLabels = new Dictionary();
         this.m_dictMinimapLabels = new Dictionary();
         if(param1 != null)
         {
            this.m_strUsername = param1.m_strUsername;
            this.m_vEncounterTriggersRemaining = param1.m_vEncounterTriggersRemaining;
            this.m_vEventQueue = param1.m_vEventQueue;
            for each(_loc2_ in param1.m_vCreatures)
            {
               _loc6_ = new SaveGameCreature(_loc2_);
               this.m_vCreatures.push(_loc6_);
            }
            for each(_loc3_ in param1.m_vVisibleHexes)
            {
               _loc7_ = new SaveGameHex(_loc3_);
               this.m_vVisibleHexes.push(_loc7_);
            }
            for(_loc4_ in param1.m_dictMapLabels)
            {
               this.m_dictMapLabels[_loc4_] = param1.m_dictMapLabels[_loc4_];
            }
            for(_loc4_ in param1.m_dictMinimapLabels)
            {
               this.m_dictMinimapLabels[_loc4_] = param1.m_dictMinimapLabels[_loc4_];
            }
            this.m_strCharname = param1.m_strCharname;
            this.m_fHours = param1.m_fHours;
            this.m_fMoney = param1.m_fMoney;
            this.m_vKnownRecipes = param1.m_vKnownRecipes;
            this.m_ptHex = new FlxPoint(param1.m_ptHex.x,param1.m_ptHex.y);
            this.m_bAlive = param1.m_bAlive;
            this.m_bScavTut = param1.m_bScavTut;
            for each(_loc5_ in param1.m_vNonInventoryItems)
            {
               _loc8_ = new SaveGameItem(_loc5_);
               this.m_vNonInventoryItems.push(_loc8_);
            }
            this.m_objPlayer = new SaveGameCreature(param1.m_objPlayer);
            this.m_strVersion = param1.m_strVersion;
         }
      }
      
      public function destroy() : void
      {
         var _loc1_:int = 0;
         this.m_strUsername = null;
         this.m_vEventQueue = null;
         this.m_vEncounterTriggersRemaining = null;
         if(this.m_vVisibleHexes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vVisibleHexes.length)
            {
               this.m_vVisibleHexes[_loc1_].destroy();
               this.m_vVisibleHexes[_loc1_] = null;
               _loc1_++;
            }
            this.m_vVisibleHexes = null;
         }
         if(this.m_vCreatures != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vCreatures.length)
            {
               this.m_vCreatures[_loc1_].destroy();
               this.m_vCreatures[_loc1_] = null;
               _loc1_++;
            }
            this.m_vCreatures = null;
         }
         for each(_loc1_ in this.m_dictMapLabels)
         {
            delete this.m_dictMapLabels[_loc1_];
         }
         this.m_dictMapLabels = null;
         for each(_loc1_ in this.m_dictMinimapLabels)
         {
            delete this.m_dictMinimapLabels[_loc1_];
         }
         this.m_dictMinimapLabels = null;
         this.m_strVersion = null;
         this.m_strCharname = null;
         this.m_vKnownRecipes = null;
         this.m_ptHex = null;
         if(this.m_vNonInventoryItems != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vNonInventoryItems.length)
            {
               this.m_vNonInventoryItems[_loc1_].destroy();
               this.m_vNonInventoryItems[_loc1_] = null;
               _loc1_++;
            }
            this.m_vNonInventoryItems = null;
         }
         this.m_objPlayer = DataHandler.DestroyObject(this.m_objPlayer);
      }
   }
}

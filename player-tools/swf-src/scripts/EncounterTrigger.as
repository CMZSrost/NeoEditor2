package
{
   import org.flixel.FlxPoint;
   
   public class EncounterTrigger
   {
       
      
      public var m_strName:String;
      
      public var m_bLocBased:Boolean;
      
      public var m_bDateBased:Boolean;
      
      public var m_bHexBased:Boolean;
      
      public var m_bUnique:Boolean;
      
      public var m_bAIPassable:Boolean;
      
      public var m_nEncounterID:uint;
      
      public var m_fChance:Number;
      
      private var m_ptLocation:FlxPoint;
      
      private var m_nRadius:uint;
      
      private var m_dateMin:Date;
      
      private var m_dateMax:Date;
      
      private var m_aHexTypes:Array;
      
      public function EncounterTrigger(param1:String, param2:uint, param3:Number)
      {
         super();
         this.m_strName = param1;
         this.m_bDateBased = false;
         this.m_bHexBased = false;
         this.m_bLocBased = false;
         this.m_bUnique = false;
         this.m_bAIPassable = true;
         this.m_dateMin = new Date(1000,0,1,0);
         this.m_dateMax = new Date(9999,11,31,23);
         this.m_nEncounterID = param2;
         this.m_fChance = param3;
      }
      
      public function Triggered(param1:Date, param2:FlxPoint, param3:uint) : Boolean
      {
         var _loc7_:Encounter = null;
         var _loc4_:* = true;
         var _loc5_:Boolean = true;
         var _loc6_:* = true;
         if(this.m_bHexBased)
         {
            _loc4_ = this.m_aHexTypes.indexOf(param3 + 1) >= 0;
         }
         if(this.m_bDateBased)
         {
            _loc5_ = param1.hours >= this.m_dateMin.hours && param1.hours <= this.m_dateMax.hours && param1.date >= this.m_dateMin.date && param1.date <= this.m_dateMax.date && param1.month >= this.m_dateMin.month && param1.month <= this.m_dateMax.month && param1.fullYear >= this.m_dateMin.fullYear && param1.fullYear <= this.m_dateMax.fullYear;
         }
         if(this.m_bLocBased)
         {
            _loc6_ = MapUtils.GetHexDistance(param2,this.m_ptLocation) <= this.m_nRadius;
         }
         if(_loc4_ && _loc6_ && _loc5_ && Math.random() < this.m_fChance)
         {
            return (_loc7_ = DataHandler.GetEncounter(this.m_nEncounterID)).PreconditionsOK(PlayState.m_objInstance.sprPlayer);
         }
         return false;
      }
      
      public function SetArea(param1:FlxPoint, param2:uint) : void
      {
         this.m_ptLocation = new FlxPoint(param1.x,param1.y);
         this.m_nRadius = param2;
         this.m_bLocBased = true;
      }
      
      public function get Location() : FlxPoint
      {
         return this.m_ptLocation;
      }
      
      public function get Radius() : uint
      {
         return this.m_nRadius;
      }
      
      public function set HexTypes(param1:Array) : void
      {
         this.m_aHexTypes = param1;
         this.m_bHexBased = true;
      }
      
      public function set MinDate(param1:Date) : void
      {
         this.m_dateMin = param1;
         this.m_bDateBased = true;
      }
      
      public function set MaxDate(param1:Date) : void
      {
         this.m_dateMax = param1;
         this.m_bDateBased = true;
      }
   }
}

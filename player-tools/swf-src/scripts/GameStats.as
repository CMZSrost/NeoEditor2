package
{
   import flash.display.BitmapData;
   
   public class GameStats
   {
       
      
      public var m_fHoursSurvived:Number;
      
      public var m_nMorality:int;
      
      public var m_strCauseOfDeath:String;
      
      public var m_strLog:String;
      
      public var m_strConditions:String;
      
      public var m_nEncounterID:int;
      
      public var m_bmpPlayer:BitmapData;
      
      public function GameStats()
      {
         super();
      }
      
      public function destroy() : void
      {
         this.m_strCauseOfDeath = null;
         this.m_strConditions = null;
         this.m_strLog = null;
      }
   }
}

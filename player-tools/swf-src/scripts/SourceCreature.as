package
{
   public class SourceCreature
   {
       
      
      public var m_strName:String;
      
      public var x:int;
      
      public var y:int;
      
      public var m_nCreatureID:int;
      
      public var m_nMin:int;
      
      public var m_nMax:int;
      
      public var m_fWeight:Number;
      
      public var m_fTempChance:Number;
      
      public function SourceCreature(param1:String, param2:int, param3:int, param4:int, param5:Number, param6:int = 1, param7:int = 1)
      {
         super();
         this.m_strName = param1;
         this.x = param2;
         this.y = param3;
         this.m_nMin = param6;
         this.m_nMax = param7;
         this.m_fWeight = param5;
         this.m_nCreatureID = param4;
      }
      
      public function destroy() : void
      {
         this.m_strName = null;
      }
   }
}

package
{
   public class BarterHex
   {
      
      public static const BARTER_NONE:uint = 0;
      
      public static const BARTER_BUYSELL:uint = 1;
      
      public static const BARTER_SELL:uint = 2;
       
      
      public var m_nID:int;
      
      public var m_bBuys:Boolean;
      
      public var m_nX:int;
      
      public var m_nY:int;
      
      public var m_nTreasureID:int;
      
      public function BarterHex(param1:int, param2:Boolean, param3:int, param4:int, param5:int)
      {
         super();
         this.m_nID = param1;
         this.m_bBuys = param2;
         this.m_nX = param3;
         this.m_nY = param4;
         this.m_nTreasureID = param5;
      }
   }
}

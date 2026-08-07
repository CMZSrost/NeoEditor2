package
{
   public class SaveGameCondition
   {
       
      
      public var m_nID:uint;
      
      public var m_fDate:Number;
      
      public var m_nStacked:uint;
      
      public function SaveGameCondition(param1:Object = null)
      {
         super();
         if(param1 != null)
         {
            this.m_nID = param1.m_nID;
            this.m_fDate = param1.m_fDate;
            this.m_nStacked = param1.m_nStacked;
         }
      }
   }
}

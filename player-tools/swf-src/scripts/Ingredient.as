package
{
   public class Ingredient
   {
       
      
      public var m_nID:int;
      
      public var m_strName:String;
      
      public var m_vRequiredProps:Vector.<int>;
      
      public var m_vForbidProps:Vector.<int>;
      
      public function Ingredient(param1:int, param2:String, param3:String, param4:String)
      {
         var _loc5_:Array = null;
         var _loc6_:String = null;
         super();
         this.m_nID = param1;
         this.m_strName = param2;
         this.m_vRequiredProps = new Vector.<int>();
         this.m_vForbidProps = new Vector.<int>();
         if(param3.length > 0)
         {
            _loc5_ = param3.split("&");
            for each(_loc6_ in _loc5_)
            {
               this.m_vRequiredProps.push(DataHandler.GetRemapID(new DataRef(_loc6_),"m_aItemProps"));
            }
         }
         if(param4.length > 0)
         {
            _loc5_ = param4.split("&");
            for each(_loc6_ in _loc5_)
            {
               this.m_vForbidProps.push(DataHandler.GetRemapID(new DataRef(_loc6_),"m_aItemProps"));
            }
         }
      }
      
      public function destroy() : void
      {
         this.m_strName = null;
         this.m_vForbidProps = null;
         this.m_vRequiredProps = null;
      }
      
      public function ItemMatches(param1:ItemInstance) : Boolean
      {
         var _loc2_:int = 0;
         for each(_loc2_ in this.m_vRequiredProps)
         {
            if(param1.ItemDefinition.m_vProperties.indexOf(_loc2_) < 0)
            {
               return false;
            }
         }
         for each(_loc2_ in this.m_vForbidProps)
         {
            if(param1.ItemDefinition.m_vProperties.indexOf(_loc2_) >= 0)
            {
               return false;
            }
         }
         return true;
      }
   }
}

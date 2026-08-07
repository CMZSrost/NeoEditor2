package
{
   import org.flixel.*;
   
   public class DataRef
   {
       
      
      private var m_strModID:String;
      
      private var m_nID:int;
      
      private var m_strID:String;
      
      public function DataRef(param1:String, param2:Boolean = false)
      {
         super();
         this.m_strModID = "";
         this.m_nID = 0;
         this.m_strID = "";
         if(param1 == null || param1.length <= 0)
         {
            return;
         }
         param1 = param1.replace(/ /gi,"");
         var _loc3_:Array = param1.split(":");
         if(_loc3_.length > 1)
         {
            this.m_strModID = _loc3_[0];
            _loc3_ = [_loc3_[1]];
         }
         _loc3_ = String(_loc3_[0]).split(".");
         if(_loc3_.length > 1)
         {
            this.m_strID = _loc3_[0] + "." + _loc3_[1];
         }
         else if(param2)
         {
            this.m_strID = _loc3_[0];
         }
         else
         {
            this.m_nID = _loc3_[0];
         }
         _loc3_.length = 0;
         _loc3_ = null;
      }
      
      public static function indexOf(param1:DataRef, param2:Array) : int
      {
         var _loc3_:int = 0;
         while(_loc3_ < param2.length)
         {
            if(DataRef(param2[_loc3_]).Mod == param1.Mod)
            {
               if(param1.StrID != "")
               {
                  if(DataRef(param2[_loc3_]).StrID == param1.StrID)
                  {
                     return _loc3_;
                  }
               }
               else if(DataRef(param2[_loc3_]).ID == param1.ID)
               {
                  return _loc3_;
               }
            }
            _loc3_++;
         }
         return -1;
      }
      
      public function destroy() : void
      {
         this.m_strID = null;
         this.m_strModID = null;
      }
      
      public function get Mod() : String
      {
         return this.m_strModID;
      }
      
      public function get StrID() : String
      {
         return this.m_strID;
      }
      
      public function set StrID(param1:String) : void
      {
         this.m_strID = param1;
      }
      
      public function get ID() : int
      {
         return this.m_nID;
      }
   }
}

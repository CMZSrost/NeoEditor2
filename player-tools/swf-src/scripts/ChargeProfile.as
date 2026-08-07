package
{
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import org.flixel.*;
   
   public class ChargeProfile
   {
       
      
      public var m_nID:int;
      
      public var m_strName:String;
      
      public var m_strItemID:String;
      
      public var m_fPerUse:Number;
      
      public var m_fPerHour:Number;
      
      public var m_fPerHourEquipped:Number;
      
      public var m_fPerHex:Number;
      
      public var m_bDegrade:Boolean;
      
      public const DISCHARGE_USE:uint = 0;
      
      public const DISCHARGE_TIME:uint = 1;
      
      public const DISCHARGE_EQUIPPED:uint = 2;
      
      public const DISCHARGE_HEX:uint = 3;
      
      public function ChargeProfile()
      {
         super();
      }
      
      public function GetQuantity(param1:int, param2:Number, param3:int, param4:Boolean) : Number
      {
         var _loc5_:Number = (_loc5_ = (_loc5_ = 0) + this.m_fPerUse * param1) + this.m_fPerHour * param2;
         if(param4)
         {
            _loc5_ += this.m_fPerHourEquipped * param2;
         }
         return _loc5_ + this.m_fPerHex * param3;
      }
   }
}

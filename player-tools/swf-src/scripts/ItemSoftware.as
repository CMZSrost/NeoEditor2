package
{
   import flash.display.BitmapData;
   import org.flixel.*;
   
   public class ItemSoftware extends ItemInstance
   {
       
      
      public function ItemSoftware(param1:Item, param2:int = 0, param3:int = 0)
      {
         super(param1,param2,param3);
      }
      
      private function IsAccessible() : Boolean
      {
         if(m_objParentContainer != null && m_objParentContainer is ItemHardware && ItemHardware(m_objParentContainer).bSoftwareAccessible == false)
         {
            return false;
         }
         return true;
      }
      
      override public function GetImagePeek() : BitmapData
      {
         return m_sprIMG.pixels;
      }
      
      override public function SwapImage(param1:Boolean) : void
      {
         super.SwapImage(param1);
         if(m_sprIMG != null && m_objParentContainer != null && m_objParentContainer is ItemHardware && ItemHardware(m_objParentContainer).bSoftwareAccessible == false)
         {
            m_sprIMG.pixels = new BitmapData(m_sprIMG.pixels.width,m_sprIMG.pixels.height,true,4278190080);
         }
      }
      
      override public function TimeDegrade(param1:Date) : void
      {
         var _loc2_:ItemInstance = null;
         var _loc3_:int = int(m_vStack.length - 1);
         while(_loc3_ >= 0)
         {
            _loc2_ = m_vStack[_loc3_];
            _loc2_.TimeDegrade(param1);
            if(_loc2_.fDurability <= 0)
            {
               _loc2_.ReplaceDegradedItem();
            }
            _loc3_--;
         }
         _loc3_ = int(m_vComponents.length - 1);
         while(_loc3_ >= 0)
         {
            _loc2_ = m_vComponents[_loc3_];
            _loc2_.TimeDegrade(param1);
            _loc3_--;
         }
         var _loc4_:Number = Number(param1.getTime());
         if(m_objLastDegradeDate == null)
         {
            m_objLastDegradeDate = new Date();
            m_objLastDegradeDate.setTime(_loc4_);
            return;
         }
         var _loc5_:Number = Number(m_objLastDegradeDate.getTime());
         var _loc6_:Number = (_loc4_ - _loc5_) / 1000 / 60 / 60;
         fDurability -= m_objItem.fDegradePerHour * _loc6_;
         if(isNaN(fDurability))
         {
            fDurability = 1;
         }
         if(fDurability <= 0)
         {
            fDurability = 0;
         }
         m_objLastDegradeDate.setTime(_loc4_);
      }
      
      override public function Discharge(param1:int, param2:Number, param3:int) : void
      {
         if(m_objParentContainer != null)
         {
            m_objParentContainer.Discharge(param1,param2,param3);
         }
      }
      
      override public function IsCharged(param1:int, param2:Number, param3:int) : Boolean
      {
         if(m_objParentContainer != null && m_objParentContainer.ItemDefinition.nGroupID != 1)
         {
            return m_objParentContainer.IsCharged(param1,param2,param3);
         }
         return false;
      }
      
      override public function GetCharges(param1:String) : Vector.<ItemInstance>
      {
         if(m_objParentContainer != null)
         {
            return m_objParentContainer.GetCharges(param1);
         }
         return new Vector.<ItemInstance>();
      }
      
      override public function GetRemainingChargeInfo() : String
      {
         if(this.IsAccessible() == false)
         {
            return "?";
         }
         return super.GetRemainingChargeInfo();
      }
      
      override public function GetTotalValue(param1:Boolean = true) : Number
      {
         if(this.IsAccessible() == false)
         {
            return 0;
         }
         return super.GetTotalValue(param1);
      }
      
      override public function Description() : String
      {
         if(this.IsAccessible() == false)
         {
            return "?";
         }
         return super.Description();
      }
   }
}

package
{
   import flash.events.*;
   import org.flixel.*;
   
   public class ItemHardware extends ItemInstance
   {
       
      
      public var bSoftwareAccessible:Boolean;
      
      public function ItemHardware(param1:Item, param2:int = 0, param3:int = 0, param4:Boolean = true)
      {
         this.bSoftwareAccessible = true;
         super(param1,param2,param3);
         this.CheckSoftwareAccessible();
      }
      
      public function GetTotalValueHidden() : Number
      {
         var _loc1_:Number = 0;
         var _loc2_:Boolean = this.bSoftwareAccessible;
         this.bSoftwareAccessible = true;
         _loc1_ = GetTotalValue();
         this.bSoftwareAccessible = _loc2_;
         return _loc1_;
      }
      
      public function CheckSoftwareAccessible() : void
      {
         this.bSoftwareAccessible = false;
         if((ItemDefinition.m_bDischargeTime || ItemDefinition.m_bDischargeEquip) && IsCharged(0,0.1,0) && ItemDefinition.vUseSlots.length > 0 && ItemDefinition.strName.indexOf("locked") < 0)
         {
            this.bSoftwareAccessible = true;
         }
      }
      
      public function UpdateSoftwareVisible() : void
      {
         var _loc1_:ItemInstance = null;
         var _loc2_:ItemSoftware = null;
         for each(_loc1_ in vItems)
         {
            if(_loc1_ is ItemSoftware && ItemSoftware(_loc1_).m_sprIMG != null)
            {
               _loc1_.SwapImage(true);
               for each(_loc2_ in _loc1_.m_vStack)
               {
                  _loc2_.SwapImage(true);
               }
            }
         }
      }
      
      override public function destroy() : void
      {
         super.destroy();
      }
      
      override public function Clone(param1:Boolean = true) : ItemInstance
      {
         var _loc3_:ItemInstance = null;
         var _loc2_:ItemHardware = super.Clone(param1) as ItemHardware;
         _loc2_.bSoftwareAccessible = true;
         _loc2_.vItems = Vector.<ItemInstance>([]);
         if(param1)
         {
            for each(_loc3_ in vItems)
            {
               _loc2_.AddItem(_loc3_.Clone(),new FlxPoint(_loc3_.ptOffset.x,_loc3_.ptOffset.y));
            }
         }
         _loc2_.CheckSoftwareAccessible();
         return _loc2_;
      }
      
      override public function AddItem(param1:ItemInstance, param2:FlxPoint) : void
      {
         if(param1 is ItemSoftware && this.bSoftwareAccessible == false)
         {
            return;
         }
         super.AddItem(param1,param2);
      }
      
      override public function RemoveItem(param1:ItemInstance, param2:Boolean = false) : ItemInstance
      {
         if(param1 is ItemSoftware && this.bSoftwareAccessible == false)
         {
            return null;
         }
         return super.RemoveItem(param1,param2);
      }
      
      override public function GetItems(param1:int = -1, param2:int = -1) : Vector.<ItemInstance>
      {
         var _loc5_:ItemInstance = null;
         var _loc3_:Vector.<ItemInstance> = super.GetItems(param1,param2);
         if(this.bSoftwareAccessible)
         {
            return _loc3_;
         }
         var _loc4_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         for each(_loc5_ in _loc3_)
         {
            if(!(_loc5_ is ItemSoftware))
            {
               _loc4_.push(_loc5_);
            }
         }
         return _loc4_;
      }
      
      override public function AcceptsItem(param1:ItemInstance) : Boolean
      {
         var _loc2_:Boolean = super.AcceptsItem(param1);
         if(_loc2_ && param1 is ItemSoftware)
         {
            _loc2_ = this.bSoftwareAccessible;
         }
         return _loc2_;
      }
      
      override public function ChangeMode(param1:Item, param2:Boolean = false) : void
      {
         super.ChangeMode(param1,param2);
         this.CheckSoftwareAccessible();
         this.UpdateSoftwareVisible();
      }
      
      override public function set SaveData(param1:SaveGameItem) : void
      {
         this.bSoftwareAccessible = true;
         super.SaveData = param1;
         this.CheckSoftwareAccessible();
      }
   }
}

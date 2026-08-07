package
{
   import org.flixel.FlxPoint;
   
   public class SaveGameItem
   {
       
      
      public var fDurability:Number;
      
      public var strID:String;
      
      public var ptOffset:FlxPoint;
      
      public var m_fAngle:Number;
      
      public var m_fZoom:Number;
      
      public var m_nSlotIndex:int;
      
      public var m_vItems:Vector.<SaveGameItem>;
      
      public var m_vStack:Vector.<SaveGameItem>;
      
      public var m_vComponents:Vector.<SaveGameItem>;
      
      public var Identified:Boolean;
      
      public var m_fSleepAwareness:Number;
      
      public var m_fVisibility:Number;
      
      public var WetTempAdjustMod:Number;
      
      public var m_fHealPerHourMod:Number;
      
      public var fSleepQuality:Number;
      
      public function SaveGameItem(param1:Object = null)
      {
         var _loc2_:Object = null;
         super();
         this.m_vItems = new Vector.<SaveGameItem>();
         this.m_vStack = new Vector.<SaveGameItem>();
         this.m_vComponents = new Vector.<SaveGameItem>();
         if(param1 != null)
         {
            this.fDurability = param1.fDurability;
            this.strID = param1.strID;
            this.ptOffset = new FlxPoint(param1.ptOffset.x,param1.ptOffset.y);
            this.m_fAngle = param1.m_fAngle;
            this.m_fZoom = param1.m_fZoom;
            this.m_nSlotIndex = param1.m_nSlotIndex;
            this.Identified = param1.Identified;
            for each(_loc2_ in param1.m_vItems)
            {
               this.m_vItems.push(new SaveGameItem(_loc2_));
            }
            for each(_loc2_ in param1.m_vStack)
            {
               this.m_vStack.push(new SaveGameItem(_loc2_));
            }
            for each(_loc2_ in param1.m_vComponents)
            {
               this.m_vComponents.push(new SaveGameItem(_loc2_));
            }
            this.m_fSleepAwareness = param1.m_fSleepAwareness;
            this.m_fVisibility = param1.m_fVisibility;
            this.WetTempAdjustMod = param1.WetTempAdjustMod;
            this.m_fHealPerHourMod = param1.m_fHealPerHourMod;
            this.fSleepQuality = param1.fSleepQuality;
         }
      }
      
      public function destroy() : void
      {
         var _loc1_:int = 0;
         this.strID = null;
         this.ptOffset = null;
         if(this.m_vItems != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vItems.length)
            {
               this.m_vItems[_loc1_].destroy();
               this.m_vItems[_loc1_] = null;
               _loc1_++;
            }
            this.m_vItems = null;
         }
         if(this.m_vStack != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vStack.length)
            {
               this.m_vStack[_loc1_].destroy();
               this.m_vStack[_loc1_] = null;
               _loc1_++;
            }
            this.m_vStack = null;
         }
         if(this.m_vComponents != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vComponents.length)
            {
               this.m_vComponents[_loc1_].destroy();
               this.m_vComponents[_loc1_] = null;
               _loc1_++;
            }
            this.m_vComponents = null;
         }
      }
   }
}

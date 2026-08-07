package
{
   import flash.display.BitmapData;
   import flash.display.MovieClip;
   import flash.geom.Matrix;
   import flash.utils.Dictionary;
   
   public class Item
   {
       
      
      public var nGroupID:uint;
      
      public var nSubgroupID:uint;
      
      public var strName:String;
      
      public var strDesc:String;
      
      public var strDescAlt:String;
      
      public var vImageList:Vector.<BitmapData>;
      
      public var vImageListNames:Vector.<String>;
      
      public var vSpriteList:Vector.<BitmapData>;
      
      public var dictImageUsage:Dictionary;
      
      public var dictSpriteUsage:Dictionary;
      
      public var fMonetaryValue:Number;
      
      public var fMonetaryValueAlt:Number;
      
      public var fWeight:Number;
      
      public var aCapacities:Array;
      
      public var m_aEquipConditions:Array;
      
      public var m_aPossessConditions:Array;
      
      public var m_aUseConditions:Array;
      
      public var fDurability:Number;
      
      public var fDegradePerHour:Number;
      
      public var fEquipDegradePerHour:Number;
      
      public var fDegradePerUse:Number;
      
      public var vDegradeTreasureIDs:Vector.<int>;
      
      public var nTreasureID:uint;
      
      public var m_nComponentID:uint;
      
      public var m_nCondID:uint;
      
      public var bSocketLocked:Boolean;
      
      public var vEquipSlots:Vector.<int>;
      
      public var vUseSlots:Vector.<int>;
      
      public var aContentIDs:Array;
      
      public var nFormatID:uint;
      
      public var m_vProperties:Vector.<int>;
      
      public var nSlotDepth:uint;
      
      public var label:String;
      
      public var icon:MovieClip;
      
      public var m_bMirrored:Boolean;
      
      public var m_vModeLabels:Vector.<String>;
      
      public var m_vModeIDs:Vector.<String>;
      
      public var m_aAttackModes:Array;
      
      public var m_vSounds:Vector.<String>;
      
      public var m_nStackLimit:uint;
      
      public var m_vChargeProfiles:Vector.<ChargeProfile>;
      
      public var m_bIgnoreSubGroupWhenStacking:Boolean;
      
      public var m_fZoom:Number;
      
      public var m_nUsefulness:int;
      
      public var m_bDischargeUse:Boolean;
      
      public var m_bDischargeTime:Boolean;
      
      public var m_bDischargeEquip:Boolean;
      
      public var m_bDischargeHex:Boolean;
      
      public function Item()
      {
         super();
         this.m_fZoom = 1;
         this.m_nUsefulness = 0;
         this.aCapacities = new Array();
         this.m_aEquipConditions = new Array();
         this.m_aPossessConditions = new Array();
         this.m_aUseConditions = new Array();
         this.aContentIDs = new Array();
         this.m_aAttackModes = new Array();
         this.m_vSounds = new Vector.<String>();
         this.vImageList = new Vector.<BitmapData>();
         this.vImageListNames = new Vector.<String>();
         this.vSpriteList = new Vector.<BitmapData>();
         this.dictImageUsage = new Dictionary();
         this.dictSpriteUsage = new Dictionary();
         this.vEquipSlots = new Vector.<int>();
         this.m_vProperties = new Vector.<int>();
         this.m_vChargeProfiles = new Vector.<ChargeProfile>();
         this.m_vModeIDs = new Vector.<String>();
         this.m_vModeLabels = new Vector.<String>();
         this.icon = null;
         this.m_bIgnoreSubGroupWhenStacking = false;
         this.m_bDischargeUse = false;
         this.m_bDischargeTime = false;
         this.m_bDischargeEquip = false;
         this.m_bDischargeHex = false;
      }
      
      public function Initialize() : void
      {
         var _loc1_:ChargeProfile = null;
         for each(_loc1_ in this.m_vChargeProfiles)
         {
            if(_loc1_.m_fPerHex != 0)
            {
               this.m_bDischargeHex = true;
            }
            if(_loc1_.m_fPerHour != 0)
            {
               this.m_bDischargeTime = true;
            }
            if(_loc1_.m_fPerHourEquipped != 0)
            {
               this.m_bDischargeEquip = true;
            }
            if(_loc1_.m_fPerUse != 0)
            {
               this.m_bDischargeUse = true;
            }
         }
         this.m_nUsefulness += this.vUseSlots.length + this.vEquipSlots.length;
      }
      
      public function Clone() : Item
      {
         var _loc1_:Item = new Item();
         _loc1_.nGroupID = this.nGroupID;
         _loc1_.nSubgroupID = this.nSubgroupID;
         _loc1_.strName = this.strName;
         _loc1_.strDesc = this.strDesc;
         _loc1_.strDescAlt = this.strDescAlt;
         _loc1_.vImageList = this.vImageList.concat();
         _loc1_.vImageListNames = this.vImageListNames.concat();
         _loc1_.vSpriteList = this.vSpriteList.concat();
         _loc1_.dictImageUsage = this.dictImageUsage;
         _loc1_.dictSpriteUsage = this.dictSpriteUsage;
         _loc1_.fMonetaryValue = this.fMonetaryValue;
         _loc1_.fMonetaryValueAlt = this.fMonetaryValueAlt;
         _loc1_.m_bIgnoreSubGroupWhenStacking = this.m_bIgnoreSubGroupWhenStacking;
         _loc1_.fWeight = this.fWeight;
         _loc1_.aCapacities = this.aCapacities.concat();
         _loc1_.m_aEquipConditions = this.m_aEquipConditions.concat();
         _loc1_.m_aPossessConditions = this.m_aPossessConditions.concat();
         _loc1_.m_aUseConditions = this.m_aUseConditions.concat();
         _loc1_.fDurability = this.fDurability;
         _loc1_.fDegradePerHour = this.fDegradePerHour;
         _loc1_.fEquipDegradePerHour = this.fEquipDegradePerHour;
         _loc1_.fDegradePerUse = this.fDegradePerUse;
         _loc1_.vDegradeTreasureIDs = this.vDegradeTreasureIDs.concat();
         _loc1_.nTreasureID = this.nTreasureID;
         _loc1_.m_nComponentID = this.m_nComponentID;
         _loc1_.m_nCondID = this.m_nCondID;
         _loc1_.bSocketLocked = this.bSocketLocked;
         _loc1_.vEquipSlots = this.vEquipSlots.concat();
         _loc1_.vUseSlots = this.vUseSlots.concat();
         _loc1_.aContentIDs = this.aContentIDs.concat();
         _loc1_.nFormatID = this.nFormatID;
         _loc1_.nSlotDepth = this.nSlotDepth;
         _loc1_.label = this.label;
         _loc1_.icon = this.icon;
         _loc1_.m_bMirrored = this.m_bMirrored;
         _loc1_.m_aAttackModes = this.m_aAttackModes.concat();
         _loc1_.m_vSounds = this.m_vSounds.concat();
         _loc1_.m_nStackLimit = this.m_nStackLimit;
         _loc1_.m_vChargeProfiles = this.m_vChargeProfiles.concat();
         _loc1_.m_fZoom = this.m_fZoom;
         _loc1_.m_vProperties = this.m_vProperties.concat();
         _loc1_.m_vChargeProfiles = this.m_vChargeProfiles.concat();
         _loc1_.m_vModeIDs = this.m_vModeIDs.concat();
         _loc1_.m_vModeLabels = this.m_vModeLabels.concat();
         _loc1_.m_nUsefulness = this.m_nUsefulness;
         return _loc1_;
      }
      
      public function SetRes(param1:Number) : void
      {
         var _loc2_:String = null;
         var _loc3_:int = 0;
         var _loc4_:BitmapData = null;
         var _loc5_:Matrix = null;
         var _loc6_:BitmapData = null;
         if(param1 != this.m_fZoom || GUIValues.m_bOverrideZoom)
         {
            _loc2_ = "";
            if(param1 == 2)
            {
               _loc2_ = DataHandler.m_strZoomPrefix;
            }
            _loc3_ = 0;
            while(_loc3_ < this.vImageList.length)
            {
               _loc4_ = DataHandler.GetImage(this.vImageListNames[_loc3_],_loc2_);
               if(this.m_bMirrored)
               {
                  (_loc5_ = new Matrix()).scale(-1,1);
                  _loc5_.translate(_loc4_.width,0);
                  (_loc6_ = new BitmapData(_loc4_.width,_loc4_.height,true,0)).draw(_loc4_,_loc5_);
                  _loc4_ = _loc6_;
               }
               this.vImageList[_loc3_] = _loc4_;
               _loc3_++;
            }
            this.m_fZoom = param1;
         }
      }
   }
}

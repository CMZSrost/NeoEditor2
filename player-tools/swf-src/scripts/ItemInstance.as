package
{
   import flash.display.BitmapData;
   import flash.events.Event;
   import flash.geom.Matrix;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import org.flixel.*;
   
   public class ItemInstance extends FlxGroup
   {
      
      public static const MODE_STORED:uint = 0;
      
      public static const MODE_STORED_FULL:uint = 1;
      
      public static const MODE_EQUIPPED:uint = 2;
      
      public static const MODE_EQUIPPED_FULL:uint = 3;
      
      public static const MODE_HELD:uint = 4;
      
      public static const MODE_HELD_FULL:uint = 5;
       
      
      public var m_sprIMG:FlxSprite;
      
      public var m_sprBracket:FlxSprite;
      
      public var strDesc:String;
      
      public var fDurability:Number;
      
      protected var m_objItem:Item;
      
      public var vItems:Vector.<ItemInstance>;
      
      public var ptOffset:FlxPoint;
      
      private var ptScrolled:FlxPoint;
      
      public var bSocketed:Boolean;
      
      public var grpItemPanelSlot:GUIInventorySlot;
      
      public var nSlotDepth:int;
      
      public var fDrawDepth:Number;
      
      public var m_objLastDegradeDate:Date;
      
      public var m_objParentContainer:ItemInstance;
      
      public var m_fAngle:Number;
      
      public var nFormatID:uint;
      
      public var aContentIDs:Array;
      
      public var bRigidContainer:Boolean;
      
      public var m_vAttackModes:Vector.<AttackMode>;
      
      public var m_vStack:Vector.<ItemInstance>;
      
      public var m_vComponents:Vector.<ItemInstance>;
      
      private var m_bWeightChanged:Boolean;
      
      private var m_fWeightContents:Number;
      
      public var m_nSize:int;
      
      public var m_objProxy:ItemInstance;
      
      public var m_bContents:Boolean;
      
      public var m_fZoom:Number;
      
      protected var m_fX:Number;
      
      protected var m_fY:Number;
      
      private var m_fWidth:Number;
      
      private var m_fHeight:Number;
      
      protected var m_fAlpha:Number;
      
      public var m_nImageIndex:int;
      
      public var m_txtStackAmount:FlxText;
      
      public var m_sprStackBG:FlxSprite;
      
      private var m_bGhosted:Boolean;
      
      public var m_bEquipped:Boolean;
      
      public var m_bDegrades:Boolean;
      
      private var m_bIdentified:Boolean;
      
      private var m_bMirrored:Boolean;
      
      public function ItemInstance(param1:Item, param2:int = 0, param3:int = 0, param4:Boolean = true)
      {
         super();
         this.m_fAngle = 0;
         this.m_fAlpha = 1;
         this.m_fX = 0;
         this.m_fY = 0;
         this.m_nSize = 0;
         this.m_fZoom = 1;
         this.m_nImageIndex = -1;
         this.m_vAttackModes = new Vector.<AttackMode>();
         this.m_vStack = new Vector.<ItemInstance>();
         this.m_vComponents = new Vector.<ItemInstance>();
         this.m_bContents = param4;
         this.ptOffset = new FlxPoint();
         this.ptScrolled = new FlxPoint();
         this.ItemDefinition = param1;
         this.m_bContents = true;
         this.m_bGhosted = false;
         this.WeightChanged = true;
         this.m_fWeightContents = 0;
         this.fDrawDepth = 0;
      }
      
      public static function MirrorImage(param1:BitmapData) : BitmapData
      {
         var _loc2_:Matrix = new Matrix();
         _loc2_.scale(-1,1);
         _loc2_.translate(param1.width,0);
         var _loc3_:BitmapData = new BitmapData(param1.width,param1.height,true,0);
         _loc3_.draw(param1,_loc2_);
         return _loc3_;
      }
      
      override public function destroy() : void
      {
         var _loc1_:ItemInstance = null;
         this.m_sprIMG = DataHandler.DestroyObject(this.m_sprIMG);
         this.m_sprBracket = DataHandler.DestroyObject(this.m_sprBracket);
         this.strDesc = null;
         this.m_objItem = null;
         for each(_loc1_ in this.vItems)
         {
            _loc1_.destroy();
         }
         this.vItems = null;
         this.ptOffset = null;
         this.grpItemPanelSlot = null;
         this.m_objLastDegradeDate = null;
         this.m_objParentContainer = null;
         this.aContentIDs = null;
         this.m_vAttackModes = null;
         for each(_loc1_ in this.m_vStack)
         {
            _loc1_.destroy();
         }
         this.m_vStack = null;
         for each(_loc1_ in this.m_vComponents)
         {
            _loc1_.destroy();
         }
         this.m_vComponents = null;
         this.m_objProxy = null;
         this.m_txtStackAmount = DataHandler.DestroyObject(this.m_txtStackAmount);
         this.m_sprStackBG = DataHandler.DestroyObject(this.m_sprStackBG);
         super.destroy();
      }
      
      public function Clone(param1:Boolean = true) : ItemInstance
      {
         var _loc3_:ItemInstance = null;
         var _loc4_:int = 0;
         var _loc5_:ItemInstance = null;
         var _loc2_:ItemInstance = DataHandler.GetItem(this.ItemDefinition.nGroupID + "." + this.ItemDefinition.nSubgroupID,false);
         this.CopyPropertiesTo(_loc2_);
         _loc2_.vItems = Vector.<ItemInstance>([]);
         if(param1)
         {
            for each(_loc5_ in this.vItems)
            {
               _loc2_.AddItem(_loc5_.Clone(),new FlxPoint(_loc5_.ptOffset.x,_loc5_.ptOffset.y));
            }
         }
         _loc2_.m_vComponents = Vector.<ItemInstance>([]);
         for each(_loc5_ in this.m_vComponents)
         {
            _loc2_.m_vComponents.push(_loc5_.Clone());
         }
         _loc2_.m_vStack = Vector.<ItemInstance>([]);
         _loc4_ = 0;
         while(_loc4_ < this.m_vStack.length)
         {
            _loc5_ = this.m_vStack[_loc4_].Clone();
            if(_loc3_ != null)
            {
               _loc3_.StackItem(_loc5_);
            }
            _loc3_ = _loc5_;
            _loc4_++;
         }
         if(_loc3_ != null)
         {
            _loc3_.StackItem(_loc2_);
         }
         return _loc2_;
      }
      
      private function CopyPropertiesTo(param1:ItemInstance) : void
      {
         param1.nFormatID = this.nFormatID;
         param1.aContentIDs = this.aContentIDs.concat();
         param1.fDurability = this.fDurability;
         param1.ptOffset = new FlxPoint(this.ptOffset.x,this.ptOffset.y);
         param1.m_bIdentified = this.m_bIdentified;
         param1.strDesc = this.strDesc;
         param1.m_fAngle = this.m_fAngle;
      }
      
      public function AddItem(param1:ItemInstance, param2:FlxPoint) : void
      {
         var _loc8_:ItemInstance = null;
         var _loc3_:GUIInventorySlot = this.Slot;
         var _loc4_:Boolean = true;
         var _loc5_:Boolean = true;
         var _loc6_:Boolean = this.m_objParentContainer != null && this.m_objParentContainer.ItemDefinition.m_vChargeProfiles.length > 0;
         if(_loc3_ != null && _loc3_.m_sprOwner != null)
         {
            if(this.ItemDefinition.m_vChargeProfiles.length > 0)
            {
               _loc4_ = this.IsCharged(0,0.1,1);
            }
            if(_loc6_)
            {
               _loc5_ = this.m_objParentContainer.IsCharged(0,0.1,1);
            }
         }
         var _loc7_:uint = this.vItems.length;
         this.vItems.push(param1);
         param1.m_objParentContainer = this;
         for each(_loc8_ in param1.m_vStack)
         {
            _loc8_.m_objParentContainer = this;
         }
         param1.ptOffset.x = param2.x;
         param1.ptOffset.y = param2.y;
         param1.x = param2.x;
         param1.y = param2.y;
         if(_loc7_ == 0)
         {
            this.SwapImage(false);
         }
         this.WeightChanged = true;
         if(_loc3_ != null && _loc3_.m_sprOwner != null)
         {
            if(_loc4_ == false && this.IsCharged(0,0.1,1))
            {
               this.UpdatePossessConditions(_loc3_,this.m_objParentContainer);
               _loc3_.UpdateEquipConditions(_loc3_.m_sprOwner,this);
            }
            if(_loc5_ == false && _loc6_ && this.m_objParentContainer.IsCharged(0,0.1,1))
            {
               this.m_objParentContainer.UpdatePossessConditions(_loc3_,this.m_objParentContainer.m_objParentContainer);
               _loc3_.UpdateEquipConditions(_loc3_.m_sprOwner,this.m_objParentContainer);
            }
            param1.UpdatePossessConditions(_loc3_,this);
            if(_loc3_.m_bCarried)
            {
               _loc3_.m_sprOwner.Encumberance += param1.WeightPlusContents;
            }
            if(_loc3_.m_sprOwner == PlayState.m_objInstance.sprPlayer && this.m_vAttackModes.indexOf(_loc3_.m_sprOwner.CurrentAttackMode) >= 0)
            {
               PlayState.m_objInstance.UpdateAttackModeUI();
            }
            if(_loc3_.nSlotIndex == 200)
            {
               _loc3_.m_sprOwner.m_tilCurrentHex.CalculateValue();
               if(_loc3_.m_sprOwner == PlayState.m_objInstance.sprPlayer && _loc3_.m_sprOwner.m_tilCurrentHex.m_nBarterTile == BarterHex.BARTER_BUYSELL)
               {
                  PlayState.m_objInstance.sprPlayer.Money += param1.GetTotalValue();
               }
            }
         }
      }
      
      public function StackItem(param1:ItemInstance) : ItemInstance
      {
         var _loc9_:ItemInstance = null;
         var _loc10_:ItemInstance = null;
         if(this.Slot != null)
         {
            return this.Slot.AddStackedItem(param1,this);
         }
         var _loc2_:uint = param1.StackCount;
         var _loc3_:uint = this.StackCount;
         var _loc4_:uint = this.StackCount;
         var _loc5_:int = 0;
         var _loc6_:FlxPoint = new FlxPoint(this.ptOffset.x,this.ptOffset.y);
         var _loc7_:FlxPoint = new FlxPoint(param1.ptOffset.x,param1.ptOffset.y);
         if((_loc4_ += param1.StackCount) > this.ItemDefinition.m_nStackLimit)
         {
            _loc4_ = this.ItemDefinition.m_nStackLimit;
         }
         _loc5_ = _loc4_ - _loc3_;
         var _loc8_:Vector.<ItemInstance> = param1.m_vStack.splice(-(_loc5_ - 1),_loc5_ - 1);
         if(param1.m_vStack.length > 0)
         {
            (_loc9_ = param1.m_vStack.pop()).m_vStack = param1.m_vStack;
            if(_loc9_.m_sprIMG == null)
            {
               _loc9_.CreateAppearance();
            }
            _loc9_.UpdateStackImage();
            _loc9_.ptOffset = _loc7_;
            for each(_loc10_ in _loc9_.m_vStack)
            {
               _loc10_.m_objParentContainer = _loc9_.m_objParentContainer;
            }
         }
         param1.m_vStack = this.m_vStack.concat();
         param1.m_vStack.push(this);
         param1.m_vStack = param1.m_vStack.concat(_loc8_);
         for each(_loc10_ in param1.m_vStack)
         {
            _loc10_.m_objParentContainer = param1.m_objParentContainer;
         }
         this.m_vStack = new Vector.<ItemInstance>();
         param1.ptOffset = _loc6_;
         param1.Rotate(this.m_fAngle);
         if(this.m_objParentContainer != null && this.m_objParentContainer is ItemCamp)
         {
            ItemCamp(this.m_objParentContainer).IncrementStackingStats(this,_loc5_);
         }
         this.UpdateStackImage();
         param1.UpdateStackImage();
         return _loc9_;
      }
      
      public function RemoveItem(param1:ItemInstance, param2:Boolean = false) : ItemInstance
      {
         var _loc6_:Creature = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:ItemInstance = null;
         var _loc12_:ItemInstance = null;
         var _loc13_:Number = NaN;
         var _loc14_:ItemInstance = null;
         var _loc3_:int = -1;
         var _loc4_:int = int(this.m_vStack.indexOf(param1));
         var _loc5_:GUIInventorySlot = this.Slot;
         var _loc7_:Boolean = false;
         var _loc8_:Boolean = false;
         var _loc9_:Boolean = this.m_objParentContainer != null && this.m_objParentContainer.ItemDefinition.m_vChargeProfiles.length > 0;
         if(_loc5_ != null)
         {
            _loc6_ = _loc5_.m_sprOwner;
         }
         if(this.ItemDefinition.m_vChargeProfiles.length > 0)
         {
            _loc7_ = this.IsCharged(0,0.1,1);
         }
         if(_loc9_)
         {
            _loc8_ = this.m_objParentContainer.IsCharged(0,0.1,1);
         }
         if(_loc4_ < 0)
         {
            for each(_loc10_ in this.vItems)
            {
               if(_loc10_ == param1)
               {
                  _loc3_ = int(this.vItems.indexOf(param1));
                  break;
               }
               if((_loc11_ = _loc10_.RemoveItem(param1,param2)) != null)
               {
                  return _loc11_;
               }
               for each(_loc12_ in _loc10_.m_vStack)
               {
                  if((_loc11_ = _loc12_.RemoveItem(param1,param2)) != null)
                  {
                     return _loc11_;
                  }
               }
            }
         }
         if(_loc3_ >= 0 || _loc4_ >= 0)
         {
            if(_loc6_ != null && _loc5_.nSlotIndex == 200)
            {
               _loc13_ = param1.GetTotalValue(false);
               if(param1.vItems.length > 0 || param2 && param1.StackCount > 1)
               {
                  _loc13_ = param1.GetTotalValue();
               }
               if(param1 is ItemHardware)
               {
                  _loc13_ = ItemHardware(param1).GetTotalValueHidden();
               }
               if(_loc6_ == PlayState.m_objInstance.sprPlayer && _loc6_.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
               {
                  if(PlayState.m_objInstance.sprPlayer.Money < _loc13_)
                  {
                     return null;
                  }
                  PlayState.m_objInstance.sprPlayer.Money -= _loc13_;
               }
            }
            if(!param2 && param1.StackCount > 1)
            {
               (_loc14_ = param1.m_vStack.pop()).ptOffset.x = param1.ptOffset.x;
               _loc14_.ptOffset.y = param1.ptOffset.y;
               _loc14_.Rotate(param1.m_fAngle);
               _loc14_.m_vStack = param1.m_vStack;
               param1.m_vStack = new Vector.<ItemInstance>();
               this.vItems[_loc3_] = _loc14_;
               _loc14_.UpdateStackImage();
               param1.UpdateStackImage();
            }
            else if(_loc4_ >= 0)
            {
               this.m_vStack.splice(_loc4_,1);
               param1.UpdateStackImage();
               this.UpdateStackImage();
            }
            else
            {
               this.vItems.splice(_loc3_,1);
               if(this.vItems.length == 0)
               {
                  this.SwapImage(false);
               }
            }
            if(_loc7_)
            {
               this.CheckShutdown();
            }
            if(this.m_objParentContainer != null && _loc8_)
            {
               this.m_objParentContainer.CheckShutdown();
            }
            param1.m_objParentContainer = null;
            for each(_loc12_ in param1.m_vStack)
            {
               _loc12_.m_objParentContainer = null;
            }
            this.WeightChanged = true;
            if(_loc6_ != null)
            {
               param1.UpdatePossessConditions(_loc5_,this,true);
               if(_loc5_.m_bCarried)
               {
                  _loc6_.Encumberance -= param1.WeightPlusContents;
               }
               if(_loc6_ == PlayState.m_objInstance.sprPlayer && this.m_vAttackModes.indexOf(_loc6_.CurrentAttackMode) >= 0)
               {
                  PlayState.m_objInstance.UpdateAttackModeUI();
               }
               if(_loc5_.nSlotIndex == 200)
               {
                  _loc6_.m_tilCurrentHex.CalculateValue();
               }
            }
            return param1;
         }
         return null;
      }
      
      public function CheckShutdown() : void
      {
         var _loc1_:Boolean = false;
         var _loc2_:String = null;
         var _loc3_:Item = null;
         if((this.ItemDefinition.m_bDischargeEquip || this.ItemDefinition.m_bDischargeTime || this.ItemDefinition.m_bDischargeHex) && this.IsCharged(0,0.1,1) == false)
         {
            _loc1_ = true;
            for each(_loc2_ in this.ItemDefinition.m_vModeIDs)
            {
               _loc3_ = DataHandler.GetItemDef(_loc2_);
               if(_loc3_.m_bDischargeEquip == false && _loc3_.m_bDischargeTime == false)
               {
                  this.ChangeMode(_loc3_,true);
                  _loc1_ = false;
                  break;
               }
            }
            if(_loc1_)
            {
               this.UpdatePossessConditions(this.Slot,this.m_objParentContainer,true);
               if(this.Slot != null)
               {
                  this.Slot.UpdateEquipConditions(this.Slot.m_sprOwner,this,true,true);
               }
            }
         }
      }
      
      public function UpdatePossessConditions(param1:GUIInventorySlot, param2:ItemInstance, param3:Boolean = false, param4:Boolean = false) : void
      {
         var _loc7_:ItemInstance = null;
         var _loc8_:Boolean = false;
         var _loc9_:Array = null;
         var _loc10_:PlayerCondition = null;
         var _loc11_:int = 0;
         var _loc5_:int = int(this.StackCount);
         if(param3)
         {
            _loc5_ = -_loc5_;
         }
         if(param2 is ItemCamp)
         {
            ItemCamp(param2).IncrementStackingStats(this,_loc5_);
         }
         if(param1 == null)
         {
            return;
         }
         var _loc6_:Vector.<ItemInstance> = this.vItems.concat();
         if(param2 is ItemCamp == false)
         {
            _loc6_ = _loc6_.concat(this.m_vStack);
         }
         for each(_loc7_ in _loc6_)
         {
            _loc7_.UpdatePossessConditions(param1,param2,param3,param4);
         }
         _loc8_ = true;
         if(param4 == false && (this.m_objItem.m_bDischargeTime || this.m_objItem.m_bDischargeHex))
         {
            _loc8_ = this.IsCharged(0,0.1,1);
         }
         if(_loc8_ == false)
         {
            return;
         }
         for each(_loc9_ in this.ItemDefinition.m_aPossessConditions)
         {
            if(!(_loc9_[0] != "" && _loc9_[0] != param1.nSlotIndex))
            {
               _loc10_ = param1.m_sprOwner.GetCondition(Math.abs(_loc9_[1]));
               _loc11_ = int(_loc9_[1]);
               if(param3)
               {
                  _loc11_ = -_loc11_;
               }
               if(_loc11_ > 0)
               {
                  param1.m_sprOwner.AddCondition(_loc10_);
               }
               else
               {
                  param1.m_sprOwner.RemoveCondition(_loc10_);
               }
            }
         }
      }
      
      public function ContainsItem(param1:ItemInstance) : Boolean
      {
         var _loc2_:ItemInstance = null;
         for each(_loc2_ in this.vItems)
         {
            if(_loc2_ == param1)
            {
               return true;
            }
         }
         return false;
      }
      
      public function GetItems(param1:int = -1, param2:int = -1) : Vector.<ItemInstance>
      {
         var _loc5_:ItemInstance = null;
         var _loc6_:Boolean = false;
         var _loc7_:Vector.<ItemInstance> = null;
         var _loc3_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc4_:Vector.<ItemInstance> = this.vItems.concat();
         for each(_loc5_ in _loc4_)
         {
            _loc6_ = true;
            if(param1 > 0 && _loc5_.ItemDefinition.nGroupID != param1 || param2 >= 0 && _loc5_.ItemDefinition.nSubgroupID != param2)
            {
               _loc6_ = false;
            }
            if(_loc6_)
            {
               _loc3_.push(_loc5_);
            }
            if((_loc7_ = _loc5_.GetItems(param1,param2)).length > 0)
            {
               _loc3_ = _loc3_.concat(_loc7_);
            }
         }
         for each(_loc5_ in this.m_vStack)
         {
            if((_loc7_ = _loc5_.GetItems(param1,param2)).length > 0)
            {
               _loc3_ = _loc3_.concat(_loc7_);
            }
         }
         return _loc3_;
      }
      
      public function GetTotalValue(param1:Boolean = true) : Number
      {
         var _loc4_:ItemInstance = null;
         if(isNaN(this.fDurability))
         {
            this.fDurability = 1;
         }
         var _loc2_:Number = this.m_objItem.fMonetaryValue * this.fDurability;
         if(this.Identified == true && this.m_objItem.strDescAlt != "")
         {
            _loc2_ = this.m_objItem.fMonetaryValueAlt * this.fDurability;
         }
         if(param1 == false)
         {
            return _loc2_;
         }
         var _loc3_:Vector.<ItemInstance> = this.GetItems();
         for each(_loc4_ in _loc3_)
         {
            if(_loc4_.m_objParentContainer == this)
            {
               _loc2_ += _loc4_.GetTotalValue();
            }
         }
         for each(_loc4_ in this.m_vStack)
         {
            _loc2_ += _loc4_.GetTotalValue();
         }
         return _loc2_;
      }
      
      public function Discharge(param1:int, param2:Number, param3:int) : void
      {
         var _loc4_:ChargeProfile = null;
         var _loc5_:int = 0;
         var _loc6_:Number = NaN;
         var _loc7_:Vector.<ItemInstance> = null;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:Number = NaN;
         var _loc11_:GUIInventory = null;
         var _loc12_:ItemInstance = null;
         for each(_loc4_ in this.ItemDefinition.m_vChargeProfiles)
         {
            _loc5_ = 0;
            if(param1 > 0 || param3 > 0)
            {
               _loc5_ = 1;
            }
            if((_loc6_ = _loc4_.GetQuantity(param1,param2,param3,this.m_bEquipped)) > 0)
            {
               _loc7_ = this.GetCharges(_loc4_.m_strItemID);
               _loc8_ = 0;
               while(_loc8_ < _loc7_.length)
               {
                  _loc9_ = int(_loc7_[_loc8_].m_vStack.length - 1);
                  while(_loc9_ >= 0)
                  {
                     _loc7_.push(_loc7_[_loc8_].m_vStack[_loc9_]);
                     _loc9_--;
                  }
                  _loc8_++;
               }
               if(_loc4_.m_bDegrade)
               {
                  _loc8_ = 0;
                  while(_loc8_ < _loc7_.length)
                  {
                     if(_loc6_ <= 0)
                     {
                        break;
                     }
                     _loc10_ = _loc7_[_loc8_].fDurability;
                     _loc7_[_loc8_].fDurability -= _loc6_;
                     if(isNaN(_loc7_[_loc8_].fDurability))
                     {
                        _loc7_[_loc8_].fDurability = 1;
                     }
                     _loc6_ = 0;
                     if(_loc7_[_loc8_].fDurability <= 0)
                     {
                        _loc6_ = -_loc7_[_loc8_].fDurability;
                        _loc7_[_loc8_].fDurability = _loc4_.GetQuantity(0,0.1,1,true);
                        if(isNaN(_loc7_[_loc8_].fDurability))
                        {
                           _loc7_[_loc8_].fDurability = 1;
                        }
                        _loc7_[_loc8_].ReplaceDegradedItem(_loc5_);
                        _loc7_[_loc8_].fDurability = 0;
                     }
                     _loc8_++;
                  }
               }
               else
               {
                  _loc8_ = 0;
                  while(_loc8_ < _loc6_)
                  {
                     if(_loc8_ >= _loc7_.length)
                     {
                        break;
                     }
                     if(this.bSocketed)
                     {
                        this.Slot.RemoveItem(_loc7_[_loc8_]);
                     }
                     else
                     {
                        this.RemoveItem(_loc7_[_loc8_]);
                     }
                     _loc8_++;
                  }
               }
            }
            else if(_loc6_ < 0)
            {
               _loc11_ = PlayState.m_objInstance.grpInventoryUI;
               if(_loc4_.m_bDegrade)
               {
                  _loc6_ = -_loc6_;
                  _loc7_ = this.GetCharges(_loc4_.m_strItemID);
                  _loc8_ = 0;
                  while(_loc8_ < _loc7_.length)
                  {
                     _loc9_ = int(_loc7_[_loc8_].m_vStack.length - 1);
                     while(_loc9_ >= 0)
                     {
                        _loc7_.push(_loc7_[_loc8_].m_vStack[_loc9_]);
                        _loc9_--;
                     }
                     _loc8_++;
                  }
                  _loc8_ = 0;
                  while(_loc8_ < _loc7_.length)
                  {
                     if(_loc6_ <= 0)
                     {
                        break;
                     }
                     _loc7_[_loc8_].fDurability += _loc6_;
                     if(isNaN(_loc7_[_loc8_].fDurability))
                     {
                        _loc7_[_loc8_].fDurability = 1;
                     }
                     _loc6_ = 0;
                     if(_loc7_[_loc8_].fDurability > 1)
                     {
                        _loc6_ = _loc7_[_loc8_].fDurability - 1;
                        _loc7_[_loc8_].fDurability = 1;
                     }
                     _loc8_++;
                  }
               }
               else
               {
                  _loc8_ = 0;
                  while(_loc8_ < -_loc6_)
                  {
                     _loc12_ = DataHandler.GetItem(_loc4_.m_strItemID);
                     if(this.m_sprIMG != null)
                     {
                        _loc12_.CreateAppearance();
                     }
                     if(this.bSocketed)
                     {
                        _loc11_.AddItemToSlot(_loc12_,this.Slot,true);
                     }
                     else
                     {
                        _loc11_.AddItemToCapBox(_loc12_,this,null,true);
                     }
                     _loc8_++;
                  }
               }
            }
         }
      }
      
      public function IsCharged(param1:int, param2:Number, param3:int) : Boolean
      {
         var _loc5_:Vector.<ItemInstance> = null;
         var _loc6_:ChargeProfile = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:ItemInstance = null;
         var _loc4_:Number = 0;
         var _loc9_:int = 0;
         var _loc10_:* = this.ItemDefinition.m_vChargeProfiles;
         while(true)
         {
            for each(_loc6_ in _loc10_)
            {
               if((_loc4_ = Number(_loc6_.GetQuantity(param1,param2,param3,true))) != 0)
               {
                  _loc5_ = this.GetCharges(_loc6_.m_strItemID);
                  for each(_loc7_ in _loc5_)
                  {
                     if(_loc6_.m_bDegrade)
                     {
                        _loc4_ -= _loc7_.fDurability;
                        for each(_loc8_ in _loc7_.m_vStack)
                        {
                           _loc4_ -= _loc8_.fDurability;
                        }
                     }
                     else
                     {
                        _loc4_ -= _loc7_.StackCount;
                     }
                     if(_loc4_ <= 0)
                     {
                        break;
                     }
                  }
                  if(_loc4_ > 0)
                  {
                     break;
                  }
               }
            }
            return true;
         }
         return false;
      }
      
      public function GetCharges(param1:String) : Vector.<ItemInstance>
      {
         var _loc3_:Vector.<ItemInstance> = null;
         if(param1 == "")
         {
            return new Vector.<ItemInstance>();
         }
         var _loc2_:Array = param1.split(".");
         if(param1 == this.IDString)
         {
            _loc3_ = Vector.<ItemInstance>([this]);
         }
         else
         {
            _loc3_ = this.GetItems(int(_loc2_[0]),int(_loc2_[1]));
         }
         return _loc3_;
      }
      
      public function GetRemainingChargeInfo() : String
      {
         var _loc2_:ChargeProfile = null;
         var _loc3_:Number = NaN;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         var _loc1_:* = "";
         for each(_loc2_ in this.ItemDefinition.m_vChargeProfiles)
         {
            _loc3_ = 0;
            _loc4_ = this.GetCharges(_loc2_.m_strItemID);
            for each(_loc5_ in _loc4_)
            {
               if(_loc2_.m_bDegrade)
               {
                  _loc3_ += _loc5_.fDurability;
                  for each(_loc6_ in _loc5_.m_vStack)
                  {
                     _loc3_ += _loc6_.fDurability;
                  }
               }
               else
               {
                  _loc3_ += _loc5_.StackCount;
               }
            }
            if(_loc2_.m_fPerHex > 0)
            {
               if(_loc1_.length > 0)
               {
                  _loc1_ += ", ";
               }
               _loc1_ = _loc3_.toFixed(2) + " (" + _loc2_.m_fPerHex.toFixed(2) + "/每一格)";
            }
            if(_loc2_.m_fPerHour > 0)
            {
               if(_loc1_.length > 0)
               {
                  _loc1_ += ", ";
               }
               _loc1_ = _loc3_.toFixed(2) + " (" + _loc2_.m_fPerHour.toFixed(2) + "/每小时)";
            }
            if(_loc2_.m_fPerHourEquipped > 0)
            {
               if(_loc1_.length > 0)
               {
                  _loc1_ += ", ";
               }
               _loc1_ = _loc3_.toFixed(2) + " (" + _loc2_.m_fPerHourEquipped.toFixed(2) + "/佩戴每小时)";
            }
            if(_loc2_.m_fPerUse > 0)
            {
               if(_loc1_.length > 0)
               {
                  _loc1_ += ", ";
               }
               _loc1_ = _loc3_.toFixed(2) + " (" + _loc2_.m_fPerUse.toFixed(2) + "/每次使用)";
            }
         }
         return _loc1_;
      }
      
      public function UseDegrade() : void
      {
         var _loc2_:Creature = null;
         var _loc4_:ItemInstance = null;
         var _loc5_:Number = NaN;
         var _loc1_:GUIInventorySlot = this.Slot;
         if(_loc1_ != null)
         {
            _loc2_ = _loc1_.m_sprOwner;
         }
         var _loc3_:Number = this.GetTotalValue();
         if(this is ItemHardware)
         {
            _loc3_ = ItemHardware(this).GetTotalValueHidden();
         }
         for each(_loc4_ in this.m_vComponents)
         {
            _loc4_.UseDegrade();
         }
         this.fDurability -= this.m_objItem.fDegradePerUse;
         if(isNaN(this.fDurability))
         {
            this.fDurability = 1;
         }
         if(this.fDurability <= 0)
         {
            this.fDurability = 0;
         }
         if(_loc1_ != null && _loc1_.nSlotIndex == 200)
         {
            _loc2_.m_tilCurrentHex.CalculateValue();
            if(_loc2_ is Player && _loc2_.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
            {
               _loc5_ = _loc3_ - this.GetTotalValue();
               if(this is ItemHardware)
               {
                  _loc5_ = _loc3_ - ItemHardware(this).GetTotalValueHidden();
               }
               PlayState.m_objInstance.sprPlayer.Money -= _loc5_;
            }
         }
      }
      
      public function EquipDegrade(param1:Number) : void
      {
         var _loc2_:ItemInstance = null;
         for each(_loc2_ in this.m_vStack)
         {
            _loc2_.EquipDegrade(param1);
            if(_loc2_.fDurability <= 0)
            {
               _loc2_.ReplaceDegradedItem(0,this.m_objItem.m_vProperties.indexOf(83) >= 0);
            }
         }
         for each(_loc2_ in this.m_vComponents)
         {
            _loc2_.EquipDegrade(param1);
         }
         this.fDurability -= this.m_objItem.fEquipDegradePerHour * param1;
         if(isNaN(this.fDurability))
         {
            this.fDurability = 1;
         }
         if(this.fDurability <= 0)
         {
            this.fDurability = 0;
         }
      }
      
      public function TimeDegrade(param1:Date) : void
      {
         var _loc6_:ItemInstance = null;
         var _loc2_:int = int(this.vItems.length - 1);
         while(_loc2_ >= 0)
         {
            (_loc6_ = this.vItems[_loc2_]).TimeDegrade(param1);
            if(_loc6_.fDurability <= 0)
            {
               _loc6_.ReplaceDegradedItem();
            }
            _loc2_--;
         }
         _loc2_ = int(this.m_vStack.length - 1);
         while(_loc2_ >= 0)
         {
            (_loc6_ = this.m_vStack[_loc2_]).TimeDegrade(param1);
            if(_loc6_.fDurability <= 0)
            {
               _loc6_.ReplaceDegradedItem();
            }
            _loc2_--;
         }
         _loc2_ = int(this.m_vComponents.length - 1);
         while(_loc2_ >= 0)
         {
            (_loc6_ = this.m_vComponents[_loc2_]).TimeDegrade(param1);
            _loc2_--;
         }
         var _loc3_:Number = Number(param1.getTime());
         if(this.m_objLastDegradeDate == null)
         {
            this.m_objLastDegradeDate = new Date();
            this.m_objLastDegradeDate.setTime(_loc3_);
            return;
         }
         var _loc4_:Number = Number(this.m_objLastDegradeDate.getTime());
         var _loc5_:Number = (_loc3_ - _loc4_) / 1000 / 60 / 60;
         this.fDurability -= this.m_objItem.fDegradePerHour * _loc5_;
         if(isNaN(this.fDurability))
         {
            this.fDurability = 1;
         }
         if(this.fDurability <= 0)
         {
            this.fDurability = 0;
         }
         this.Discharge(0,_loc5_,0);
         this.m_objLastDegradeDate.setTime(_loc3_);
      }
      
      public function GetDegradedItems(param1:int = 0) : Vector.<ItemInstance>
      {
         var _loc5_:ItemInstance = null;
         var _loc2_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc3_:int = int(_loc2_.length);
         _loc2_ = _loc2_.concat(DataHandler.GetTreasure(this.ItemDefinition.vDegradeTreasureIDs[param1]).GenerateTreasure());
         var _loc4_:int = _loc3_;
         while(_loc4_ < _loc2_.length)
         {
            _loc2_[_loc4_].Rotate(this.m_fAngle);
            _loc4_++;
         }
         _loc2_ = _loc2_.concat(this.vItems);
         _loc4_ = 0;
         while(_loc4_ < this.m_vComponents.length)
         {
            if(this.m_vComponents[_loc4_].fDurability > 0)
            {
               (_loc5_ = this.m_vComponents[_loc4_].Clone()).SetFormatIDs();
               _loc2_.push(_loc5_);
            }
            else
            {
               _loc2_ = _loc2_.concat(DataHandler.GetTreasure(this.m_vComponents[_loc4_].ItemDefinition.vDegradeTreasureIDs[param1]).GenerateTreasure());
            }
            _loc4_++;
         }
         return _loc2_;
      }
      
      public function ReplaceDegradedItem(param1:int = 0, param2:Boolean = false) : void
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc5_:Creature = null;
         var _loc9_:ItemInstance = null;
         var _loc12_:GUIFitItemResult = null;
         var _loc13_:ItemInstance = null;
         var _loc14_:Vector.<int> = null;
         var _loc15_:ItemInstance = null;
         var _loc4_:ItemInstance = this.m_objParentContainer;
         var _loc6_:int = -1;
         var _loc7_:Boolean = false;
         var _loc8_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc10_:Boolean = false;
         if(this.grpItemPanelSlot == null)
         {
            if(this.m_objParentContainer == null)
            {
               return;
            }
            _loc9_ = this.m_objParentContainer.RemoveItem(this);
         }
         else
         {
            _loc7_ = this.grpItemPanelSlot.m_bCarried;
            (_loc5_ = this.grpItemPanelSlot.m_sprOwner).m_bCondQueue = true;
            _loc3_ = this.grpItemPanelSlot;
            if(_loc5_.m_tilCurrentHex == PlayState.m_objInstance.tilCurrentHex && param2 == false && PlayState.m_objInstance.sprPlayer.Alive)
            {
               _loc10_ = true;
            }
            if(_loc7_)
            {
               if(this.bSocketed && _loc10_)
               {
                  _loc5_.MessageFloaty(_loc5_.Name + "的 " + this.strDesc + " 损坏了。",true,null,GUIMessageWindow.COLOR_BAD);
               }
               _loc9_ = _loc5_.RemoveItem(this,false);
            }
            else
            {
               _loc9_ = this.grpItemPanelSlot.RemoveItem(this);
            }
         }
         _loc8_ = this.GetDegradedItems(param1);
         this.vItems.length = 0;
         var _loc11_:int = 0;
         while(_loc11_ < _loc8_.length)
         {
            _loc12_ = new GUIFitItemResult();
            _loc8_[_loc11_].m_objParentContainer = null;
            if((_loc13_ = PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(_loc8_[_loc11_],_loc3_)) != null)
            {
               if(_loc5_ != null)
               {
                  _loc14_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT]);
                  _loc15_ = _loc5_.DropItem(_loc13_,true,true,_loc14_);
                  if(_loc13_ != _loc15_ && _loc15_ != null)
                  {
                     if(_loc7_ && _loc10_)
                     {
                        _loc5_.MessageFloaty(_loc5_.Name + "的 " + _loc13_.strDesc + " 掉到了地上。",true,null,GUIMessageWindow.COLOR_BAD);
                     }
                     _loc8_.push(_loc15_);
                  }
               }
               else if(_loc4_ != null)
               {
                  _loc13_ = PlayState.m_objInstance.grpInventoryUI.AddItemToCapBox(_loc13_,_loc4_,null,true);
               }
            }
            _loc11_++;
         }
         if(_loc5_ != null)
         {
            _loc5_.ProcessConditionQueue();
         }
      }
      
      public function GetImagePeek() : BitmapData
      {
         var _loc1_:BitmapData = this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][0]].clone();
         if(this.m_sprStackBG.visible == false)
         {
            return _loc1_;
         }
         _loc1_.fillRect(new Rectangle(this.m_sprIMG.width - this.m_sprStackBG.scale.x,this.m_sprIMG.height - this.m_sprStackBG.scale.y,this.m_sprStackBG.scale.x,this.m_sprStackBG.scale.y),4278190080);
         _loc1_.copyPixels(this.m_txtStackAmount.pixels,this.m_txtStackAmount.pixels.rect,new Point(this.m_txtStackAmount.x - this.m_sprIMG.x,this.m_txtStackAmount.y - this.m_sprIMG.y),null,null,true);
         return _loc1_;
      }
      
      public function SwapImage(param1:Boolean) : void
      {
         var _loc2_:uint = MODE_STORED;
         var _loc3_:uint = 0;
         if(this.bSocketed && this.grpItemPanelSlot != null)
         {
            _loc3_ = uint(this.Slot.nSlotIndex);
         }
         if(this.vItems.length > 0)
         {
            _loc2_++;
         }
         var _loc4_:Number = this.m_fAngle;
         var _loc5_:Boolean = false;
         var _loc6_:uint = uint(this.m_objItem.dictImageUsage[0][_loc2_]);
         if(this.m_objItem.dictImageUsage[_loc3_] != null)
         {
            _loc6_ = uint(this.m_objItem.dictImageUsage[_loc3_][_loc2_]);
         }
         if(param1 || this.m_nImageIndex != _loc6_)
         {
            _loc5_ = true;
            this.Rotate(0);
            if(this.m_sprIMG != null)
            {
               this.m_sprIMG.pixels = this.m_objItem.vImageList[_loc6_];
               this.m_bMirrored = false;
            }
            this.m_fWidth = this.m_objItem.vImageList[_loc6_].width;
            this.m_fHeight = this.m_objItem.vImageList[_loc6_].height;
            this.m_nImageIndex = _loc6_;
         }
         if(_loc3_ != 0 && this.m_bMirrored == false)
         {
            if(this.m_sprIMG != null && this.grpItemPanelSlot != null && this.grpItemPanelSlot.bMirrored != this.ItemDefinition.m_bMirrored)
            {
               this.m_sprIMG.pixels = MirrorImage(this.m_sprIMG.pixels);
               this.m_bMirrored = !this.m_bMirrored;
            }
         }
         else if(_loc5_)
         {
            this.Rotate(_loc4_);
         }
         this.m_nSize = this.m_objItem.vImageList[_loc6_].width + this.m_objItem.vImageList[_loc6_].height;
      }
      
      public function Rotate(param1:Number) : void
      {
         var _loc3_:BitmapData = null;
         var _loc4_:BitmapData = null;
         var _loc6_:Number = NaN;
         while(param1 > 360)
         {
            param1 -= 360;
         }
         while(param1 < 0)
         {
            param1 += 360;
         }
         var _loc2_:Number = param1 - this.m_fAngle;
         if(_loc2_ == 0)
         {
            return;
         }
         while(_loc2_ < 0)
         {
            _loc2_ += 360;
         }
         var _loc5_:Matrix;
         (_loc5_ = new Matrix()).translate(-this.m_fWidth / 2,-this.m_fHeight / 2);
         _loc5_.rotate(_loc2_ * Math.PI * 2 / 360);
         if(_loc2_ != 90 && _loc2_ != 270)
         {
            _loc5_.translate(this.m_fWidth / 2,this.m_fHeight / 2);
            if(this.m_sprIMG != null)
            {
               _loc3_ = new BitmapData(this.m_fWidth,this.m_fHeight,true,0);
            }
            if(this.m_sprIMG != null)
            {
               _loc4_ = new BitmapData(this.m_fWidth,this.m_fHeight,true,0);
            }
         }
         else
         {
            _loc5_.translate(this.m_fHeight / 2,this.m_fWidth / 2);
            if(this.m_sprIMG != null)
            {
               _loc3_ = new BitmapData(this.m_fHeight,this.m_fWidth,true,0);
            }
            if(this.m_sprIMG != null)
            {
               _loc4_ = new BitmapData(this.m_fHeight,this.m_fWidth,true,0);
            }
            _loc6_ = this.m_fWidth;
            this.m_fWidth = this.m_fHeight;
            this.m_fHeight = _loc6_;
         }
         if(this.m_sprIMG != null)
         {
            _loc3_.draw(this.m_sprIMG.pixels,_loc5_);
            this.m_sprIMG.pixels = _loc3_;
            _loc4_.draw(this.m_sprBracket.pixels,_loc5_);
            this.m_sprBracket.pixels = _loc4_;
         }
         this.m_fAngle = param1;
      }
      
      public function CreateAppearance() : void
      {
         var _loc1_:ItemInstance = null;
         this.m_sprIMG = new FlxSprite();
         this.m_sprBracket = new FlxSprite();
         this.m_sprBracket.visible = false;
         this.m_sprBracket.x = 1;
         this.m_sprBracket.y = 1;
         this.m_txtStackAmount = new FlxText(0,10,14,"5");
         this.m_txtStackAmount.setFormat("pixelsupertiny",8,4294967295,"right",4278190080);
         this.m_txtStackAmount.visible = false;
         this.m_sprStackBG = new FlxSprite(this.m_txtStackAmount.x,this.m_txtStackAmount.y);
         this.m_sprStackBG.makeGraphic(1,1,4278190080);
         this.m_sprStackBG.visible = false;
         this.SwapImage(true);
         this.UpdateStackImage();
         add(this.m_sprIMG);
         add(this.m_sprBracket);
         add(this.m_sprStackBG);
         add(this.m_txtStackAmount);
         setAll("scrollFactor",new FlxPoint());
         setAll("cameras",[FlxG.camera]);
         setAll("alpha",this.m_fAlpha);
         setAll("x",this.m_fX);
         setAll("y",this.m_fY);
         this.m_txtStackAmount.x = this.m_fX + this.m_fWidth - 11;
         this.m_txtStackAmount.y = this.m_fY + this.m_fHeight - 8;
         this.m_sprStackBG.x = this.m_fX + this.m_fWidth - this.m_sprStackBG.scale.x / 2;
         this.m_sprStackBG.y = this.m_fY + this.m_fHeight - this.m_sprStackBG.scale.y / 2;
         for each(_loc1_ in this.m_vStack)
         {
            _loc1_.CreateAppearance();
         }
         for each(_loc1_ in this.vItems)
         {
            _loc1_.CreateAppearance();
         }
      }
      
      public function DestroyAppearance() : void
      {
         var _loc1_:ItemInstance = null;
         remove(this.m_sprIMG);
         remove(this.m_sprBracket);
         remove(this.m_sprStackBG);
         remove(this.m_txtStackAmount);
         this.m_sprIMG = DataHandler.DestroyObject(this.m_sprIMG);
         this.m_sprBracket = DataHandler.DestroyObject(this.m_sprBracket);
         this.m_txtStackAmount = DataHandler.DestroyObject(this.m_txtStackAmount);
         this.m_sprStackBG = DataHandler.DestroyObject(this.m_sprStackBG);
         for each(_loc1_ in this.m_vStack)
         {
            _loc1_.DestroyAppearance();
         }
         for each(_loc1_ in this.vItems)
         {
            _loc1_.DestroyAppearance();
         }
      }
      
      public function ChangeMode(param1:Item, param2:Boolean = false) : void
      {
         if(this.m_vStack != null && this.m_vStack.length > 0)
         {
            if(this.Slot.m_sprOwner != null)
            {
               this.Slot.m_sprOwner.MessageFloaty("无法切换堆处于堆叠状态的物品。",false);
            }
            return;
         }
         this.UpdatePossessConditions(this.Slot,this.m_objParentContainer,true,param2);
         if(this.Slot != null && this.bSocketed)
         {
            this.Slot.UpdateEquipConditions(this.Slot.m_sprOwner,this,true,param2);
         }
         var _loc3_:Item = this.m_objItem;
         this.m_objItem = param1;
         this.strDesc = this.m_objItem.strDesc;
         this.nSlotDepth = this.m_objItem.nSlotDepth;
         this.m_bDegrades = this.m_objItem.fDegradePerHour != 0 || this.m_objItem.fEquipDegradePerHour != 0 || this.m_objItem.fDegradePerUse > 0 && this.m_objItem.fDegradePerUse < 1;
         this.m_fZoom = param1.m_fZoom;
         this.nFormatID = this.m_objItem.nFormatID;
         this.aContentIDs = this.m_objItem.aContentIDs.concat();
         this.SwapImage(true);
         if(this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][1]].width > this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][0]].width || this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][1]].height > this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][0]].height)
         {
            this.bRigidContainer = false;
         }
         else
         {
            this.bRigidContainer = true;
         }
         this.UpdatePossessConditions(this.Slot,this.m_objParentContainer);
         if(this.Slot != null && this.bSocketed)
         {
            this.Slot.UpdateEquipConditions(this.Slot.m_sprOwner,this);
         }
         var _loc4_:Number = Number(PlayState.m_objInstance.objDate.getTime());
         if(this.m_objLastDegradeDate == null)
         {
            this.m_objLastDegradeDate = new Date();
            this.m_objLastDegradeDate.setTime(_loc4_);
         }
         var _loc5_:Boolean = false;
         var _loc6_:String = "";
         var _loc7_:* = true;
         if(this.m_objParentContainer != null)
         {
            _loc7_ = PlayState.m_objInstance.grpInventoryUI.TestItemInCapBox(this,this.m_objParentContainer,this.ptOffset).m_nResult == GUIFitItemResult.RESULT_CAN_FIT;
         }
         if(_loc7_ == false)
         {
            _loc5_ = true;
            _loc6_ = "没有足够的空间切换到 " + this.Description();
         }
         if(this.IsCharged(0,0.1,0) == false)
         {
            _loc5_ = true;
            _loc6_ = "没有足够的充能切换到 " + this.Description();
         }
         if(_loc5_)
         {
            if(this.Slot.m_sprOwner != null)
            {
               this.Slot.m_sprOwner.MessageFloaty(_loc6_,false);
            }
            this.ChangeMode(_loc3_);
         }
      }
      
      public function get ItemDefinition() : Item
      {
         return this.m_objItem;
      }
      
      public function set ItemDefinition(param1:Item) : void
      {
         var _loc2_:Vector.<ItemInstance> = null;
         var _loc3_:Number = NaN;
         var _loc4_:GUIInventorySlot = null;
         var _loc5_:ItemInstance = null;
         this.m_objItem = param1;
         this.strDesc = this.m_objItem.strDesc;
         this.nSlotDepth = this.m_objItem.nSlotDepth;
         this.fDurability = this.m_objItem.fDurability;
         if(isNaN(this.fDurability))
         {
            this.fDurability = 1;
         }
         this.m_bDegrades = this.m_objItem.fDegradePerHour != 0 || this.m_objItem.fEquipDegradePerHour != 0 || this.m_objItem.fDegradePerUse > 0 && this.m_objItem.fDegradePerUse < 1;
         this.m_bIdentified = false;
         this.m_bMirrored = false;
         if(param1.strDescAlt == "")
         {
            this.m_bIdentified = true;
         }
         this.m_fZoom = param1.m_fZoom;
         this.vItems = new Vector.<ItemInstance>();
         this.m_vComponents = DataHandler.GetTreasure(this.m_objItem.m_nComponentID).GenerateTreasure();
         this.nFormatID = this.m_objItem.nFormatID;
         this.aContentIDs = this.m_objItem.aContentIDs.concat();
         if(FlxG.state is PlayState)
         {
            if(this.m_bDegrades || param1.m_bDischargeEquip || param1.m_bDischargeTime)
            {
               _loc3_ = Number(PlayState.m_objInstance.objDate.getTime());
               this.m_objLastDegradeDate = new Date();
               this.m_objLastDegradeDate.setTime(_loc3_);
            }
            _loc2_ = new Vector.<ItemInstance>();
            if(this.m_bContents)
            {
               _loc2_ = DataHandler.GetTreasure(this.m_objItem.nTreasureID).GenerateTreasure();
            }
            if(_loc2_.length > 0)
            {
               (_loc4_ = PlayState.m_objInstance.grpInventoryUI.grpTempSlot).SocketItem(this);
               for each(_loc5_ in _loc2_)
               {
                  PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(_loc5_,_loc4_,true);
               }
               _loc4_.UnSocketItem(true);
            }
         }
         this.SwapImage(false);
         if(this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][1]].width > this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][0]].width || this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][1]].height > this.ItemDefinition.vImageList[this.ItemDefinition.dictImageUsage[0][0]].height)
         {
            this.bRigidContainer = false;
         }
         else
         {
            this.bRigidContainer = true;
         }
      }
      
      public function AcceptsItem(param1:ItemInstance) : Boolean
      {
         var _loc2_:Boolean = this.ItemDefinition.aCapacities.length > 0 && this.aContentIDs.indexOf(param1.nFormatID) >= 0;
         if(_loc2_)
         {
            if(this.IDString == param1.IDString)
            {
               if(param1.bRigidContainer)
               {
                  _loc2_ = false;
               }
               else if(param1.vItems.length > 0)
               {
                  _loc2_ = false;
               }
            }
         }
         return _loc2_;
      }
      
      public function UpdateStackImage() : void
      {
         if(this.m_txtStackAmount == null)
         {
            return;
         }
         this.m_txtStackAmount.text = this.StackCount.toString();
         this.m_sprStackBG.scale = new FlxPoint(7,7);
         if(this.StackCount > 1)
         {
            this.m_txtStackAmount.visible = true;
            this.m_sprStackBG.visible = true;
            if(this.StackCount > 9)
            {
               this.m_sprStackBG.scale = new FlxPoint(10,7);
            }
            this.m_sprStackBG.x = this.m_sprIMG.x + this.m_sprIMG.width - this.m_sprStackBG.scale.x / 2;
            this.m_sprStackBG.y = this.m_sprIMG.y + this.m_sprIMG.height - this.m_sprStackBG.scale.y / 2;
         }
         else
         {
            this.m_txtStackAmount.visible = false;
            this.m_sprStackBG.visible = false;
         }
      }
      
      public function pixelsOverlapPoint(param1:FlxPoint, param2:uint = 17) : Boolean
      {
         if(this.m_sprIMG == null)
         {
            return false;
         }
         this.ptScrolled.x = param1.x + FlxG.camera.scroll.x;
         this.ptScrolled.y = param1.y + FlxG.camera.scroll.y;
         return this.m_sprIMG.pixelsOverlapPoint(this.ptScrolled,param2);
      }
      
      public function overlapsPoint(param1:FlxPoint) : Boolean
      {
         return param1.x > this.m_fX && param1.x < this.m_fX + this.m_fWidth && param1.y > this.m_fY && param1.y < this.m_fY + this.m_fHeight;
      }
      
      public function overlapsAt(param1:Number, param2:Number, param3:ItemInstance) : Boolean
      {
         return param3.x + param3.width > param1 && param3.x < param1 + this.m_fWidth && param3.y + param3.height > param2 && param3.y < param2 + this.m_fHeight;
      }
      
      public function SetFormatIDs(param1:int = -1, param2:Array = null) : void
      {
         if(param1 < 0)
         {
            param1 = int(this.ItemDefinition.nFormatID);
         }
         if(param2 == null)
         {
            param2 = this.ItemDefinition.aContentIDs.concat();
         }
         this.nFormatID = param1;
         this.aContentIDs = param2.concat();
         var _loc3_:int = 0;
         while(_loc3_ < this.StackCount - 1)
         {
            this.m_vStack[_loc3_].nFormatID = param1;
            this.m_vStack[_loc3_].aContentIDs = param2.concat();
            _loc3_++;
         }
      }
      
      public function Description() : String
      {
         if(this.Identified)
         {
            return this.strDesc;
         }
         if(this.ItemDefinition.strDescAlt != "" && PlayState.m_objInstance.sprPlayer.HasCondition(this.ItemDefinition.m_nCondID))
         {
            this.Identified = true;
            return this.strDesc;
         }
         return this.strDesc;
      }
      
      public function IdentifyContents() : void
      {
         var _loc1_:ItemInstance = null;
         for each(_loc1_ in this.vItems)
         {
            if(_loc1_.Identified == false)
            {
               _loc1_.Identified = true;
            }
            _loc1_.IdentifyContents();
         }
      }
      
      public function get IDString() : String
      {
         return this.ItemDefinition.nGroupID + "." + this.ItemDefinition.nSubgroupID;
      }
      
      public function get Alpha() : Number
      {
         return this.m_fAlpha;
      }
      
      public function set Alpha(param1:Number) : void
      {
         setAll("alpha",param1);
         this.m_fAlpha = param1;
      }
      
      public function get x() : Number
      {
         return this.m_fX;
      }
      
      public function set x(param1:Number) : void
      {
         setAll("x",param1);
         if(this.m_txtStackAmount != null)
         {
            this.m_txtStackAmount.x = param1 + this.m_fWidth - 11;
         }
         if(this.m_sprStackBG != null)
         {
            this.m_sprStackBG.x = param1 + this.m_fWidth - this.m_sprStackBG.scale.x / 2;
         }
         this.m_fX = param1;
      }
      
      public function get y() : Number
      {
         return this.m_fY;
      }
      
      public function set y(param1:Number) : void
      {
         setAll("y",param1);
         if(this.m_txtStackAmount != null)
         {
            this.m_txtStackAmount.y = param1 + this.m_sprIMG.height - 8;
         }
         if(this.m_sprStackBG != null)
         {
            this.m_sprStackBG.y = param1 + this.m_sprIMG.height - this.m_sprStackBG.scale.y / 2;
         }
         this.m_fY = param1;
      }
      
      public function get width() : Number
      {
         return this.m_fWidth;
      }
      
      public function get height() : Number
      {
         return this.m_fHeight;
      }
      
      public function set Ghosted(param1:Boolean) : void
      {
         var _loc2_:ItemInstance = null;
         this.m_bGhosted = param1;
         if(param1)
         {
            this.Alpha = 0.5;
         }
         else
         {
            this.Alpha = 1;
         }
         for each(_loc2_ in this.m_vStack)
         {
            _loc2_.Ghosted = param1;
         }
      }
      
      public function get Ghosted() : Boolean
      {
         return this.m_bGhosted;
      }
      
      public function get StackCount() : uint
      {
         return this.m_vStack.length + 1;
      }
      
      public function get Weight() : Number
      {
         return this.ItemDefinition.fWeight * this.StackCount;
      }
      
      public function get WeightPlusContents() : Number
      {
         var _loc1_:ItemInstance = null;
         if(this.WeightChanged)
         {
            this.m_fWeightContents = 0;
            for each(_loc1_ in this.vItems)
            {
               this.m_fWeightContents += _loc1_.WeightPlusContents;
            }
            this.m_bWeightChanged = false;
         }
         return this.m_fWeightContents + this.Weight;
      }
      
      public function get WeightChanged() : Boolean
      {
         return this.m_bWeightChanged;
      }
      
      public function set WeightChanged(param1:Boolean) : void
      {
         this.m_bWeightChanged = param1;
         if(this.m_objParentContainer != null)
         {
            this.m_objParentContainer.WeightChanged = param1;
         }
      }
      
      public function get Identified() : Boolean
      {
         return this.m_bIdentified;
      }
      
      public function set Identified(param1:Boolean) : void
      {
         var _loc2_:ItemInstance = null;
         if(this.m_bIdentified == param1)
         {
            return;
         }
         if(param1)
         {
            if(this.ItemDefinition.strDescAlt != "")
            {
               this.strDesc = this.ItemDefinition.strDescAlt;
            }
            this.m_bIdentified = true;
         }
         else
         {
            this.strDesc = this.ItemDefinition.strDesc;
            this.m_bIdentified = false;
         }
         for each(_loc2_ in this.m_vStack)
         {
            _loc2_.Identified = param1;
         }
      }
      
      public function get Slot() : GUIInventorySlot
      {
         if(this.grpItemPanelSlot != null)
         {
            return this.grpItemPanelSlot;
         }
         if(this.m_objParentContainer != null)
         {
            return this.m_objParentContainer.Slot;
         }
         return null;
      }
      
      public function SetBracket() : void
      {
         this.m_sprBracket.visible = true;
         var _loc1_:String = "";
         if(GUIValues.m_fItemZoom == 2)
         {
            _loc1_ = DataHandler.m_strZoomPrefix;
         }
         this.m_sprBracket.pixels = new BitmapData(this.m_sprIMG.width - 2,this.m_sprIMG.height - 2,true,1157605888);
      }
      
      public function SetRes(param1:Number) : void
      {
         var _loc2_:ItemInstance = null;
         if(param1 != this.m_fZoom || GUIValues.m_bOverrideZoom)
         {
            this.m_objItem.SetRes(param1);
            this.SwapImage(this.m_sprIMG != null);
            this.m_fWidth = this.m_objItem.vImageList[this.m_nImageIndex].width;
            this.m_fHeight = this.m_objItem.vImageList[this.m_nImageIndex].height;
            if(this.m_sprBracket != null && this.m_sprBracket.visible)
            {
               this.SetBracket();
            }
            for each(_loc2_ in this.vItems)
            {
               _loc2_.SetRes(param1);
            }
            for each(_loc2_ in this.m_vStack)
            {
               _loc2_.SetRes(param1);
            }
            for each(_loc2_ in this.m_vComponents)
            {
               _loc2_.SetRes(param1);
            }
            this.ptOffset.x *= param1 / this.m_fZoom;
            this.ptOffset.y *= param1 / this.m_fZoom;
            this.UpdateStackImage();
            this.m_nSize = this.m_fWidth + this.m_fHeight;
            this.m_fZoom = param1;
         }
      }
      
      public function get SaveData() : SaveGameItem
      {
         var _loc2_:ItemInstance = null;
         var _loc1_:SaveGameItem = new SaveGameItem();
         _loc1_.fDurability = this.fDurability;
         _loc1_.m_fAngle = this.m_fAngle;
         _loc1_.m_fZoom = this.m_fZoom;
         _loc1_.m_nSlotIndex = -1;
         _loc1_.Identified = this.m_bIdentified;
         if(this.bSocketed)
         {
            _loc1_.m_nSlotIndex = this.Slot.nSlotIndex;
         }
         _loc1_.ptOffset = this.ptOffset;
         _loc1_.strID = this.ItemDefinition.nGroupID + "." + this.ItemDefinition.nSubgroupID;
         for each(_loc2_ in this.vItems)
         {
            _loc1_.m_vItems.push(_loc2_.SaveData);
         }
         for each(_loc2_ in this.m_vStack)
         {
            _loc1_.m_vStack.push(_loc2_.SaveData);
         }
         for each(_loc2_ in this.m_vComponents)
         {
            _loc1_.m_vComponents.push(_loc2_.SaveData);
         }
         return _loc1_;
      }
      
      public function set SaveData(param1:SaveGameItem) : void
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:SaveGameItem = null;
         this.fDurability = param1.fDurability;
         this.ptOffset = param1.ptOffset;
         if(isNaN(this.fDurability))
         {
            this.fDurability = 1;
         }
         var _loc2_:int = GUIValues.GetInt("Item.zoom");
         if(param1.m_fZoom != _loc2_)
         {
            this.ptOffset.x *= _loc2_ / param1.m_fZoom;
            this.ptOffset.y *= _loc2_ / param1.m_fZoom;
            this.m_fZoom = _loc2_;
         }
         var _loc3_:Vector.<ItemInstance> = this.vItems.concat();
         for each(_loc4_ in _loc3_)
         {
            this.RemoveItem(_loc4_,true);
         }
         for each(_loc5_ in param1.m_vItems)
         {
            if((_loc4_ = DataHandler.GetItem(_loc5_.strID,false)) != null)
            {
               _loc4_.SaveData = _loc5_;
               this.AddItem(_loc4_,_loc5_.ptOffset);
            }
         }
         this.Identified = param1.Identified;
         for each(_loc5_ in param1.m_vStack)
         {
            if((_loc4_ = DataHandler.GetItem(_loc5_.strID,false)) != null)
            {
               _loc4_.SaveData = _loc5_;
               this.m_vStack.push(_loc4_);
            }
         }
         this.UpdateStackImage();
         this.m_vComponents.length = 0;
         for each(_loc5_ in param1.m_vComponents)
         {
            if((_loc4_ = DataHandler.GetItem(_loc5_.strID,false)) != null)
            {
               _loc4_.SaveData = _loc5_;
               this.m_vComponents.push(_loc4_);
            }
         }
         this.Rotate(param1.m_fAngle);
      }
   }
}

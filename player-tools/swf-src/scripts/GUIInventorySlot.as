package
{
   import flash.events.*;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import org.flixel.*;
   
   public class GUIInventorySlot extends FlxGroup
   {
      
      private static var nCapacityPixel:uint = 10;
       
      
      public var strName:String;
      
      public var nSlotIndex:int;
      
      public var vSocketedItems:Vector.<Vector.<ItemInstance>>;
      
      public var sprCap:FlxSprite;
      
      public var bCreSprites:Boolean;
      
      public var vCreSprites:Vector.<Vector.<FlxSprite>>;
      
      private var grpSlotBG:FlxGroup;
      
      private var grpItemPanel:FlxGroup;
      
      public var btnSlot:ImgButton;
      
      public var btnSort:ImgButton;
      
      private var nCapColor:uint = 4280296739;
      
      private var fAlpha:Number = 0.5;
      
      public var nZSort:int;
      
      public var bMirrored:Boolean;
      
      public var m_vStackAllowances:Vector.<int>;
      
      public var m_sprOwner:Creature;
      
      public var bHoldSlot:Boolean = false;
      
      public var m_bCarried:Boolean;
      
      public var m_bAllowStacks:Boolean = false;
      
      public var m_bLocked:Boolean = false;
      
      public var m_fZoom:Number;
      
      public function GUIInventorySlot(param1:Creature, param2:String, param3:int, param4:FlxPoint, param5:String, param6:String, param7:int, param8:Boolean, param9:Boolean = false, param10:FlxPoint = null, param11:Vector.<int> = null, param12:Boolean = false)
      {
         var _loc14_:Vector.<FlxSprite> = null;
         var _loc15_:int = 0;
         super();
         this.m_fZoom = 1;
         this.m_sprOwner = param1;
         this.nZSort = param7;
         this.strName = param2;
         this.nSlotIndex = param3;
         this.bMirrored = param9;
         this.m_bCarried = param8;
         if(param11 != null)
         {
            this.m_vStackAllowances = param11;
         }
         else
         {
            this.m_vStackAllowances = Vector.<int>([1]);
         }
         this.vSocketedItems = new Vector.<Vector.<ItemInstance>>();
         var _loc13_:uint = 0;
         while(_loc13_ < this.m_vStackAllowances.length)
         {
            this.vSocketedItems.push(new Vector.<ItemInstance>());
            if(this.nSlotIndex < 23)
            {
               if(this.bCreSprites == false)
               {
                  this.bCreSprites = true;
                  this.vCreSprites = new Vector.<Vector.<FlxSprite>>();
               }
               _loc14_ = new Vector.<FlxSprite>();
               _loc15_ = 0;
               while(_loc15_ < this.m_vStackAllowances[_loc13_])
               {
                  _loc14_.push(new FlxSprite());
                  _loc14_[_loc14_.length - 1].visible = false;
                  _loc14_[_loc14_.length - 1].ID = param7;
                  param1.add(_loc14_[_loc14_.length - 1]);
                  param7++;
                  _loc15_++;
               }
               this.vCreSprites.push(_loc14_);
            }
            _loc13_++;
         }
         this.btnSlot = new ImgButton(param5,param5,param5,param6,param4.x,param4.y,this.OnButtonPress,false,true);
         this.btnSort = new ImgButton("btn_inv_sort_on.png","btn_inv_sort.png","btn_inv_sort_on.png","btn_inv_sort_on.png",0,0,this.SortItems,false,false);
         this.btnSort.m_strPopUpText = "将这个容器中的物品向左上方排列。";
         this.btnSort.scrollFactor = new FlxPoint();
         if(param1 is Player)
         {
            PlayState.m_objInstance.m_aMouseOverItems.push(this.btnSort);
         }
         this.grpSlotBG = new FlxGroup();
         this.grpItemPanel = new FlxGroup();
         if(param10 != null)
         {
            this.sprCap = new FlxSprite(param10.x,param10.y);
            this.sprCap.scrollFactor = new FlxPoint();
            this.sprCap.makeGraphic(this.m_fZoom,this.m_fZoom,this.nCapColor);
            this.sprCap.visible = false;
            this.grpSlotBG.add(this.sprCap);
            if(param12)
            {
               this.grpSlotBG.add(this.btnSort);
            }
            this.btnSort.alive = param12;
            if(param12)
            {
               this.btnSort.x = param10.x + this.sprCap.width;
               this.btnSort.y = param10.y;
            }
         }
         add(this.grpSlotBG);
         add(this.grpItemPanel);
         this.grpSlotBG.add(this.btnSlot);
      }
      
      public static function get CapacityPixel() : int
      {
         return GUIValues.m_fItemZoom * nCapacityPixel;
      }
      
      override public function destroy() : void
      {
         var _loc1_:Vector.<ItemInstance> = null;
         var _loc2_:Vector.<FlxSprite> = null;
         var _loc3_:ItemInstance = null;
         var _loc4_:FlxSprite = null;
         this.strName = null;
         for each(_loc1_ in this.vSocketedItems)
         {
            for each(_loc3_ in _loc1_)
            {
               _loc3_.destroy();
            }
            _loc1_ = null;
         }
         this.vSocketedItems = null;
         this.sprCap = DataHandler.DestroyObject(this.sprCap);
         this.grpSlotBG = DataHandler.DestroyObject(this.grpSlotBG);
         this.grpItemPanel = DataHandler.DestroyObject(this.grpItemPanel);
         this.btnSlot = DataHandler.DestroyObject(this.btnSlot);
         this.btnSort = DataHandler.DestroyObject(this.btnSort);
         this.m_vStackAllowances = null;
         this.m_sprOwner = null;
         for each(_loc2_ in this.vCreSprites)
         {
            for each(_loc4_ in _loc2_)
            {
               _loc4_ = DataHandler.DestroyObject(_loc4_);
            }
            _loc2_.length = 0;
         }
         this.vCreSprites = null;
         super.destroy();
      }
      
      public function IsSlotDepthFree(param1:int) : Boolean
      {
         if(this.bHoldSlot)
         {
            return this.vSocketedItems[0].length < this.m_vStackAllowances[0];
         }
         return param1 < this.m_vStackAllowances.length && this.vSocketedItems[param1].length < this.m_vStackAllowances[param1];
      }
      
      public function ShowOverlay(param1:ItemInstance, param2:Boolean = false) : void
      {
         var _loc3_:Vector.<ItemInstance> = null;
         var _loc4_:ItemInstance = null;
         this.btnSlot.on = this.AcceptsItem(param1);
         if(param2)
         {
            this.btnSlot.on = true;
         }
         for each(_loc3_ in this.vSocketedItems)
         {
            for each(_loc4_ in _loc3_)
            {
               _loc4_.Alpha = this.fAlpha;
               if(this.sprCap != null && _loc4_.ItemDefinition.aCapacities.length > 0 && _loc4_.AcceptsItem(param1) == false)
               {
                  this.sprCap.alpha = 0;
               }
            }
         }
         if(this.btnSlot.on == false)
         {
            this.btnSlot.visible = false;
         }
      }
      
      public function HideOverlay() : void
      {
         var _loc1_:Vector.<ItemInstance> = null;
         var _loc2_:ItemInstance = null;
         this.btnSlot.on = false;
         if(this.sprCap != null)
         {
            this.sprCap.alpha = 1;
         }
         for each(_loc1_ in this.vSocketedItems)
         {
            for each(_loc2_ in _loc1_)
            {
               _loc2_.Alpha = 1;
            }
         }
         this.btnSlot.visible = true;
      }
      
      public function AddItem(param1:ItemInstance, param2:FlxPoint) : void
      {
         this.SocketedItem().AddItem(param1,param2);
         this.DrawItem(param1);
      }
      
      public function AddStackedItem(param1:ItemInstance, param2:ItemInstance) : ItemInstance
      {
         var _loc4_:ItemInstance = null;
         if(param2 == param1)
         {
         }
         var _loc3_:ItemInstance = this.SocketedItem();
         var _loc5_:GUIFitItemResult = new GUIFitItemResult();
         if(_loc3_ == null)
         {
            return param1;
         }
         if(_loc3_ == param2)
         {
            if((_loc4_ = this.UnSocketItem(false,param2,true)) == null)
            {
               return param1;
            }
            _loc5_.m_grpSlot = this;
            _loc5_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET;
         }
         else
         {
            _loc5_.m_objItem = param2.m_objParentContainer;
            _loc5_.m_ptPos = param2.ptOffset;
            if((_loc4_ = this.RemoveItem(param2,true)) != null)
            {
               _loc5_.m_nResult = GUIFitItemResult.RESULT_CAN_FIT;
            }
         }
         if(_loc5_.m_nResult == 0)
         {
            return param1;
         }
         var _loc6_:ItemInstance = param1;
         if(_loc5_.m_nResult > 0)
         {
            _loc6_ = param2.StackItem(param1);
         }
         if(_loc5_.m_nResult == GUIFitItemResult.RESULT_CAN_FIT)
         {
            if(_loc5_.m_objItem.bSocketed)
            {
               this.AddItem(param1,_loc5_.m_ptPos);
            }
            else
            {
               _loc5_.m_objItem.AddItem(param1,_loc5_.m_ptPos);
            }
         }
         else if(_loc5_.m_nResult == GUIFitItemResult.RESULT_CAN_SOCKET)
         {
            this.SocketItem(param1);
         }
         return _loc6_;
      }
      
      public function RemoveItem(param1:ItemInstance, param2:Boolean = false) : ItemInstance
      {
         var _loc6_:ItemInstance = null;
         if(param1 == null)
         {
            return param1;
         }
         var _loc3_:ItemInstance = this.SocketedItem();
         if(param1.m_objParentContainer != null)
         {
            if(param1.m_objParentContainer.bSocketed == false)
            {
               return param1.m_objParentContainer.RemoveItem(param1,param2);
            }
            _loc3_ = param1.m_objParentContainer;
         }
         if(_loc3_ == null)
         {
            return param1;
         }
         var _loc4_:int = int(_loc3_.vItems.indexOf(param1));
         var _loc5_:int = int(_loc3_.m_vStack.indexOf(param1));
         if(!param2 && param1.StackCount > 1)
         {
            if((_loc6_ = _loc3_.RemoveItem(param1)) != null && _loc4_ >= 0)
            {
               this.DrawItem(_loc3_.vItems[_loc4_]);
               this.RemoveFromItemPanel(param1);
            }
            return _loc6_;
         }
         if(!param2 && _loc5_ >= 0)
         {
            _loc3_.m_vStack.splice(_loc5_,1);
            _loc3_.UpdateStackImage();
            return param1;
         }
         if((_loc6_ = _loc3_.RemoveItem(param1,param2)) != null)
         {
            this.RemoveFromItemPanel(param1);
         }
         return _loc6_;
      }
      
      private function DrawItem(param1:ItemInstance) : void
      {
         param1.x = param1.ptOffset.x;
         param1.y = param1.ptOffset.y;
         if(this.sprCap != null)
         {
            param1.x += this.sprCap.x;
            param1.y += this.sprCap.y;
         }
         this.AddToItemPanel(param1);
      }
      
      public function AcceptsItem(param1:ItemInstance) : Boolean
      {
         if(this.nSlotIndex == 212)
         {
            return true;
         }
         if(param1.m_objProxy != null)
         {
            return false;
         }
         var _loc2_:int = int(param1.ItemDefinition.vEquipSlots.indexOf(this.nSlotIndex));
         if(_loc2_ < 0)
         {
            _loc2_ = int(param1.ItemDefinition.vUseSlots.indexOf(this.nSlotIndex));
         }
         if(_loc2_ >= 0)
         {
            return true;
         }
         return false;
      }
      
      public function SocketItem(param1:ItemInstance) : ItemInstance
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:Array = null;
         var _loc9_:AttackMode = null;
         if(param1 == null)
         {
            return param1;
         }
         var _loc2_:int = -1;
         var _loc3_:int = -1;
         if(!this.IsSlotDepthFree(param1.nSlotDepth))
         {
            if(this.bHoldSlot == false || this is GUIInventoryWound == false)
            {
               return param1;
            }
            if((_loc4_ = this.SocketedItem(param1.nSlotDepth)) != null && Boolean(PlayState.m_objInstance.grpInventoryUI.CanStackOnItem(param1,_loc4_)))
            {
               return this.AddStackedItem(param1,_loc4_);
            }
            return param1;
         }
         if(this.AcceptsItem(param1))
         {
            _loc5_ = this.SocketedItem();
            if(this.bHoldSlot || this is GUIInventoryWound)
            {
               this.vSocketedItems[0].push(param1);
               _loc3_ = 0;
               _loc2_ = 0;
               param1.fDrawDepth = this.vSocketedItems[0].length / 10;
               if(this.vSocketedItems[0].length > 1)
               {
               }
            }
            else
            {
               this.vSocketedItems[param1.nSlotDepth].push(param1);
               _loc3_ = int(this.vSocketedItems[param1.nSlotDepth].length - 1);
               _loc2_ = param1.nSlotDepth;
               param1.fDrawDepth = param1.nSlotDepth + this.vSocketedItems[param1.nSlotDepth].length / 10;
            }
            param1.bSocketed = true;
            if(!this.m_bAllowStacks)
            {
               if((_loc6_ = param1.m_vStack.pop()) != null)
               {
                  _loc6_.m_vStack = param1.m_vStack;
                  param1.m_vStack = new Vector.<ItemInstance>();
                  _loc6_.UpdateStackImage();
                  param1.UpdateStackImage();
               }
            }
            this.AddToItemPanel(param1);
            param1.m_bEquipped = true;
            param1.SwapImage(false);
            this.AlignImageToSocket(param1);
            if(this.sprCap != null)
            {
               if(!(_loc5_ != null && _loc5_.nSlotDepth > param1.nSlotDepth))
               {
                  if(param1.ItemDefinition.aCapacities.length > 0)
                  {
                     if(_loc5_ != null)
                     {
                        for each(_loc7_ in _loc5_.vItems)
                        {
                           this.RemoveFromItemPanel(_loc7_);
                        }
                     }
                     this.SetupCapBox(param1.ItemDefinition,this.sprCap);
                     for each(_loc7_ in param1.vItems)
                     {
                        this.DrawItem(_loc7_);
                     }
                  }
                  else
                  {
                     this.sprCap.makeGraphic(this.m_fZoom,this.m_fZoom,this.nCapColor);
                     this.sprCap.visible = false;
                     this.btnSort.x = this.sprCap.x + this.sprCap.width;
                     this.btnSort.visible = false;
                     if(_loc5_ != null)
                     {
                        for each(_loc7_ in _loc5_.vItems)
                        {
                           this.RemoveFromItemPanel(_loc7_);
                        }
                     }
                  }
               }
            }
            if(this.m_sprOwner != null)
            {
               this.UpdateEquipConditions(this.m_sprOwner,param1);
               param1.UpdatePossessConditions(this,null);
               for each(_loc8_ in param1.ItemDefinition.m_aAttackModes)
               {
                  if(_loc8_[0] == this.nSlotIndex)
                  {
                     (_loc9_ = DataHandler.GetAttackMode(_loc8_[1])).Link(param1);
                     this.m_sprOwner.AddAttackMode(_loc9_);
                  }
               }
               if(this.m_bCarried)
               {
                  this.m_sprOwner.Encumberance += param1.WeightPlusContents;
               }
               if(this.bCreSprites && this.m_sprOwner.HasCondition(577) == false)
               {
                  if(param1.ItemDefinition.dictSpriteUsage[this.nSlotIndex] != undefined)
                  {
                     this.vCreSprites[_loc2_][_loc3_].pixels = param1.ItemDefinition.vSpriteList[param1.ItemDefinition.dictSpriteUsage[this.nSlotIndex]];
                     this.vCreSprites[_loc2_][_loc3_].visible = true;
                  }
                  else
                  {
                     this.vCreSprites[_loc2_][_loc3_].pixels = DataHandler.GetImage("blank.png");
                     this.vCreSprites[_loc2_][_loc3_].visible = false;
                  }
                  GUIBattleScreen.grpInstance.UpdateCreatureSprite(this.m_sprOwner);
               }
            }
            this.grpItemPanel.sort("fDrawDepth");
            return _loc6_;
         }
         return param1;
      }
      
      public function UnSocketItem(param1:Boolean = false, param2:ItemInstance = null, param3:Boolean = true) : ItemInstance
      {
         var _loc5_:Vector.<ItemInstance> = null;
         var _loc8_:int = 0;
         var _loc9_:ItemInstance = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:ItemInstance = null;
         var _loc12_:AttackMode = null;
         var _loc13_:ItemInstance = null;
         if(this.m_bLocked)
         {
            return null;
         }
         var _loc4_:int = int(this.vSocketedItems.length - 1);
         if(this.bHoldSlot || this is GUIInventoryWound)
         {
            _loc4_ = 0;
         }
         else if(param2 != null)
         {
            _loc4_ = param2.nSlotDepth;
         }
         var _loc6_:int = -1;
         var _loc7_:int = -1;
         while(_loc4_ >= 0)
         {
            _loc5_ = this.vSocketedItems[_loc4_];
            if(param2 == null && _loc5_.length > 0)
            {
               param2 = _loc5_[_loc5_.length - 1];
            }
            if(param2 != null)
            {
               if((_loc6_ = int(_loc5_.indexOf(param2))) < 0)
               {
                  _loc8_ = 0;
                  for each(_loc9_ in this.vSocketedItems[_loc4_])
                  {
                     if((_loc7_ = int(_loc9_.m_vStack.indexOf(param2))) >= 0)
                     {
                        this.vSocketedItems[_loc4_][_loc8_] = param2;
                        param2.m_vStack = _loc9_.m_vStack;
                        param2.m_vStack.splice(_loc7_,1);
                        _loc9_.m_vStack = new Vector.<ItemInstance>();
                        param2.m_vStack.push(_loc9_);
                        _loc6_ = _loc8_;
                        break;
                     }
                     _loc8_++;
                  }
                  if(_loc7_ < 0)
                  {
                     return null;
                  }
               }
               _loc5_.splice(_loc6_,1);
               break;
            }
            _loc4_--;
         }
         if(param2 != null && (!param2.ItemDefinition.bSocketLocked || param1))
         {
            param2.bSocketed = false;
            this.RemoveFromItemPanel(param2);
            param2.m_bEquipped = false;
            param2.SwapImage(false);
            if(this.sprCap != null)
            {
               if((_loc10_ = this.SocketedItem()) == null || _loc10_.ItemDefinition.aCapacities.length == 0)
               {
                  this.sprCap.makeGraphic(this.m_fZoom,this.m_fZoom,this.nCapColor);
                  this.sprCap.visible = false;
                  this.btnSort.x = this.sprCap.x + this.sprCap.width;
                  this.btnSort.visible = false;
               }
               else
               {
                  this.SetupCapBox(_loc10_.ItemDefinition,this.sprCap);
                  for each(_loc11_ in _loc10_.vItems)
                  {
                     this.DrawItem(_loc11_);
                  }
               }
            }
            for each(_loc11_ in param2.vItems)
            {
               this.RemoveFromItemPanel(_loc11_);
               _loc11_.x = _loc11_.ptOffset.x;
               _loc11_.y = _loc11_.ptOffset.y;
            }
            if(this.m_sprOwner != null)
            {
               this.UpdateEquipConditions(this.m_sprOwner,param2,true);
               param2.UpdatePossessConditions(this,null,true);
               for each(_loc12_ in param2.m_vAttackModes)
               {
                  this.m_sprOwner.RemoveAttackMode(_loc12_);
               }
               if(param2.m_vAttackModes != null)
               {
                  param2.m_vAttackModes.length = 0;
               }
               if(this.m_bCarried)
               {
                  this.m_sprOwner.Encumberance -= param2.WeightPlusContents;
               }
               if(this.bCreSprites)
               {
                  this.vCreSprites[_loc4_][_loc6_].pixels = DataHandler.GetImage("blank.png");
                  this.vCreSprites[_loc4_][_loc6_].visible = false;
                  GUIBattleScreen.grpInstance.UpdateCreatureSprite(this.m_sprOwner);
               }
            }
            if(!param3 && param2.StackCount > 1)
            {
               (_loc13_ = param2.m_vStack.pop()).m_vStack = param2.m_vStack;
               param2.m_vStack = new Vector.<ItemInstance>();
               _loc13_.UpdateStackImage();
               param2.UpdateStackImage();
               this.SocketItem(_loc13_);
            }
            return param2;
         }
         if(_loc5_ != null && param2 != null)
         {
            _loc5_.push(param2);
         }
         return null;
      }
      
      public function UseItem(param1:ItemInstance) : void
      {
         var _loc2_:Array = null;
         var _loc3_:PlayerCondition = null;
         if(this.m_sprOwner == null)
         {
            return;
         }
         for each(_loc2_ in param1.ItemDefinition.m_aUseConditions)
         {
            if(_loc2_[0] == this.nSlotIndex)
            {
               if(_loc2_[1] > 0)
               {
                  _loc3_ = this.m_sprOwner.GetCondition(_loc2_[1]);
                  this.m_sprOwner.AddCondition(_loc3_);
               }
               else
               {
                  _loc3_ = this.m_sprOwner.GetCondition(-_loc2_[1]);
                  this.m_sprOwner.RemoveCondition(_loc3_);
               }
            }
         }
         this.m_sprOwner.UpdateStatus();
         if(this.m_sprOwner is Player)
         {
            PlayState.m_objInstance.UpdatePlayerUI();
         }
      }
      
      private function SetupCapBox(param1:Item, param2:FlxSprite) : void
      {
         param2.makeGraphic(FlxPoint(param1.aCapacities[0]).x,FlxPoint(param1.aCapacities[0]).y,this.nCapColor);
         param2.pixels.copyPixels(DataHandler.GetImage("GUIGrid.png"),new Rectangle(0,0,param2.width,param2.height),new Point(0,0));
         param2.pixels = GUIValues.ScaleBitmapData(param2.pixels,this.m_fZoom);
         param2.visible = true;
         param2.dirty = true;
         this.btnSort.x = param2.x + param2.width;
         this.btnSort.y = param2.y;
         this.btnSort.visible = true;
      }
      
      public function OnButtonPress() : void
      {
         this.btnSlot.on = false;
      }
      
      public function SortItems() : void
      {
         PlayState.m_objInstance.grpInventoryUI.ArrangeItemsInCapBox(this.SocketedItem());
         this.btnSort.on = false;
      }
      
      private function AddToItemPanel(param1:ItemInstance) : void
      {
         var _loc2_:ItemInstance = null;
         param1.grpItemPanelSlot = this;
         for each(_loc2_ in param1.m_vStack)
         {
            _loc2_.grpItemPanelSlot = this;
         }
         this.grpItemPanel.add(param1);
         if(this.grpItemPanel.alive)
         {
            param1.revive();
         }
         else
         {
            param1.kill();
         }
      }
      
      private function RemoveFromItemPanel(param1:ItemInstance) : void
      {
         var _loc2_:ItemInstance = null;
         param1.grpItemPanelSlot = null;
         for each(_loc2_ in param1.m_vStack)
         {
            _loc2_.grpItemPanelSlot = null;
         }
         this.grpItemPanel.remove(param1,true);
      }
      
      public function CreateAppearance() : void
      {
         var _loc2_:ItemInstance = null;
         var _loc1_:Vector.<ItemInstance> = this.GetAllSocketedItems();
         for each(_loc2_ in _loc1_)
         {
            _loc2_.CreateAppearance();
         }
      }
      
      public function DestroyAppearance() : void
      {
         var _loc2_:ItemInstance = null;
         var _loc1_:Vector.<ItemInstance> = this.GetAllSocketedItems();
         for each(_loc2_ in _loc1_)
         {
            _loc2_.DestroyAppearance();
         }
      }
      
      public function AlignImageToSocket(param1:ItemInstance) : void
      {
         if(param1 == null)
         {
            return;
         }
         param1.x = this.btnSlot.x + this.btnSlot.width / 2 - param1.width / 2;
         param1.y = this.btnSlot.y + this.btnSlot.height / 2 - param1.height / 2;
      }
      
      public function SocketedItem(param1:int = -1) : ItemInstance
      {
         var _loc3_:Vector.<ItemInstance> = null;
         if(this.bHoldSlot || param1 < 0 || param1 > this.vSocketedItems.length - 1)
         {
            param1 = int(this.vSocketedItems.length - 1);
         }
         if(this.vSocketedItems[param1].length > 0 && param1 >= 0 && this.vSocketedItems.length > param1)
         {
            _loc3_ = this.vSocketedItems[param1];
            return _loc3_[_loc3_.length - 1];
         }
         var _loc2_:int = int(this.vSocketedItems.length - 1);
         while(_loc2_ >= 0)
         {
            _loc3_ = this.vSocketedItems[_loc2_];
            if(_loc3_.length > 0)
            {
               return _loc3_[_loc3_.length - 1];
            }
            _loc2_--;
         }
         return null;
      }
      
      public function GetAllSocketedItems() : Vector.<ItemInstance>
      {
         var _loc1_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc2_:uint = 0;
         while(_loc2_ < this.vSocketedItems.length)
         {
            _loc1_ = _loc1_.concat(this.vSocketedItems[_loc2_]);
            _loc2_++;
         }
         return _loc1_;
      }
      
      public function AddConditionsToCreature(param1:ItemInstance, param2:Creature) : void
      {
         var _loc4_:Array = null;
         var _loc5_:AttackMode = null;
         var _loc3_:Creature = this.m_sprOwner;
         this.m_sprOwner = param2;
         this.UpdateEquipConditions(param2,param1);
         param1.UpdatePossessConditions(this,null);
         for each(_loc4_ in param1.ItemDefinition.m_aAttackModes)
         {
            if(_loc4_[0] == this.nSlotIndex)
            {
               (_loc5_ = DataHandler.GetAttackMode(_loc4_[1])).Link(param1);
               param2.AddAttackMode(_loc5_);
            }
         }
         if(this.m_bCarried)
         {
            param2.Encumberance += param1.WeightPlusContents;
         }
         this.m_sprOwner = _loc3_;
      }
      
      public function UpdateEquipConditions(param1:IConditionOwner, param2:ItemInstance, param3:Boolean = false, param4:Boolean = false) : void
      {
         var _loc6_:ItemInstance = null;
         var _loc7_:Array = null;
         var _loc8_:PlayerCondition = null;
         var _loc9_:int = 0;
         var _loc5_:Boolean = true;
         if(param2.ItemDefinition.m_bDischargeEquip || param2.ItemDefinition.m_bDischargeHex)
         {
            _loc5_ = param2.IsCharged(0,0.1,1);
         }
         if(_loc5_ || param4)
         {
            for each(_loc7_ in param2.ItemDefinition.m_aEquipConditions)
            {
               if(!(_loc7_[0] != "" && _loc7_[0] != this.nSlotIndex))
               {
                  _loc8_ = param1.GetCondition(Math.abs(_loc7_[1]));
                  _loc9_ = int(_loc7_[1]);
                  if(param3)
                  {
                     _loc9_ = -_loc9_;
                  }
                  if(_loc9_ > 0)
                  {
                     param1.AddCondition(_loc8_);
                  }
                  else
                  {
                     param1.RemoveCondition(_loc8_);
                  }
               }
            }
            if(param4)
            {
               return;
            }
         }
         for each(_loc6_ in param2.m_vStack)
         {
            this.UpdateEquipConditions(param1,_loc6_,param3);
         }
      }
      
      public function SetRes(param1:Number, param2:String = "", param3:String = "") : void
      {
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         if(param2 != "")
         {
            GUIValues.SetPosition(this.btnSlot,param2);
         }
         if(param3 != "" && this.sprCap != null)
         {
            GUIValues.SetPosition(this.sprCap,param3);
         }
         if(param1 != this.m_fZoom || true)
         {
            this.btnSlot.Zoom(param1);
            this.btnSort.Zoom(param1);
            if(this.sprCap != null)
            {
               this.sprCap.pixels = GUIValues.ScaleBitmapData(this.sprCap.pixels,param1 / this.m_fZoom);
               this.btnSort.x = this.sprCap.x + this.sprCap.width;
               this.btnSort.y = this.sprCap.y;
            }
            for each(_loc4_ in this.vSocketedItems)
            {
               for each(_loc5_ in _loc4_)
               {
                  _loc5_.SetRes(param1);
                  this.AlignImageToSocket(_loc5_);
                  if(this.sprCap != null)
                  {
                     for each(_loc6_ in _loc5_.vItems)
                     {
                        _loc6_.x = this.sprCap.x + _loc6_.ptOffset.x;
                        _loc6_.y = this.sprCap.y + _loc6_.ptOffset.y;
                     }
                  }
               }
            }
            this.m_fZoom = param1;
         }
      }
   }
}

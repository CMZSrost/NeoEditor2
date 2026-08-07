package org.flixel
{
   import flash.display.Graphics;
   
   public class FlxHexTile extends FlxBasic
   {
      
      protected static const _pZero:FlxPoint = new FlxPoint();
      
      public static const MAX_SCAVENGE_ITEMS:uint = 5;
       
      
      public var x:Number;
      
      public var y:Number;
      
      public var width:Number;
      
      public var height:Number;
      
      public var scrollFactor:FlxPoint;
      
      protected var _flicker:Boolean;
      
      protected var _flickerTimer:Number;
      
      public var health:Number;
      
      protected var _point:FlxPoint;
      
      protected var _rect:FlxRect;
      
      public var tilemap:FlxTilemap;
      
      public var index:uint;
      
      public var mapIndex:uint;
      
      public var nExploredState:uint;
      
      public var strName:String = "";
      
      public var strDesc:String = "";
      
      public var nTerrainCost:int = 0;
      
      public var nVizLimiter:int = 0;
      
      public var nVizIncrease:int = 0;
      
      public var nTreasureID:uint;
      
      public var nMapIndex:uint;
      
      public var nCampItems:uint;
      
      private var m_objGroundItem:ItemInstance;
      
      public var m_vCampItems:Vector.<ItemCamp>;
      
      public var nDefaultCampID:int;
      
      public var bVisited:Boolean;
      
      public var bScavenged:Boolean;
      
      public var bPassable:Boolean;
      
      private var m_fScent:Number;
      
      public var m_objScentOwner:Creature;
      
      public var m_fTotalValue:Number;
      
      public var m_vOccupants:Vector.<Creature>;
      
      public var m_nBarterTile:uint;
      
      private var m_bCampTile:Boolean;
      
      public var m_vScavengeItems:Vector.<ItemInstance>;
      
      public var m_nScavengeInitialID:uint;
      
      public var m_nScavengeItemsIDPerHour:uint;
      
      public var m_objLastScavengeUpdate:Date;
      
      public var m_vLightLevels:Vector.<Number>;
      
      public var m_vCondIDs:Vector.<int>;
      
      public var m_ptHexCoords:FlxPoint;
      
      public var m_nDMCDist:int;
      
      public var m_nMinRange:int;
      
      public var m_nMaxRange:int;
      
      public var m_fDMCCoeff:Number;
      
      public var m_objBattle:Battle;
      
      public function FlxHexTile(param1:FlxTilemap, param2:uint, param3:Number, param4:Number, param5:Boolean, param6:uint, param7:uint)
      {
         super();
         this.x = 0;
         this.y = 0;
         this.width = param3;
         this.height = param4;
         this.scrollFactor = new FlxPoint(1,1);
         this._flicker = false;
         this._flickerTimer = 0;
         this._point = new FlxPoint();
         this._rect = new FlxRect();
         this.tilemap = param1;
         this.index = param2;
         visible = param5;
         this.mapIndex = 0;
         this.nExploredState = param7;
         this.bScavenged = false;
         this.m_fScent = 0;
         this.bPassable = true;
         this.bVisited = false;
         this.m_fTotalValue = 0;
         this.m_nBarterTile = BarterHex.BARTER_NONE;
         this.m_bCampTile = false;
         this.m_vScavengeItems = new Vector.<ItemInstance>();
         this.m_vLightLevels = new Vector.<Number>();
         this.m_vCondIDs = new Vector.<int>();
         this.m_vOccupants = new Vector.<Creature>();
         this.m_ptHexCoords = new FlxPoint();
         this.m_nDMCDist = 1;
         this.m_fDMCCoeff = 1;
         this.m_nMinRange = 3;
         this.m_nMaxRange = 6;
      }
      
      public function SetHexType(param1:FlxHexTile) : void
      {
         this.strName = param1.strName;
         this.strDesc = param1.strDesc;
         this.nDefaultCampID = param1.nDefaultCampID;
         this.nTerrainCost = param1.nTerrainCost;
         this.nVizLimiter = param1.nVizLimiter;
         this.nVizIncrease = param1.nVizIncrease;
         this.nTreasureID = param1.nTreasureID;
         this.nCampItems = param1.nCampItems;
         this.bPassable = param1.bPassable;
         this.m_nScavengeInitialID = param1.m_nScavengeInitialID;
         this.m_nScavengeItemsIDPerHour = param1.m_nScavengeItemsIDPerHour;
         this.index = param1.index;
         this.m_vLightLevels = param1.m_vLightLevels.concat();
         this.m_vCondIDs = param1.m_vCondIDs.concat();
         this.m_nMinRange = param1.m_nMinRange;
         this.m_nMaxRange = param1.m_nMaxRange;
         if(DM.m_vDMCHexes.indexOf(this.index) < 0)
         {
            this.m_fDMCCoeff = this.m_nDMCDist / DM.m_nDMCRadius;
            if(this.m_fDMCCoeff > 1)
            {
               this.m_fDMCCoeff = 1;
            }
            this.m_fDMCCoeff *= this.m_fDMCCoeff;
         }
      }
      
      override public function destroy() : void
      {
         var _loc1_:Creature = null;
         var _loc2_:ItemInstance = null;
         var _loc3_:ItemCamp = null;
         this.scrollFactor = null;
         this._point = null;
         this._rect = null;
         cameras = null;
         this.tilemap = null;
         this.m_objGroundItem = DataHandler.DestroyObject(this.m_objGroundItem);
         this.m_objGroundItem = null;
         this.m_objLastScavengeUpdate = null;
         this.m_objScentOwner = DataHandler.DestroyObject(this.m_objScentOwner);
         this.m_objScentOwner = null;
         for each(_loc1_ in this.m_vOccupants)
         {
            _loc1_.destroy();
         }
         this.m_vOccupants = null;
         for each(_loc2_ in this.m_vScavengeItems)
         {
            _loc2_.destroy();
         }
         this.m_vScavengeItems = null;
         for each(_loc3_ in this.m_vCampItems)
         {
            _loc3_.destroy();
         }
         this.m_vCampItems = null;
         this.strName = null;
         this.strDesc = null;
         this.m_vLightLevels = null;
         this.m_vCondIDs = null;
         this.m_ptHexCoords = null;
         this.m_objBattle = DataHandler.DestroyObject(this.m_objBattle);
      }
      
      override public function preUpdate() : void
      {
         ++_ACTIVECOUNT;
         if(this._flickerTimer != 0)
         {
            if(this._flickerTimer > 0)
            {
               this._flickerTimer -= FlxG.elapsed;
               if(this._flickerTimer <= 0)
               {
                  this._flickerTimer = 0;
                  this._flicker = false;
               }
            }
         }
      }
      
      override public function postUpdate() : void
      {
      }
      
      override public function draw() : void
      {
         var _loc1_:FlxCamera = null;
         if(cameras == null)
         {
            cameras = FlxG.cameras;
         }
         var _loc2_:uint = 0;
         var _loc3_:uint = cameras.length;
         while(_loc2_ < _loc3_)
         {
            _loc1_ = cameras[_loc2_++];
            if(this.onScreen(_loc1_))
            {
               ++_VISIBLECOUNT;
               if(FlxG.visualDebug && !ignoreDrawDebug)
               {
                  this.drawDebug(_loc1_);
               }
            }
         }
      }
      
      override public function drawDebug(param1:FlxCamera = null) : void
      {
         if(param1 == null)
         {
            param1 = FlxG.camera;
         }
         var _loc2_:Number = this.x - int(param1.scroll.x * this.scrollFactor.x);
         var _loc3_:Number = this.y - int(param1.scroll.y * this.scrollFactor.y);
         _loc2_ = int(_loc2_ + (_loc2_ > 0 ? 1e-7 : -1e-7));
         _loc3_ = int(_loc3_ + (_loc3_ > 0 ? 1e-7 : -1e-7));
         var _loc4_:int = this.width != int(this.width) ? int(this.width) : int(this.width - 1);
         var _loc5_:int = this.height != int(this.height) ? int(this.height) : int(this.height - 1);
         var _loc6_:Graphics;
         (_loc6_ = FlxG.flashGfx).clear();
         _loc6_.moveTo(_loc2_,_loc3_);
         var _loc7_:uint = FlxG.BLUE;
         _loc6_.lineStyle(1,_loc7_,0.5);
         _loc6_.lineTo(_loc2_ + _loc4_,_loc3_);
         _loc6_.lineTo(_loc2_ + _loc4_,_loc3_ + _loc5_);
         _loc6_.lineTo(_loc2_,_loc3_ + _loc5_);
         _loc6_.lineTo(_loc2_,_loc3_);
         param1.buffer.draw(FlxG.flashGfxSprite);
      }
      
      public function overlaps(param1:FlxBasic, param2:Boolean = false, param3:FlxCamera = null) : Boolean
      {
         var _loc6_:Boolean = false;
         var _loc7_:uint = 0;
         var _loc8_:Array = null;
         if(param1 is FlxGroup)
         {
            _loc6_ = false;
            _loc7_ = 0;
            _loc8_ = (param1 as FlxGroup).members;
            while(_loc7_ < length)
            {
               if(this.overlaps(_loc8_[_loc7_++],param2,param3))
               {
                  _loc6_ = true;
               }
            }
            return _loc6_;
         }
         if(param1 is FlxTilemap)
         {
            return (param1 as FlxTilemap).overlaps(this,param2,param3);
         }
         var _loc4_:FlxObject = param1 as FlxObject;
         if(!param2)
         {
            return _loc4_.x + _loc4_.width > this.x && _loc4_.x < this.x + this.width && _loc4_.y + _loc4_.height > this.y && _loc4_.y < this.y + this.height;
         }
         if(param3 == null)
         {
            param3 = FlxG.camera;
         }
         var _loc5_:FlxPoint = _loc4_.getScreenXY(null,param3);
         this.getScreenXY(this._point,param3);
         return _loc5_.x + _loc4_.width > this._point.x && _loc5_.x < this._point.x + this.width && _loc5_.y + _loc4_.height > this._point.y && _loc5_.y < this._point.y + this.height;
      }
      
      public function overlapsAt(param1:Number, param2:Number, param3:FlxBasic, param4:Boolean = false, param5:FlxCamera = null) : Boolean
      {
         var _loc8_:Boolean = false;
         var _loc9_:FlxBasic = null;
         var _loc10_:uint = 0;
         var _loc11_:Array = null;
         var _loc12_:FlxTilemap = null;
         if(param3 is FlxGroup)
         {
            _loc8_ = false;
            _loc10_ = 0;
            _loc11_ = (param3 as FlxGroup).members;
            while(_loc10_ < length)
            {
               if(this.overlapsAt(param1,param2,_loc11_[_loc10_++],param4,param5))
               {
                  _loc8_ = true;
               }
            }
            return _loc8_;
         }
         if(param3 is FlxTilemap)
         {
            _loc12_ = param3 as FlxTilemap;
            return _loc12_.overlapsAt(_loc12_.x - (param1 - this.x),_loc12_.y - (param2 - this.y),this,param4,param5);
         }
         var _loc6_:FlxObject = param3 as FlxObject;
         if(!param4)
         {
            return _loc6_.x + _loc6_.width > param1 && _loc6_.x < param1 + this.width && _loc6_.y + _loc6_.height > param2 && _loc6_.y < param2 + this.height;
         }
         if(param5 == null)
         {
            param5 = FlxG.camera;
         }
         var _loc7_:FlxPoint = _loc6_.getScreenXY(null,param5);
         this._point.x = param1 - int(param5.scroll.x * this.scrollFactor.x);
         this._point.y = param2 - int(param5.scroll.y * this.scrollFactor.y);
         this._point.x += this._point.x > 0 ? 1e-7 : -1e-7;
         this._point.y += this._point.y > 0 ? 1e-7 : -1e-7;
         return _loc7_.x + _loc6_.width > this._point.x && _loc7_.x < this._point.x + this.width && _loc7_.y + _loc6_.height > this._point.y && _loc7_.y < this._point.y + this.height;
      }
      
      public function overlapsPoint(param1:FlxPoint, param2:Boolean = false, param3:FlxCamera = null) : Boolean
      {
         if(!param2)
         {
            return param1.x > this.x && param1.x < this.x + this.width && param1.y > this.y && param1.y < this.y + this.height;
         }
         if(param3 == null)
         {
            param3 = FlxG.camera;
         }
         var _loc4_:Number = param1.x - param3.scroll.x;
         var _loc5_:Number = param1.y - param3.scroll.y;
         this.getScreenXY(this._point,param3);
         return _loc4_ > this._point.x && _loc4_ < this._point.x + this.width && _loc5_ > this._point.y && _loc5_ < this._point.y + this.height;
      }
      
      public function onScreen(param1:FlxCamera = null) : Boolean
      {
         if(param1 == null)
         {
            param1 = FlxG.camera;
         }
         this.getScreenXY(this._point,param1);
         return this._point.x + this.width > 0 && this._point.x < param1.width && this._point.y + this.height > 0 && this._point.y < param1.height;
      }
      
      public function getScreenXY(param1:FlxPoint = null, param2:FlxCamera = null) : FlxPoint
      {
         if(param1 == null)
         {
            param1 = new FlxPoint();
         }
         if(param2 == null)
         {
            param2 = FlxG.camera;
         }
         param1.x = this.x - int(param2.scroll.x * this.scrollFactor.x);
         param1.y = this.y - int(param2.scroll.y * this.scrollFactor.y);
         param1.x += param1.x > 0 ? 1e-7 : -1e-7;
         param1.y += param1.y > 0 ? 1e-7 : -1e-7;
         return param1;
      }
      
      public function flicker(param1:Number = 1) : void
      {
         this._flickerTimer = param1;
         if(this._flickerTimer == 0)
         {
            this._flicker = false;
         }
      }
      
      public function get flickering() : Boolean
      {
         return this._flickerTimer != 0;
      }
      
      public function getMidpoint(param1:FlxPoint = null) : FlxPoint
      {
         if(param1 == null)
         {
            param1 = new FlxPoint();
         }
         param1.x = this.x + this.width * 0.5;
         param1.y = this.y + this.height * 0.5;
         return param1;
      }
      
      public function reset(param1:Number, param2:Number) : void
      {
         revive();
         this.x = param1;
         this.y = param2;
      }
      
      public function CalculateValue() : void
      {
         var _loc3_:ItemInstance = null;
         var _loc1_:Number = this.m_fTotalValue;
         this.m_fTotalValue = 0;
         if(this.m_objGroundItem == null)
         {
            return;
         }
         var _loc2_:Vector.<ItemInstance> = this.GroundObject.GetItems();
         for each(_loc3_ in _loc2_)
         {
            this.m_fTotalValue += _loc3_.GetTotalValue();
         }
         if(this.m_fTotalValue != _loc1_)
         {
            this.tilemap.setDirty();
         }
      }
      
      public function UpdateScavengeItems(param1:Date, param2:Boolean) : void
      {
         var _loc3_:Vector.<ItemInstance> = null;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc8_:Vector.<int> = null;
         var _loc9_:ItemInstance = null;
         var _loc10_:int = 0;
         var _loc11_:Boolean = false;
         var _loc12_:Number = NaN;
         if(!this.bScavenged)
         {
            _loc3_ = DataHandler.GetTreasure(this.nTreasureID).GenerateTreasure(true);
            _loc8_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_STACK_PARTIAL,GUIFitItemResult.RESULT_CANNOT_FIT]);
            for each(_loc9_ in _loc3_)
            {
               PlayState.m_objInstance.grpInventoryUI.AddItemToCapBox(_loc9_,this.GroundObject,_loc8_,true);
            }
            this.CalculateValue();
            this.bScavenged = true;
         }
         var _loc4_:Number = Math.random();
         if(param2 == false || !this.bPassable || this.m_vScavengeItems.length >= MAX_SCAVENGE_ITEMS || _loc4_ > this.m_fDMCCoeff)
         {
            return;
         }
         var _loc5_:Number = Number(param1.getTime());
         if(this.m_objLastScavengeUpdate == null)
         {
            this.m_objLastScavengeUpdate = new Date();
            this.m_objLastScavengeUpdate.setTime(_loc5_);
            _loc3_ = DataHandler.GetTreasure(this.m_nScavengeInitialID).GenerateTreasure();
            if(this.m_vScavengeItems.length < MAX_SCAVENGE_ITEMS)
            {
               this.m_vScavengeItems = this.m_vScavengeItems.concat(_loc3_);
            }
         }
         else
         {
            _loc7_ = Number(this.m_objLastScavengeUpdate.getTime());
            _loc6_ = (_loc5_ - _loc7_) / 1000 / 60 / 60;
         }
         if(_loc6_ >= 1)
         {
            _loc10_ = int(this.m_nScavengeItemsIDPerHour);
            _loc11_ = false;
            if(_loc6_ > 300)
            {
               _loc6_ = 300;
            }
            _loc12_ = _loc6_;
            while(_loc12_ > 0)
            {
               if(Math.random() <= this.m_fDMCCoeff)
               {
                  _loc3_ = DataHandler.GetTreasure(_loc10_).GenerateTreasure(false,_loc11_,false);
                  if(this.m_vScavengeItems.length < MAX_SCAVENGE_ITEMS)
                  {
                     this.m_vScavengeItems = this.m_vScavengeItems.concat(_loc3_);
                  }
               }
               _loc12_--;
            }
            this.m_objLastScavengeUpdate.setTime(_loc5_);
         }
      }
      
      public function get Scent() : Number
      {
         return this.m_fScent;
      }
      
      public function set Scent(param1:Number) : void
      {
         var _loc2_:Number = this.m_fScent;
         this.m_fScent = param1;
         if(this.m_fScent < 0)
         {
            this.m_fScent = 0;
         }
         if(this.m_fScent != _loc2_)
         {
            this.tilemap.setDirty();
         }
      }
      
      public function get HasGroundItems() : Boolean
      {
         return this.m_objGroundItem != null && this.m_objGroundItem.vItems.length > 0;
      }
      
      public function get GroundObject() : ItemInstance
      {
         if(this.m_objGroundItem == null)
         {
            this.m_objGroundItem = DataHandler.GetItem("93.4");
         }
         return this.m_objGroundItem;
      }
      
      private function Randomize(param1:*, param2:*) : int
      {
         return Math.random() > 0.5 ? 1 : -1;
      }
      
      public function GetCampObject(param1:int = -1) : ItemCamp
      {
         var _loc2_:Vector.<ItemInstance> = null;
         var _loc3_:ItemInstance = null;
         if(this.m_vCampItems == null)
         {
            this.m_vCampItems = new Vector.<ItemCamp>();
            _loc2_ = DataHandler.GetTreasure(this.nDefaultCampID).GenerateTreasure();
            for each(_loc3_ in _loc2_)
            {
               this.m_vCampItems.push(_loc3_);
            }
            this.m_vCampItems = this.m_vCampItems.sort(this.Randomize);
         }
         if(this.m_vCampItems.length == 0)
         {
            _loc2_ = DataHandler.GetTreasure(517).GenerateTreasure();
            for each(_loc3_ in _loc2_)
            {
               this.m_vCampItems.push(_loc3_);
            }
         }
         if(param1 == -1 || this.m_vCampItems.length <= param1)
         {
            return this.m_vCampItems[this.m_vCampItems.length - 1];
         }
         return this.m_vCampItems[param1];
      }
      
      public function GetHexCoords() : FlxPoint
      {
         return new FlxPoint(this.m_ptHexCoords.x,this.m_ptHexCoords.y);
      }
      
      public function AddCreature(param1:Creature) : void
      {
         var _loc2_:int = int(this.m_vOccupants.indexOf(param1));
         if(_loc2_ < 0)
         {
            this.m_vOccupants.push(param1);
         }
      }
      
      public function RemoveCreature(param1:Creature) : void
      {
         var _loc2_:int = int(this.m_vOccupants.indexOf(param1));
         if(_loc2_ >= 0)
         {
            this.m_vOccupants.splice(_loc2_,1);
         }
      }
      
      public function get IsCampTile() : Boolean
      {
         return this.m_bCampTile;
      }
      
      public function set IsCampTile(param1:Boolean) : void
      {
         if(this.m_bCampTile != param1)
         {
            this.tilemap.setDirty();
         }
         this.m_bCampTile = param1;
      }
      
      public function get SaveData() : SaveGameHex
      {
         var _loc2_:ItemInstance = null;
         var _loc3_:Creature = null;
         var _loc4_:ItemCamp = null;
         var _loc1_:SaveGameHex = new SaveGameHex();
         _loc1_.m_fScent = this.m_fScent;
         _loc1_.m_nIndex = this.index;
         _loc1_.m_vOccupantIndices = new Vector.<int>();
         _loc1_.m_nScentOwnerIndex = -1;
         if(this.m_objScentOwner != null)
         {
            _loc1_.m_nScentOwnerIndex = 0;
         }
         _loc1_.m_nCampItems = this.nCampItems;
         _loc1_.m_bCampTile = this.m_bCampTile;
         _loc1_.m_nExploredState = this.nExploredState;
         if(this.m_objLastScavengeUpdate != null)
         {
            _loc1_.m_fDate = this.m_objLastScavengeUpdate.getTime();
            if(this.m_vScavengeItems != null)
            {
               for each(_loc2_ in this.m_vScavengeItems)
               {
                  _loc1_.m_vScavengeItems.push(_loc2_.SaveData);
               }
            }
         }
         if(this.m_objGroundItem != null)
         {
            _loc1_.m_vItems.push(this.m_objGroundItem.SaveData);
         }
         if(this.m_vOccupants.length > 0)
         {
            for each(_loc3_ in this.m_vOccupants)
            {
               _loc1_.m_vOccupantIndices.push(PlayState(FlxG.state).m_aCreatures.indexOf(_loc3_));
            }
         }
         if(this.m_vCampItems != null)
         {
            for each(_loc4_ in this.m_vCampItems)
            {
               _loc1_.m_vCampItems.push(_loc4_.SaveData);
            }
         }
         return _loc1_;
      }
      
      public function set SaveData(param1:SaveGameHex) : void
      {
         var _loc2_:SaveGameItem = null;
         var _loc3_:ItemInstance = null;
         var _loc4_:ItemCamp = null;
         this.m_fScent = param1.m_fScent;
         this.nCampItems = param1.m_nCampItems;
         this.nExploredState = param1.m_nExploredState;
         this.bScavenged = true;
         this.m_bCampTile = param1.m_bCampTile;
         MapUtils.tmapHexes.setTileByIndex(param1.m_nMapIndex,param1.m_nIndex);
         if(param1.m_fDate >= 0)
         {
            this.m_objLastScavengeUpdate = new Date();
            this.m_objLastScavengeUpdate.setTime(param1.m_fDate);
            this.m_vScavengeItems.length = 0;
            for each(_loc2_ in param1.m_vScavengeItems)
            {
               _loc3_ = ItemInstance(DataHandler.GetItem(_loc2_.strID));
               if(_loc3_ != null)
               {
                  _loc3_.SaveData = _loc2_;
                  this.m_vScavengeItems.push(_loc3_);
               }
            }
         }
         for each(_loc2_ in param1.m_vItems)
         {
            this.GroundObject.SaveData = _loc2_;
         }
         if(param1.m_vCampItems.length > 0)
         {
            this.m_vCampItems = new Vector.<ItemCamp>();
         }
         for each(_loc2_ in param1.m_vCampItems)
         {
            if((_loc4_ = ItemCamp(DataHandler.GetItem(_loc2_.strID))) != null)
            {
               _loc4_.SaveData = _loc2_;
               this.m_vCampItems.push(_loc4_);
            }
         }
      }
   }
}

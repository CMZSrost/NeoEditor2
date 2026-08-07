package org.flixel
{
   import flash.display.BitmapData;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import org.flixel.system.FlxHexmapBuffer;
   
   public class FlxHexmap extends FlxTilemap
   {
       
      
      private var nXOffset:int = 0;
      
      private var nYOffset:int = 0;
      
      private var ptIcon:Point;
      
      public var vVisibleHexes:Vector.<FlxHexTile>;
      
      private var bmpNewItems:BitmapData;
      
      private var bmpOldItems:BitmapData;
      
      private var bmpSearch:BitmapData;
      
      private var bmpCamp:BitmapData;
      
      private var bmpHexBlank:BitmapData;
      
      private var aTracks:Array;
      
      private var nHexSheetIndex:int;
      
      protected var rectOffset:Rectangle;
      
      public function FlxHexmap()
      {
         super();
         this.vVisibleHexes = new Vector.<FlxHexTile>();
         this.bmpNewItems = DataHandler.GetImage("IcoNewItems.png");
         this.bmpOldItems = DataHandler.GetImage("IcoOldItems.png");
         this.bmpSearch = DataHandler.GetImage("IcoSearch.png");
         this.bmpCamp = DataHandler.GetImage("IcoCamp.png");
         this.bmpHexBlank = DataHandler.GetImage(MapUtils.m_vHexBlanks[0]);
         this.rectOffset = new Rectangle();
         this.aTracks = new Array(DataHandler.GetImage("IcoTracks01.png"),DataHandler.GetImage("IcoTracks02.png"),DataHandler.GetImage("IcoTracks03.png"),DataHandler.GetImage("IcoTracks04.png"),DataHandler.GetImage("IcoTracks05.png"));
      }
      
      override public function destroy() : void
      {
         _flashPoint = null;
         _flashRect = null;
         _tiles = null;
         var _loc1_:uint = 0;
         var _loc2_:uint = 0;
         if(_tileObjects != null)
         {
            _loc2_ = _tileObjects.length;
            while(_loc1_ < _loc2_)
            {
               (_tileObjects[_loc1_++] as FlxHexTile).destroy();
            }
            _tileObjects = null;
         }
         if(_buffers != null)
         {
            _loc1_ = 0;
            _loc2_ = _buffers.length;
            while(_loc1_ < _loc2_)
            {
               (_buffers[_loc1_++] as FlxHexmapBuffer).destroy();
            }
            _buffers = null;
         }
         _data = null;
         _rects = null;
         _debugTileNotSolid = null;
         _debugTilePartial = null;
         _debugTileSolid = null;
         _debugRect = null;
         velocity = null;
         acceleration = null;
         drag = null;
         maxVelocity = null;
         scrollFactor = null;
         _point = null;
         _rect = null;
         last = null;
         cameras = null;
         if(path != null)
         {
            path.destroy();
         }
         path = null;
         this.ptIcon = null;
         this.vVisibleHexes = null;
         this.bmpNewItems = null;
         this.bmpOldItems = null;
         this.bmpSearch = null;
         this.bmpCamp = null;
         this.bmpHexBlank = null;
         if(this.aTracks != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aTracks.length)
            {
               this.aTracks[_loc1_] = null;
               _loc1_++;
            }
            this.aTracks = null;
         }
      }
      
      public function loadHexMap(param1:String, param2:BitmapData, param3:uint = 0, param4:uint = 0, param5:uint = 0, param6:uint = 0, param7:uint = 1, param8:uint = 1, param9:int = 0, param10:int = 0) : FlxTilemap
      {
         var _loc11_:Array = null;
         var _loc14_:uint = 0;
         var _loc15_:uint = 0;
         var _loc17_:uint = 0;
         var _loc18_:int = 0;
         var _loc19_:FlxPoint = null;
         var _loc20_:uint = 0;
         var _loc21_:FlxHexTile = null;
         auto = param5;
         _startingIndex = param6;
         this.nXOffset = param9;
         this.nYOffset = param10;
         this.ptIcon = new Point();
         var _loc12_:Array = param1.split("\n");
         heightInTiles = _loc12_.length;
         _data = new Array();
         var _loc13_:uint = 0;
         while(_loc13_ < heightInTiles)
         {
            if((_loc11_ = _loc12_[_loc13_++].split(",")).length <= 1)
            {
               --heightInTiles;
            }
            else
            {
               if(widthInTiles == 0)
               {
                  widthInTiles = _loc11_.length;
               }
               _loc14_ = 0;
               while(_loc14_ < widthInTiles)
               {
                  _data.push(uint(_loc11_[_loc14_++]));
               }
            }
         }
         totalTiles = widthInTiles * heightInTiles;
         if(auto > OFF)
         {
            _startingIndex = 1;
            param7 = 1;
            param8 = 1;
            _loc15_ = 0;
            while(_loc15_ < totalTiles)
            {
               autoTile(_loc15_++);
            }
         }
         _tiles = param2;
         _tileWidth = param3;
         if(_tileWidth == 0)
         {
            _tileWidth = _tiles.height;
         }
         _tileHeight = param4;
         if(_tileHeight == 0)
         {
            _tileHeight = _tileWidth;
         }
         _loc15_ = 0;
         var _loc16_:uint = totalTiles;
         if(auto > OFF)
         {
            _loc16_++;
         }
         _tileObjects = new Array(_loc16_);
         while(_loc15_ < _loc16_)
         {
            _loc18_ = _loc15_ / widthInTiles % 2;
            _loc19_ = new FlxPoint(x + uint(_loc15_ % widthInTiles) * this.nXOffset,y + uint(_loc15_ / widthInTiles) * this.nYOffset);
            if(_loc18_ != 0)
            {
               _loc19_.x += this.nXOffset / 2;
            }
            _loc20_ = uint(_data[_loc15_]);
            (_loc21_ = new FlxHexTile(this,_loc20_,_tileWidth,_tileHeight,_loc20_ >= param7,_loc20_ >= param8 ? allowCollisions : NONE,2)).nMapIndex = _loc15_;
            _loc21_.m_ptHexCoords.y = int(_loc15_ / widthInTiles);
            _loc21_.m_ptHexCoords.x = int(_loc15_ % widthInTiles);
            _loc21_.m_nDMCDist = MapUtils.GetHexDistance(DM.m_ptDMC,_loc21_.m_ptHexCoords);
            _loc21_.SetHexType(DataHandler.GetHexType(_loc20_));
            _loc21_.x = _loc19_.x;
            _loc21_.y = _loc19_.y;
            _tileObjects[_loc15_] = _loc21_;
            _loc15_++;
         }
         _debugTileNotSolid = makeDebugTile(FlxG.BLUE);
         _debugTilePartial = makeDebugTile(FlxG.PINK);
         _debugTileSolid = makeDebugTile(FlxG.GREEN);
         _debugRect = new Rectangle(0,0,_tileWidth,_tileHeight);
         width = this.nXOffset * (widthInTiles - 1) + _tileWidth + this.nXOffset / 2;
         height = this.nYOffset * (heightInTiles - 1) + _tileHeight;
         _rects = new Array(totalTiles);
         _loc15_ = 0;
         while(_loc15_ < totalTiles)
         {
            this.updateTile(_loc15_++);
         }
         return this;
      }
      
      protected function drawHexmap(param1:FlxHexmapBuffer, param2:FlxCamera) : void
      {
         var _loc9_:uint = 0;
         var _loc10_:uint = 0;
         var _loc11_:FlxHexTile = null;
         var _loc12_:BitmapData = null;
         var _loc13_:int = 0;
         var _loc14_:FlxHexTile = null;
         var _loc15_:BitmapData = null;
         param1.fill();
         _point.x = int(param2.scroll.x * scrollFactor.x) - x;
         _point.y = int(param2.scroll.y * scrollFactor.y) - y;
         var _loc3_:int = (_point.x + (_point.x > 0 ? 1e-7 : -1e-7)) / this.nXOffset - 1;
         var _loc4_:int = (_point.y + (_point.y > 0 ? 1e-7 : -1e-7)) / this.nYOffset - 1;
         var _loc5_:uint = param1.rows;
         var _loc6_:uint = param1.columns;
         if(_loc3_ < 0)
         {
            _loc3_ = 0;
         }
         if(_loc3_ > widthInTiles - _loc6_)
         {
            _loc3_ = widthInTiles - _loc6_;
         }
         if(_loc4_ < 0)
         {
            _loc4_ = 0;
         }
         if(_loc4_ > heightInTiles - _loc5_)
         {
            _loc4_ = heightInTiles - _loc5_;
         }
         var _loc7_:int = _loc4_ * widthInTiles + _loc3_;
         _flashPoint.y = 0;
         var _loc8_:uint = 0;
         while(_loc8_ < _loc5_)
         {
            _loc10_ = uint(_loc7_);
            _loc9_ = 0;
            if((_loc13_ = _loc7_ / widthInTiles % 2) == 0)
            {
               _flashPoint.x = 0;
            }
            else
            {
               _flashPoint.x = this.nXOffset / 2;
            }
            while(_loc9_ < _loc6_)
            {
               _flashRect = _rects[_loc10_] as Rectangle;
               this.rectOffset.x = _flashRect.x;
               this.rectOffset.y = _flashRect.y;
               this.rectOffset.width = _flashRect.width;
               this.rectOffset.height = _flashRect.height;
               if(_flashRect != null)
               {
                  if((_loc14_ = FlxHexTile(_tileObjects[_loc10_])).nExploredState <= 1)
                  {
                     this.rectOffset.y += _tileHeight * _loc14_.nExploredState;
                     param1.pixels.copyPixels(_tiles,this.rectOffset,_flashPoint,null,null,true);
                     if(_loc14_.nExploredState == 0)
                     {
                        this.ptIcon.x = _flashPoint.x + 15;
                        this.ptIcon.y = _flashPoint.y + 22;
                        if(_loc14_.m_fTotalValue > 0)
                        {
                           if(_loc14_.bVisited)
                           {
                              param1.pixels.copyPixels(this.bmpOldItems,this.bmpOldItems.rect,this.ptIcon,null,null,true);
                           }
                           else
                           {
                              param1.pixels.copyPixels(this.bmpNewItems,this.bmpNewItems.rect,this.ptIcon,null,null,true);
                           }
                        }
                        if(_loc14_.m_vScavengeItems.length > 0)
                        {
                           this.ptIcon.y += this.bmpSearch.height + 1;
                           param1.pixels.copyPixels(this.bmpSearch,this.bmpSearch.rect,this.ptIcon,null,null,true);
                        }
                        this.ptIcon.x = _flashPoint.x + 65;
                        this.ptIcon.y = _flashPoint.y + 22;
                        if(_loc14_.Scent > 0 && _loc14_.Scent > PlayState(FlxG.state).sprPlayer.m_fTrackingThreshold)
                        {
                           _loc15_ = this.aTracks[Math.min(this.aTracks.length - 1,Math.round(_loc14_.Scent))];
                           param1.pixels.copyPixels(_loc15_,_loc15_.rect,this.ptIcon,null,null,true);
                        }
                        if(_loc14_.IsCampTile)
                        {
                           this.ptIcon.x += 5;
                           this.ptIcon.y += this.bmpCamp.height - 9;
                           param1.pixels.copyPixels(this.bmpCamp,this.bmpCamp.rect,this.ptIcon,null,null,true);
                        }
                     }
                  }
                  else
                  {
                     param1.pixels.copyPixels(this.bmpHexBlank,this.bmpHexBlank.rect,_flashPoint,null,null,true);
                  }
                  if(FlxG.visualDebug && !ignoreDrawDebug)
                  {
                     if((_loc11_ = _tileObjects[_loc10_]) != null)
                     {
                        _loc12_ = _debugTileSolid;
                        param1.pixels.copyPixels(_loc12_,_debugRect,_flashPoint,null,null,true);
                     }
                  }
               }
               _flashPoint.x += this.nXOffset;
               _loc9_++;
               _loc10_++;
            }
            _loc7_ += widthInTiles;
            _flashPoint.y += this.nYOffset;
            _loc8_++;
         }
         param1.x = _loc3_ * this.nXOffset;
         param1.y = _loc4_ * this.nYOffset;
      }
      
      override public function draw() : void
      {
         var _loc1_:FlxCamera = null;
         var _loc2_:FlxHexmapBuffer = null;
         if(_flickerTimer != 0)
         {
            _flicker = !_flicker;
            if(_flicker)
            {
               return;
            }
         }
         if(cameras == null)
         {
            cameras = FlxG.cameras;
         }
         var _loc3_:uint = 0;
         var _loc4_:uint = cameras.length;
         while(_loc3_ < _loc4_)
         {
            _loc1_ = cameras[_loc3_];
            if(_buffers[_loc3_] == null)
            {
               _buffers[_loc3_] = new FlxHexmapBuffer(_tileWidth,_tileHeight,widthInTiles,heightInTiles,_loc1_,this.nXOffset,this.nYOffset);
            }
            _loc2_ = _buffers[_loc3_++] as FlxHexmapBuffer;
            if(!_loc2_.dirty)
            {
               _point.x = x - int(_loc1_.scroll.x * scrollFactor.x) + _loc2_.x;
               _point.y = y - int(_loc1_.scroll.y * scrollFactor.y) + _loc2_.y;
               _loc2_.dirty = _point.x > 0 || _point.y > 0 || _point.x + _loc2_.width < _loc1_.width || _point.y + _loc2_.height < _loc1_.height;
            }
            if(_loc2_.dirty)
            {
               this.drawHexmap(_loc2_,_loc1_);
               _loc2_.dirty = false;
            }
            _flashPoint.x = x - int(_loc1_.scroll.x * scrollFactor.x) + _loc2_.x;
            _flashPoint.y = y - int(_loc1_.scroll.y * scrollFactor.y) + _loc2_.y;
            _flashPoint.x += _flashPoint.x > 0 ? 1e-7 : -1e-7;
            _flashPoint.y += _flashPoint.y > 0 ? 1e-7 : -1e-7;
            _loc2_.draw(_loc1_,_flashPoint);
            ++_VISIBLECOUNT;
         }
      }
      
      override public function setDirty(param1:Boolean = true) : void
      {
         var _loc2_:uint = 0;
         var _loc3_:uint = _buffers.length;
         while(_loc2_ < _loc3_)
         {
            (_buffers[_loc2_++] as FlxHexmapBuffer).dirty = param1;
         }
      }
      
      override public function getTileCoords(param1:uint, param2:Boolean = true) : Array
      {
         var _loc4_:FlxPoint = null;
         var _loc3_:Array = null;
         var _loc5_:uint = 0;
         var _loc6_:uint = widthInTiles * heightInTiles;
         while(_loc5_ < _loc6_)
         {
            if(_data[_loc5_] == param1)
            {
               _loc4_ = new FlxPoint(x + uint(_loc5_ % widthInTiles) * this.nXOffset,y + uint(_loc5_ / widthInTiles) * this.nYOffset);
               if(_loc5_ / widthInTiles % 2 != 0)
               {
                  _loc4_.x += this.nXOffset;
               }
               if(param2)
               {
                  _loc4_.x += _tileWidth * 0.5;
                  _loc4_.y += _tileHeight * 0.5;
               }
               if(_loc3_ == null)
               {
                  _loc3_ = new Array();
               }
               _loc3_.push(_loc4_);
            }
            _loc5_++;
         }
         return _loc3_;
      }
      
      override protected function updateTile(param1:uint) : void
      {
         var _loc2_:FlxHexTile = _tileObjects[param1] as FlxHexTile;
         if(_loc2_ == null || !_loc2_.visible)
         {
            _rects[param1] = null;
            return;
         }
         var _loc3_:uint = (_data[param1] - _startingIndex) * _tileWidth;
         var _loc4_:uint = 0;
         if(_loc3_ >= _tiles.width)
         {
            _loc4_ = uint(_loc3_ / _tiles.width) * _tileHeight * 2;
            _loc3_ %= _tiles.width;
         }
         _rects[param1] = new Rectangle(_loc3_,_loc4_,_tileWidth,_tileHeight);
      }
      
      public function getTiles() : Array
      {
         return _tileObjects;
      }
      
      public function SwapImage(param1:int) : Boolean
      {
         if(param1 == this.nHexSheetIndex)
         {
            return false;
         }
         var _loc2_:BitmapData = DataHandler.GetImage(MapUtils.m_vHexSheets[param1]);
         _tiles = _loc2_;
         this.bmpHexBlank = DataHandler.GetImage(MapUtils.m_vHexBlanks[param1]);
         this.setDirty();
         this.nHexSheetIndex = param1;
         return true;
      }
      
      public function GetImage(param1:uint) : BitmapData
      {
         var _loc2_:uint = param1 * _tileWidth;
         var _loc3_:uint = 0;
         if(_loc2_ >= _tiles.width)
         {
            _loc3_ = uint(_loc2_ / _tiles.width) * _tileHeight * 2;
            _loc2_ %= _tiles.width;
         }
         var _loc4_:Rectangle = new Rectangle(_loc2_,_loc3_,_tileWidth,_tileHeight);
         var _loc5_:BitmapData;
         (_loc5_ = new BitmapData(_tileWidth,_tileHeight,true,0)).copyPixels(_tiles,_loc4_,new Point(),null,null,true);
         return _loc5_;
      }
      
      override public function setTileByIndex(param1:uint, param2:uint, param3:Boolean = true) : Boolean
      {
         if(param1 >= _data.length)
         {
            return false;
         }
         var _loc4_:Boolean = true;
         _data[param1] = param2;
         if(!param3)
         {
            return _loc4_;
         }
         this.setDirty();
         var _loc5_:FlxHexTile;
         (_loc5_ = _tileObjects[param1] as FlxHexTile).SetHexType(DataHandler.GetHexType(param2));
         this.updateTile(param1);
         return _loc4_;
      }
   }
}

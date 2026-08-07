package org.flixel
{
   import flash.display.BitmapData;
   import flash.display.Graphics;
   import flash.geom.Matrix;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import org.flixel.system.FlxTile;
   import org.flixel.system.FlxTilemapBuffer;
   
   public class FlxTilemap extends FlxObject
   {
      
      public static var ImgAuto:Class = FlxTilemap_ImgAuto;
      
      public static var ImgAutoAlt:Class = FlxTilemap_ImgAutoAlt;
      
      public static const OFF:uint = 0;
      
      public static const AUTO:uint = 1;
      
      public static const ALT:uint = 2;
       
      
      public var auto:uint;
      
      public var widthInTiles:uint;
      
      public var heightInTiles:uint;
      
      public var totalTiles:uint;
      
      protected var _flashPoint:Point;
      
      protected var _flashRect:Rectangle;
      
      protected var _tiles:BitmapData;
      
      protected var _buffers:Array;
      
      protected var _data:Array;
      
      protected var _rects:Array;
      
      protected var _tileWidth:uint;
      
      protected var _tileHeight:uint;
      
      protected var _tileObjects:Array;
      
      protected var _debugTileNotSolid:BitmapData;
      
      protected var _debugTilePartial:BitmapData;
      
      protected var _debugTileSolid:BitmapData;
      
      protected var _debugRect:Rectangle;
      
      protected var _lastVisualDebug:Boolean;
      
      protected var _startingIndex:uint;
      
      public function FlxTilemap()
      {
         super();
         this.auto = OFF;
         this.widthInTiles = 0;
         this.heightInTiles = 0;
         this.totalTiles = 0;
         this._buffers = new Array();
         this._flashPoint = new Point();
         this._flashRect = null;
         this._data = null;
         this._tileWidth = 0;
         this._tileHeight = 0;
         this._rects = null;
         this._tiles = null;
         this._tileObjects = null;
         immovable = true;
         cameras = null;
         this._debugTileNotSolid = null;
         this._debugTilePartial = null;
         this._debugTileSolid = null;
         this._debugRect = null;
         this._lastVisualDebug = FlxG.visualDebug;
         this._startingIndex = 0;
      }
      
      public static function arrayToCSV(param1:Array, param2:int, param3:Boolean = false) : String
      {
         var _loc5_:uint = 0;
         var _loc6_:String = null;
         var _loc8_:int = 0;
         var _loc4_:uint = 0;
         var _loc7_:int = param1.length / param2;
         while(_loc4_ < _loc7_)
         {
            _loc5_ = 0;
            while(_loc5_ < param2)
            {
               _loc8_ = int(param1[_loc4_ * param2 + _loc5_]);
               if(param3)
               {
                  if(_loc8_ == 0)
                  {
                     _loc8_ = 1;
                  }
                  else if(_loc8_ == 1)
                  {
                     _loc8_ = 0;
                  }
               }
               if(_loc5_ == 0)
               {
                  if(_loc4_ == 0)
                  {
                     _loc6_ += _loc8_;
                  }
                  else
                  {
                     _loc6_ += "\n" + _loc8_;
                  }
               }
               else
               {
                  _loc6_ += ", " + _loc8_;
               }
               _loc5_++;
            }
            _loc4_++;
         }
         return _loc6_;
      }
      
      public static function bitmapToCSV(param1:BitmapData, param2:Boolean = false, param3:uint = 1) : String
      {
         var _loc5_:uint = 0;
         var _loc6_:uint = 0;
         var _loc10_:BitmapData = null;
         var _loc11_:Matrix = null;
         if(param3 > 1)
         {
            _loc10_ = param1;
            param1 = new BitmapData(param1.width * param3,param1.height * param3);
            (_loc11_ = new Matrix()).scale(param3,param3);
            param1.draw(_loc10_,_loc11_);
         }
         var _loc4_:uint = 0;
         var _loc7_:String = "";
         var _loc8_:uint = uint(param1.width);
         var _loc9_:uint = uint(param1.height);
         while(_loc4_ < _loc9_)
         {
            _loc5_ = 0;
            while(_loc5_ < _loc8_)
            {
               _loc6_ = param1.getPixel(_loc5_,_loc4_);
               if(param2 && _loc6_ > 0 || !param2 && _loc6_ == 0)
               {
                  _loc6_ = 1;
               }
               else
               {
                  _loc6_ = 0;
               }
               if(_loc5_ == 0)
               {
                  if(_loc4_ == 0)
                  {
                     _loc7_ += _loc6_;
                  }
                  else
                  {
                     _loc7_ += "\n" + _loc6_;
                  }
               }
               else
               {
                  _loc7_ += ", " + _loc6_;
               }
               _loc5_++;
            }
            _loc4_++;
         }
         return _loc7_;
      }
      
      public static function imageToCSV(param1:Class, param2:Boolean = false, param3:uint = 1) : String
      {
         return bitmapToCSV(new param1().bitmapData,param2,param3);
      }
      
      override public function destroy() : void
      {
         this._flashPoint = null;
         this._flashRect = null;
         this._tiles = null;
         var _loc1_:uint = 0;
         var _loc2_:uint = this._tileObjects.length;
         while(_loc1_ < _loc2_)
         {
            (this._tileObjects[_loc1_++] as FlxTile).destroy();
         }
         this._tileObjects = null;
         _loc1_ = 0;
         _loc2_ = this._buffers.length;
         while(_loc1_ < _loc2_)
         {
            (this._buffers[_loc1_++] as FlxTilemapBuffer).destroy();
         }
         this._buffers = null;
         this._data = null;
         this._rects = null;
         this._debugTileNotSolid = null;
         this._debugTilePartial = null;
         this._debugTileSolid = null;
         this._debugRect = null;
         super.destroy();
      }
      
      public function loadMap(param1:String, param2:Class, param3:uint = 0, param4:uint = 0, param5:uint = 0, param6:uint = 0, param7:uint = 1, param8:uint = 1) : FlxTilemap
      {
         var _loc9_:Array = null;
         var _loc12_:uint = 0;
         var _loc13_:uint = 0;
         var _loc15_:uint = 0;
         this.auto = param5;
         this._startingIndex = param6;
         var _loc10_:Array = param1.split("\n");
         this.heightInTiles = _loc10_.length;
         this._data = new Array();
         var _loc11_:uint = 0;
         while(_loc11_ < this.heightInTiles)
         {
            if((_loc9_ = _loc10_[_loc11_++].split(",")).length <= 1)
            {
               --this.heightInTiles;
            }
            else
            {
               if(this.widthInTiles == 0)
               {
                  this.widthInTiles = _loc9_.length;
               }
               _loc12_ = 0;
               while(_loc12_ < this.widthInTiles)
               {
                  this._data.push(uint(_loc9_[_loc12_++]));
               }
            }
         }
         this.totalTiles = this.widthInTiles * this.heightInTiles;
         if(this.auto > OFF)
         {
            this._startingIndex = 1;
            param7 = 1;
            param8 = 1;
            _loc13_ = 0;
            while(_loc13_ < this.totalTiles)
            {
               this.autoTile(_loc13_++);
            }
         }
         this._tiles = FlxG.addBitmap(param2);
         this._tileWidth = param3;
         if(this._tileWidth == 0)
         {
            this._tileWidth = this._tiles.height;
         }
         this._tileHeight = param4;
         if(this._tileHeight == 0)
         {
            this._tileHeight = this._tileWidth;
         }
         _loc13_ = 0;
         var _loc14_:uint = this._tiles.width / this._tileWidth * (this._tiles.height / this._tileHeight);
         if(this.auto > OFF)
         {
            _loc14_++;
         }
         this._tileObjects = new Array(_loc14_);
         while(_loc13_ < _loc14_)
         {
            this._tileObjects[_loc13_] = new FlxTile(this,_loc13_,this._tileWidth,this._tileHeight,_loc13_ >= param7,_loc13_ >= param8 ? allowCollisions : NONE);
            _loc13_++;
         }
         this._debugTileNotSolid = this.makeDebugTile(FlxG.BLUE);
         this._debugTilePartial = this.makeDebugTile(FlxG.PINK);
         this._debugTileSolid = this.makeDebugTile(FlxG.GREEN);
         this._debugRect = new Rectangle(0,0,this._tileWidth,this._tileHeight);
         width = this.widthInTiles * this._tileWidth;
         height = this.heightInTiles * this._tileHeight;
         this._rects = new Array(this.totalTiles);
         _loc13_ = 0;
         while(_loc13_ < this.totalTiles)
         {
            this.updateTile(_loc13_++);
         }
         return this;
      }
      
      protected function makeDebugTile(param1:uint) : BitmapData
      {
         var _loc2_:BitmapData = null;
         _loc2_ = new BitmapData(this._tileWidth,this._tileHeight,true,0);
         var _loc3_:Graphics = FlxG.flashGfx;
         _loc3_.clear();
         _loc3_.moveTo(0,0);
         _loc3_.lineStyle(1,param1,0.5);
         _loc3_.lineTo(this._tileWidth - 1,0);
         _loc3_.lineTo(this._tileWidth - 1,this._tileHeight - 1);
         _loc3_.lineTo(0,this._tileHeight - 1);
         _loc3_.lineTo(0,0);
         _loc2_.draw(FlxG.flashGfxSprite);
         return _loc2_;
      }
      
      override public function update() : void
      {
         if(this._lastVisualDebug != FlxG.visualDebug)
         {
            this._lastVisualDebug = FlxG.visualDebug;
            this.setDirty();
         }
      }
      
      protected function drawTilemap(param1:FlxTilemapBuffer, param2:FlxCamera) : void
      {
         var _loc9_:uint = 0;
         var _loc10_:uint = 0;
         var _loc11_:FlxTile = null;
         var _loc12_:BitmapData = null;
         param1.fill();
         _point.x = int(param2.scroll.x * scrollFactor.x) - x;
         _point.y = int(param2.scroll.y * scrollFactor.y) - y;
         var _loc3_:int = (_point.x + (_point.x > 0 ? 1e-7 : -1e-7)) / this._tileWidth;
         var _loc4_:int = (_point.y + (_point.y > 0 ? 1e-7 : -1e-7)) / this._tileHeight;
         var _loc5_:uint = param1.rows;
         var _loc6_:uint = param1.columns;
         if(_loc3_ < 0)
         {
            _loc3_ = 0;
         }
         if(_loc3_ > this.widthInTiles - _loc6_)
         {
            _loc3_ = this.widthInTiles - _loc6_;
         }
         if(_loc4_ < 0)
         {
            _loc4_ = 0;
         }
         if(_loc4_ > this.heightInTiles - _loc5_)
         {
            _loc4_ = this.heightInTiles - _loc5_;
         }
         var _loc7_:int = _loc4_ * this.widthInTiles + _loc3_;
         this._flashPoint.y = 0;
         var _loc8_:uint = 0;
         while(_loc8_ < _loc5_)
         {
            _loc10_ = uint(_loc7_);
            _loc9_ = 0;
            this._flashPoint.x = 0;
            while(_loc9_ < _loc6_)
            {
               this._flashRect = this._rects[_loc10_] as Rectangle;
               if(this._flashRect != null)
               {
                  param1.pixels.copyPixels(this._tiles,this._flashRect,this._flashPoint,null,null,true);
                  if(FlxG.visualDebug && !ignoreDrawDebug)
                  {
                     if((_loc11_ = this._tileObjects[this._data[_loc10_]]) != null)
                     {
                        if(_loc11_.allowCollisions <= NONE)
                        {
                           _loc12_ = this._debugTileNotSolid;
                        }
                        else if(_loc11_.allowCollisions != ANY)
                        {
                           _loc12_ = this._debugTilePartial;
                        }
                        else
                        {
                           _loc12_ = this._debugTileSolid;
                        }
                        param1.pixels.copyPixels(_loc12_,this._debugRect,this._flashPoint,null,null,true);
                     }
                  }
               }
               this._flashPoint.x += this._tileWidth;
               _loc9_++;
               _loc10_++;
            }
            _loc7_ += this.widthInTiles;
            this._flashPoint.y += this._tileHeight;
            _loc8_++;
         }
         param1.x = _loc3_ * this._tileWidth;
         param1.y = _loc4_ * this._tileHeight;
      }
      
      override public function draw() : void
      {
         var _loc1_:FlxCamera = null;
         var _loc2_:FlxTilemapBuffer = null;
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
            if(this._buffers[_loc3_] == null)
            {
               this._buffers[_loc3_] = new FlxTilemapBuffer(this._tileWidth,this._tileHeight,this.widthInTiles,this.heightInTiles,_loc1_);
            }
            _loc2_ = this._buffers[_loc3_++] as FlxTilemapBuffer;
            if(!_loc2_.dirty)
            {
               _point.x = x - int(_loc1_.scroll.x * scrollFactor.x) + _loc2_.x;
               _point.y = y - int(_loc1_.scroll.y * scrollFactor.y) + _loc2_.y;
               _loc2_.dirty = _point.x > 0 || _point.y > 0 || _point.x + _loc2_.width < _loc1_.width || _point.y + _loc2_.height < _loc1_.height;
            }
            if(_loc2_.dirty)
            {
               this.drawTilemap(_loc2_,_loc1_);
               _loc2_.dirty = false;
            }
            this._flashPoint.x = x - int(_loc1_.scroll.x * scrollFactor.x) + _loc2_.x;
            this._flashPoint.y = y - int(_loc1_.scroll.y * scrollFactor.y) + _loc2_.y;
            this._flashPoint.x += this._flashPoint.x > 0 ? 1e-7 : -1e-7;
            this._flashPoint.y += this._flashPoint.y > 0 ? 1e-7 : -1e-7;
            _loc2_.draw(_loc1_,this._flashPoint);
            ++_VISIBLECOUNT;
         }
      }
      
      public function getData(param1:Boolean = false) : Array
      {
         if(!param1)
         {
            return this._data;
         }
         var _loc2_:uint = 0;
         var _loc3_:uint = this._data.length;
         var _loc4_:Array = new Array(_loc3_);
         while(_loc2_ < _loc3_)
         {
            _loc4_[_loc2_] = (this._tileObjects[this._data[_loc2_]] as FlxTile).allowCollisions > 0 ? 1 : 0;
            _loc2_++;
         }
         return _loc4_;
      }
      
      public function setDirty(param1:Boolean = true) : void
      {
         var _loc2_:uint = 0;
         var _loc3_:uint = this._buffers.length;
         while(_loc2_ < _loc3_)
         {
            (this._buffers[_loc2_++] as FlxTilemapBuffer).dirty = param1;
         }
      }
      
      public function findPath(param1:FlxPoint, param2:FlxPoint, param3:Boolean = true, param4:Boolean = false) : FlxPath
      {
         var _loc9_:FlxPoint = null;
         var _loc5_:uint = int((param1.y - y) / this._tileHeight) * this.widthInTiles + int((param1.x - x) / this._tileWidth);
         var _loc6_:uint = int((param2.y - y) / this._tileHeight) * this.widthInTiles + int((param2.x - x) / this._tileWidth);
         if((this._tileObjects[this._data[_loc5_]] as FlxTile).allowCollisions > 0 || (this._tileObjects[this._data[_loc6_]] as FlxTile).allowCollisions > 0)
         {
            return null;
         }
         var _loc7_:Array;
         if((_loc7_ = this.computePathDistance(_loc5_,_loc6_)) == null)
         {
            return null;
         }
         var _loc8_:Array = new Array();
         this.walkPath(_loc7_,_loc6_,_loc8_);
         (_loc9_ = _loc8_[_loc8_.length - 1] as FlxPoint).x = param1.x;
         _loc9_.y = param1.y;
         (_loc9_ = _loc8_[0] as FlxPoint).x = param2.x;
         _loc9_.y = param2.y;
         if(param3)
         {
            this.simplifyPath(_loc8_);
         }
         if(param4)
         {
            this.raySimplifyPath(_loc8_);
         }
         var _loc10_:FlxPath = new FlxPath();
         var _loc11_:int = int(_loc8_.length - 1);
         while(_loc11_ >= 0)
         {
            if((_loc9_ = _loc8_[_loc11_--] as FlxPoint) != null)
            {
               _loc10_.addPoint(_loc9_,true);
            }
         }
         return _loc10_;
      }
      
      protected function simplifyPath(param1:Array) : void
      {
         var _loc2_:Number = NaN;
         var _loc3_:Number = NaN;
         var _loc5_:FlxPoint = null;
         var _loc4_:FlxPoint = param1[0];
         var _loc6_:uint = 1;
         var _loc7_:uint = param1.length - 1;
         while(_loc6_ < _loc7_)
         {
            _loc2_ = ((_loc5_ = param1[_loc6_]).x - _loc4_.x) / (_loc5_.y - _loc4_.y);
            _loc3_ = (_loc5_.x - param1[_loc6_ + 1].x) / (_loc5_.y - param1[_loc6_ + 1].y);
            if(_loc4_.x == param1[_loc6_ + 1].x || _loc4_.y == param1[_loc6_ + 1].y || _loc2_ == _loc3_)
            {
               param1[_loc6_] = null;
            }
            else
            {
               _loc4_ = _loc5_;
            }
            _loc6_++;
         }
      }
      
      protected function raySimplifyPath(param1:Array) : void
      {
         var _loc4_:FlxPoint = null;
         var _loc2_:FlxPoint = param1[0];
         var _loc3_:int = -1;
         var _loc5_:uint = 1;
         var _loc6_:uint = param1.length;
         while(_loc5_ < _loc6_)
         {
            if((_loc4_ = param1[_loc5_++]) != null)
            {
               if(this.ray(_loc2_,_loc4_,_point))
               {
                  if(_loc3_ >= 0)
                  {
                     param1[_loc3_] = null;
                  }
               }
               else
               {
                  _loc2_ = param1[_loc3_];
               }
               _loc3_ = int(_loc5_ - 1);
            }
         }
      }
      
      protected function computePathDistance(param1:uint, param2:uint) : Array
      {
         var _loc8_:Array = null;
         var _loc9_:uint = 0;
         var _loc10_:* = false;
         var _loc11_:* = false;
         var _loc12_:* = false;
         var _loc13_:* = false;
         var _loc14_:uint = 0;
         var _loc16_:uint = 0;
         var _loc3_:uint = this.widthInTiles * this.heightInTiles;
         var _loc4_:Array = new Array(_loc3_);
         var _loc5_:int = 0;
         while(_loc5_ < _loc3_)
         {
            if((this._tileObjects[this._data[_loc5_]] as FlxTile).allowCollisions)
            {
               _loc4_[_loc5_] = -2;
            }
            else
            {
               _loc4_[_loc5_] = -1;
            }
            _loc5_++;
         }
         _loc4_[param1] = 0;
         var _loc6_:uint = 1;
         var _loc7_:Array = [param1];
         var _loc15_:Boolean = false;
         while(_loc7_.length > 0)
         {
            _loc8_ = _loc7_;
            _loc7_ = new Array();
            _loc5_ = 0;
            _loc14_ = _loc8_.length;
            while(_loc5_ < _loc14_)
            {
               if((_loc9_ = uint(_loc8_[_loc5_++])) == param2)
               {
                  _loc15_ = true;
                  _loc7_.length = 0;
                  break;
               }
               _loc10_ = _loc9_ % this.widthInTiles > 0;
               _loc11_ = _loc9_ % this.widthInTiles < this.widthInTiles - 1;
               _loc12_ = _loc9_ / this.widthInTiles > 0;
               _loc13_ = _loc9_ / this.widthInTiles < this.heightInTiles - 1;
               if(_loc12_)
               {
                  _loc16_ = uint(_loc9_ - this.widthInTiles);
                  if(_loc4_[_loc16_] == -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc11_)
               {
                  _loc16_ = uint(_loc9_ + 1);
                  if(_loc4_[_loc16_] == -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc13_)
               {
                  _loc16_ = _loc9_ + this.widthInTiles;
                  if(_loc4_[_loc16_] == -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc10_)
               {
                  _loc16_ = _loc9_ - 1;
                  if(_loc4_[_loc16_] == -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc12_ && _loc11_)
               {
                  _loc16_ = uint(_loc9_ - this.widthInTiles + 1);
                  if(_loc4_[_loc16_] == -1 && _loc4_[_loc9_ - this.widthInTiles] >= -1 && _loc4_[_loc9_ + 1] >= -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc11_ && _loc13_)
               {
                  _loc16_ = uint(_loc9_ + this.widthInTiles + 1);
                  if(_loc4_[_loc16_] == -1 && _loc4_[_loc9_ + this.widthInTiles] >= -1 && _loc4_[_loc9_ + 1] >= -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc10_ && _loc13_)
               {
                  _loc16_ = _loc9_ + this.widthInTiles - 1;
                  if(_loc4_[_loc16_] == -1 && _loc4_[_loc9_ + this.widthInTiles] >= -1 && _loc4_[_loc9_ - 1] >= -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
               if(_loc12_ && _loc10_)
               {
                  _loc16_ = uint(_loc9_ - this.widthInTiles - 1);
                  if(_loc4_[_loc16_] == -1 && _loc4_[_loc9_ - this.widthInTiles] >= -1 && _loc4_[_loc9_ - 1] >= -1)
                  {
                     _loc4_[_loc16_] = _loc6_;
                     _loc7_.push(_loc16_);
                  }
               }
            }
            _loc6_++;
         }
         if(!_loc15_)
         {
            _loc4_ = null;
         }
         return _loc4_;
      }
      
      protected function walkPath(param1:Array, param2:uint, param3:Array) : void
      {
         var _loc9_:uint = 0;
         param3.push(new FlxPoint(x + uint(param2 % this.widthInTiles) * this._tileWidth + this._tileWidth * 0.5,y + uint(param2 / this.widthInTiles) * this._tileHeight + this._tileHeight * 0.5));
         if(param1[param2] == 0)
         {
            return;
         }
         var _loc4_:* = param2 % this.widthInTiles > 0;
         var _loc5_:* = param2 % this.widthInTiles < this.widthInTiles - 1;
         var _loc6_:* = param2 / this.widthInTiles > 0;
         var _loc7_:* = param2 / this.widthInTiles < this.heightInTiles - 1;
         var _loc8_:uint = uint(param1[param2]);
         if(_loc6_)
         {
            _loc9_ = uint(param2 - this.widthInTiles);
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc5_)
         {
            _loc9_ = uint(param2 + 1);
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc7_)
         {
            _loc9_ = param2 + this.widthInTiles;
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc4_)
         {
            _loc9_ = param2 - 1;
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc6_ && _loc5_)
         {
            _loc9_ = uint(param2 - this.widthInTiles + 1);
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc5_ && _loc7_)
         {
            _loc9_ = uint(param2 + this.widthInTiles + 1);
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc4_ && _loc7_)
         {
            _loc9_ = param2 + this.widthInTiles - 1;
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
         if(_loc6_ && _loc4_)
         {
            _loc9_ = uint(param2 - this.widthInTiles - 1);
            if(param1[_loc9_] >= 0 && param1[_loc9_] < _loc8_)
            {
               this.walkPath(param1,_loc9_,param3);
               return;
            }
         }
      }
      
      override public function overlaps(param1:FlxBasic, param2:Boolean = false, param3:FlxCamera = null) : Boolean
      {
         var _loc4_:Boolean = false;
         var _loc5_:FlxBasic = null;
         var _loc6_:uint = 0;
         var _loc7_:Array = null;
         if(param1 is FlxGroup)
         {
            _loc4_ = false;
            _loc6_ = 0;
            _loc7_ = (param1 as FlxGroup).members;
            while(_loc6_ < length)
            {
               if((_loc5_ = _loc7_[_loc6_++] as FlxBasic) is FlxObject)
               {
                  if(this.overlapsWithCallback(_loc5_ as FlxObject))
                  {
                     _loc4_ = true;
                  }
               }
               else if(this.overlaps(_loc5_,param2,param3))
               {
                  _loc4_ = true;
               }
            }
            return _loc4_;
         }
         if(param1 is FlxObject)
         {
            return this.overlapsWithCallback(param1 as FlxObject);
         }
         return false;
      }
      
      override public function overlapsAt(param1:Number, param2:Number, param3:FlxBasic, param4:Boolean = false, param5:FlxCamera = null) : Boolean
      {
         var _loc6_:Boolean = false;
         var _loc7_:FlxBasic = null;
         var _loc8_:uint = 0;
         var _loc9_:Array = null;
         if(param3 is FlxGroup)
         {
            _loc6_ = false;
            _loc8_ = 0;
            _loc9_ = (param3 as FlxGroup).members;
            while(_loc8_ < length)
            {
               if((_loc7_ = _loc9_[_loc8_++] as FlxBasic) is FlxObject)
               {
                  _point.x = param1;
                  _point.y = param2;
                  if(this.overlapsWithCallback(_loc7_ as FlxObject,null,false,_point))
                  {
                     _loc6_ = true;
                  }
               }
               else if(this.overlapsAt(param1,param2,_loc7_,param4,param5))
               {
                  _loc6_ = true;
               }
            }
            return _loc6_;
         }
         if(param3 is FlxObject)
         {
            _point.x = param1;
            _point.y = param2;
            return this.overlapsWithCallback(param3 as FlxObject,null,false,_point);
         }
         return false;
      }
      
      public function overlapsWithCallback(param1:FlxObject, param2:Function = null, param3:Boolean = false, param4:FlxPoint = null) : Boolean
      {
         var _loc14_:uint = 0;
         var _loc15_:FlxTile = null;
         var _loc16_:Boolean = false;
         var _loc5_:Boolean = false;
         var _loc6_:Number = x;
         var _loc7_:Number = y;
         if(param4 != null)
         {
            _loc6_ = param4.x;
            _loc7_ = param4.y;
         }
         var _loc8_:int = FlxU.floor((param1.x - _loc6_) / this._tileWidth);
         var _loc9_:int = FlxU.floor((param1.y - _loc7_) / this._tileHeight);
         var _loc10_:uint = _loc8_ + FlxU.ceil(param1.width / this._tileWidth) + 1;
         var _loc11_:uint = _loc9_ + FlxU.ceil(param1.height / this._tileHeight) + 1;
         if(_loc8_ < 0)
         {
            _loc8_ = 0;
         }
         if(_loc9_ < 0)
         {
            _loc9_ = 0;
         }
         if(_loc10_ > this.widthInTiles)
         {
            _loc10_ = this.widthInTiles;
         }
         if(_loc11_ > this.heightInTiles)
         {
            _loc11_ = this.heightInTiles;
         }
         var _loc12_:uint = _loc9_ * this.widthInTiles;
         var _loc13_:uint = uint(_loc9_);
         var _loc17_:Number = _loc6_ - last.x;
         var _loc18_:Number = _loc7_ - last.y;
         while(_loc13_ < _loc11_)
         {
            _loc14_ = uint(_loc8_);
            while(_loc14_ < _loc10_)
            {
               _loc16_ = false;
               if((_loc15_ = this._tileObjects[this._data[_loc12_ + _loc14_]] as FlxTile).allowCollisions)
               {
                  _loc15_.x = _loc6_ + _loc14_ * this._tileWidth;
                  _loc15_.y = _loc7_ + _loc13_ * this._tileHeight;
                  _loc15_.last.x = _loc15_.x - _loc17_;
                  _loc15_.last.y = _loc15_.y - _loc18_;
                  if(param2 != null)
                  {
                     if(param3)
                     {
                        _loc16_ = param2(param1,_loc15_);
                     }
                     else
                     {
                        _loc16_ = param2(_loc15_,param1);
                     }
                  }
                  else
                  {
                     _loc16_ = param1.x + param1.width > _loc15_.x && param1.x < _loc15_.x + _loc15_.width && param1.y + param1.height > _loc15_.y && param1.y < _loc15_.y + _loc15_.height;
                  }
                  if(_loc16_)
                  {
                     if(_loc15_.callback != null && (_loc15_.filter == null || param1 is _loc15_.filter))
                     {
                        _loc15_.mapIndex = _loc12_ + _loc14_;
                        _loc15_.callback(_loc15_,param1);
                     }
                     _loc5_ = true;
                  }
               }
               else if(_loc15_.callback != null && (_loc15_.filter == null || param1 is _loc15_.filter))
               {
                  _loc15_.mapIndex = _loc12_ + _loc14_;
                  _loc15_.callback(_loc15_,param1);
               }
               _loc14_++;
            }
            _loc12_ += this.widthInTiles;
            _loc13_++;
         }
         return _loc5_;
      }
      
      override public function overlapsPoint(param1:FlxPoint, param2:Boolean = false, param3:FlxCamera = null) : Boolean
      {
         if(!param2)
         {
            return (this._tileObjects[this._data[uint(uint((param1.y - y) / this._tileHeight) * this.widthInTiles + (param1.x - x) / this._tileWidth)]] as FlxTile).allowCollisions > 0;
         }
         if(param3 == null)
         {
            param3 = FlxG.camera;
         }
         param1.x -= param3.scroll.x;
         param1.y -= param3.scroll.y;
         getScreenXY(_point,param3);
         return (this._tileObjects[this._data[uint(uint((param1.y - _point.y) / this._tileHeight) * this.widthInTiles + (param1.x - _point.x) / this._tileWidth)]] as FlxTile).allowCollisions > 0;
      }
      
      public function getTile(param1:uint, param2:uint) : uint
      {
         return this._data[param2 * this.widthInTiles + param1] as uint;
      }
      
      public function getTileByIndex(param1:uint) : uint
      {
         return this._data[param1] as uint;
      }
      
      public function getTileInstances(param1:uint) : Array
      {
         var _loc2_:Array = null;
         var _loc3_:uint = 0;
         var _loc4_:uint = this.widthInTiles * this.heightInTiles;
         while(_loc3_ < _loc4_)
         {
            if(this._data[_loc3_] == param1)
            {
               if(_loc2_ == null)
               {
                  _loc2_ = new Array();
               }
               _loc2_.push(_loc3_);
            }
            _loc3_++;
         }
         return _loc2_;
      }
      
      public function getTileCoords(param1:uint, param2:Boolean = true) : Array
      {
         var _loc4_:FlxPoint = null;
         var _loc3_:Array = null;
         var _loc5_:uint = 0;
         var _loc6_:uint = this.widthInTiles * this.heightInTiles;
         while(_loc5_ < _loc6_)
         {
            if(this._data[_loc5_] == param1)
            {
               _loc4_ = new FlxPoint(x + uint(_loc5_ % this.widthInTiles) * this._tileWidth,y + uint(_loc5_ / this.widthInTiles) * this._tileHeight);
               if(param2)
               {
                  _loc4_.x += this._tileWidth * 0.5;
                  _loc4_.y += this._tileHeight * 0.5;
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
      
      public function setTile(param1:uint, param2:uint, param3:uint, param4:Boolean = true) : Boolean
      {
         if(param1 >= this.widthInTiles || param2 >= this.heightInTiles)
         {
            return false;
         }
         return this.setTileByIndex(param2 * this.widthInTiles + param1,param3,param4);
      }
      
      public function setTileByIndex(param1:uint, param2:uint, param3:Boolean = true) : Boolean
      {
         var _loc5_:uint = 0;
         if(param1 >= this._data.length)
         {
            return false;
         }
         var _loc4_:Boolean = true;
         this._data[param1] = param2;
         if(!param3)
         {
            return _loc4_;
         }
         this.setDirty();
         if(this.auto == OFF)
         {
            this.updateTile(param1);
            return _loc4_;
         }
         var _loc6_:int;
         var _loc7_:int = (_loc6_ = int(param1 / this.widthInTiles) - 1) + 3;
         var _loc8_:int;
         var _loc9_:int = (_loc8_ = param1 % this.widthInTiles - 1) + 3;
         while(_loc6_ < _loc7_)
         {
            _loc8_ = _loc9_ - 3;
            while(_loc8_ < _loc9_)
            {
               if(_loc6_ >= 0 && _loc6_ < this.heightInTiles && _loc8_ >= 0 && _loc8_ < this.widthInTiles)
               {
                  _loc5_ = _loc6_ * this.widthInTiles + _loc8_;
                  this.autoTile(_loc5_);
                  this.updateTile(_loc5_);
               }
               _loc8_++;
            }
            _loc6_++;
         }
         return _loc4_;
      }
      
      public function setTileProperties(param1:uint, param2:uint = 4369, param3:Function = null, param4:Class = null, param5:uint = 1) : void
      {
         var _loc6_:FlxTile = null;
         if(param5 <= 0)
         {
            param5 = 1;
         }
         var _loc7_:uint = param1;
         var _loc8_:uint = param1 + param5;
         while(_loc7_ < _loc8_)
         {
            (_loc6_ = this._tileObjects[_loc7_++] as FlxTile).allowCollisions = param2;
            _loc6_.callback = param3;
            _loc6_.filter = param4;
         }
      }
      
      public function follow(param1:FlxCamera = null, param2:int = 0, param3:Boolean = true) : void
      {
         if(param1 == null)
         {
            param1 = FlxG.camera;
         }
         param1.setBounds(x + param2 * this._tileWidth,y + param2 * this._tileHeight,width - param2 * this._tileWidth * 2,height - param2 * this._tileHeight * 2,param3);
      }
      
      public function getBounds(param1:FlxRect = null) : FlxRect
      {
         if(param1 == null)
         {
            param1 = new FlxRect();
         }
         return param1.make(x,y,width,height);
      }
      
      public function ray(param1:FlxPoint, param2:FlxPoint, param3:FlxPoint = null, param4:Number = 1) : Boolean
      {
         var _loc14_:uint = 0;
         var _loc15_:uint = 0;
         var _loc17_:Number = NaN;
         var _loc18_:Number = NaN;
         var _loc19_:Number = NaN;
         var _loc20_:Number = NaN;
         var _loc21_:Number = NaN;
         var _loc5_:Number = this._tileWidth;
         if(this._tileHeight < this._tileWidth)
         {
            _loc5_ = this._tileHeight;
         }
         _loc5_ /= param4;
         var _loc6_:Number = param2.x - param1.x;
         var _loc7_:Number = param2.y - param1.y;
         var _loc8_:Number = Math.sqrt(_loc6_ * _loc6_ + _loc7_ * _loc7_);
         var _loc9_:uint = Math.ceil(_loc8_ / _loc5_);
         var _loc10_:Number = _loc6_ / _loc9_;
         var _loc11_:Number = _loc7_ / _loc9_;
         var _loc12_:Number = param1.x - _loc10_ - x;
         var _loc13_:Number = param1.y - _loc11_ - y;
         var _loc16_:uint = 0;
         while(_loc16_ < _loc9_)
         {
            _loc12_ += _loc10_;
            _loc13_ += _loc11_;
            if(_loc12_ < 0 || _loc12_ > width || _loc13_ < 0 || _loc13_ > height)
            {
               _loc16_++;
            }
            else
            {
               _loc14_ = _loc12_ / this._tileWidth;
               _loc15_ = _loc13_ / this._tileHeight;
               if((this._tileObjects[this._data[_loc15_ * this.widthInTiles + _loc14_]] as FlxTile).allowCollisions)
               {
                  _loc14_ *= this._tileWidth;
                  _loc15_ *= this._tileHeight;
                  _loc17_ = 0;
                  _loc18_ = 0;
                  _loc20_ = _loc12_ - _loc10_;
                  _loc21_ = _loc13_ - _loc11_;
                  _loc19_ = _loc14_;
                  if(_loc6_ < 0)
                  {
                     _loc19_ += this._tileWidth;
                  }
                  _loc17_ = _loc19_;
                  if((_loc18_ = _loc21_ + _loc11_ * ((_loc19_ - _loc20_) / _loc10_)) > _loc15_ && _loc18_ < _loc15_ + this._tileHeight)
                  {
                     if(param3 == null)
                     {
                        param3 = new FlxPoint();
                     }
                     param3.x = _loc17_;
                     param3.y = _loc18_;
                     return false;
                  }
                  _loc19_ = _loc15_;
                  if(_loc7_ < 0)
                  {
                     _loc19_ += this._tileHeight;
                  }
                  _loc17_ = _loc20_ + _loc10_ * ((_loc19_ - _loc21_) / _loc11_);
                  _loc18_ = _loc19_;
                  if(_loc17_ > _loc14_ && _loc17_ < _loc14_ + this._tileWidth)
                  {
                     if(param3 == null)
                     {
                        param3 = new FlxPoint();
                     }
                     param3.x = _loc17_;
                     param3.y = _loc18_;
                     return false;
                  }
                  return true;
               }
               _loc16_++;
            }
         }
         return true;
      }
      
      protected function autoTile(param1:uint) : void
      {
         if(this._data[param1] == 0)
         {
            return;
         }
         this._data[param1] = 0;
         if(param1 - this.widthInTiles < 0 || this._data[param1 - this.widthInTiles] > 0)
         {
            this._data[param1] += 1;
         }
         if(param1 % this.widthInTiles >= this.widthInTiles - 1 || this._data[param1 + 1] > 0)
         {
            this._data[param1] += 2;
         }
         if(param1 + this.widthInTiles >= this.totalTiles || this._data[param1 + this.widthInTiles] > 0)
         {
            this._data[param1] += 4;
         }
         if(param1 % this.widthInTiles <= 0 || this._data[param1 - 1] > 0)
         {
            this._data[param1] += 8;
         }
         if(this.auto == ALT && this._data[param1] == 15)
         {
            if(param1 % this.widthInTiles > 0 && param1 + this.widthInTiles < this.totalTiles && this._data[param1 + this.widthInTiles - 1] <= 0)
            {
               this._data[param1] = 1;
            }
            if(param1 % this.widthInTiles > 0 && param1 - this.widthInTiles >= 0 && this._data[param1 - this.widthInTiles - 1] <= 0)
            {
               this._data[param1] = 2;
            }
            if(param1 % this.widthInTiles < this.widthInTiles - 1 && param1 - this.widthInTiles >= 0 && this._data[param1 - this.widthInTiles + 1] <= 0)
            {
               this._data[param1] = 4;
            }
            if(param1 % this.widthInTiles < this.widthInTiles - 1 && param1 + this.widthInTiles < this.totalTiles && this._data[param1 + this.widthInTiles + 1] <= 0)
            {
               this._data[param1] = 8;
            }
         }
         this._data[param1] += 1;
      }
      
      protected function updateTile(param1:uint) : void
      {
         var _loc2_:FlxTile = this._tileObjects[this._data[param1]] as FlxTile;
         if(_loc2_ == null || !_loc2_.visible)
         {
            this._rects[param1] = null;
            return;
         }
         var _loc3_:uint = (this._data[param1] - this._startingIndex) * this._tileWidth;
         var _loc4_:uint = 0;
         if(_loc3_ >= this._tiles.width)
         {
            _loc4_ = uint(_loc3_ / this._tiles.width) * this._tileHeight;
            _loc3_ %= this._tiles.width;
         }
         this._rects[param1] = new Rectangle(_loc3_,_loc4_,this._tileWidth,this._tileHeight);
      }
   }
}

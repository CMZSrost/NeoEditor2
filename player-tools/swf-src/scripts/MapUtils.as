package
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.BlendMode;
   import flash.events.*;
   import flash.utils.getTimer;
   import org.flixel.*;
   
   public class MapUtils
   {
      
      private static var nXOffset:int;
      
      private static var nYOffset:int;
      
      private static var nTileWidth:int;
      
      private static var nTileHeight:int;
      
      public static var aMapKey:Array;
      
      public static var tmapHexes:FlxHexmap;
      
      public static var strFinishedBitmap:String;
      
      private static var nReadRow:uint;
      
      private static var strMapDef:String;
      
      private static var bmp:BitmapData;
      
      private static var nHeight:uint;
      
      private static var nWidth:uint;
      
      private static var bLogTerrain:Boolean;
      
      public static var m_vHexSheets:Vector.<String>;
      
      public static var m_vHexBlanks:Vector.<String>;
      
      private static var ptTemp1:FlxPoint;
      
      private static var ptTemp2:FlxPoint;
      
      protected static var m_objDisp:EventDispatcher;
       
      
      public function MapUtils()
      {
         super();
      }
      
      public static function Initialize() : void
      {
         m_objDisp = new EventDispatcher();
         bLogTerrain = false;
         nXOffset = 160;
         nYOffset = 20;
         nTileWidth = 100;
         nTileHeight = 76;
         ptTemp1 = new FlxPoint();
         ptTemp2 = new FlxPoint();
         m_vHexSheets = Vector.<String>([§_a_-_---§.§_a_--_--§(-1820302798),§_a_-_---§.§_a_--_--§(-1820302800),§_a_-_---§.§_a_--_--§(-1820302800),§_a_-_---§.§_a_--_--§(-1820302788),§_a_-_---§.§_a_--_--§(-1820302788),§_a_-_---§.§_a_--_--§(-1820302793)]);
         m_vHexBlanks = Vector.<String>(["HexBlankDawn.png","HexBlankDay.png","HexBlankDay.png","HexBlankDusk.png","HexBlankDusk.png","HexBlankNight.png"]);
         aMapKey = new Array(255,65535,1118481,65280,32512,16744192,16711680,13224393,16777215,6250335,24064,8355711,0,8355711,8355711,8355711,0,0,0,0,6250335,0,0,0,0,52377,52377,52377);
         strFinishedBitmap = "FinishedReadingBitmap";
      }
      
      public static function destroy() : void
      {
         aMapKey = null;
         tmapHexes = DataHandler.DestroyObject(tmapHexes);
         strFinishedBitmap = null;
         strMapDef = null;
         bmp = null;
         m_vHexSheets = null;
         m_vHexBlanks = null;
      }
      
      public static function GetHexDistance(param1:FlxPoint, param2:FlxPoint) : uint
      {
         ptTemp1.x = param1.x * 2 + (param1.y % 2 == 0 ? 0 : 1);
         ptTemp1.y = param1.y;
         ptTemp2.x = param2.x * 2 + (param2.y % 2 == 0 ? 0 : 1);
         ptTemp2.y = param2.y;
         var _loc3_:Number = ptTemp1.x - ptTemp2.x;
         _loc3_ = _loc3_ > 0 ? _loc3_ : -_loc3_;
         var _loc4_:Number = (_loc4_ = ptTemp1.y - ptTemp2.y) > 0 ? _loc4_ : -_loc4_;
         _loc4_ = (_loc4_ -= _loc4_ > _loc3_ ? _loc3_ : _loc4_) / 2;
         return _loc3_ + _loc4_;
      }
      
      public static function GetTileByCoords(param1:FlxPoint) : FlxHexTile
      {
         var _loc2_:uint = param1.y * tmapHexes.widthInTiles + param1.x;
         return tmapHexes.getTiles()[_loc2_];
      }
      
      public static function CanSeeHex(param1:FlxHexTile, param2:Number, param3:int) : Boolean
      {
         if(param1 == null)
         {
            return false;
         }
         var _loc4_:Number = param1.m_vLightLevels[param3];
         if(param2 <= _loc4_)
         {
            return true;
         }
         return false;
      }
      
      public static function GetHexRing(param1:FlxPoint, param2:uint) : Vector.<FlxHexTile>
      {
         if(param2 == 0)
         {
            return Vector.<FlxHexTile>([GetTileByCoords(param1)]);
         }
         var _loc3_:Vector.<FlxHexTile> = new Vector.<FlxHexTile>(6 * param2);
         var _loc4_:Array = tmapHexes.getTiles();
         var _loc5_:uint = 0;
         var _loc6_:FlxPoint = new FlxPoint();
         var _loc7_:* = param1.y % 2 == 1;
         var _loc8_:uint = 0;
         var _loc9_:uint = 0;
         while(_loc9_ < param2)
         {
            _loc6_.x = param1.x + _loc9_ / 2;
            if(_loc7_)
            {
               _loc6_.x = Math.ceil(_loc6_.x);
            }
            _loc6_.y = param1.y - 2 * param2 + _loc9_;
            if(_loc6_.x < 0 || _loc6_.x >= tmapHexes.widthInTiles || _loc6_.y < 0 || _loc6_.y >= tmapHexes.heightInTiles)
            {
               _loc3_[_loc8_] = null;
            }
            else
            {
               _loc5_ = _loc6_.y * tmapHexes.widthInTiles + _loc6_.x;
               _loc3_[_loc8_] = _loc4_[_loc5_];
            }
            _loc8_++;
            _loc9_++;
         }
         _loc9_ = 0;
         while(_loc9_ < param2)
         {
            _loc6_.x = param1.x + param2 / 2;
            if(_loc7_)
            {
               _loc6_.x = Math.ceil(_loc6_.x);
            }
            _loc6_.y = param1.y - param2 + 2 * _loc9_;
            if(_loc6_.x < 0 || _loc6_.x >= tmapHexes.widthInTiles || _loc6_.y < 0 || _loc6_.y >= tmapHexes.heightInTiles)
            {
               _loc3_[_loc8_] = null;
            }
            else
            {
               _loc5_ = _loc6_.y * tmapHexes.widthInTiles + _loc6_.x;
               _loc3_[_loc8_] = _loc4_[_loc5_];
            }
            _loc8_++;
            _loc9_++;
         }
         _loc9_ = 0;
         while(_loc9_ < param2)
         {
            _loc6_.x = param1.x + param2 / 2 - _loc9_ / 2;
            if(_loc7_)
            {
               _loc6_.x = Math.ceil(_loc6_.x);
            }
            _loc6_.y = param1.y + param2 + _loc9_;
            if(_loc6_.x < 0 || _loc6_.x >= tmapHexes.widthInTiles || _loc6_.y < 0 || _loc6_.y >= tmapHexes.heightInTiles)
            {
               _loc3_[_loc8_] = null;
            }
            else
            {
               _loc5_ = _loc6_.y * tmapHexes.widthInTiles + _loc6_.x;
               _loc3_[_loc8_] = _loc4_[_loc5_];
            }
            _loc8_++;
            _loc9_++;
         }
         _loc9_ = 0;
         while(_loc9_ < param2)
         {
            _loc6_.x = param1.x - _loc9_ / 2;
            if(_loc7_)
            {
               _loc6_.x = Math.ceil(_loc6_.x);
            }
            _loc6_.y = param1.y + 2 * param2 - _loc9_;
            if(_loc6_.x < 0 || _loc6_.x >= tmapHexes.widthInTiles || _loc6_.y < 0 || _loc6_.y >= tmapHexes.heightInTiles)
            {
               _loc3_[_loc8_] = null;
            }
            else
            {
               _loc5_ = _loc6_.y * tmapHexes.widthInTiles + _loc6_.x;
               _loc3_[_loc8_] = _loc4_[_loc5_];
            }
            _loc8_++;
            _loc9_++;
         }
         _loc9_ = 0;
         while(_loc9_ < param2)
         {
            _loc6_.x = param1.x - param2 / 2;
            if(_loc7_)
            {
               _loc6_.x = Math.ceil(_loc6_.x);
            }
            _loc6_.y = param1.y + param2 - 2 * _loc9_;
            if(_loc6_.x < 0 || _loc6_.x >= tmapHexes.widthInTiles || _loc6_.y < 0 || _loc6_.y >= tmapHexes.heightInTiles)
            {
               _loc3_[_loc8_] = null;
            }
            else
            {
               _loc5_ = _loc6_.y * tmapHexes.widthInTiles + _loc6_.x;
               _loc3_[_loc8_] = _loc4_[_loc5_];
            }
            _loc8_++;
            _loc9_++;
         }
         _loc9_ = 0;
         while(_loc9_ < param2)
         {
            _loc6_.x = param1.x - param2 / 2 + _loc9_ / 2;
            if(_loc7_)
            {
               _loc6_.x = Math.ceil(_loc6_.x);
            }
            _loc6_.y = param1.y - param2 - _loc9_;
            if(_loc6_.x < 0 || _loc6_.x >= tmapHexes.widthInTiles || _loc6_.y < 0 || _loc6_.y >= tmapHexes.heightInTiles)
            {
               _loc3_[_loc8_] = null;
            }
            else
            {
               _loc5_ = _loc6_.y * tmapHexes.widthInTiles + _loc6_.x;
               _loc3_[_loc8_] = _loc4_[_loc5_];
            }
            _loc8_++;
            _loc9_++;
         }
         return _loc3_;
      }
      
      public static function GetVisibleHexes(param1:FlxPoint, param2:uint, param3:Number, param4:Boolean) : Vector.<FlxHexTile>
      {
         var _loc7_:Vector.<FlxHexTile> = null;
         var _loc11_:FlxHexTile = null;
         var _loc12_:Vector.<uint> = null;
         var _loc13_:int = 0;
         var _loc14_:uint = 0;
         var _loc5_:Vector.<FlxHexTile> = GetHexRing(param1,1);
         var _loc6_:Vector.<uint> = new <uint>[2,2,2,2,2,2];
         var _loc8_:int = PlayState.m_objInstance.nTimeOfDay;
         var _loc9_:Vector.<FlxHexTile> = new <FlxHexTile>[GetTileByCoords(param1)];
         var _loc10_:uint = 0;
         while(_loc10_ < 6)
         {
            if(CanSeeHex(_loc5_[_loc10_],param3,_loc8_))
            {
               _loc6_[_loc10_] = 0;
               _loc9_.push(_loc5_[_loc10_]);
            }
            _loc10_++;
         }
         _loc10_ = 2;
         while(_loc10_ <= param2)
         {
            _loc7_ = GetHexRing(param1,_loc10_);
            _loc12_ = new Vector.<uint>();
            _loc13_ = 0;
            while(_loc13_ < _loc5_.length)
            {
               _loc14_ = _loc13_ * _loc10_ / (_loc10_ - 1);
               if(_loc13_ % (_loc10_ - 1) == _loc10_ - 2)
               {
                  if(_loc5_[_loc13_] != null && _loc5_[_loc13_].nVizLimiter == 0 && _loc6_[_loc13_] == 0)
                  {
                     if(CanSeeHex(_loc7_[_loc14_],param3,_loc8_))
                     {
                        _loc9_.push(_loc7_[_loc14_]);
                        _loc12_.push(0);
                     }
                     else
                     {
                        _loc12_.push(2);
                     }
                     if(CanSeeHex(_loc7_[_loc14_ + 1],param3,_loc8_))
                     {
                        _loc9_.push(_loc7_[_loc14_ + 1]);
                        _loc12_.push(0);
                     }
                     else
                     {
                        _loc12_.push(2);
                     }
                  }
                  else
                  {
                     _loc12_.push(2);
                     _loc12_.push(2);
                  }
               }
               else if(_loc5_[_loc13_] != null && _loc5_[_loc13_].nVizLimiter == 0 && _loc6_[_loc13_] == 0)
               {
                  if(CanSeeHex(_loc7_[_loc14_],param3,_loc8_))
                  {
                     _loc9_.push(_loc7_[_loc14_]);
                     _loc12_.push(0);
                  }
                  else
                  {
                     _loc12_.push(2);
                  }
               }
               else
               {
                  _loc12_.push(2);
               }
               _loc13_++;
            }
            _loc5_ = _loc7_.concat();
            _loc6_ = _loc12_.concat();
            _loc10_++;
         }
         for each(_loc11_ in _loc9_)
         {
            if(_loc11_ != null)
            {
               _loc11_.UpdateScavengeItems(PlayState.m_objInstance.objDate,param4);
            }
         }
         return _loc9_;
      }
      
      public static function GetHexUnderPoint(param1:FlxPoint) : FlxHexTile
      {
         var _loc6_:FlxHexTile = null;
         var _loc8_:FlxHexTile = null;
         var _loc9_:Number = NaN;
         var _loc2_:int = 0;
         var _loc3_:int = int(tmapHexes.widthInTiles);
         var _loc4_:Array = tmapHexes.getTiles();
         var _loc5_:Number = -1;
         var _loc7_:int = 0;
         while(_loc7_ < tmapHexes.totalTiles)
         {
            _loc8_ = _loc4_[_loc7_];
            if(param1.y - _loc8_.y > _loc8_.height || _loc8_.x - param1.x > _loc8_.width)
            {
               _loc7_ += tmapHexes.widthInTiles - _loc7_ % tmapHexes.widthInTiles - 1;
            }
            else if(_loc8_.y - param1.y > 0)
            {
               break;
            }
            if(_loc8_.overlapsPoint(param1))
            {
               ptTemp1 = _loc8_.getMidpoint(ptTemp1);
               _loc9_ = FlxU.abs(FlxU.getDistance(param1,ptTemp1));
               if(_loc5_ < 0)
               {
                  _loc5_ = _loc9_;
                  _loc6_ = _loc8_;
               }
               else if(_loc9_ < _loc5_)
               {
                  _loc5_ = _loc9_;
                  _loc6_ = _loc8_;
               }
            }
            _loc7_++;
         }
         return _loc6_;
      }
      
      public static function SetHex(param1:FlxHexTile, param2:int) : void
      {
         var _loc3_:FlxPoint = param1.GetHexCoords();
         tmapHexes.setTile(_loc3_.x,_loc3_.y,param2);
      }
      
      public static function LoadMapDef(param1:String) : void
      {
         tmapHexes = new FlxHexmap();
         tmapHexes.loadHexMap(DataHandler.GetMap(param1),DataHandler.GetImage(m_vHexSheets[0]),nTileWidth,nTileHeight,FlxTilemap.OFF,0,0,0,nXOffset,nYOffset);
         dispatchEvent(new Event(strFinishedBitmap));
      }
      
      public static function LoadTerrainBitmap(param1:String) : void
      {
         if(bLogTerrain)
         {
            FlxG.log("LoadTerrainBitmap: Starting...");
         }
         nReadRow = 0;
         strMapDef = "";
         tmapHexes = new FlxHexmap();
         bmp = DataHandler.GetImage(param1);
         nHeight = bmp.height;
         nWidth = bmp.width;
         if(bLogTerrain)
         {
            FlxG.log("LoadTerrainBitmap: Finished: " + getTimer());
         }
      }
      
      public static function ReadTerrainBitmapRow() : void
      {
         var _loc3_:uint = 0;
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         if(bLogTerrain)
         {
            FlxG.log("ReadTerrainBitmapRow: " + nReadRow + ": " + getTimer());
         }
         var _loc1_:Number = 0;
         var _loc2_:uint = 0;
         while(_loc2_ < nWidth)
         {
            if(_loc2_ > 0)
            {
               strMapDef += ",";
            }
            _loc3_ = bmp.getPixel(_loc2_,nReadRow);
            if((_loc4_ = int(aMapKey.indexOf(_loc3_))) < 0)
            {
               _loc4_ = 2;
            }
            else if(_loc4_ == 11)
            {
               _loc1_ = Math.random();
               if(_loc1_ < 0.5)
               {
                  _loc4_ = 15;
               }
            }
            strMapDef += _loc4_.toString();
            _loc2_++;
         }
         strMapDef += "\n";
         ++nReadRow;
         if(nReadRow >= nHeight)
         {
            _loc5_ = getTimer();
            tmapHexes.loadHexMap(strMapDef,DataHandler.GetImage(m_vHexSheets[0]),nTileWidth,nTileHeight,FlxTilemap.OFF,0,0,0,nXOffset,nYOffset);
            FlxG.log("ReadTerrainBitmapRow: Finished: " + (getTimer() - _loc5_) + " ms.");
            dispatchEvent(new Event(strFinishedBitmap));
         }
      }
      
      public static function RandomizeTerrain(param1:String) : void
      {
         var _loc10_:uint = 0;
         var _loc11_:uint = 0;
         var _loc2_:Vector.<uint> = new <uint>[3,4,5,7,9,10,11,15,25,26,27];
         _loc2_.fixed = true;
         var _loc3_:Vector.<uint> = new <uint>[58,17,15,1,1,2,1,2,1,1,1];
         _loc3_.fixed = true;
         var _loc4_:Vector.<uint> = new Vector.<uint>(100,true);
         var _loc5_:uint = 0;
         var _loc6_:uint = 0;
         while(_loc6_ < _loc2_.length)
         {
            _loc10_ = 0;
            while(_loc10_ < _loc3_[_loc6_])
            {
               _loc4_[_loc5_] = _loc2_[_loc6_];
               _loc5_++;
               _loc10_++;
            }
            _loc6_++;
         }
         var _loc7_:*;
         var _loc8_:Array = (_loc7_ = DataHandler.GetMap(param1)).split("\n");
         _loc7_ = "";
         var _loc9_:uint = 0;
         while(_loc9_ < _loc8_.length)
         {
            if(_loc9_ > 0)
            {
               _loc7_ += "\n";
            }
            _loc8_[_loc9_] = String(_loc8_[_loc9_]).split(",");
            _loc11_ = 0;
            while(_loc11_ < _loc8_[_loc9_].length)
            {
               if(_loc11_ > 0)
               {
                  _loc7_ += ",";
               }
               if(_loc8_[_loc9_][_loc11_] == 3)
               {
                  _loc5_ = uint(int(Math.random() * 100));
                  _loc7_ += _loc4_[_loc5_];
               }
               else
               {
                  _loc7_ += _loc8_[_loc9_][_loc11_];
               }
               _loc11_++;
            }
            _loc9_++;
         }
         tmapHexes = new FlxHexmap();
         tmapHexes.loadHexMap(_loc7_,DataHandler.GetImage(m_vHexSheets[0]),nTileWidth,nTileHeight,FlxTilemap.OFF,0,0,0,nXOffset,nYOffset);
         dispatchEvent(new Event(strFinishedBitmap));
      }
      
      public static function GenerateTerrain(param1:uint, param2:uint) : void
      {
         var _loc10_:uint = 0;
         var _loc11_:uint = 0;
         var _loc3_:Vector.<uint> = new <uint>[3,4,5,7,9,10,11,15,25,26,27];
         _loc3_.fixed = true;
         var _loc4_:Vector.<uint>;
         (_loc4_ = new <uint>[58,17,15,1,1,2,1,2,1,1,1]).fixed = true;
         var _loc5_:Vector.<uint> = new Vector.<uint>(100,true);
         var _loc6_:uint = 0;
         var _loc7_:uint = 0;
         while(_loc7_ < _loc3_.length)
         {
            _loc10_ = 0;
            while(_loc10_ < _loc4_[_loc7_])
            {
               _loc5_[_loc6_] = _loc3_[_loc7_];
               _loc6_++;
               _loc10_++;
            }
            _loc7_++;
         }
         var _loc8_:* = "";
         var _loc9_:uint = 0;
         while(_loc9_ < param2)
         {
            if(_loc9_ > 0)
            {
               _loc8_ += "\n";
            }
            _loc11_ = 0;
            while(_loc11_ < param1)
            {
               if(_loc11_ > 0)
               {
                  _loc8_ += ",";
               }
               _loc6_ = uint(int(Math.random() * 100));
               _loc8_ += _loc5_[_loc6_];
               _loc11_++;
            }
            _loc9_++;
         }
         tmapHexes = new FlxHexmap();
         tmapHexes.loadHexMap(_loc8_,DataHandler.GetImage(m_vHexSheets[0]),nTileWidth,nTileHeight,FlxTilemap.OFF,0,0,0,nXOffset,nYOffset);
         dispatchEvent(new Event(strFinishedBitmap));
      }
      
      public static function GenerateTerrainBMP(param1:uint, param2:uint) : FlxHexmap
      {
         var _loc8_:uint = 0;
         var _loc11_:Array = null;
         var _loc12_:uint = 0;
         var _loc13_:Number = NaN;
         var _loc14_:Number = NaN;
         if(bLogTerrain)
         {
            FlxG.log("GenerateTerrain: Starting...");
         }
         var _loc3_:* = "";
         var _loc4_:FlxHexmap = new FlxHexmap();
         if(bLogTerrain)
         {
            FlxG.log("GenerateTerrain: creating bitmaps: " + getTimer());
         }
         var _loc5_:BitmapData = new BitmapData(param1,param2,false,8421504);
         var _loc6_:BitmapData = new BitmapData(param1,param2,false,8421504);
         var _loc7_:Bitmap;
         (_loc7_ = new Bitmap(_loc5_)).width = param1 * 4;
         _loc7_.height = param2 * 4;
         _loc7_.x = 5;
         _loc7_.y = 100;
         if(bLogTerrain)
         {
            FlxG.log("GenerateTerrain: populating bitmaps: " + getTimer());
         }
         var _loc9_:uint = 0;
         while(_loc9_ < 30)
         {
            _loc8_ = Math.random() * 1000;
            _loc6_.perlinNoise(param1,param2,3,_loc8_,true,true,7,true);
            _loc5_.draw(_loc6_,null,null,BlendMode.DIFFERENCE);
            _loc9_++;
         }
         if(bLogTerrain)
         {
            FlxG.log("GenerateTerrain: getting pixel heights: " + getTimer());
         }
         var _loc10_:uint = 0;
         while(_loc10_ < param2)
         {
            _loc11_ = new Array();
            _loc12_ = 0;
            while(_loc12_ < param1)
            {
               if(_loc12_ > 0)
               {
                  _loc3_ += ",";
               }
               if((_loc14_ = (_loc13_ = _loc5_.getPixel(_loc12_,_loc10_)) / 16777216 * 10) > 6)
               {
                  _loc14_ = 6;
               }
               _loc3_ += int(_loc14_).toString();
               _loc12_++;
            }
            _loc3_ += "\n";
            _loc10_++;
         }
         _loc4_.loadHexMap(_loc3_,DataHandler.GetImage(m_vHexSheets[0]),nTileWidth,nTileHeight,FlxTilemap.OFF,0,0,0,nXOffset,nYOffset);
         if(bLogTerrain)
         {
            FlxG.log("GenerateTerrain: Finished: " + getTimer());
         }
         return _loc4_;
      }
      
      public static function WriteTerrain(param1:Array) : void
      {
         var _loc3_:FlxHexTile = null;
         var _loc4_:uint = 0;
         var _loc5_:Item = null;
         var _loc2_:* = "";
         for each(_loc3_ in param1)
         {
            _loc2_ += _loc3_.index + "-";
            _loc2_ += _loc3_.nExploredState + "-";
            _loc2_ += int(_loc3_.bScavenged) + "-";
            _loc2_ += _loc3_.Scent + "-";
            _loc4_ = 0;
            while(_loc4_ < _loc3_.GroundObject.vItems.length)
            {
               _loc5_ = _loc3_.GroundObject.vItems[_loc4_].ItemDefinition;
               _loc2_ += _loc5_.nGroupID + "." + _loc5_.nSubgroupID;
               if(_loc4_ < _loc3_.GroundObject.vItems.length - 1)
               {
                  _loc2_ += ",";
               }
               _loc4_++;
            }
            _loc2_ += "=";
         }
         _loc2_ = _loc2_;
      }
      
      public static function addEventListener(param1:String, param2:Function) : void
      {
         m_objDisp.addEventListener(param1,param2);
      }
      
      public static function removeEventListener(param1:String, param2:Function) : void
      {
         m_objDisp.removeEventListener(param1,param2);
      }
      
      public static function dispatchEvent(param1:Event) : void
      {
         m_objDisp.dispatchEvent(param1);
      }
   }
}

package org.flixel.system.input
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.Sprite;
   import flash.events.MouseEvent;
   import flash.geom.Point;
   import flash.ui.Mouse;
   import flash.ui.MouseCursorData;
   import org.flixel.FlxCamera;
   import org.flixel.FlxG;
   import org.flixel.FlxPoint;
   import org.flixel.system.replay.MouseRecord;
   
   public class Mouse extends FlxPoint
   {
       
      
      protected var ImgDefaultCursor:Class;
      
      public var wheel:int;
      
      public var screenX:int;
      
      public var screenY:int;
      
      protected var _current:int;
      
      protected var _last:int;
      
      protected var _currentRight:int;
      
      protected var _lastRight:int;
      
      protected var _currentDouble:int;
      
      protected var _lastDouble:int;
      
      protected var _cursorContainer:Sprite;
      
      protected var _cursor:Bitmap;
      
      protected var _lastX:int;
      
      protected var _lastY:int;
      
      protected var _lastWheel:int;
      
      protected var _point:FlxPoint;
      
      protected var _globalScreenPosition:FlxPoint;
      
      public function Mouse(param1:Sprite)
      {
         var _loc2_:Bitmap = null;
         var _loc3_:MouseCursorData = null;
         this.ImgDefaultCursor = Mouse_ImgDefaultCursor;
         super();
         this._cursorContainer = param1;
         this._lastX = this.screenX = 0;
         this._lastY = this.screenY = 0;
         this._lastWheel = this.wheel = 0;
         this._current = 0;
         this._last = 0;
         this._currentRight = 0;
         this._lastRight = 0;
         this._currentDouble = 0;
         this._lastDouble = 0;
         this._cursor = null;
         this._point = new FlxPoint();
         this._globalScreenPosition = new FlxPoint();
         if(flash.ui.Mouse.supportsNativeCursor)
         {
            _loc2_ = new this.ImgDefaultCursor();
            _loc3_ = new MouseCursorData();
            _loc3_.hotSpot = new Point(0,0);
            _loc3_.data = Vector.<BitmapData>([_loc2_.bitmapData]);
            flash.ui.Mouse.registerCursor("Default",_loc3_);
            flash.ui.Mouse.cursor = "Default";
         }
      }
      
      public function destroy() : void
      {
         this._cursorContainer = null;
         this._cursor = null;
         this._point = null;
         this._globalScreenPosition = null;
      }
      
      public function show(param1:Class = null, param2:Number = 1, param3:int = 0, param4:int = 0) : void
      {
         this._cursorContainer.visible = true;
         if(param1 != null)
         {
            this.load(param1,param2,param3,param4);
         }
         else if(this._cursor == null)
         {
            this.load();
         }
      }
      
      public function hide() : void
      {
         this._cursorContainer.visible = false;
      }
      
      public function get visible() : Boolean
      {
         return this._cursorContainer.visible;
      }
      
      public function load(param1:Class = null, param2:Number = 1, param3:int = 0, param4:int = 0) : void
      {
         var _loc5_:MouseCursorData = null;
         if(this._cursor != null)
         {
            this._cursorContainer.removeChild(this._cursor);
         }
         if(param1 == null)
         {
            param1 = this.ImgDefaultCursor;
         }
         if(flash.ui.Mouse.supportsNativeCursor)
         {
            (_loc5_ = new MouseCursorData()).hotSpot = new Point(param3,param4);
            _loc5_.data = Vector.<BitmapData>([param1.bitmapData]);
            flash.ui.Mouse.registerCursor("Default",_loc5_);
            flash.ui.Mouse.cursor = "Default";
         }
         else
         {
            this._cursor = new param1();
            this._cursor.x = param3;
            this._cursor.y = param4;
            this._cursor.scaleX = param2;
            this._cursor.scaleY = param2;
            this._cursorContainer.addChild(this._cursor);
         }
      }
      
      public function loadBitmap(param1:Bitmap = null, param2:Number = 1, param3:int = 0, param4:int = 0) : void
      {
         var _loc5_:MouseCursorData = null;
         if(this._cursor != null)
         {
            this._cursorContainer.removeChild(this._cursor);
         }
         if(param1 == null)
         {
            param1 = new this.ImgDefaultCursor();
         }
         if(flash.ui.Mouse.supportsNativeCursor)
         {
            (_loc5_ = new MouseCursorData()).hotSpot = new Point(param3,param4);
            _loc5_.data = Vector.<BitmapData>([param1.bitmapData]);
            flash.ui.Mouse.registerCursor("Default",_loc5_);
            flash.ui.Mouse.cursor = "Default";
         }
         else
         {
            this._cursor = param1;
            this._cursor.x = param3;
            this._cursor.y = param4;
            this._cursor.scaleX = param2;
            this._cursor.scaleY = param2;
            this._cursorContainer.addChild(this._cursor);
         }
      }
      
      public function unload() : void
      {
         if(this._cursor != null)
         {
            if(this._cursorContainer.visible)
            {
               this.load();
            }
            else
            {
               this._cursorContainer.removeChild(this._cursor);
               this._cursor = null;
            }
         }
      }
      
      public function update(param1:int, param2:int) : void
      {
         this._globalScreenPosition.x = param1;
         this._globalScreenPosition.y = param2;
         this.updateCursor();
         if(this._last == -1 && this._current == -1)
         {
            this._current = 0;
         }
         else if(this._last == 2 && this._current == 2)
         {
            this._current = 1;
         }
         this._last = this._current;
         if(this._lastRight == -1 && this._currentRight == -1)
         {
            this._currentRight = 0;
         }
         else if(this._lastRight == 2 && this._currentRight == 2)
         {
            this._currentRight = 1;
         }
         this._lastRight = this._currentRight;
         if(this._lastDouble == 1 && this._currentDouble == 1)
         {
            this._currentDouble = 0;
         }
         this._lastDouble = this._currentDouble;
      }
      
      protected function updateCursor() : void
      {
         this._cursorContainer.x = this._globalScreenPosition.x;
         this._cursorContainer.y = this._globalScreenPosition.y;
         var _loc1_:FlxCamera = FlxG.camera;
         this.screenX = (this._globalScreenPosition.x - _loc1_.x) / _loc1_.zoom;
         this.screenY = (this._globalScreenPosition.y - _loc1_.y) / _loc1_.zoom;
         x = this.screenX + _loc1_.scroll.x;
         y = this.screenY + _loc1_.scroll.y;
      }
      
      public function getWorldPosition(param1:FlxCamera = null, param2:FlxPoint = null) : FlxPoint
      {
         if(param1 == null)
         {
            param1 = FlxG.camera;
         }
         if(param2 == null)
         {
            param2 = new FlxPoint();
         }
         this.getScreenPosition(param1,this._point);
         param2.x = this._point.x + param1.scroll.x;
         param2.y = this._point.y + param1.scroll.y;
         return param2;
      }
      
      public function getScreenPosition(param1:FlxCamera = null, param2:FlxPoint = null) : FlxPoint
      {
         if(param1 == null)
         {
            param1 = FlxG.camera;
         }
         if(param2 == null)
         {
            param2 = new FlxPoint();
         }
         param2.x = (this._globalScreenPosition.x - param1.x) / param1.zoom;
         param2.y = (this._globalScreenPosition.y - param1.y) / param1.zoom;
         return param2;
      }
      
      public function reset() : void
      {
         this._current = 0;
         this._last = 0;
         this._currentRight = 0;
         this._lastRight = 0;
         this._currentDouble = 0;
         this._lastDouble = 0;
      }
      
      public function pressed() : Boolean
      {
         return this._current > 0;
      }
      
      public function justPressed() : Boolean
      {
         return this._current == 2;
      }
      
      public function justReleased() : Boolean
      {
         return this._current == -1;
      }
      
      public function pressedRight() : Boolean
      {
         return this._currentRight > 0;
      }
      
      public function justPressedRight() : Boolean
      {
         return this._currentRight == 2;
      }
      
      public function justReleasedRight() : Boolean
      {
         return this._currentRight == -1;
      }
      
      public function clickedDouble() : Boolean
      {
         return this._currentDouble > 0;
      }
      
      public function handleMouseDown(param1:MouseEvent) : void
      {
         param1.target.doubleClickEnabled = true;
         if(this._current > 0)
         {
            this._current = 1;
         }
         else
         {
            this._current = 2;
         }
      }
      
      public function handleMouseUp(param1:MouseEvent) : void
      {
         if(this._current > 0)
         {
            this._current = -1;
         }
         else
         {
            this._current = 0;
         }
      }
      
      public function handleRightMouseDown(param1:MouseEvent) : void
      {
         param1.target.doubleClickEnabled = true;
         if(this._currentRight > 0)
         {
            this._currentRight = 1;
         }
         else
         {
            this._currentRight = 2;
         }
      }
      
      public function handleRightMouseUp(param1:MouseEvent) : void
      {
         if(this._currentRight > 0)
         {
            this._currentRight = -1;
         }
         else
         {
            this._currentRight = 0;
         }
      }
      
      public function handleDoubleClick(param1:MouseEvent) : void
      {
         this._currentDouble = 1;
      }
      
      public function handleMouseWheel(param1:MouseEvent) : void
      {
         this.wheel = param1.delta;
      }
      
      public function record() : MouseRecord
      {
         if(this._lastX == this._globalScreenPosition.x && this._lastY == this._globalScreenPosition.y && this._current == 0 && this._lastWheel == this.wheel)
         {
            return null;
         }
         this._lastX = this._globalScreenPosition.x;
         this._lastY = this._globalScreenPosition.y;
         this._lastWheel = this.wheel;
         return new MouseRecord(this._lastX,this._lastY,this._current,this._lastWheel);
      }
      
      public function playback(param1:MouseRecord) : void
      {
         this._current = param1.button;
         this.wheel = param1.wheel;
         this._globalScreenPosition.x = param1.x;
         this._globalScreenPosition.y = param1.y;
         this.updateCursor();
      }
   }
}

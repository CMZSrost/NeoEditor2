package
{
   import flash.display.BitmapData;
   import org.flixel.*;
   
   public class GUIMenuButton extends FlxButton
   {
       
      
      private var bmpImgOn:BitmapData;
      
      private var bmpImgDown:BitmapData;
      
      private var bmpImgOut:BitmapData;
      
      private var bmpImgOver:BitmapData;
      
      private var fnOnMouseUp:Function;
      
      private var ptMouse:FlxPoint;
      
      public var nMask:uint;
      
      public var strPopUpText:String;
      
      public function GUIMenuButton(param1:BitmapData, param2:BitmapData, param3:BitmapData, param4:BitmapData, param5:Number = 0, param6:Number = 0, param7:Function = null)
      {
         super(param5,param6,null);
         this.bmpImgOn = param4;
         this.bmpImgOut = param2;
         this.bmpImgOver = param3;
         this.bmpImgDown = param1;
         this.ptMouse = new FlxPoint();
         this.nMask = 255;
         this.strPopUpText = "";
         pixels = param2;
         onOver = this.OnOver;
         onOut = this.OnOut;
         onDown = this.OnDown;
         onUp = this.OnUp;
         this.fnOnMouseUp = param7;
      }
      
      override public function destroy() : void
      {
         this.bmpImgOn = null;
         this.bmpImgOut = null;
         this.bmpImgOver = null;
         this.bmpImgDown = null;
         this.fnOnMouseUp = null;
         this.ptMouse = null;
         this.strPopUpText = null;
         super.destroy();
      }
      
      override protected function updateButton() : void
      {
         var _loc1_:FlxCamera = null;
         var _loc2_:uint = 0;
         var _loc3_:uint = 0;
         var _loc4_:Boolean = false;
         if(FlxG.mouse.visible && active)
         {
            if(cameras == null)
            {
               cameras = FlxG.cameras;
            }
            _loc2_ = 0;
            _loc3_ = cameras.length;
            _loc4_ = true;
            while(_loc2_ < _loc3_)
            {
               _loc1_ = cameras[_loc2_++] as FlxCamera;
               FlxG.mouse.getWorldPosition(_loc1_,this.ptMouse);
               if(pixelsOverlapPoint(this.ptMouse,this.nMask,_loc1_))
               {
                  _loc4_ = false;
                  if(FlxG.mouse.justPressed())
                  {
                     status = PRESSED;
                     if(onDown != null)
                     {
                        onDown();
                     }
                     if(soundDown != null)
                     {
                        soundDown.play(true);
                     }
                  }
                  if(status == NORMAL)
                  {
                     status = HIGHLIGHT;
                     if(onOver != null)
                     {
                        onOver();
                     }
                     if(soundOver != null)
                     {
                        soundOver.play(true);
                     }
                  }
               }
            }
            if(_loc4_)
            {
               if(status != NORMAL)
               {
                  if(onOut != null)
                  {
                     onOut();
                  }
                  if(soundOut != null)
                  {
                     soundOut.play(true);
                  }
               }
               status = NORMAL;
            }
         }
         if(label != null)
         {
            label.x = x;
            label.y = y;
         }
         if(labelOffset != null)
         {
            label.x += labelOffset.x;
            label.y += labelOffset.y;
         }
         if(status == HIGHLIGHT && _onToggle)
         {
            frame = NORMAL;
         }
         else
         {
            frame = status;
         }
      }
      
      override public function set on(param1:Boolean) : void
      {
         super.on = param1;
         this.UpdateImage();
      }
      
      public function UpdateImage() : void
      {
         if(on)
         {
            pixels = this.bmpImgOn;
         }
         else
         {
            pixels = this.bmpImgOut;
         }
      }
      
      private function OnOver() : void
      {
      }
      
      public function OnUp() : void
      {
         if(this.fnOnMouseUp != null)
         {
            this.fnOnMouseUp(this);
         }
      }
      
      private function OnOut() : void
      {
      }
      
      private function OnDown() : void
      {
      }
   }
}

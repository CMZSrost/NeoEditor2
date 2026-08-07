package
{
   import flash.display.BitmapData;
   import org.flixel.*;
   
   public class ImgButton extends FlxButton
   {
       
      
      public var bmpImgOn:BitmapData;
      
      public var bmpImgDown:BitmapData;
      
      public var bmpImgOut:BitmapData;
      
      public var bmpImgOver:BitmapData;
      
      public var strImgOn:String;
      
      public var strImgDown:String;
      
      public var strImgOut:String;
      
      public var strImgOver:String;
      
      private var fnOnClick:Function;
      
      private var ptMouse:FlxPoint;
      
      public var nMask:uint;
      
      public var bMouseOver:Boolean;
      
      private var m_bMask:Boolean;
      
      public var m_strPopUpText:String;
      
      public var nZSort:int;
      
      private var m_bCallbackRef:Boolean;
      
      public function ImgButton(param1:String, param2:String, param3:String, param4:String, param5:Number = 0, param6:Number = 0, param7:Function = null, param8:Boolean = false, param9:Boolean = false)
      {
         super(param5,param6,null);
         this.bmpImgOn = DataHandler.GetImage(param4);
         this.bmpImgOut = DataHandler.GetImage(param2);
         this.bmpImgOver = DataHandler.GetImage(param3);
         this.bmpImgDown = DataHandler.GetImage(param1);
         this.strImgOn = param4;
         this.strImgOut = param2;
         this.strImgOver = param3;
         this.strImgDown = param1;
         this.ptMouse = new FlxPoint();
         this.nMask = 255;
         this.bMouseOver = false;
         this.m_bMask = param9;
         this.nZSort = 0;
         pixels = this.bmpImgOut;
         onOver = this.OnOver;
         onOut = this.OnOut;
         onDown = this.OnDown;
         onUp = this.OnUp;
         this.fnOnClick = param7;
         this.m_bCallbackRef = param8;
      }
      
      override public function destroy() : void
      {
         this.bmpImgOn = null;
         this.bmpImgOut = null;
         this.bmpImgOver = null;
         this.bmpImgDown = null;
         this.fnOnClick = null;
         this.strImgOn = null;
         this.strImgDown = null;
         this.strImgOut = null;
         this.strImgOver = null;
         this.ptMouse = null;
         this.m_strPopUpText = null;
         super.destroy();
      }
      
      override protected function updateButton() : void
      {
         var _loc1_:FlxCamera = null;
         var _loc2_:uint = 0;
         var _loc3_:uint = 0;
         var _loc4_:Boolean = false;
         var _loc5_:Boolean = false;
         this.bMouseOver = false;
         if(FlxG.mouse.visible && active)
         {
            if(cameras == null)
            {
               cameras = FlxG.cameras;
            }
            _loc2_ = 0;
            _loc3_ = cameras.length;
            _loc4_ = true;
            _loc5_ = false;
            while(_loc2_ < _loc3_)
            {
               if(alive != false)
               {
                  _loc1_ = cameras[_loc2_++] as FlxCamera;
                  FlxG.mouse.getWorldPosition(_loc1_,this.ptMouse);
                  if(this.m_bMask)
                  {
                     _loc5_ = pixelsOverlapPoint(this.ptMouse,this.nMask,_loc1_);
                  }
                  else
                  {
                     _loc5_ = overlapsPoint(this.ptMouse,true,_loc1_);
                  }
                  if(_loc5_)
                  {
                     this.bMouseOver = true;
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
         pixels = this.bmpImgOver;
      }
      
      public function OnUp() : void
      {
         this.on = !on;
         this.UpdateImage();
         if(this.fnOnClick != null)
         {
            if(this.m_bCallbackRef)
            {
               this.fnOnClick(this);
            }
            else
            {
               this.fnOnClick();
            }
         }
      }
      
      private function OnOut() : void
      {
         this.UpdateImage();
      }
      
      private function OnDown() : void
      {
         pixels = this.bmpImgDown;
      }
      
      public function SetFunction(param1:Function, param2:Boolean) : void
      {
         this.fnOnClick = param1;
         this.m_bCallbackRef = param2;
      }
      
      override public function Zoom(param1:Number) : void
      {
         if(param1 == fZoom)
         {
            return;
         }
         var _loc2_:String = "";
         if(param1 == 2)
         {
            _loc2_ = DataHandler.m_strZoomPrefix;
         }
         this.bmpImgDown = DataHandler.GetImage(this.strImgDown,_loc2_);
         this.bmpImgOn = DataHandler.GetImage(this.strImgOn,_loc2_);
         this.bmpImgOut = DataHandler.GetImage(this.strImgOut,_loc2_);
         this.bmpImgOver = DataHandler.GetImage(this.strImgOver,_loc2_);
         fZoom = param1;
         this.UpdateImage();
      }
   }
}

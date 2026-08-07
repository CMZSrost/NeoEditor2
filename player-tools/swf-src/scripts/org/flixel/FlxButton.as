package org.flixel
{
   import flash.events.MouseEvent;
   
   public class FlxButton extends FlxSprite
   {
      
      public static var NORMAL:uint = 0;
      
      public static var HIGHLIGHT:uint = 1;
      
      public static var PRESSED:uint = 2;
       
      
      protected var ImgDefaultButton:Class;
      
      protected var SndBeep:Class;
      
      public var label:FlxText;
      
      public var labelOffset:FlxPoint;
      
      public var onUp:Function;
      
      public var onDown:Function;
      
      public var onOver:Function;
      
      public var onOut:Function;
      
      public var status:uint;
      
      public var soundOver:FlxSound;
      
      public var soundOut:FlxSound;
      
      public var soundDown:FlxSound;
      
      public var soundUp:FlxSound;
      
      protected var _onToggle:Boolean;
      
      protected var _pressed:Boolean;
      
      protected var _initialized:Boolean;
      
      public function FlxButton(param1:Number = 0, param2:Number = 0, param3:String = null, param4:Function = null)
      {
         this.ImgDefaultButton = FlxButton_ImgDefaultButton;
         this.SndBeep = FlxButton_SndBeep;
         super(param1,param2);
         if(param3 != null)
         {
            this.label = new FlxText(0,0,80,param3);
            this.label.setFormat(null,8,3355443,"center");
            this.labelOffset = new FlxPoint(-1,3);
         }
         loadGraphic(this.ImgDefaultButton,true,false,80,20);
         this.onUp = param4;
         this.onDown = null;
         this.onOut = null;
         this.onOver = null;
         this.soundOver = null;
         this.soundOut = null;
         this.soundDown = null;
         this.soundUp = null;
         this.status = NORMAL;
         this._onToggle = false;
         this._pressed = false;
         this._initialized = false;
      }
      
      override public function destroy() : void
      {
         if(FlxG.stage != null)
         {
            FlxG.stage.removeEventListener(MouseEvent.MOUSE_UP,this.onMouseUp);
         }
         if(this.label != null)
         {
            this.label.destroy();
            this.label = null;
         }
         this.onUp = null;
         this.onDown = null;
         this.onOut = null;
         this.onOver = null;
         if(this.soundOver != null)
         {
            this.soundOver.destroy();
         }
         if(this.soundOut != null)
         {
            this.soundOut.destroy();
         }
         if(this.soundDown != null)
         {
            this.soundDown.destroy();
         }
         if(this.soundUp != null)
         {
            this.soundUp.destroy();
         }
         super.destroy();
      }
      
      override public function preUpdate() : void
      {
         super.preUpdate();
         if(!this._initialized)
         {
            if(FlxG.stage != null)
            {
               FlxG.stage.addEventListener(MouseEvent.MOUSE_UP,this.onMouseDown);
               FlxG.stage.addEventListener(MouseEvent.MOUSE_UP,this.onMouseUp);
               this._initialized = true;
            }
         }
      }
      
      override public function update() : void
      {
         this.updateButton();
         if(this.label == null)
         {
            return;
         }
         switch(frame)
         {
            case HIGHLIGHT:
               this.label.alpha = 1;
               break;
            case PRESSED:
               this.label.alpha = 0.5;
               ++this.label.y;
               break;
            case NORMAL:
            default:
               this.label.alpha = 0.8;
         }
      }
      
      protected function updateButton() : void
      {
         var _loc1_:FlxCamera = null;
         var _loc2_:uint = 0;
         var _loc3_:uint = 0;
         var _loc4_:Boolean = false;
         if(FlxG.mouse.visible)
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
               FlxG.mouse.getWorldPosition(_loc1_,_point);
               if(overlapsPoint(_point,true,_loc1_))
               {
                  _loc4_ = false;
                  if(FlxG.mouse.justPressed())
                  {
                     this.status = PRESSED;
                     if(this.onDown != null)
                     {
                        this.onDown();
                     }
                     if(this.soundDown != null)
                     {
                        this.soundDown.play(true);
                     }
                  }
                  if(this.status == NORMAL)
                  {
                     this.status = HIGHLIGHT;
                     if(this.onOver != null)
                     {
                        this.onOver();
                     }
                     if(this.soundOver != null)
                     {
                        this.soundOver.play(true);
                     }
                  }
               }
            }
            if(_loc4_)
            {
               if(this.status != NORMAL)
               {
                  if(this.onOut != null)
                  {
                     this.onOut();
                  }
                  if(this.soundOut != null)
                  {
                     this.soundOut.play(true);
                  }
               }
               this.status = NORMAL;
            }
         }
         if(this.label != null)
         {
            this.label.x = x;
            this.label.y = y;
         }
         if(this.labelOffset != null)
         {
            this.label.x += this.labelOffset.x;
            this.label.y += this.labelOffset.y;
         }
         if(this.status == HIGHLIGHT && this._onToggle)
         {
            frame = NORMAL;
         }
         else
         {
            frame = this.status;
         }
      }
      
      override public function draw() : void
      {
         super.draw();
         if(this.label != null)
         {
            this.label.scrollFactor = scrollFactor;
            this.label.cameras = cameras;
            this.label.draw();
         }
      }
      
      override protected function resetHelpers() : void
      {
         super.resetHelpers();
         if(this.label != null)
         {
            this.label.width = width;
         }
      }
      
      public function setSounds(param1:Class = null, param2:Number = 1, param3:Class = null, param4:Number = 1, param5:Class = null, param6:Number = 1, param7:Class = null, param8:Number = 1) : void
      {
         if(param1 != null)
         {
            this.soundOver = FlxG.loadSound(param1,param2);
         }
         if(param3 != null)
         {
            this.soundOut = FlxG.loadSound(param3,param4);
         }
         if(param5 != null)
         {
            this.soundDown = FlxG.loadSound(param5,param6);
         }
         if(param7 != null)
         {
            this.soundUp = FlxG.loadSound(param7,param8);
         }
      }
      
      public function get on() : Boolean
      {
         return this._onToggle;
      }
      
      public function set on(param1:Boolean) : void
      {
         this._onToggle = param1;
      }
      
      protected function onMouseDown(param1:MouseEvent) : void
      {
         if(!exists || !visible || !active || this.status != HIGHLIGHT)
         {
            return;
         }
         this.status = PRESSED;
         if(this.onDown != null)
         {
            this.onDown();
         }
         if(this.soundDown != null)
         {
            this.soundDown.play(true);
         }
      }
      
      protected function onMouseUp(param1:MouseEvent) : void
      {
         if(!exists || !visible || !active || this.status != PRESSED)
         {
            return;
         }
         if(this.onUp != null)
         {
            this.onUp();
         }
         if(this.soundUp != null)
         {
            this.soundUp.play(true);
         }
      }
   }
}

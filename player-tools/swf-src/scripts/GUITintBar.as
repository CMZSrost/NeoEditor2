package
{
   import flash.display.BitmapData;
   import org.flixel.*;
   
   public class GUITintBar extends FlxGroup
   {
       
      
      public var m_txtLabel:FlxText;
      
      private var m_sprBar:FlxSprite;
      
      private var m_sprOverlay:FlxSprite;
      
      private var m_sprArrow:FlxSprite;
      
      public var m_aColors:Array;
      
      public var m_btnBG:GUIMenuButton;
      
      public var m_fTotalBarWidth:Number;
      
      public var height:uint;
      
      private var fPercentLast:Number;
      
      private var fTimeLast:Number;
      
      public function GUITintBar(param1:int, param2:int, param3:uint, param4:String, param5:Array)
      {
         super();
         var _loc6_:BitmapData = new BitmapData(param3,10,false,4281545523);
         this.m_fTotalBarWidth = param3 - 2 - 4;
         this.m_aColors = param5;
         this.m_txtLabel = new FlxText(param1,param2,param3 * 2,param4);
         this.m_txtLabel.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.m_btnBG = new GUIMenuButton(_loc6_,_loc6_,_loc6_,_loc6_,param1,param2 + this.m_txtLabel.height - 1);
         add(this.m_txtLabel);
         add(this.m_btnBG);
         this.m_sprBar = new FlxSprite(param1,this.m_btnBG.y + 1);
         this.m_sprBar.makeGraphic(this.m_fTotalBarWidth,this.m_btnBG.height - 2,this.m_aColors[0]);
         this.m_sprBar.origin = new FlxPoint();
         add(this.m_sprBar);
         this.m_sprOverlay = new FlxSprite(this.m_btnBG.x,this.m_btnBG.y);
         this.m_sprOverlay.pixels = DataHandler.GetImage("GUINBar.png");
         add(this.m_sprOverlay);
         this.m_sprArrow = new FlxSprite(this.m_btnBG.x,this.m_btnBG.y);
         this.m_sprArrow.pixels = DataHandler.GetImage("GUINBarArrowGreen.png");
         add(this.m_sprArrow);
         this.height = this.m_txtLabel.height + this.m_sprBar.height + 2;
         this.fPercentLast = 0;
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         this.m_txtLabel = DataHandler.DestroyObject(this.m_txtLabel);
         this.m_sprBar = DataHandler.DestroyObject(this.m_sprBar);
         this.m_sprOverlay = DataHandler.DestroyObject(this.m_sprOverlay);
         this.m_sprArrow = DataHandler.DestroyObject(this.m_sprArrow);
         this.m_btnBG = DataHandler.DestroyObject(this.m_btnBG);
         if(this.m_aColors != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aColors.length)
            {
               this.m_aColors[_loc1_] = null;
               _loc1_++;
            }
            this.m_aColors = null;
         }
         super.destroy();
      }
      
      public function UpdateBars(param1:Number, param2:Array) : void
      {
         var _loc5_:int = 0;
         var _loc6_:* = false;
         var _loc7_:int = 0;
         if(param2.length >= 3)
         {
            _loc5_ = 0;
            while(_loc5_ < param2.length - 1)
            {
               if(param2[_loc5_] < param2[0])
               {
                  param2[_loc5_] = param2[0];
               }
               if(param2[_loc5_] > param2[param2.length - 1])
               {
                  param2[_loc5_] = param2[param2.length - 1];
               }
               _loc5_++;
            }
         }
         if(param1 < param2[0])
         {
            param1 = Number(param2[0]);
         }
         else if(param1 > param2[param2.length - 1])
         {
            param1 = Number(param2[param2.length - 1]);
         }
         var _loc3_:Number = (param1 - param2[0]) / (param2[param2.length - 1] - param2[0]);
         if(isNaN(_loc3_))
         {
            _loc3_ = 0;
         }
         var _loc4_:Number = 1 + _loc3_ * (this.m_fTotalBarWidth - 1);
         _loc5_ = int(param2.length - 1);
         while(_loc5_ >= 0)
         {
            _loc6_ = param1 >= param2[_loc5_];
            if(param2[_loc5_] < 0 && _loc5_ > 0)
            {
               _loc6_ = param1 > param2[_loc5_];
            }
            if(_loc6_)
            {
               if((_loc7_ = _loc5_) > this.m_aColors.length - 1)
               {
                  _loc7_ = int(this.m_aColors.length - 1);
               }
               this.m_sprBar.makeGraphic(_loc4_,this.m_btnBG.height - 2,this.m_aColors[_loc7_]);
               break;
            }
            _loc5_--;
         }
         if(PlayState.m_objInstance.objDate.getTime() != this.fTimeLast || _loc3_ != this.fPercentLast)
         {
            this.m_sprArrow.alpha = 1;
            if(_loc3_ > this.fPercentLast)
            {
               this.m_sprArrow.pixels = DataHandler.GetImage("GUINBarArrowGreen.png");
            }
            else if(_loc3_ < this.fPercentLast)
            {
               this.m_sprArrow.pixels = DataHandler.GetImage("GUINBarArrowRed.png");
            }
            else
            {
               this.m_sprArrow.alpha = 0;
            }
            this.fPercentLast = _loc3_;
            this.fTimeLast = PlayState.m_objInstance.objDate.getTime();
         }
      }
      
      public function get x() : int
      {
         return this.m_txtLabel.x;
      }
      
      public function set x(param1:int) : void
      {
         this.m_txtLabel.x = param1;
         this.m_btnBG.x = param1;
         this.m_sprBar.x = param1;
         this.m_sprOverlay.x = param1;
         this.m_sprArrow.x = param1;
      }
      
      public function get y() : int
      {
         return this.m_txtLabel.y;
      }
      
      public function set y(param1:int) : void
      {
         this.m_txtLabel.y = param1;
         this.m_btnBG.y = param1 + this.m_txtLabel.height - 1;
         this.m_sprBar.y = this.m_btnBG.y + 1;
         this.m_sprOverlay.y = this.m_btnBG.y;
         this.m_sprArrow.y = this.m_btnBG.y;
      }
   }
}

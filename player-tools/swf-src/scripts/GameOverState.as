package
{
   import flash.desktop.Clipboard;
   import flash.desktop.ClipboardFormats;
   import flash.ui.Mouse;
   import org.flixel.*;
   
   public class GameOverState extends FlxState
   {
       
      
      private var sprGameOver:FlxSprite;
      
      private var sprPlayer:FlxSprite;
      
      private var txtStats:FlxText;
      
      private var txtLog:FlxText;
      
      private var txtConds:FlxText;
      
      private var m_btnPlay:ImgButton;
      
      private var m_btnClipboard:ImgButton;
      
      private var m_sndGameOver:FlxSound;
      
      private var m_sprFrameB:FlxSprite;
      
      private var m_sprFrameT:FlxSprite;
      
      private var m_sprFramePlayer:FlxSprite;
      
      private var ptOffset:FlxPoint;
      
      public function GameOverState()
      {
         super();
      }
      
      override public function create() : void
      {
         var _loc2_:String = null;
         if(!Mouse.supportsNativeCursor)
         {
            FlxG.mouse.show();
         }
         FlxG.bgColor = 4278190080;
         FlxG.sounds.callAll("stop");
         var _loc1_:GameStats = FlxG.scores[0];
         _loc2_ = "生存时间: ";
         var _loc3_:int = Math.floor(_loc1_.m_fHoursSurvived / 24);
         var _loc4_:Number = _loc1_.m_fHoursSurvived % 24;
         if(_loc3_ > 0)
         {
            _loc2_ += _loc3_ + " 天, ";
         }
         _loc2_ += _loc4_.toFixed(2) + " 小时.\n";
         _loc2_ += _loc1_.m_strCauseOfDeath;
         this.m_sndGameOver = FlxG.loadSound(cueGameOver);
         this.m_sndGameOver.play();
         this.m_sndGameOver.volume = GUIEscMenu.m_fMusicVolume;
         var _loc5_:FlxCamera = FlxG.camera;
         var _loc6_:FlxPoint = new FlxPoint(1360,768);
         var _loc7_:int = GUIValues.GetInt("zoom");
         this.ptOffset = GUIValues.GetPoint("offset");
         var _loc8_:FlxPoint;
         (_loc8_ = new FlxPoint()).x = _loc6_.x / 2 - (_loc6_.x / 2 - _loc8_.x) * _loc7_;
         _loc8_.y = _loc6_.y / 2 - (_loc6_.y / 2 - _loc8_.y) * _loc7_;
         _loc5_.x = _loc8_.x;
         _loc5_.y = _loc8_.y;
         _loc5_.zoom = _loc7_;
         _loc5_.SetSize(_loc6_.x,_loc6_.y);
         var _loc9_:String = "";
         if((_loc7_ = GUIValues.GetInt("GUIEscMenu.UI.Zoom")) == 2)
         {
            _loc9_ = DataHandler.m_strZoomPrefix;
         }
         this.sprGameOver = new FlxSprite(GUIValues.GetPoint("GameOverState.sprGameOver").x,GUIValues.GetPoint("GameOverState.sprGameOver").y);
         this.sprGameOver.pixels = DataHandler.GetImage("GameOver.png",_loc9_);
         this.m_btnPlay = new ImgButton(_loc9_ + "btn_play_dn.png",_loc9_ + "btn_play_off.png",_loc9_ + "btn_play_on.png",_loc9_ + "btn_play_on.png",GUIValues.GetPoint("GameOverState.m_btnPlay").x,GUIValues.GetPoint("GameOverState.m_btnPlay").y,this.PlayAgain);
         this.sprPlayer = new FlxSprite(GUIValues.GetPoint("GameOverState.sprPlayer").x,GUIValues.GetPoint("GameOverState.sprPlayer").y);
         this.sprPlayer.pixels = _loc1_.m_bmpPlayer;
         this.sprPlayer.Zoom(2);
         this.txtStats = new FlxText(GUIValues.GetPoint("GameOverState.txtStats").x,GUIValues.GetPoint("GameOverState.txtStats").y,GUIValues.GetInt("GameOverState.txtStats.width"),_loc2_);
         this.txtStats.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.txtLog = new FlxText(GUIValues.GetPoint("GameOverState.txtLog").x,GUIValues.GetPoint("GameOverState.txtLog").y,GUIValues.GetInt("GameOverState.txtLog.width"),_loc1_.m_strLog);
         this.txtLog.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         var _loc10_:int;
         if((_loc10_ = GUIValues.GetInt("height") - (this.txtLog.y - this.ptOffset.y + this.txtLog.height)) > 10)
         {
            this.txtLog.y += _loc10_ - 10;
         }
         this.txtConds = new FlxText(GUIValues.GetPoint("GameOverState.txtConds").x,GUIValues.GetPoint("GameOverState.txtConds").y,GUIValues.GetInt("GameOverState.txtConds.width"),_loc1_.m_strConditions);
         this.txtConds.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         if((_loc10_ = (_loc10_ = GUIValues.GetInt("height")) - (this.txtConds.y - this.ptOffset.y + this.txtConds.height)) > 10)
         {
            this.txtConds.y += _loc10_ - 10;
         }
         this.m_btnClipboard = new ImgButton(_loc9_ + "btn_clipboard_dn.png",_loc9_ + "btn_clipboard_off.png",_loc9_ + "btn_clipboard_on.png",_loc9_ + "btn_clipboard_off.png",GUIValues.GetPoint("GameOverState.m_btnClipboard").x,GUIValues.GetPoint("GameOverState.m_btnClipboard").y,this.CopyClipboard);
         this.m_sprFrameB = new FlxSprite(0,this.ptOffset.y + GUIValues.GetInt("height"));
         this.m_sprFrameB.makeGraphic(FlxG.width,FlxG.height,4278190080);
         this.m_sprFrameT = new FlxSprite(0,this.ptOffset.y - FlxG.height);
         this.m_sprFrameT.pixels = this.m_sprFrameB.pixels;
         this.m_sprFramePlayer = new FlxSprite(this.txtLog.x,0);
         this.m_sprFramePlayer.makeGraphic(this.txtLog.width,this.txtStats.y + this.txtStats.height,4278190080);
         add(this.txtLog);
         add(this.txtConds);
         add(this.m_sprFrameB);
         add(this.m_sprFrameT);
         add(this.m_sprFramePlayer);
         add(this.txtStats);
         add(this.sprGameOver);
         add(this.sprPlayer);
         add(this.m_btnPlay);
         add(this.m_btnClipboard);
      }
      
      override public function update() : void
      {
         var _loc1_:FlxPoint = null;
         var _loc2_:int = 0;
         super.update();
         _loc1_ = FlxG.mouse.getScreenPosition(null,_loc1_);
         if(_loc1_.x > this.txtLog.x && _loc1_.x < this.txtLog.x + this.txtLog.width)
         {
            _loc2_ = _loc1_.y - (this.txtStats.y + this.txtStats.height + this.m_sprFrameB.y) / 2;
            _loc2_ = -_loc2_;
            if(_loc2_ < 0 && this.txtLog.y + this.txtLog.height < this.m_sprFrameB.y)
            {
               _loc2_ = 0;
            }
            if(_loc2_ > 0 && this.txtLog.y > this.m_sprFramePlayer.y + this.m_sprFramePlayer.height)
            {
               _loc2_ = 0;
            }
            if(_loc2_ != 0)
            {
               this.txtLog.y += _loc2_ / 20;
            }
         }
         else if(_loc1_.x > this.txtConds.x && _loc1_.x < this.txtConds.x + this.txtConds.width)
         {
            _loc2_ = _loc1_.y - (this.ptOffset.y + this.m_sprFrameB.y) / 2;
            _loc2_ = -_loc2_;
            if(_loc2_ < 0 && this.txtConds.y + this.txtConds.height < this.m_sprFrameB.y)
            {
               _loc2_ = 0;
            }
            if(_loc2_ > 0 && this.txtConds.y > this.ptOffset.y)
            {
               _loc2_ = 0;
            }
            if(_loc2_ != 0)
            {
               this.txtConds.y += _loc2_ / 20;
            }
         }
      }
      
      private function CalculateMorality(param1:int) : String
      {
         var _loc3_:Array = null;
         var _loc2_:Array = new Array();
         _loc2_.push(new Array(13,"Selfless"));
         _loc2_.push(new Array(10,"Good Samaritan"));
         _loc2_.push(new Array(7,"Vigilante"));
         _loc2_.push(new Array(4,"Accidental Hero"));
         _loc2_.push(new Array(-3,"Survivor"));
         _loc2_.push(new Array(-6,"Miscreant"));
         _loc2_.push(new Array(-9,"Back-stabber"));
         _loc2_.push(new Array(-12,"Thug"));
         _loc2_.push(new Array(-500,"Psychopath"));
         for each(_loc3_ in _loc2_)
         {
            if(param1 >= _loc3_[0])
            {
               return _loc3_[1];
            }
         }
         return "Mysterious";
      }
      
      private function PlayAgain() : void
      {
         FlxG.fade(4278190080,1,this.NewGame);
         this.m_sndGameOver.fadeOut(1);
      }
      
      private function CopyClipboard() : void
      {
         Clipboard.generalClipboard.clear();
         Clipboard.generalClipboard.setData(ClipboardFormats.TEXT_FORMAT,this.txtStats.text + "\n\n" + this.txtLog.text + "\n\n" + this.txtConds.text);
      }
      
      private function NewGame() : void
      {
         FlxG.switchState(new MenuState());
      }
   }
}

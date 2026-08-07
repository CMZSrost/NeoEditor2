package
{
   import flash.desktop.Clipboard;
   import flash.desktop.ClipboardFormats;
   import flash.ui.Mouse;
   import org.flixel.*;
   
   public class EndState extends FlxState
   {
       
      
      private var sprEncounter:FlxSprite;
      
      private var sprPlayer:FlxSprite;
      
      private var txtConds:FlxText;
      
      private var m_btnPlay:ImgButton;
      
      private var m_btnClipboard:ImgButton;
      
      private var m_sndGameEnd:FlxSound;
      
      private var m_sprFrameB:FlxSprite;
      
      private var m_sprFrameT:FlxSprite;
      
      private var ptOffset:FlxPoint;
      
      private var ptMouseStart:FlxPoint;
      
      private var bMouseMoved:Boolean;
      
      private var bScroll:Boolean;
      
      private var m_fElapsed:Number;
      
      private var m_fStartScroll:Number = 2;
      
      public function EndState()
      {
         super();
      }
      
      override public function create() : void
      {
         if(!Mouse.supportsNativeCursor)
         {
            FlxG.mouse.show();
         }
         FlxG.bgColor = 4278190080;
         FlxG.sounds.callAll("stop");
         this.m_fElapsed = 0;
         var _loc1_:GameStats = FlxG.scores[0];
         var _loc2_:* = DataHandler.GetEncounter(_loc1_.m_nEncounterID).m_strDesc;
         _loc2_ += "\n\n\n\n\n\n生存时间:  ";
         var _loc3_:int = Math.floor(_loc1_.m_fHoursSurvived / 24);
         var _loc4_:Number = _loc1_.m_fHoursSurvived % 24;
         if(_loc3_ > 0)
         {
            _loc2_ += _loc3_ + " 天,  ";
         }
         _loc2_ += _loc4_.toFixed(2) + " 小时.\n";
         _loc2_ += "\n\n" + _loc1_.m_strConditions;
         this.m_sndGameEnd = FlxG.loadSound(cueEnding01);
         this.m_sndGameEnd.play();
         this.m_sndGameEnd.volume = GUIEscMenu.m_fMusicVolume;
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
         this.sprEncounter = new FlxSprite(GUIValues.GetPoint("GUIInventory.grpEncounterSlot").x,GUIValues.GetPoint("GUIInventory.grpEncounterSlot").y);
         this.sprEncounter.pixels = DataHandler.GetImage(DataHandler.GetEncounter(_loc1_.m_nEncounterID).m_strImg);
         this.sprEncounter.Zoom(GUIValues.GetInt("GUIInventory.grpEncounterSlot.zoom"));
         GUIValues.SetPosition(this.sprEncounter,"GUIInventory.grpEncounterSlot");
         this.sprEncounter.x += -this.sprEncounter.pixels.width / 2;
         this.sprEncounter.y += -this.sprEncounter.pixels.height / 2;
         this.m_btnPlay = new ImgButton(_loc9_ + "btn_play_dn.png",_loc9_ + "btn_play_off.png",_loc9_ + "btn_play_on.png",_loc9_ + "btn_play_on.png",GUIValues.GetPoint("EndState.m_btnPlay").x,GUIValues.GetPoint("EndState.m_btnPlay").y,this.PlayAgain);
         this.sprPlayer = new FlxSprite(GUIValues.GetPoint("EndState.sprPlayer").x,GUIValues.GetPoint("EndState.sprPlayer").y);
         this.sprPlayer.pixels = _loc1_.m_bmpPlayer;
         this.sprPlayer.Zoom(2);
         this.txtConds = new FlxText(GUIValues.GetPoint("GUIInventory.txtEncounter").x,GUIValues.GetPoint("GUIInventory.txtEncounter").y,GUIValues.GetInt("GUIInventory.txtEncounter.size"),_loc2_);
         this.txtConds.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_btnClipboard = new ImgButton(_loc9_ + "btn_clipboard_dn.png",_loc9_ + "btn_clipboard_off.png",_loc9_ + "btn_clipboard_on.png",_loc9_ + "btn_clipboard_off.png",GUIValues.GetPoint("EndState.m_btnClipboard").x,GUIValues.GetPoint("EndState.m_btnClipboard").y,this.CopyClipboard);
         this.m_sprFrameB = new FlxSprite(0,this.ptOffset.y + GUIValues.GetInt("height"));
         this.m_sprFrameB.makeGraphic(FlxG.width,FlxG.height,4278190080);
         this.m_sprFrameT = new FlxSprite(0,this.ptOffset.y - FlxG.height);
         this.m_sprFrameT.pixels = this.m_sprFrameB.pixels;
         this.bScroll = true;
         add(this.txtConds);
         add(this.m_sprFrameB);
         add(this.m_sprFrameT);
         add(this.sprEncounter);
         add(this.sprPlayer);
         add(this.m_btnPlay);
         add(this.m_btnClipboard);
      }
      
      override public function update() : void
      {
         var _loc1_:FlxPoint = null;
         super.update();
         this.m_fElapsed += FlxG.elapsed;
         if(this.m_fElapsed < this.m_fStartScroll)
         {
            return;
         }
         _loc1_ = FlxG.mouse.getScreenPosition(null,_loc1_);
         if(this.ptMouseStart == null)
         {
            this.ptMouseStart = new FlxPoint(_loc1_.x,_loc1_.y);
            this.bMouseMoved = false;
         }
         else if(this.bScroll && this.m_fElapsed > this.m_fStartScroll && _loc1_.x != this.ptMouseStart.x && _loc1_.y != this.ptMouseStart.y)
         {
            this.bMouseMoved = true;
         }
         var _loc2_:int = 0;
         if(this.bMouseMoved && _loc1_.x > this.txtConds.x && _loc1_.x < this.txtConds.x + this.txtConds.width)
         {
            this.bScroll = false;
            _loc2_ = _loc1_.y - (this.ptOffset.y + this.m_sprFrameB.y) / 2;
            _loc2_ = -_loc2_;
         }
         else if(this.bScroll && this.m_fElapsed > this.m_fStartScroll)
         {
            _loc2_ = -1;
         }
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
      
      private function PlayAgain() : void
      {
         FlxG.fade(4278190080,1,this.NewGame);
         this.m_sndGameEnd.fadeOut(1);
      }
      
      private function CopyClipboard() : void
      {
         Clipboard.generalClipboard.clear();
         Clipboard.generalClipboard.setData(ClipboardFormats.TEXT_FORMAT,this.txtConds.text);
      }
      
      private function NewGame() : void
      {
         FlxG.switchState(new MenuState());
      }
   }
}

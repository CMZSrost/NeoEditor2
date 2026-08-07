package
{
   import org.flixel.*;
   
   public class GUIMessageWindow extends FlxGroup
   {
      
      public static const COLOR_GOOD:uint = 4278255360;
      
      public static const COLOR_BAD:uint = 4294925623;
      
      public static const COLOR_DEFAULT:uint = 4294967295;
      
      public static const COLOR_IMPORTANT:uint = 4294952448;
      
      public static const m_vColors:Vector.<int> = Vector.<int>([COLOR_DEFAULT,COLOR_BAD,COLOR_GOOD,COLOR_IMPORTANT]);
       
      
      private var m_nTextHeight:uint;
      
      private var m_nTextWidth:uint;
      
      private var m_aTexts:Array;
      
      private var m_nMaxTexts:uint;
      
      private var m_aTextLatest:Array;
      
      private var m_nY:int;
      
      private var m_sprBG:FlxSprite;
      
      public var m_bIgnoreMessages:Boolean;
      
      public var m_bCollapsed:Boolean;
      
      public var m_bFontBig:Boolean;
      
      public var m_strEndTurnMsg:String;
      
      private var btnFont:ImgButton;
      
      private var btnExpand:ImgButton;
      
      private var btnCollapse:ImgButton;
      
      public function GUIMessageWindow(param1:uint, param2:uint)
      {
         super();
         this.m_aTexts = new Array();
         this.m_aTextLatest = new Array();
         this.m_nY = 0;
         this.m_nMaxTexts = 22;
         this.m_nTextHeight = param1;
         this.m_nTextWidth = param2;
         this.m_strEndTurnMsg = "** 回合结束 ***";
         this.m_bCollapsed = true;
         this.m_bFontBig = false;
         this.m_sprBG = new FlxSprite(0,0);
         this.m_sprBG.pixels = DataHandler.GetImage("GUIMessageFrameBig.png");
         add(this.m_sprBG);
         this.btnFont = new ImgButton("btn_font_dn.png","btn_font_up.png","btn_font_on.png","btn_font_on.png",GUIValues.GetPoint("PlayState.camMsg.btnFont").x,GUIValues.GetPoint("PlayState.camMsg.btnFont").y,this.ToggleFont);
         this.btnFont.m_strPopUpText = "切换信息字体大小.";
         PlayState.m_objInstance.m_aMouseOverItems.push(this.btnFont);
         this.btnExpand = new ImgButton("btn_up_dn.png","btn_up_off.png","btn_up_on.png","btn_up_on.png",GUIValues.GetPoint("PlayState.camMsg.btnExpand").x,GUIValues.GetPoint("PlayState.camMsg.btnExpand").y);
         this.btnCollapse = new ImgButton("btn_dn_dn.png","btn_dn_off.png","btn_dn_on.png","btn_dn_on.png",GUIValues.GetPoint("PlayState.camMsg.btnExpand").x,GUIValues.GetPoint("PlayState.camMsg.btnExpand").y);
         this.btnCollapse.visible = false;
         add(this.btnFont);
         add(this.btnExpand);
         add(this.btnCollapse);
         setAll("cameras",[PlayState.m_objInstance.camMsg]);
         this.m_bIgnoreMessages = true;
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         if(this.m_aTexts != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aTexts.length)
            {
               FlxText(this.m_aTexts[_loc1_]).destroy();
               this.m_aTexts[_loc1_] = null;
               _loc1_++;
            }
            this.m_aTexts = null;
         }
         if(this.m_aTextLatest != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aTextLatest.length)
            {
               FlxText(this.m_aTextLatest[_loc1_]).destroy();
               this.m_aTextLatest[_loc1_] = null;
               _loc1_++;
            }
            this.m_aTextLatest = null;
         }
         this.m_sprBG = DataHandler.DestroyObject(this.m_sprBG);
         this.m_strEndTurnMsg = null;
         this.btnFont = DataHandler.DestroyObject(this.btnFont);
         this.btnExpand = DataHandler.DestroyObject(this.btnExpand);
         this.btnCollapse = DataHandler.DestroyObject(this.btnCollapse);
      }
      
      public function UpdateAlpha() : void
      {
         var _loc1_:FlxText = null;
         for each(_loc1_ in this.m_aTextLatest)
         {
            _loc1_.alpha = 0.5;
         }
         this.m_aTextLatest = [];
      }
      
      public function ScrollUp(param1:int = 0) : void
      {
         var _loc3_:FlxText = null;
         var _loc2_:FlxText = this.m_aTexts[0];
         if(_loc2_.y < 0)
         {
            if(param1 == 0)
            {
               param1 = _loc2_.height;
            }
            for each(_loc3_ in this.m_aTexts)
            {
               _loc3_.y += param1;
            }
            this.m_nY += param1;
         }
      }
      
      public function ScrollDown(param1:int = 0) : void
      {
         var _loc3_:FlxText = null;
         var _loc2_:FlxText = this.m_aTexts[this.m_aTexts.length - 1];
         if(_loc2_.y + _loc2_.height > this.m_nTextHeight + 4)
         {
            if(param1 == 0)
            {
               param1 = _loc2_.height;
            }
            for each(_loc3_ in this.m_aTexts)
            {
               _loc3_.y -= param1;
            }
            this.m_nY -= param1;
         }
      }
      
      public function ScrollToBottom() : void
      {
         var _loc3_:FlxText = null;
         var _loc1_:FlxText = this.m_aTexts[this.m_aTexts.length - 1];
         var _loc2_:int = this.m_nTextHeight - (_loc1_.y + _loc1_.height);
         for each(_loc3_ in this.m_aTexts)
         {
            _loc3_.y += _loc2_;
         }
         this.m_nY += _loc2_;
      }
      
      public function Expand() : void
      {
         if(!this.m_bCollapsed || this.btnFont.bMouseOver)
         {
            return;
         }
         var _loc1_:FlxPoint = new FlxPoint(1360,768);
         var _loc2_:int = (GUIValues.GetInt("PlayState.camMsg.maxheight") - GUIValues.GetInt("PlayState.camMsg.minheight")) * GUIValues.m_fCamZoom;
         var _loc3_:FlxPoint = GUIValues.GetPoint("PlayState.camMsg");
         _loc3_.y = _loc1_.y / 2 - (_loc1_.y / 2 - _loc3_.y) * GUIValues.m_fCamZoom - _loc2_;
         var _loc4_:int = _loc3_.y - PlayState.m_objInstance.camMsg.y;
         this.m_nTextHeight = GUIValues.GetInt("PlayState.camMsg.maxheight");
         PlayState.m_objInstance.camMsg.SetSize(PlayState.m_objInstance.camMsg.width,GUIValues.GetInt("PlayState.camMsg.maxheight") * GUIValues.m_fCamZoom);
         PlayState.m_objInstance.camMsg.y += _loc4_;
         PlayState.m_objInstance.camMsg.setBounds(0,0,PlayState.m_objInstance.camMsg.width,PlayState.m_objInstance.camMsg.height);
         this.m_bCollapsed = false;
         if(this.m_aTexts.length > 0)
         {
            this.ScrollToBottom();
         }
         this.btnExpand.on = false;
         this.btnExpand.status = FlxButton.NORMAL;
         this.btnExpand.visible = false;
         this.btnCollapse.visible = true;
      }
      
      public function Collapse() : void
      {
         if(this.m_bCollapsed || this.btnFont.bMouseOver)
         {
            return;
         }
         var _loc1_:FlxPoint = new FlxPoint(1360,768);
         var _loc2_:FlxPoint = GUIValues.GetPoint("PlayState.camMsg");
         _loc2_.y = _loc1_.y / 2 - (_loc1_.y / 2 - _loc2_.y) * GUIValues.m_fCamZoom;
         var _loc3_:int = _loc2_.y - PlayState.m_objInstance.camMsg.y;
         this.m_nTextHeight = GUIValues.GetInt("PlayState.camMsg.minheight");
         PlayState.m_objInstance.camMsg.SetSize(PlayState.m_objInstance.camMsg.width,GUIValues.GetInt("PlayState.camMsg.minheight") * GUIValues.m_fCamZoom);
         PlayState.m_objInstance.camMsg.y += _loc3_;
         PlayState.m_objInstance.camMsg.setBounds(0,0,PlayState.m_objInstance.camMsg.width,PlayState.m_objInstance.camMsg.height);
         this.m_bCollapsed = true;
         if(this.m_aTexts.length > 0)
         {
            this.ScrollToBottom();
         }
         this.btnCollapse.on = false;
         this.btnCollapse.status = FlxButton.NORMAL;
         this.btnCollapse.visible = false;
         this.btnExpand.visible = true;
      }
      
      public function EndTurn() : void
      {
         if(this.m_aTexts.length > 0 && FlxText(this.m_aTexts[this.m_aTexts.length - 1]).text != this.m_strEndTurnMsg)
         {
            this.MessageFloaty(this.m_strEndTurnMsg,false);
         }
         this.UpdateAlpha();
      }
      
      public function MessageFloaty(param1:String, param2:Boolean = true, param3:FlxPoint = null, param4:uint = 4294967295) : void
      {
         if(param1 == "" || this.m_bIgnoreMessages)
         {
            return;
         }
         if(param3 == null)
         {
            param3 = new FlxPoint(FlxG.width / 2,FlxG.height / 2);
         }
         var _loc5_:FlxText = new FlxText(2,this.m_nY,GUIValues.GetInt("PlayState.camMsg.width"),param1);
         if(this.m_bFontBig)
         {
            _loc5_.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),param4);
         }
         else
         {
            _loc5_.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),param4);
         }
         _loc5_.alpha = 1;
         _loc5_.cameras = [PlayState.m_objInstance.camMsg];
         this.m_aTexts.push(_loc5_);
         this.m_aTextLatest.push(_loc5_);
         add(_loc5_);
         this.m_nY += _loc5_.height - 2;
         if(this.m_aTexts.length > 0)
         {
            this.ScrollToBottom();
         }
         if(this.m_aTexts.length > this.m_nMaxTexts)
         {
            remove(this.m_aTexts[0]);
            this.m_aTexts.splice(0,1);
         }
         if(param2 && PlayState.m_objInstance.m_nGameState != PlayState.GAMESTATE_INVENTORY)
         {
            PlayState.m_objInstance.QueueTextFloaty(param1,param3,param4);
         }
      }
      
      private function ToggleFont() : void
      {
         var _loc4_:FlxText = null;
         var _loc1_:String = GUIValues.GetString("strBodyFontName");
         var _loc2_:int = GUIValues.GetInt("nBodyFontSize");
         if(this.m_bFontBig)
         {
            _loc1_ = GUIValues.GetString("strTinyFontName");
            _loc2_ = GUIValues.GetInt("nTinyFontSize");
            this.m_bFontBig = false;
         }
         else
         {
            this.m_bFontBig = true;
         }
         var _loc3_:FlxPoint = new FlxPoint(2,0);
         for each(_loc4_ in this.m_aTexts)
         {
            _loc4_.y = _loc3_.y;
            _loc4_.setFormat(_loc1_,_loc2_,_loc4_.color);
            _loc4_.SetWidth(GUIValues.GetInt("PlayState.camMsg.width"));
            _loc3_.y += _loc4_.height - 2;
         }
         this.m_nY = _loc3_.y;
         if(this.m_aTexts.length > 0)
         {
            this.ScrollToBottom();
         }
         this.btnFont.on = false;
         this.btnFont.status = FlxButton.NORMAL;
      }
      
      public function Tail() : String
      {
         var _loc2_:FlxText = null;
         var _loc1_:String = "";
         for each(_loc2_ in this.m_aTexts)
         {
            _loc1_ += _loc2_.text + "\n";
         }
         return _loc1_;
      }
      
      public function SetRes() : void
      {
         var _loc2_:FlxText = null;
         var _loc1_:FlxPoint = new FlxPoint(2,0);
         for each(_loc2_ in this.m_aTexts)
         {
            _loc2_.y = _loc1_.y;
            _loc2_.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),_loc2_.color);
            _loc2_.SetWidth(GUIValues.GetInt("PlayState.camMsg.width"));
            _loc1_.y += _loc2_.height - 2;
         }
         if(GUIValues.GetString("strBodyFontName") != GUIValues.GetString("strTinyFontName"))
         {
            this.m_bFontBig = true;
         }
         else
         {
            this.m_bFontBig = false;
         }
         this.m_nY = _loc1_.y;
         this.m_nTextHeight = GUIValues.GetInt("PlayState.camMsg.minheight");
         this.m_nTextWidth = GUIValues.GetInt("PlayState.camMsg.width");
         this.m_sprBG.pixels = DataHandler.GetImage(GUIValues.GetString("PlayState.GUIMessageWindow.m_sprBG.bigimage"));
         var _loc3_:int = GUIValues.GetInt("Item.zoom");
         GUIValues.SetPosition(this.btnFont,"PlayState.camMsg.btnFont");
         this.btnFont.Zoom(_loc3_);
         GUIValues.SetPosition(this.btnExpand,"PlayState.camMsg.btnExpand");
         this.btnExpand.Zoom(_loc3_);
         GUIValues.SetPosition(this.btnCollapse,"PlayState.camMsg.btnExpand");
         this.btnCollapse.Zoom(_loc3_);
         if(this.m_aTexts.length > 0)
         {
            this.ScrollToBottom();
         }
      }
   }
}

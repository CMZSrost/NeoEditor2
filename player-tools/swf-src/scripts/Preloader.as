package
{
   import flash.desktop.Clipboard;
   import flash.desktop.ClipboardFormats;
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.DisplayObject;
   import flash.display.MovieClip;
   import flash.display.Sprite;
   import flash.display.StageAlign;
   import flash.events.Event;
   import flash.events.MouseEvent;
   import flash.net.URLRequest;
   import flash.net.navigateToURL;
   import flash.system.Security;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.text.TextFormat;
   import flash.utils.getDefinitionByName;
   import flash.utils.getTimer;
   import org.flixel.FlxG;
   
   public class Preloader extends MovieClip
   {
       
      
      protected var ImgLogo:Class;
      
      protected var ImgLoadingScreenBG:Class;
      
      protected var ImgLoadingBar:Class;
      
      public var PixelSuperTiny:String = "Preloader_PixelSuperTiny";
      
      public var PixelTiny:String = "Preloader_PixelTiny";
      
      public var PixelMid:String = "Preloader_PixelMid";
      
      protected var _init:Boolean;
      
      protected var _buffer:Sprite;
      
      protected var _bmpBar:Bitmap;
      
      protected var bmpBarOverlay:Bitmap;
      
      private var bmpLogo:Bitmap;
      
      private var sprLogo:Sprite;
      
      protected var _text:TextField;
      
      protected var txtDebug:TextField;
      
      protected var _width:uint;
      
      protected var _height:uint;
      
      protected var _min:uint;
      
      public var className:String;
      
      public var minDisplayTime:Number;
      
      private var vBarColors:Vector.<int>;
      
      private var nColorIndex:uint = 0;
      
      private var mainClass:Class;
      
      private var app:Object;
      
      public function Preloader()
      {
         this.ImgLogo = Preloader_ImgLogo;
         this.ImgLoadingScreenBG = Preloader_ImgLoadingScreenBG;
         this.ImgLoadingBar = Preloader_ImgLoadingBar;
         this.vBarColors = Vector.<int>([14155777,16343296,16175104,8977665]);
         super();
         this.className = "NEOScavenger";
         this.minDisplayTime = 0;
         stop();
         GUIValues.SetRes(GUIValues.GUITYPE_800X450);
         GUIValues.Initialize();
         root.stage.scaleMode = GUIValues.GetString("strScaleMode");
         root.stage.align = StageAlign.TOP_LEFT;
         FlxG.debug = false;
         var _loc1_:Array = root.loaderInfo.url.split("://");
         _loc1_ = String(_loc1_[1]).split("/");
         var _loc2_:String = _loc1_[0];
         DataHandler.stage = root.stage;
         var _loc3_:Boolean = true;
         _loc3_ = false;
         if(_loc3_)
         {
            Security.loadPolicyFile(§_a_-_---§.§_a_--_--§(-1820302810));
            DataHandler.CheckOwnList();
            DataHandler.LoadData(new URLRequest(§_a_-_---§.§_a_--_--§(-1820302812)),DataHandler.ParseDomains);
         }
         this._init = false;
         addEventListener(Event.ENTER_FRAME,this.onEnterFrame);
         if(false == false && false == false)
         {
            this.mainClass = Class(NEOScavenger);
            if(this.mainClass)
            {
               this.app = new this.mainClass();
               DataHandler.Initialize();
            }
         }
      }
      
      public function ThrowUp() : void
      {
         var _loc1_:Bitmap = new Bitmap(new BitmapData(stage.stageWidth,stage.stageHeight,true,4294967295));
         addChild(_loc1_);
         var _loc2_:TextFormat = new TextFormat();
         _loc2_.color = 0;
         _loc2_.size = 16;
         _loc2_.align = "center";
         _loc2_.bold = true;
         _loc2_.font = "system";
         var _loc3_:TextField = new TextField();
         _loc3_.width = _loc1_.width - 16;
         _loc3_.height = _loc1_.height - 16;
         _loc3_.y = 8;
         _loc3_.multiline = true;
         _loc3_.wordWrap = true;
         _loc3_.embedFonts = true;
         _loc3_.defaultTextFormat = _loc2_;
         _loc3_.text = §_a_-_---§.§_a_--_--§(-1820302804) + DataHandler.strProductURL + "\n\nto play the game at my site.  Thanks, and have fun!";
         addChild(_loc3_);
         _loc3_.addEventListener(MouseEvent.CLICK,this.goToMyURL);
         _loc1_.addEventListener(MouseEvent.CLICK,this.goToMyURL);
      }
      
      private function goToMyURL(param1:MouseEvent = null) : void
      {
         navigateToURL(new URLRequest(DataHandler.strProductURL));
      }
      
      private function onEnterFrame(param1:Event) : void
      {
         var _loc4_:Object = null;
         var _loc2_:Number = 0;
         var _loc3_:uint = uint(getTimer());
         DataHandler.update();
         if(false == false && false == false)
         {
            if(!this._init)
            {
               if(stage.stageWidth <= 0 || stage.stageHeight <= 0)
               {
                  return;
               }
               this.create();
               this._init = true;
            }
            graphics.clear();
            if(framesLoaded >= totalFrames && _loc3_ > this._min && DataHandler.bLoadingComplete)
            {
               removeEventListener(Event.ENTER_FRAME,this.onEnterFrame);
               nextFrame();
               addChild(this.app as DisplayObject);
               this.destroy();
            }
            else
            {
               _loc2_ = DataHandler.m_fPercentLoaded;
               if(this._min > 0 && _loc2_ > _loc3_ / this._min)
               {
                  _loc2_ = _loc3_ / this._min;
               }
               this.update(_loc2_);
            }
         }
         else
         {
            if(!this._init)
            {
               if(stage.stageWidth <= 0 || stage.stageHeight <= 0)
               {
                  return;
               }
               this.create();
               this._init = true;
            }
            graphics.clear();
            if(framesLoaded >= totalFrames && _loc3_ > this._min)
            {
               removeEventListener(Event.ENTER_FRAME,this.onEnterFrame);
               nextFrame();
               this.mainClass = Class(getDefinitionByName(this.className));
               if(this.mainClass)
               {
                  _loc4_ = new this.mainClass();
                  DataHandler.Initialize();
                  addChild(_loc4_ as DisplayObject);
               }
               this.destroy();
            }
            else
            {
               _loc2_ = root.loaderInfo.bytesLoaded / root.loaderInfo.bytesTotal;
               if(this._min > 0 && _loc2_ > _loc3_ / this._min)
               {
                  _loc2_ = _loc3_ / this._min;
               }
               this.update(_loc2_);
            }
         }
      }
      
      protected function create() : void
      {
         this._min = 0;
         this._min = this.minDisplayTime * 1000;
         this._buffer = new Sprite();
         addChild(this._buffer);
         this._width = stage.stageWidth / this._buffer.scaleX;
         this._height = stage.stageHeight / this._buffer.scaleY;
         var _loc1_:Bitmap = new this.ImgLoadingScreenBG();
         _loc1_.scaleX = stage.stageWidth / _loc1_.width;
         _loc1_.scaleY = stage.stageHeight / _loc1_.height;
         this._buffer.addChild(_loc1_);
         this.sprLogo = new Sprite();
         this._buffer.addChild(this.sprLogo);
         this.sprLogo.addEventListener(MouseEvent.CLICK,this.goToMyURL);
         this.bmpLogo = new this.ImgLogo();
         this.bmpLogo.x = stage.stageWidth / 2 - this.bmpLogo.width / 2;
         this.bmpLogo.y = stage.stageHeight / 2 - this.bmpLogo.height / 2;
         this.sprLogo.addChild(this.bmpLogo);
         this.bmpBarOverlay = new this.ImgLoadingBar();
         this.bmpBarOverlay.x = this.bmpLogo.x + this.bmpLogo.width / 2 - this.bmpBarOverlay.width / 2 - 40;
         this.bmpBarOverlay.y = this.bmpLogo.y + this.bmpLogo.height + 15;
         this._bmpBar = new Bitmap(new BitmapData(1,8,false,this.vBarColors[0]));
         this._bmpBar.x = this.bmpBarOverlay.x + 1;
         this._bmpBar.y = this.bmpBarOverlay.y + 1;
         this._buffer.addChild(this._bmpBar);
         this._buffer.addChild(this.bmpBarOverlay);
         this._text = new TextField();
         this._text.defaultTextFormat = new TextFormat(GUIValues.GetString("strTinyFontName"),16,16777215);
         this._text.embedFonts = true;
         this._text.selectable = false;
         this._text.multiline = false;
         this._text.x = int(this.bmpBarOverlay.x + this.bmpBarOverlay.width + 2);
         this._text.y = this.bmpBarOverlay.y;
         this._text.width = 180;
         this._buffer.addChild(this._text);
         this.txtDebug = new TextField();
         this.txtDebug.defaultTextFormat = new TextFormat(GUIValues.GetString("strTinyFontName"),16,16777215);
         this.txtDebug.embedFonts = true;
         this.txtDebug.selectable = false;
         this.txtDebug.multiline = true;
         this.txtDebug.x = 10;
         this.txtDebug.y = 20;
         this.txtDebug.width = 500;
         this.txtDebug.autoSize = TextFieldAutoSize.LEFT;
         this._buffer.addChild(this.txtDebug);
      }
      
      protected function destroy() : void
      {
         this.sprLogo.removeEventListener(MouseEvent.CLICK,this.goToMyURL);
         removeChild(this._buffer);
         this.bmpLogo = null;
         this.sprLogo = null;
         this._buffer = null;
         this._bmpBar = null;
         this._text = null;
         this.txtDebug = null;
         this.bmpBarOverlay = null;
      }
      
      protected function update(param1:Number) : void
      {
         var _loc2_:String = this.txtDebug.text;
         this._text.text = "加载中..." + Math.floor(param1 * 100) + "%\n" + DataHandler.m_strVersion;
         this._text.setTextFormat(this._text.defaultTextFormat);
         if(DataHandler.m_strDebug != null)
         {
            this.txtDebug.text = DataHandler.m_strDebugTail + " row: " + DataHandler.m_nDebugCounter + "\n打开文本编辑器并粘贴以查看所有启动日志";
            if(this.txtDebug.y + this.txtDebug.height > stage.stageHeight - 40)
            {
               this.txtDebug.y -= this.txtDebug.height / this.txtDebug.numLines;
            }
         }
         this.txtDebug.setTextFormat(this.txtDebug.defaultTextFormat);
         if(_loc2_ != this.txtDebug.text)
         {
            Clipboard.generalClipboard.clear();
            Clipboard.generalClipboard.setData(ClipboardFormats.TEXT_FORMAT,DataHandler.m_strDebug);
         }
         if(param1 > 0.75 && this.nColorIndex < 3)
         {
            this.nColorIndex = 3;
            this._bmpBar.bitmapData = new BitmapData(1,8,false,this.vBarColors[this.nColorIndex]);
         }
         else if(param1 > 0.5 && this.nColorIndex < 2)
         {
            this.nColorIndex = 2;
            this._bmpBar.bitmapData = new BitmapData(1,8,false,this.vBarColors[this.nColorIndex]);
         }
         else if(param1 > 0.25 && this.nColorIndex < 1)
         {
            this.nColorIndex = 1;
            this._bmpBar.bitmapData = new BitmapData(1,8,false,this.vBarColors[this.nColorIndex]);
         }
         this._bmpBar.scaleX = param1 * (this.bmpBarOverlay.width - 1);
      }
   }
}

package
{
   import flash.display.BitmapData;
   import flash.geom.Rectangle;
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class Minimap extends FlxGroup
   {
       
      
      private var sprMinimap:FlxSprite;
      
      private var sprMinimapBounds:FlxSprite;
      
      private var sprPlayer:FlxSprite;
      
      private var nPixelSize:uint;
      
      private var aLabels:Array;
      
      public var camMinimap:FlxCamera;
      
      public var nScrollMod:Number;
      
      private var grpScrollItems:FlxGroup;
      
      public var m_dictLabels:Dictionary;
      
      public function Minimap()
      {
         super();
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         this.sprMinimap = DataHandler.DestroyObject(this.sprMinimap);
         this.sprMinimapBounds = DataHandler.DestroyObject(this.sprMinimapBounds);
         this.sprPlayer = DataHandler.DestroyObject(this.sprPlayer);
         if(this.aLabels != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aLabels.length)
            {
               FlxText(this.aLabels[_loc1_]).destroy();
               _loc1_++;
            }
            this.aLabels = null;
         }
         this.camMinimap = DataHandler.DestroyObject(this.camMinimap);
         this.grpScrollItems = DataHandler.DestroyObject(this.grpScrollItems);
         for each(_loc1_ in this.m_dictLabels)
         {
            if(this.m_dictLabels[_loc1_] != null)
            {
               FlxText(this.m_dictLabels[_loc1_]).destroy();
            }
            delete this.m_dictLabels[_loc1_];
         }
         this.m_dictLabels = null;
      }
      
      public function Initialize() : void
      {
         this.nPixelSize = 4;
         this.nScrollMod = 16;
         this.aLabels = new Array();
         this.grpScrollItems = new FlxGroup();
         this.m_dictLabels = new Dictionary();
         var _loc1_:uint = MapUtils.tmapHexes.widthInTiles * this.nPixelSize * 2;
         var _loc2_:uint = (MapUtils.tmapHexes.heightInTiles + 1) * this.nPixelSize / 2;
         this.sprMinimap = new FlxSprite();
         this.sprMinimap.pixels = new BitmapData(_loc1_,_loc2_,false,4281545523);
         var _loc3_:int = GUIValues.GetInt("width") / 80 * this.nPixelSize;
         var _loc4_:int = GUIValues.GetInt("height") / 40 * this.nPixelSize;
         var _loc5_:int = 4294967295;
         var _loc6_:int = 2281701376;
         this.sprMinimapBounds = new FlxSprite();
         this.sprMinimapBounds.pixels = new BitmapData(_loc3_,_loc4_,true,0);
         this.sprMinimapBounds.drawLine(0,0,_loc3_ - 1,0,_loc5_);
         this.sprMinimapBounds.drawLine(_loc3_ - 1,0,_loc3_ - 1,_loc4_ - 1,_loc5_);
         this.sprMinimapBounds.drawLine(0,_loc4_ - 1,_loc3_ - 1,_loc4_ - 1,_loc5_);
         this.sprMinimapBounds.drawLine(0,0,0,_loc4_ - 1,_loc5_);
         this.sprPlayer = new FlxSprite();
         this.sprPlayer.pixels = new BitmapData(this.nPixelSize * 2 + 1,this.nPixelSize * 2 + 1,true,0);
         this.sprPlayer.drawLine(this.nPixelSize + 1,0,this.nPixelSize + 1,this.nPixelSize * 2 + 1,_loc6_);
         this.sprPlayer.drawLine(0,this.nPixelSize + 1,this.nPixelSize * 2 + 1,this.nPixelSize + 1,_loc6_);
         this.sprPlayer.drawLine(this.nPixelSize,0,this.nPixelSize,this.nPixelSize * 2 + 1,_loc5_);
         this.sprPlayer.drawLine(0,this.nPixelSize,this.nPixelSize * 2 + 1,this.nPixelSize,_loc5_);
         add(this.sprMinimap);
         add(this.grpScrollItems);
         add(this.sprMinimapBounds);
         add(this.sprPlayer);
         this.camMinimap = new FlxCamera(0,0,1,1,1);
         this.SetRes();
         this.camMinimap.setBounds(-this.sprMinimap.width,-this.sprMinimap.height,3 * this.sprMinimap.width,3 * this.sprMinimap.height);
         FlxG.addCamera(this.camMinimap);
         this.camMinimap.visible = false;
         setAll("cameras",[this.camMinimap]);
         this.camMinimap.follow(this.sprMinimapBounds);
      }
      
      public function MoveCamera(param1:FlxSprite) : void
      {
         var _loc2_:FlxPoint = new FlxPoint(param1.x / MapUtils.tmapHexes.width,param1.y / MapUtils.tmapHexes.height);
         this.sprMinimapBounds.x = _loc2_.x * this.sprMinimap.width - this.sprMinimapBounds.width / 2;
         this.sprMinimapBounds.y = _loc2_.y * this.sprMinimap.height - this.sprMinimapBounds.height / 2;
      }
      
      public function MovePlayer(param1:Number, param2:Number) : void
      {
         var _loc3_:FlxPoint = new FlxPoint(param1 / MapUtils.tmapHexes.width,param2 / MapUtils.tmapHexes.height);
         this.sprPlayer.x = _loc3_.x * this.sprMinimap.width - this.nPixelSize / 2;
         this.sprPlayer.y = _loc3_.y * this.sprMinimap.height;
      }
      
      public function MinimapHex(param1:FlxHexTile, param2:String = null, param3:Boolean = true) : void
      {
         var _loc4_:FlxPoint = param1.GetHexCoords();
         var _loc5_:int = 0;
         var _loc6_:int = 0;
         if(_loc4_.y % 2 > 0)
         {
            _loc5_ = int(this.nPixelSize);
            _loc6_ = 0;
         }
         _loc4_.x = 2 * this.nPixelSize * _loc4_.x + _loc5_;
         _loc4_.y = 0.5 * this.nPixelSize * _loc4_.y + _loc6_;
         var _loc7_:int = int(MapUtils.aMapKey[param1.index]);
         if(param1.nExploredState > 0)
         {
            _loc7_ = this.ScaleColor(_loc7_,0.5);
         }
         this.sprMinimap.pixels.fillRect(new Rectangle(_loc4_.x,_loc4_.y,this.nPixelSize,this.nPixelSize),_loc7_);
         this.sprMinimap.drawFrame(true);
         if(param2 == null)
         {
            return;
         }
         var _loc8_:FlxText = this.m_dictLabels[MapUtils.tmapHexes.getTiles().indexOf(param1)];
         if(param2 != "")
         {
            if(_loc8_ == null)
            {
               (_loc8_ = new FlxText(this.sprMinimap.x + _loc4_.x,this.sprMinimap.y + _loc4_.y,150,param2)).setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
               _loc8_.cameras = [this.camMinimap];
               this.grpScrollItems.add(_loc8_);
               this.m_dictLabels[MapUtils.tmapHexes.getTiles().indexOf(param1)] = _loc8_;
            }
            else
            {
               _loc8_.text = param2;
            }
            if(param3 == false)
            {
               _loc8_.kill();
            }
         }
         else if(_loc8_ != null)
         {
            this.grpScrollItems.remove(_loc8_);
            _loc8_.destroy();
            delete this.m_dictLabels[MapUtils.tmapHexes.getTiles().indexOf(param1)];
         }
      }
      
      private function ScaleColor(param1:int, param2:Number) : int
      {
         var _loc3_:* = (param1 & 0xFF0000) >> 16;
         var _loc4_:* = (param1 & 0xFF00) >> 8;
         var _loc5_:* = param1 & 0xFF;
         _loc3_ *= param2;
         _loc4_ *= param2;
         _loc5_ *= param2;
         return _loc3_ << 16 | _loc4_ << 8 | _loc5_;
      }
      
      public function Show() : void
      {
         PlayState.m_objInstance.btnMap.on = true;
         if(this.camMinimap != null)
         {
            this.camMinimap.visible = true;
         }
      }
      
      public function Hide() : void
      {
         PlayState.m_objInstance.btnMap.on = false;
         if(this.camMinimap != null)
         {
            this.camMinimap.visible = false;
         }
      }
      
      public function ZoomCam() : void
      {
         if(this.camMinimap == null)
         {
            return;
         }
         var _loc1_:FlxPoint = new FlxPoint(1360,768);
         var _loc2_:FlxPoint = GUIValues.GetPoint("Minimap");
         _loc2_.x = _loc1_.x / 2 - (_loc1_.x / 2 - _loc2_.x) * GUIValues.m_fCamZoom;
         _loc2_.y = _loc1_.y / 2 - (_loc1_.y / 2 - _loc2_.y) * GUIValues.m_fCamZoom;
         this.camMinimap.x = _loc2_.x;
         this.camMinimap.y = _loc2_.y;
         this.camMinimap.zoom = GUIValues.GetInt("Minimap.zoom") * GUIValues.m_fCamZoom;
         this.camMinimap.SetSize(GUIValues.GetPoint("Minimap.size").x * GUIValues.m_fCamZoom / this.camMinimap.zoom,GUIValues.GetPoint("Minimap.size").y * GUIValues.m_fCamZoom / this.camMinimap.zoom);
         var _loc3_:int = GUIValues.GetInt("width") / 80 * this.nPixelSize;
         var _loc4_:int = GUIValues.GetInt("height") / 40 * this.nPixelSize;
         var _loc5_:int = 4294967295;
         this.sprMinimapBounds.pixels = new BitmapData(_loc3_,_loc4_,true,0);
         this.sprMinimapBounds.drawLine(0,0,_loc3_ - 1,0,_loc5_);
         this.sprMinimapBounds.drawLine(_loc3_ - 1,0,_loc3_ - 1,_loc4_ - 1,_loc5_);
         this.sprMinimapBounds.drawLine(0,_loc4_ - 1,_loc3_ - 1,_loc4_ - 1,_loc5_);
         this.sprMinimapBounds.drawLine(0,0,0,_loc4_ - 1,_loc5_);
      }
      
      public function SetRes() : void
      {
         this.ZoomCam();
      }
   }
}

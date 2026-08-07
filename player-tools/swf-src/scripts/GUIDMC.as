package
{
   import flash.geom.Rectangle;
   import flash.utils.getTimer;
   import org.flixel.*;
   
   public class GUIDMC extends FlxGroup
   {
       
      
      private var sprMap:FlxSprite;
      
      public var camMinimap:FlxCamera;
      
      private var sprToSprawl:FlxSprite;
      
      private var sprScroll:FlxSprite;
      
      public var nScrollSpeed:Number;
      
      private var grpVFXLayer:FlxGroup;
      
      private var grpLabelsLayer:FlxGroup;
      
      private var vLightPath01:FlxPath;
      
      private var vLightPath02:FlxPath;
      
      private var vLightPath03:FlxPath;
      
      private var vLightPath04:FlxPath;
      
      private var vLightPath05:FlxPath;
      
      private var vLightPath06:FlxPath;
      
      private var vLightPath07:FlxPath;
      
      private var vLightPath08:FlxPath;
      
      private var vLightPath09:FlxPath;
      
      private var vLightPath10:FlxPath;
      
      private var vLightPath11:FlxPath;
      
      private var vLightPath12:FlxPath;
      
      private var vLightPath13:FlxPath;
      
      private var vLightPath14:FlxPath;
      
      private var vLightPath15:FlxPath;
      
      private var vLightPath16:FlxPath;
      
      private var vLightPath17:FlxPath;
      
      private var vLightPath18:FlxPath;
      
      private var vLightPath19:FlxPath;
      
      private var vLightPath20:FlxPath;
      
      private var vLightPath21:FlxPath;
      
      private var vLightPath22:FlxPath;
      
      private var vLightPath23:FlxPath;
      
      private var vLightPath24:FlxPath;
      
      private var vLightPath25:FlxPath;
      
      private var vLightPath26:FlxPath;
      
      private var vLightPath27:FlxPath;
      
      private var vLightPath28:FlxPath;
      
      private var vLightPath29:FlxPath;
      
      private var vLightPath30:FlxPath;
      
      private var vLightPath31:FlxPath;
      
      private var vLightPath32:FlxPath;
      
      private var vLightPath33:FlxPath;
      
      private var vLightPath34:FlxPath;
      
      private var vLightPath35:FlxPath;
      
      private var vLightPath36:FlxPath;
      
      private var vPads:Vector.<FlxPoint>;
      
      public function GUIDMC()
      {
         super();
         this.nScrollSpeed = 6;
         this.sprMap = new FlxSprite(0,0);
         this.sprMap.loadBitmap(DataHandler.GetImage("DMCGate11.png"));
         this.sprToSprawl = new FlxSprite(this.sprMap.x,this.sprMap.y);
         this.sprToSprawl.loadBitmap(DataHandler.GetImage("DMCToSprawl.png"));
         this.sprScroll = new FlxSprite(this.sprMap.x + this.sprMap.width / 2,this.sprMap.y);
         this.sprScroll.loadBitmap(DataHandler.GetImage("DMCScroll.png"));
         this.sprScroll.x -= this.sprScroll.width / 2;
         this.grpVFXLayer = new FlxGroup();
         this.grpLabelsLayer = new FlxGroup();
         add(this.sprMap);
         add(this.grpVFXLayer);
         add(this.grpLabelsLayer);
         this.grpLabelsLayer.add(this.sprToSprawl);
         this.grpLabelsLayer.add(this.sprScroll);
         this.SetPaths();
         this.camMinimap = new FlxCamera(0,0,1,1,1);
         this.SetRes();
         this.camMinimap.setBounds(0,0,this.sprMap.width,this.sprMap.height);
         FlxG.addCamera(this.camMinimap);
         this.camMinimap.visible = false;
         setAll("cameras",[this.camMinimap]);
         setAll("scrollFactor",new FlxPoint(0,0));
         this.sprScroll.scrollFactor = new FlxPoint();
      }
      
      override public function destroy() : void
      {
         var _loc1_:FlxPoint = null;
         this.UpdateButtons(true);
         this.sprMap = DataHandler.DestroyObject(this.sprMap);
         this.camMinimap = DataHandler.DestroyObject(this.camMinimap);
         this.sprToSprawl = DataHandler.DestroyObject(this.sprToSprawl);
         this.sprScroll = DataHandler.DestroyObject(this.sprScroll);
         this.grpVFXLayer = DataHandler.DestroyObject(this.grpVFXLayer);
         this.grpLabelsLayer = DataHandler.DestroyObject(this.grpLabelsLayer);
         this.vLightPath01 = DataHandler.DestroyObject(this.vLightPath01);
         this.vLightPath02 = DataHandler.DestroyObject(this.vLightPath02);
         this.vLightPath03 = DataHandler.DestroyObject(this.vLightPath03);
         this.vLightPath04 = DataHandler.DestroyObject(this.vLightPath04);
         this.vLightPath05 = DataHandler.DestroyObject(this.vLightPath05);
         this.vLightPath06 = DataHandler.DestroyObject(this.vLightPath06);
         this.vLightPath07 = DataHandler.DestroyObject(this.vLightPath07);
         this.vLightPath08 = DataHandler.DestroyObject(this.vLightPath08);
         this.vLightPath09 = DataHandler.DestroyObject(this.vLightPath09);
         this.vLightPath10 = DataHandler.DestroyObject(this.vLightPath10);
         this.vLightPath11 = DataHandler.DestroyObject(this.vLightPath11);
         this.vLightPath12 = DataHandler.DestroyObject(this.vLightPath12);
         this.vLightPath13 = DataHandler.DestroyObject(this.vLightPath13);
         this.vLightPath14 = DataHandler.DestroyObject(this.vLightPath14);
         this.vLightPath15 = DataHandler.DestroyObject(this.vLightPath15);
         this.vLightPath16 = DataHandler.DestroyObject(this.vLightPath16);
         this.vLightPath17 = DataHandler.DestroyObject(this.vLightPath17);
         this.vLightPath18 = DataHandler.DestroyObject(this.vLightPath18);
         this.vLightPath19 = DataHandler.DestroyObject(this.vLightPath19);
         this.vLightPath20 = DataHandler.DestroyObject(this.vLightPath20);
         this.vLightPath21 = DataHandler.DestroyObject(this.vLightPath21);
         this.vLightPath22 = DataHandler.DestroyObject(this.vLightPath22);
         this.vLightPath23 = DataHandler.DestroyObject(this.vLightPath23);
         this.vLightPath24 = DataHandler.DestroyObject(this.vLightPath24);
         this.vLightPath25 = DataHandler.DestroyObject(this.vLightPath25);
         this.vLightPath26 = DataHandler.DestroyObject(this.vLightPath26);
         this.vLightPath27 = DataHandler.DestroyObject(this.vLightPath27);
         this.vLightPath28 = DataHandler.DestroyObject(this.vLightPath28);
         this.vLightPath29 = DataHandler.DestroyObject(this.vLightPath29);
         this.vLightPath30 = DataHandler.DestroyObject(this.vLightPath30);
         this.vLightPath31 = DataHandler.DestroyObject(this.vLightPath31);
         this.vLightPath32 = DataHandler.DestroyObject(this.vLightPath32);
         this.vLightPath33 = DataHandler.DestroyObject(this.vLightPath33);
         this.vLightPath34 = DataHandler.DestroyObject(this.vLightPath34);
         this.vLightPath35 = DataHandler.DestroyObject(this.vLightPath35);
         this.vLightPath36 = DataHandler.DestroyObject(this.vLightPath36);
         if(this.vPads != null)
         {
            for each(_loc1_ in this.vPads)
            {
               _loc1_ = null;
            }
            this.vPads = null;
         }
         super.destroy();
      }
      
      override public function update() : void
      {
         super.update();
         var _loc1_:FlxPoint = FlxG.mouse.getScreenPosition();
         var _loc2_:Rectangle = new Rectangle(GUIValues.GetPoint("Minimap").x,GUIValues.GetPoint("Minimap").y,GUIValues.GetPoint("Minimap.size").x,GUIValues.GetPoint("Minimap.size").y);
         if(_loc1_.x >= _loc2_.x && _loc1_.x <= _loc2_.x + _loc2_.width && _loc1_.y >= _loc2_.y && _loc1_.y <= _loc2_.y + _loc2_.height)
         {
            if(!this.grpLabelsLayer.alive)
            {
               this.grpLabelsLayer.revive();
            }
         }
         else if(this.grpLabelsLayer.alive)
         {
            this.grpLabelsLayer.kill();
         }
      }
      
      override public function revive() : void
      {
         if(this.camMinimap != null)
         {
            this.camMinimap.visible = true;
         }
         super.revive();
      }
      
      override public function kill() : void
      {
         if(this.camMinimap != null)
         {
            this.camMinimap.visible = false;
         }
         super.kill();
      }
      
      public function MoveCamera(param1:int, param2:int) : void
      {
         this.camMinimap.scroll.x -= param1;
         this.camMinimap.scroll.y -= param2;
      }
      
      private function GoButton(param1:ImgButton) : void
      {
         param1.status = FlxButton.NORMAL;
         param1.on = false;
         DM.AppendEncounter(DataHandler.GetEncounter(param1.ID));
         DM.NextEncounter();
      }
      
      public function UpdateButtons(param1:Boolean = false) : void
      {
         var _loc4_:Encounter = null;
         var _loc2_:int = 1;
         var _loc3_:DMCPlace = DataHandler.GetDMCPlace(_loc2_);
         while(_loc3_ != null)
         {
            _loc3_.m_btn.SetFunction(this.GoButton,true);
            _loc3_.m_btn.ID = _loc3_.m_nEncounterID;
            _loc4_ = DataHandler.GetEncounter(_loc3_.m_nEncounterID);
            if(param1 == false && _loc4_ != null && _loc4_.PreconditionsOK(PlayState.m_objInstance.sprPlayer))
            {
               this.grpLabelsLayer.add(_loc3_.m_btn);
               _loc3_.m_btn.cameras = [this.camMinimap];
            }
            else
            {
               this.grpLabelsLayer.remove(_loc3_.m_btn);
            }
            _loc4_ = null;
            _loc2_++;
            _loc3_ = DataHandler.GetDMCPlace(_loc2_);
         }
      }
      
      private function AddTraffic(param1:FlxPath, param2:int) : void
      {
         var _loc6_:int = 0;
         var _loc7_:Number = NaN;
         var _loc8_:FlxPoint = null;
         var _loc9_:FlxSprite = null;
         var _loc3_:Array = new Array();
         var _loc4_:int = 0;
         while(_loc4_ < param2)
         {
            _loc6_ = Math.floor((param1.nodes.length - 1) * Math.random());
            _loc7_ = Math.random();
            _loc8_ = new FlxPoint(_loc7_ * (param1.nodes[_loc6_ + 1].x - param1.nodes[_loc6_].x) + param1.nodes[_loc6_].x,_loc7_ * (param1.nodes[_loc6_ + 1].y - param1.nodes[_loc6_].y) + param1.nodes[_loc6_].y);
            (_loc9_ = new FlxSprite(_loc8_.x,_loc8_.y)).pixels = DataHandler.GetImage("DMCLight01.png");
            _loc9_.x -= _loc9_.width / 2;
            _loc9_.y -= _loc9_.height / 2;
            _loc9_.followPath(param1,2 + DM.Rand(DM.RAND_MID) * 2,FlxObject.PATH_LOOP_FORWARD);
            _loc9_._pathNodeIndex = _loc6_ + 1;
            _loc9_.moves = true;
            _loc3_.push(_loc9_);
            _loc4_++;
         }
         var _loc5_:VFX = new VFX(_loc3_,null,this.VFXTrafficPerFrame,null);
         this.grpVFXLayer.add(_loc5_);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[this.camMinimap]);
      }
      
      private function VFXTrafficPerFrame(param1:VFX) : void
      {
         var _loc3_:FlxPoint = null;
         var _loc5_:FlxSprite = null;
         var _loc2_:Array = param1.aSprites;
         var _loc4_:int = 0;
         while(_loc4_ < _loc2_.length)
         {
            if((_loc5_ = FlxSprite(_loc2_[_loc4_]))._pathNodeIndex == 0)
            {
               _loc3_ = _loc5_.path.nodes[0];
               _loc5_.x = _loc3_.x - _loc5_.width / 2;
               _loc5_.y = _loc3_.y - _loc5_.height / 2;
               _loc5_._pathNodeIndex = 1;
            }
            _loc4_++;
         }
      }
      
      private function AddTwinkle(param1:FlxPath, param2:int) : void
      {
         var _loc7_:FlxPoint = null;
         var _loc8_:FlxSprite = null;
         var _loc3_:Array = new Array();
         var _loc4_:Vector.<String> = Vector.<String>(["DMCLight02.png","DMCLight03.png"]);
         var _loc5_:int = 0;
         while(_loc5_ < param2)
         {
            _loc7_ = new FlxPoint(Math.random() * param1.nodes[1].x + param1.nodes[0].x,Math.random() * param1.nodes[1].y + param1.nodes[0].y);
            (_loc8_ = new FlxSprite(_loc7_.x,_loc7_.y)).pixels = DataHandler.GetImage(_loc4_[Math.floor(Math.random() * _loc4_.length)]);
            _loc8_.x -= _loc8_.width / 2;
            _loc8_.y -= _loc8_.height / 2;
            _loc8_.alpha = Math.random() * 0.5 + 0.5;
            _loc8_.ID = Math.random() * 13;
            _loc8_.path = param1;
            _loc3_.push(_loc8_);
            _loc5_++;
         }
         var _loc6_:VFX = new VFX(_loc3_,null,this.VFXTwinklePerFrame,null);
         this.grpVFXLayer.add(_loc6_);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[this.camMinimap]);
      }
      
      private function VFXTwinklePerFrame(param1:VFX) : void
      {
         var _loc4_:FlxSprite = null;
         var _loc2_:Array = param1.aSprites;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_.length)
         {
            _loc4_ = FlxSprite(_loc2_[_loc3_]);
            _loc4_.alpha += (DM.Rand(DM.RAND_MID) - 0.5) * 0.2;
            if(_loc4_.alpha <= 0.1)
            {
               _loc4_.x = Math.random() * _loc4_.path.nodes[1].x + _loc4_.path.nodes[0].x;
               _loc4_.y = Math.random() * _loc4_.path.nodes[1].y + _loc4_.path.nodes[0].y;
               _loc4_.x -= _loc4_.width / 2;
               _loc4_.y -= _loc4_.height / 2;
               _loc4_.alpha = 0.2;
            }
            else if(_loc4_.alpha > 1)
            {
               _loc4_.alpha = 1;
            }
            _loc3_++;
         }
      }
      
      private function AddRedGlow(param1:Vector.<FlxPoint>, param2:Number, param3:int) : void
      {
         var _loc7_:FlxSprite = null;
         var _loc4_:Array = new Array();
         var _loc5_:int = 0;
         while(_loc5_ < param1.length)
         {
            (_loc7_ = new FlxSprite(param1[_loc5_].x,param1[_loc5_].y)).pixels = DataHandler.GetImage("DMCLight04.png");
            _loc7_.health = param2;
            _loc7_.ID = param3;
            _loc4_.push(_loc7_);
            _loc5_++;
         }
         var _loc6_:VFX = new VFX(_loc4_,null,this.VFXRedGlowPerFrame,null);
         this.grpVFXLayer.add(_loc6_);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[this.camMinimap]);
      }
      
      private function VFXRedGlowPerFrame(param1:VFX) : void
      {
         var _loc4_:FlxSprite = null;
         var _loc2_:Array = param1.aSprites;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_.length)
         {
            if((_loc4_ = FlxSprite(_loc2_[_loc3_])).ID == 0)
            {
               _loc4_.alpha = Math.sin(getTimer() / 1000 * _loc4_.health) / 2 + 0.5;
            }
            else if(Math.tan(getTimer() / 1000 * _loc4_.health) / 10 > 0.5)
            {
               _loc4_.alpha = 1;
            }
            else
            {
               _loc4_.alpha = 0.1;
            }
            _loc3_++;
         }
      }
      
      private function AddHoverLand(param1:int) : void
      {
         var _loc5_:FlxPoint = null;
         var _loc6_:FlxSprite = null;
         var _loc2_:Array = new Array();
         var _loc3_:int = 0;
         while(_loc3_ < param1)
         {
            _loc5_ = new FlxPoint(-1,-1);
            (_loc6_ = new FlxSprite(_loc5_.x,_loc5_.y)).pixels = DataHandler.GetImage("DMCLight01.png");
            _loc6_.x -= _loc6_.width / 2;
            _loc6_.y -= _loc6_.height / 2;
            _loc6_.alpha = 0.5;
            _loc6_.ID = -10000 * Math.random();
            _loc6_.health = Math.random() * 7;
            _loc6_.path = new FlxPath([this.GetPadCoords()]);
            _loc2_.push(_loc6_);
            _loc3_++;
         }
         var _loc4_:VFX = new VFX(_loc2_,null,this.VFXHoverLandPerFrame,null);
         this.grpVFXLayer.add(_loc4_);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[this.camMinimap]);
      }
      
      private function VFXHoverLandPerFrame(param1:VFX) : void
      {
         var _loc4_:FlxSprite = null;
         var _loc5_:Number = NaN;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc8_:Number = NaN;
         var _loc9_:Number = NaN;
         var _loc10_:Number = NaN;
         var _loc11_:Number = NaN;
         var _loc2_:Array = param1.aSprites;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_.length)
         {
            _loc4_ = FlxSprite(_loc2_[_loc3_]);
            if((_loc5_ = Math.tan(getTimer() / 1000 + _loc4_.health) / 10) > 0.5)
            {
               _loc4_.alpha = 1;
            }
            else
            {
               _loc4_.alpha = 0.5;
            }
            _loc6_ = _loc4_.path.nodes[0].x - _loc4_.x - _loc4_.width / 2;
            _loc7_ = _loc4_.path.nodes[0].y - _loc4_.y - _loc4_.height / 2;
            _loc8_ = _loc6_ * _loc6_ + _loc7_ * _loc7_;
            _loc9_ = 1;
            if(_loc4_.ID > 1000)
            {
               _loc4_.ID -= FlxG.elapsed * 1000;
               _loc9_ = 0;
               _loc4_.alpha = 0;
            }
            else if(_loc4_.ID > 0)
            {
               if(_loc8_ < 1)
               {
                  _loc4_.ID = -5000 - Math.random() * 5000;
                  _loc4_.x = _loc4_.path.nodes[0].x - _loc4_.width / 2;
                  _loc4_.y = _loc4_.path.nodes[0].y - _loc4_.height / 2;
               }
            }
            else if(_loc4_.ID >= -1000)
            {
               _loc9_ = -1;
               if(_loc4_.x < this.sprMap.x || _loc4_.x > this.sprMap.x + this.sprMap.width || _loc4_.y < this.sprMap.y || _loc4_.y > this.sprMap.y + this.sprMap.height)
               {
                  _loc4_.ID = 5000 + Math.random() * 5000;
                  _loc11_ = Math.random();
                  _loc4_.path.nodes[0] = this.GetPadCoords();
                  if(_loc11_ < 0.1)
                  {
                     _loc4_.x = this.sprMap.x;
                     _loc4_.y = this.sprMap.y + Math.random() * this.sprMap.height;
                  }
                  else if(_loc11_ < 0.5)
                  {
                     _loc4_.x = this.sprMap.x + this.sprMap.width;
                     _loc4_.y = this.sprMap.y + Math.random() * this.sprMap.height;
                  }
                  else if(_loc11_ < 0.75)
                  {
                     _loc4_.x = this.sprMap.x + Math.random() * this.sprMap.width;
                     _loc4_.y = this.sprMap.y;
                  }
                  else
                  {
                     _loc4_.x = this.sprMap.x + Math.random() * this.sprMap.width;
                     _loc4_.y = this.sprMap.y + this.sprMap.height;
                  }
               }
            }
            else
            {
               _loc4_.ID += FlxG.elapsed * 1000;
               _loc9_ = 0;
               if(_loc4_.ID > -1000)
               {
                  _loc4_.x += Math.random() - 2;
                  _loc4_.y += Math.random() - 2;
                  _loc9_ = -1;
               }
            }
            _loc10_ = 50;
            if(_loc8_ < 10000)
            {
               _loc10_ *= Math.sqrt(_loc8_ / 10000);
            }
            _loc10_ *= _loc9_;
            if(Math.abs(_loc6_) > Math.abs(_loc7_))
            {
               _loc7_ /= Math.abs(_loc6_);
               _loc6_ /= Math.abs(_loc6_);
            }
            else
            {
               _loc6_ /= Math.abs(_loc7_);
               _loc7_ /= Math.abs(_loc7_);
            }
            _loc4_.x += _loc6_ * _loc10_ * FlxG.elapsed;
            _loc4_.y += _loc7_ * _loc10_ * FlxG.elapsed;
            _loc3_++;
         }
      }
      
      private function GetPadCoords() : FlxPoint
      {
         var _loc1_:FlxPoint = this.vPads[Math.floor(Math.random() * this.vPads.length)];
         return new FlxPoint(_loc1_.x + Math.random() * 6 - 3,_loc1_.y + Math.random() * 6 - 3);
      }
      
      private function AddCrissCross(param1:int) : void
      {
         var _loc2_:Array = null;
         var _loc5_:FlxPoint = null;
         var _loc6_:FlxSprite = null;
         _loc2_ = new Array();
         var _loc3_:int = 0;
         while(_loc3_ < param1)
         {
            _loc5_ = new FlxPoint(-1,-1);
            (_loc6_ = new FlxSprite(_loc5_.x,_loc5_.y)).pixels = DataHandler.GetImage("DMCLight01.png");
            _loc6_.x -= _loc6_.width / 2;
            _loc6_.y -= _loc6_.height / 2;
            _loc6_.alpha = 0.5;
            _loc6_.ID = 10;
            _loc6_.health = Math.random() * 7;
            _loc6_.path = new FlxPath([new FlxPoint()]);
            _loc2_.push(_loc6_);
            _loc3_++;
         }
         var _loc4_:VFX = new VFX(_loc2_,null,this.VFXCrissCrossPerFrame,null);
         this.grpVFXLayer.add(_loc4_);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[this.camMinimap]);
      }
      
      private function VFXCrissCrossPerFrame(param1:VFX) : void
      {
         var _loc4_:FlxSprite = null;
         var _loc5_:Number = NaN;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc8_:Number = NaN;
         var _loc9_:Number = NaN;
         var _loc10_:Number = NaN;
         var _loc11_:Number = NaN;
         var _loc2_:Array = param1.aSprites;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_.length)
         {
            _loc4_ = FlxSprite(_loc2_[_loc3_]);
            if((_loc5_ = Math.tan(getTimer() / 1000 + _loc4_.health) / 10) > 0.5)
            {
               _loc4_.alpha = 1;
            }
            else
            {
               _loc4_.alpha = 0.5;
            }
            _loc6_ = _loc4_.path.nodes[0].x - _loc4_.x - _loc4_.width / 2;
            _loc7_ = _loc4_.path.nodes[0].y - _loc4_.y - _loc4_.height / 2;
            _loc8_ = _loc6_ * _loc6_ + _loc7_ * _loc7_;
            _loc9_ = 1;
            if(_loc4_.ID > 1000)
            {
               _loc4_.ID -= FlxG.elapsed * 1000;
               _loc9_ = 0;
               _loc4_.alpha = 0;
            }
            else if(_loc4_.ID > 0)
            {
               if(_loc4_.x < this.sprMap.x || _loc4_.x > this.sprMap.x + this.sprMap.width || _loc4_.y < this.sprMap.y || _loc4_.y > this.sprMap.y + this.sprMap.height)
               {
                  _loc4_.ID = Math.random() * 10000;
                  if((_loc11_ = Math.random()) < 0.1)
                  {
                     _loc4_.x = this.sprMap.x;
                     _loc4_.y = this.sprMap.y + Math.random() * this.sprMap.height;
                     _loc4_.path.nodes[0] = new FlxPoint(this.sprMap.x + this.sprMap.width + 10,this.sprMap.y + Math.random() * this.sprMap.height);
                  }
                  else if(_loc11_ < 0.25)
                  {
                     _loc4_.x = this.sprMap.x + this.sprMap.width;
                     _loc4_.y = this.sprMap.y + Math.random() * this.sprMap.height;
                     _loc4_.path.nodes[0] = new FlxPoint(this.sprMap.x - 10,this.sprMap.y + Math.random() * this.sprMap.height);
                  }
                  else if(_loc11_ < 0.65)
                  {
                     _loc4_.x = this.sprMap.x + Math.random() * this.sprMap.width;
                     _loc4_.y = this.sprMap.y;
                     _loc4_.path.nodes[0] = new FlxPoint(this.sprMap.x + Math.random() * this.sprMap.width,this.sprMap.y + this.sprMap.height + 10);
                  }
                  else
                  {
                     _loc4_.x = this.sprMap.x + Math.random() * this.sprMap.width;
                     _loc4_.y = this.sprMap.y + this.sprMap.height;
                     _loc4_.path.nodes[0] = new FlxPoint(this.sprMap.x + Math.random() * this.sprMap.width,this.sprMap.y - 10);
                  }
               }
            }
            _loc10_ = 50;
            if(Math.abs(_loc6_) > Math.abs(_loc7_))
            {
               _loc7_ /= Math.abs(_loc6_);
               _loc6_ /= Math.abs(_loc6_);
            }
            else
            {
               _loc6_ /= Math.abs(_loc7_);
               _loc7_ /= Math.abs(_loc7_);
            }
            _loc4_.x += _loc6_ * _loc10_ * FlxG.elapsed;
            _loc4_.y += _loc7_ * _loc10_ * FlxG.elapsed;
            _loc3_++;
         }
      }
      
      private function SetPaths() : void
      {
         this.vLightPath01 = new FlxPath([new FlxPoint(108 + this.sprMap.x,264 + this.sprMap.y),new FlxPoint(153 + this.sprMap.x,240 + this.sprMap.y),new FlxPoint(194 + this.sprMap.x,218 + this.sprMap.y),new FlxPoint(205 + this.sprMap.x,213 + this.sprMap.y),new FlxPoint(242 + this.sprMap.x,200 + this.sprMap.y),new FlxPoint(264 + this.sprMap.x,195 + this.sprMap.y),new FlxPoint(288 + this.sprMap.x,191 + this.sprMap.y),new FlxPoint(381 + this.sprMap.x,189 + this.sprMap.y),new FlxPoint(452 + this.sprMap.x,186 + this.sprMap.y),new FlxPoint(533 + this.sprMap.x,183 + this.sprMap.y),new FlxPoint(575 + this.sprMap.x,182 + this.sprMap.y)]);
         this.vLightPath02 = new FlxPath(this.vLightPath01.nodes.concat());
         this.vLightPath02.nodes.reverse();
         this.vLightPath03 = new FlxPath([new FlxPoint(674 + this.sprMap.x,210 + this.sprMap.y),new FlxPoint(701 + this.sprMap.x,223 + this.sprMap.y),new FlxPoint(720 + this.sprMap.x,232 + this.sprMap.y),new FlxPoint(738 + this.sprMap.x,233 + this.sprMap.y)]);
         this.vLightPath04 = new FlxPath(this.vLightPath03.nodes.concat());
         this.vLightPath04.nodes.reverse();
         this.vLightPath05 = new FlxPath([new FlxPoint(108 + this.sprMap.x,264 + this.sprMap.y),new FlxPoint(138 + this.sprMap.x,248 + this.sprMap.y),new FlxPoint(160 + this.sprMap.x,291 + this.sprMap.y),new FlxPoint(183 + this.sprMap.x,334 + this.sprMap.y),new FlxPoint(194 + this.sprMap.x,355 + this.sprMap.y),new FlxPoint(209 + this.sprMap.x,383 + this.sprMap.y),new FlxPoint(230 + this.sprMap.x,422 + this.sprMap.y),new FlxPoint(243 + this.sprMap.x,436 + this.sprMap.y),new FlxPoint(255 + this.sprMap.x,444 + this.sprMap.y),new FlxPoint(269 + this.sprMap.x,449 + this.sprMap.y),new FlxPoint(298 + this.sprMap.x,455 + this.sprMap.y),new FlxPoint(322 + this.sprMap.x,461 + this.sprMap.y),new FlxPoint(342 + this.sprMap.x,466 + this.sprMap.y),new FlxPoint(422 + this.sprMap.x,487 + this.sprMap.y),new FlxPoint(498 + this.sprMap.x,507 + this.sprMap.y),new FlxPoint(557 + this.sprMap.x,524 + this.sprMap.y),new FlxPoint(597 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath06 = new FlxPath(this.vLightPath05.nodes.concat());
         this.vLightPath06.nodes.reverse();
         this.vLightPath07 = new FlxPath([new FlxPoint(678 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(678 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath08 = new FlxPath(this.vLightPath07.nodes.concat());
         this.vLightPath08.nodes.reverse();
         this.vLightPath09 = new FlxPath([new FlxPoint(566 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(566 + this.sprMap.x,457 + this.sprMap.y),new FlxPoint(564 + this.sprMap.x,463 + this.sprMap.y),new FlxPoint(567 + this.sprMap.x,496 + this.sprMap.y)]);
         this.vLightPath10 = new FlxPath(this.vLightPath09.nodes.concat());
         this.vLightPath10.nodes.reverse();
         this.vLightPath11 = new FlxPath([new FlxPoint(451 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(451 + this.sprMap.x,363 + this.sprMap.y),new FlxPoint(458 + this.sprMap.x,373 + this.sprMap.y),new FlxPoint(469 + this.sprMap.x,402 + this.sprMap.y),new FlxPoint(479 + this.sprMap.x,408 + this.sprMap.y),new FlxPoint(478 + this.sprMap.x,415 + this.sprMap.y),new FlxPoint(451 + this.sprMap.x,408 + this.sprMap.y),new FlxPoint(451 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath12 = new FlxPath(this.vLightPath11.nodes.concat());
         this.vLightPath12.nodes.reverse();
         this.vLightPath13 = new FlxPath([new FlxPoint(337 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(337 + this.sprMap.x,203 + this.sprMap.y),new FlxPoint(340 + this.sprMap.x,209 + this.sprMap.y),new FlxPoint(333 + this.sprMap.x,215 + this.sprMap.y),new FlxPoint(323 + this.sprMap.x,218 + this.sprMap.y),new FlxPoint(318 + this.sprMap.x,229 + this.sprMap.y),new FlxPoint(318 + this.sprMap.x,276 + this.sprMap.y),new FlxPoint(323 + this.sprMap.x,285 + this.sprMap.y),new FlxPoint(332 + this.sprMap.x,292 + this.sprMap.y),new FlxPoint(337 + this.sprMap.x,302 + this.sprMap.y),new FlxPoint(337 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath14 = new FlxPath(this.vLightPath13.nodes.concat());
         this.vLightPath14.nodes.reverse();
         this.vLightPath15 = new FlxPath([new FlxPoint(224 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(224 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath16 = new FlxPath(this.vLightPath15.nodes.concat());
         this.vLightPath16.nodes.reverse();
         this.vLightPath17 = new FlxPath([new FlxPoint(114 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(114 + this.sprMap.x,274 + this.sprMap.y)]);
         this.vLightPath18 = new FlxPath(this.vLightPath17.nodes.concat());
         this.vLightPath18.nodes.reverse();
         this.vLightPath19 = new FlxPath([new FlxPoint(77 + this.sprMap.x,0 + this.sprMap.y),new FlxPoint(76 + this.sprMap.x,212 + this.sprMap.y),new FlxPoint(147 + this.sprMap.x,331 + this.sprMap.y),new FlxPoint(147 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath20 = new FlxPath(this.vLightPath19.nodes.concat());
         this.vLightPath20.nodes.reverse();
         this.vLightPath21 = new FlxPath([new FlxPoint(77 + this.sprMap.x,41 + this.sprMap.y),new FlxPoint(738 + this.sprMap.x,41 + this.sprMap.y)]);
         this.vLightPath22 = new FlxPath(this.vLightPath21.nodes.concat());
         this.vLightPath22.nodes.reverse();
         this.vLightPath23 = new FlxPath([new FlxPoint(77 + this.sprMap.x,157 + this.sprMap.y),new FlxPoint(738 + this.sprMap.x,157 + this.sprMap.y)]);
         this.vLightPath24 = new FlxPath(this.vLightPath23.nodes.concat());
         this.vLightPath24.nodes.reverse();
         this.vLightPath25 = new FlxPath([new FlxPoint(225 + this.sprMap.x,272 + this.sprMap.y),new FlxPoint(318 + this.sprMap.x,272 + this.sprMap.y)]);
         this.vLightPath26 = new FlxPath(this.vLightPath25.nodes.concat());
         this.vLightPath26.nodes.reverse();
         this.vLightPath27 = new FlxPath([new FlxPoint(451 + this.sprMap.x,272 + this.sprMap.y),new FlxPoint(738 + this.sprMap.x,272 + this.sprMap.y)]);
         this.vLightPath28 = new FlxPath(this.vLightPath27.nodes.concat());
         this.vLightPath28.nodes.reverse();
         this.vLightPath29 = new FlxPath([new FlxPoint(148 + this.sprMap.x,387 + this.sprMap.y),new FlxPoint(337 + this.sprMap.x,387 + this.sprMap.y),new FlxPoint(394 + this.sprMap.x,393 + this.sprMap.y),new FlxPoint(410 + this.sprMap.x,397 + this.sprMap.y),new FlxPoint(451 + this.sprMap.x,408 + this.sprMap.y),new FlxPoint(507 + this.sprMap.x,423 + this.sprMap.y),new FlxPoint(566 + this.sprMap.x,457 + this.sprMap.y),new FlxPoint(564 + this.sprMap.x,463 + this.sprMap.y),new FlxPoint(565 + this.sprMap.x,470 + this.sprMap.y),new FlxPoint(592 + this.sprMap.x,470 + this.sprMap.y),new FlxPoint(655 + this.sprMap.x,499 + this.sprMap.y),new FlxPoint(736 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath30 = new FlxPath(this.vLightPath29.nodes.concat());
         this.vLightPath30.nodes.reverse();
         this.vLightPath31 = new FlxPath([new FlxPoint(410 + this.sprMap.x,397 + this.sprMap.y),new FlxPoint(477 + this.sprMap.x,440 + this.sprMap.y),new FlxPoint(538 + this.sprMap.x,478 + this.sprMap.y),new FlxPoint(630 + this.sprMap.x,536 + this.sprMap.y)]);
         this.vLightPath32 = new FlxPath(this.vLightPath31.nodes.concat());
         this.vLightPath32.nodes.reverse();
         this.vLightPath33 = new FlxPath([new FlxPoint(148 + this.sprMap.x,502 + this.sprMap.y),new FlxPoint(479 + this.sprMap.x,502 + this.sprMap.y)]);
         this.vLightPath34 = new FlxPath(this.vLightPath33.nodes.concat());
         this.vLightPath34.nodes.reverse();
         this.vLightPath35 = new FlxPath([new FlxPoint(566 + this.sprMap.x,536 + this.sprMap.y),new FlxPoint(566 + this.sprMap.x,527 + this.sprMap.y),new FlxPoint(588 + this.sprMap.x,504 + this.sprMap.y),new FlxPoint(738 + this.sprMap.x,504 + this.sprMap.y)]);
         this.vLightPath36 = new FlxPath(this.vLightPath35.nodes.concat());
         this.vLightPath36.nodes.reverse();
         this.vPads = Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 592,this.sprMap.y + 180),new FlxPoint(this.sprMap.x + 592,this.sprMap.y + 250),new FlxPoint(this.sprMap.x + 652,this.sprMap.y + 180),new FlxPoint(this.sprMap.x + 652,this.sprMap.y + 250),new FlxPoint(this.sprMap.x + 470,this.sprMap.y + 510),new FlxPoint(this.sprMap.x + 262,this.sprMap.y + 480),new FlxPoint(this.sprMap.x + 431,this.sprMap.y + 50),new FlxPoint(this.sprMap.x + 432,this.sprMap.y + 57),new FlxPoint(this.sprMap.x + 246,this.sprMap.y + 223)]);
      }
      
      public function ShowVFX() : void
      {
         this.AddTraffic(this.vLightPath01,20);
         this.AddTraffic(this.vLightPath02,10);
         this.AddTraffic(this.vLightPath03,5);
         this.AddTraffic(this.vLightPath04,3);
         this.AddTraffic(this.vLightPath05,20);
         this.AddTraffic(this.vLightPath06,10);
         this.AddTraffic(this.vLightPath07,10);
         this.AddTraffic(this.vLightPath08,10);
         this.AddTraffic(this.vLightPath09,10);
         this.AddTraffic(this.vLightPath10,10);
         this.AddTraffic(this.vLightPath11,10);
         this.AddTraffic(this.vLightPath12,10);
         this.AddTraffic(this.vLightPath13,10);
         this.AddTraffic(this.vLightPath14,10);
         this.AddTraffic(this.vLightPath15,10);
         this.AddTraffic(this.vLightPath16,10);
         this.AddTraffic(this.vLightPath17,10);
         this.AddTraffic(this.vLightPath18,10);
         this.AddTraffic(this.vLightPath19,10);
         this.AddTraffic(this.vLightPath20,10);
         this.AddTraffic(this.vLightPath21,10);
         this.AddTraffic(this.vLightPath22,10);
         this.AddTraffic(this.vLightPath23,10);
         this.AddTraffic(this.vLightPath24,10);
         this.AddTraffic(this.vLightPath25,3);
         this.AddTraffic(this.vLightPath26,3);
         this.AddTraffic(this.vLightPath27,5);
         this.AddTraffic(this.vLightPath28,5);
         this.AddTraffic(this.vLightPath29,10);
         this.AddTraffic(this.vLightPath30,10);
         this.AddTraffic(this.vLightPath31,5);
         this.AddTraffic(this.vLightPath32,5);
         this.AddTraffic(this.vLightPath33,10);
         this.AddTraffic(this.vLightPath34,10);
         this.AddTraffic(this.vLightPath35,5);
         this.AddTraffic(this.vLightPath36,5);
         var _loc1_:Array = [new FlxPoint(this.sprMap.x,this.sprMap.y),new FlxPoint(57,537)];
         this.AddTwinkle(new FlxPath(_loc1_.concat()),65);
         _loc1_ = [new FlxPoint(this.sprMap.x + 58,this.sprMap.y + 329),new FlxPoint(69,208)];
         this.AddTwinkle(new FlxPath(_loc1_.concat()),30);
         _loc1_ = [new FlxPoint(this.sprMap.x + 56,this.sprMap.y + 270),new FlxPoint(36,35)];
         this.AddTwinkle(new FlxPath(_loc1_.concat()),20);
         this.AddHoverLand(4);
         this.AddCrissCross(10);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 10),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 41),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 69),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 99),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 127),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 158),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 183),new FlxPoint(this.sprMap.x + 60,this.sprMap.y + 211),new FlxPoint(this.sprMap.x + 75,this.sprMap.y + 237),new FlxPoint(this.sprMap.x + 92,this.sprMap.y + 266),new FlxPoint(this.sprMap.x + 94,this.sprMap.y + 271),new FlxPoint(this.sprMap.x + 96,this.sprMap.y + 275),new FlxPoint(this.sprMap.x + 112,this.sprMap.y + 303),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 330),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 358),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 388),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 415),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 446),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 473),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 503),new FlxPoint(this.sprMap.x + 128,this.sprMap.y + 531)]),0.5,1);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 278,this.sprMap.y + 220),new FlxPoint(this.sprMap.x + 306,this.sprMap.y + 247)]),1.6,0);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 409,this.sprMap.y + 260),new FlxPoint(this.sprMap.x + 416,this.sprMap.y + 262),new FlxPoint(this.sprMap.x + 419,this.sprMap.y + 269),new FlxPoint(this.sprMap.x + 417,this.sprMap.y + 277),new FlxPoint(this.sprMap.x + 410,this.sprMap.y + 279),new FlxPoint(this.sprMap.x + 402,this.sprMap.y + 277),new FlxPoint(this.sprMap.x + 400,this.sprMap.y + 270),new FlxPoint(this.sprMap.x + 402,this.sprMap.y + 263)]),1.6,1);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 429,this.sprMap.y + 58)]),3,0);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 230,this.sprMap.y + 342)]),1.5,0);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 231,this.sprMap.y + 449),new FlxPoint(this.sprMap.x + 229,this.sprMap.y + 492),new FlxPoint(this.sprMap.x + 277,this.sprMap.y + 498),new FlxPoint(this.sprMap.x + 297,this.sprMap.y + 458),new FlxPoint(this.sprMap.x + 332,this.sprMap.y + 470),new FlxPoint(this.sprMap.x + 334,this.sprMap.y + 497)]),2,0);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 561,this.sprMap.y + 373)]),1.4,0);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 584,this.sprMap.y + 180),new FlxPoint(this.sprMap.x + 592,this.sprMap.y + 172),new FlxPoint(this.sprMap.x + 652,this.sprMap.y + 172),new FlxPoint(this.sprMap.x + 660,this.sprMap.y + 180),new FlxPoint(this.sprMap.x + 660,this.sprMap.y + 249),new FlxPoint(this.sprMap.x + 652,this.sprMap.y + 257),new FlxPoint(this.sprMap.x + 592,this.sprMap.y + 257),new FlxPoint(this.sprMap.x + 584,this.sprMap.y + 249)]),0.8,1);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 594,this.sprMap.y + 190),new FlxPoint(this.sprMap.x + 602,this.sprMap.y + 182),new FlxPoint(this.sprMap.x + 650,this.sprMap.y + 190),new FlxPoint(this.sprMap.x + 650,this.sprMap.y + 239),new FlxPoint(this.sprMap.x + 641,this.sprMap.y + 247),new FlxPoint(this.sprMap.x + 602,this.sprMap.y + 247),new FlxPoint(this.sprMap.x + 594,this.sprMap.y + 239),new FlxPoint(this.sprMap.x + 667,this.sprMap.y + 208)]),1.4,1);
         this.AddRedGlow(Vector.<FlxPoint>([new FlxPoint(this.sprMap.x + 619,this.sprMap.y + 212)]),1.8,1);
         setAll("scrollFactor",new FlxPoint(1,1));
         setAll("cameras",[this.camMinimap]);
         this.sprScroll.scrollFactor = new FlxPoint();
         if(this.camMinimap != null)
         {
            this.camMinimap.visible = true;
         }
      }
      
      public function HideVFX() : void
      {
         if(this.camMinimap != null)
         {
            this.camMinimap.visible = false;
         }
         if(this.grpVFXLayer != null)
         {
            this.grpVFXLayer.callAll("destroy");
            this.grpVFXLayer.clear();
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
      }
      
      public function SetRes() : void
      {
         this.ZoomCam();
      }
   }
}

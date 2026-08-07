package
{
   import flash.display.Bitmap;
   import org.flixel.*;
   
   public class WeatherNode extends FlxGroup
   {
       
      
      public var aTemps:Array;
      
      private var aPrecips:Array;
      
      private var aOvercast:Array;
      
      public var objWeatherLast:Weather;
      
      private var aSpecialWeatherTypes:Array;
      
      private var aWarmPrecipTypes:Array;
      
      private var aColdPrecipTypes:Array;
      
      private var aTempTypes:Array;
      
      private var aTempTypeMaxes:Array;
      
      private var aOvercastTypes:Array;
      
      private var aNonOvercastTypes:Array;
      
      public var aMonths:Array;
      
      private var vTimeStrings:Vector.<String>;
      
      private var fFreezingPoint:Number;
      
      private var fPastWeight:Number;
      
      private var fSunrise:Number;
      
      private var fSunset:Number;
      
      public var strWeather:String;
      
      public var m_strPopUpText:String;
      
      public var x:int;
      
      public var y:int;
      
      public var width:int;
      
      public var height:int;
      
      public var grpSky:FlxGroup;
      
      public var sprSkyDay:FlxSprite;
      
      public var sprSkyCloud:FlxSprite;
      
      public var sprSkyNight:FlxSprite;
      
      public var sprSkyRain:FlxSprite;
      
      public var sprSkySnow:FlxSprite;
      
      private var grpVFXLayer:FlxGroup;
      
      private var grpVFX:VFX;
      
      private var bmpVFXRain:Bitmap;
      
      private var strVFXRain:String;
      
      private var bmpVFXSnow:Bitmap;
      
      private var strVFXSnow:String;
      
      private var nRaindrops:int;
      
      private var fRainAngle:Number;
      
      private var ptRain:FlxPoint;
      
      private var nSnowdrops:int;
      
      private var fSnowAngle:Number;
      
      private var ptSnow:FlxPoint;
      
      private var ptTemp:FlxPoint;
      
      private var sndRain:FlxSound;
      
      private var sndDaytime:FlxSound;
      
      private var sndNighttime:FlxSound;
      
      private var sndCity:FlxSound;
      
      public function WeatherNode(param1:FlxGroup)
      {
         super();
         this.grpVFXLayer = param1;
         this.Initialize();
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         this.aTemps = null;
         this.aPrecips = null;
         this.aOvercast = null;
         this.objWeatherLast = DataHandler.DestroyObject(this.objWeatherLast);
         if(this.aSpecialWeatherTypes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aSpecialWeatherTypes.length)
            {
               this.aSpecialWeatherTypes[_loc1_] = null;
               _loc1_++;
            }
            this.aSpecialWeatherTypes = null;
         }
         if(this.aWarmPrecipTypes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aWarmPrecipTypes.length)
            {
               this.aWarmPrecipTypes[_loc1_] = null;
               _loc1_++;
            }
            this.aWarmPrecipTypes = null;
         }
         if(this.aColdPrecipTypes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aColdPrecipTypes.length)
            {
               this.aColdPrecipTypes[_loc1_] = null;
               _loc1_++;
            }
            this.aColdPrecipTypes = null;
         }
         if(this.aTempTypes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aTempTypes.length)
            {
               this.aTempTypes[_loc1_] = null;
               _loc1_++;
            }
            this.aTempTypes = null;
         }
         this.aTempTypeMaxes = null;
         if(this.aOvercastTypes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aOvercastTypes.length)
            {
               this.aOvercastTypes[_loc1_] = null;
               _loc1_++;
            }
            this.aOvercastTypes = null;
         }
         if(this.aNonOvercastTypes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aNonOvercastTypes.length)
            {
               this.aNonOvercastTypes[_loc1_] = null;
               _loc1_++;
            }
            this.aNonOvercastTypes = null;
         }
         if(this.aMonths != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aMonths.length)
            {
               this.aMonths[_loc1_] = null;
               _loc1_++;
            }
            this.aMonths = null;
         }
         if(this.vTimeStrings != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.vTimeStrings.length)
            {
               this.vTimeStrings[_loc1_] = null;
               _loc1_++;
            }
            this.vTimeStrings = null;
         }
         this.strWeather = null;
         this.m_strPopUpText = null;
         this.grpVFXLayer = DataHandler.DestroyObject(this.grpVFXLayer);
         this.grpVFX = DataHandler.DestroyObject(this.grpVFX);
         this.bmpVFXRain = null;
         this.strVFXRain = null;
         this.bmpVFXSnow = null;
         this.strVFXSnow = null;
         this.ptRain = null;
         this.ptSnow = null;
         this.ptTemp = null;
         this.sndRain = DataHandler.DestroyObject(this.sndRain);
         this.sndDaytime = DataHandler.DestroyObject(this.sndDaytime);
         this.sndNighttime = DataHandler.DestroyObject(this.sndNighttime);
         this.sndCity = DataHandler.DestroyObject(this.sndCity);
         this.sprSkyCloud = DataHandler.DestroyObject(this.sprSkyCloud);
         this.sprSkyNight = DataHandler.DestroyObject(this.sprSkyNight);
         this.sprSkyDay = DataHandler.DestroyObject(this.sprSkyDay);
         this.sprSkyRain = DataHandler.DestroyObject(this.sprSkyRain);
         this.sprSkySnow = DataHandler.DestroyObject(this.sprSkySnow);
         this.grpSky = DataHandler.DestroyObject(this.grpSky);
         super.destroy();
      }
      
      private function Initialize() : void
      {
         this.fPastWeight = 0.85;
         this.fFreezingPoint = 32;
         this.strVFXRain = "VFXRain.png";
         this.nRaindrops = 100;
         this.fRainAngle = 24 * 2 * Math.PI / 360;
         this.ptRain = new FlxPoint(-50 * Math.tan(this.fRainAngle),50);
         this.strVFXSnow = "VFXSnow.png";
         this.nSnowdrops = 100;
         this.fSnowAngle = 24 * 2 * Math.PI / 360;
         this.ptSnow = new FlxPoint(-1 * Math.tan(this.fSnowAngle),1 * 1);
         this.ptTemp = new FlxPoint();
         this.sndDaytime = FlxG.loadSound(loopWildernessBirds,1,true);
         this.sndNighttime = FlxG.loadSound(loopCrickets,1,true);
         this.sndRain = FlxG.loadSound(loopRainLight,0.25,true);
         this.sndCity = FlxG.loadSound(loopCityAmbient01,1,true);
         this.aSpecialWeatherTypes = new Array("大风","雷暴","大雾","雹暴","龙卷风");
         this.aWarmPrecipTypes = new Array("下雨","下毛毛细雨","下倾盆大雨");
         this.aColdPrecipTypes = new Array("下冰雹","下雪","下雨夹雪");
         this.aTempTypes = new Array("极寒","上冻","寒冷","凉爽","温暖","炎热","炙热");
         this.aTempTypeMaxes = new Array(-40,5,32,60,75,85,95,105);
         this.aOvercastTypes = new Array("阴天","多云","大部多云");
         this.aNonOvercastTypes = new Array("晴朗","少部多云");
         this.vTimeStrings = Vector.<String>(["黎明","早晨","中午","下午","黄昏","夜晚"]);
         this.aMonths = new Array("一月","二月","三月","四月","五月","六月","七月","八月","九月","十月","十一月","十二月");
         this.aTemps = new Array(DataHandler.GetGameVars()["nTempJulyHigh"],DataHandler.GetGameVars()["nTempJulyLow"],DataHandler.GetGameVars()["nTempJulyVar"],DataHandler.GetGameVars()["nTempJanHigh"],DataHandler.GetGameVars()["nTempJanLow"],DataHandler.GetGameVars()["nTempJanVar"]);
         this.aPrecips = new Array(DataHandler.GetGameVars()["fPrecipChanceJuly"],DataHandler.GetGameVars()["fPrecipChanceJan"],DataHandler.GetGameVars()["fPrecipChanceVar"]);
         this.aOvercast = new Array(DataHandler.GetGameVars()["fCloudChanceJuly"],DataHandler.GetGameVars()["fCloudChanceJan"],DataHandler.GetGameVars()["fCloudChanceVar"]);
         if(this.aTemps[0] == undefined)
         {
            this.aTemps = new Array(80,58,25,25,8,42);
            this.aPrecips = new Array(30,50,35);
            this.aOvercast = new Array(20,80,30);
         }
         this.fSunrise = 6;
         this.fSunset = 19;
         this.grpSky = new FlxGroup();
         this.sprSkyCloud = new FlxSprite();
         this.sprSkyDay = new FlxSprite();
         this.sprSkyNight = new FlxSprite();
         this.sprSkyRain = new FlxSprite();
         this.sprSkySnow = new FlxSprite();
         this.grpSky.add(this.sprSkyDay);
         this.grpSky.add(this.sprSkyNight);
         this.grpSky.add(this.sprSkyCloud);
         this.grpSky.add(this.sprSkyRain);
         this.grpSky.add(this.sprSkySnow);
         add(this.grpSky);
         setAll("scrollFactor",new FlxPoint(0,0));
         setAll("cameras",[FlxG.camera]);
         this.SetRes();
      }
      
      public function Reset() : void
      {
         this.objWeatherLast = null;
      }
      
      public function GetTimeOfDay(param1:Date) : int
      {
         var _loc2_:Number = Number(param1.getHours());
         if(_loc2_ <= this.fSunrise - 2)
         {
            return 5;
         }
         if(_loc2_ <= this.fSunrise + 2)
         {
            return 0;
         }
         if(_loc2_ <= 12)
         {
            return 1;
         }
         if(_loc2_ <= this.fSunset - 3)
         {
            return 2;
         }
         if(_loc2_ <= this.fSunset - 1)
         {
            return 3;
         }
         if(_loc2_ <= this.fSunset + 2)
         {
            return 4;
         }
         return 5;
      }
      
      public function GetTimeOfDayString(param1:Date, param2:Boolean = false) : String
      {
         var _loc3_:String = null;
         var _loc4_:String = null;
         if(param2)
         {
            _loc3_ = "";
            _loc4_ = param1.getHours().toString();
            if(param1.getHours() < 10)
            {
               _loc4_ = "0" + _loc4_;
            }
            _loc3_ += _loc4_ + ":";
            _loc4_ = param1.getMinutes().toString();
            if(param1.getMinutes() < 10)
            {
               _loc4_ = "0" + _loc4_;
            }
            _loc3_ += _loc4_ + "\n";
            return _loc3_ + (this.aMonths[param1.getMonth()] + " " + param1.getDate());
         }
         return this.vTimeStrings[this.GetTimeOfDay(param1)];
      }
      
      public function GetTemperatureString(param1:Date, param2:Boolean = false) : String
      {
         var _loc4_:int = 0;
         var _loc3_:String = "";
         if(param2)
         {
            return this.objWeatherLast.fTemp.toFixed(2) + "F";
         }
         _loc4_ = 0;
         while(_loc4_ < this.aTempTypes.length)
         {
            if(this.objWeatherLast.fTemp <= this.aTempTypeMaxes[_loc4_])
            {
               break;
            }
            _loc3_ = this.aTempTypes[_loc4_];
            _loc4_++;
         }
         return _loc3_;
      }
      
      public function MoveIcon(param1:FlxPoint) : void
      {
         this.ptTemp = param1;
         this.sprSkyDay.x = this.ptTemp.x;
         this.sprSkyDay.y = this.ptTemp.y;
         this.sprSkyNight.x = this.ptTemp.x;
         this.sprSkyNight.y = this.ptTemp.y;
         this.sprSkyCloud.x = this.ptTemp.x;
         this.sprSkyCloud.y = this.ptTemp.y;
         this.sprSkyRain.x = this.ptTemp.x;
         this.sprSkyRain.y = this.ptTemp.y;
         this.sprSkySnow.x = this.ptTemp.x;
         this.sprSkySnow.y = this.ptTemp.y;
         this.x = this.ptTemp.x;
         this.y = this.ptTemp.y;
         this.width = this.sprSkyCloud.width;
         this.height = this.sprSkyCloud.height;
      }
      
      public function UpdateWeather(param1:Date) : void
      {
         this.sprSkyDay.visible = false;
         this.sprSkyNight.visible = false;
         this.sprSkyCloud.visible = false;
         this.sprSkyRain.visible = false;
         this.sprSkySnow.visible = false;
         var _loc2_:Number = this.GetTemperature(param1);
         var _loc3_:Number = this.GetPrecipChance(param1);
         var _loc4_:Number = this.GetOvercastChance(param1);
         var _loc5_:* = 100 * Math.random() < _loc3_;
         var _loc6_:* = 100 * Math.random() < _loc4_;
         if(this.objWeatherLast != null)
         {
            _loc2_ = (1 - this.fPastWeight) * _loc2_ + this.fPastWeight * this.objWeatherLast.fTemp;
            _loc3_ = (1 - this.fPastWeight) * _loc3_ + this.fPastWeight * this.objWeatherLast.fPrecipChance;
            _loc4_ = (1 - this.fPastWeight) * _loc4_ + this.fPastWeight * this.objWeatherLast.fOvercastChance;
            if(_loc5_ != this.objWeatherLast.bPrecip)
            {
               _loc5_ = 100 * Math.random() < _loc3_;
            }
            if(_loc6_ != this.objWeatherLast.bOvercast)
            {
               _loc6_ = 100 * Math.random() < _loc4_;
            }
         }
         var _loc7_:int = Math.random() * this.aNonOvercastTypes.length;
         this.strWeather = this.aNonOvercastTypes[_loc7_];
         if(_loc5_ || _loc6_)
         {
            _loc6_ = true;
            _loc7_ = Math.random() * this.aOvercastTypes.length;
            this.strWeather = this.aOvercastTypes[_loc7_];
            this.sprSkyCloud.visible = true;
            if(_loc5_)
            {
               if(_loc2_ <= this.fFreezingPoint)
               {
                  _loc7_ = Math.random() * this.aColdPrecipTypes.length;
                  this.strWeather += ", " + this.aColdPrecipTypes[_loc7_];
               }
               else
               {
                  _loc7_ = Math.random() * this.aWarmPrecipTypes.length;
                  this.strWeather += ", " + this.aWarmPrecipTypes[_loc7_];
               }
            }
         }
         var _loc8_:* = "";
         var _loc9_:int = 0;
         while(_loc9_ < this.aTempTypes.length)
         {
            if(_loc2_ <= this.aTempTypeMaxes[_loc9_])
            {
               break;
            }
            _loc8_ = this.aTempTypes[_loc9_] + ", ";
            _loc9_++;
         }
         this.objWeatherLast = new Weather(_loc2_,_loc3_,_loc4_,_loc5_,_loc6_,param1);
         this.strWeather = _loc8_ + this.strWeather;
         var _loc10_:* = this.strWeather.indexOf("Partly") != -1;
         if(!_loc6_ || _loc10_)
         {
            if(_loc10_)
            {
               this.sprSkyCloud.visible = true;
            }
            if(this.GetTimeOfDay(param1) != 5)
            {
               this.sprSkyDay.visible = true;
            }
            else
            {
               this.sprSkyNight.visible = true;
            }
         }
         if(this.grpVFX != null)
         {
            this.RemovePrecip();
         }
         if(_loc5_)
         {
            if(this.strWeather.indexOf("Snow") != -1)
            {
               this.AddSnow();
               this.sprSkySnow.visible = true;
            }
            else
            {
               this.AddRain();
               this.sprSkyRain.visible = true;
            }
         }
         this.m_strPopUpText = this.strWeather;
      }
      
      public function UpdateSounds(param1:Number = -1) : void
      {
         if(param1 >= 0)
         {
            this.sndDaytime.volume = param1 * GUIEscMenu.m_fSoundVolume;
            this.sndNighttime.volume = param1 * GUIEscMenu.m_fSoundVolume;
            this.sndCity.volume = param1 * GUIEscMenu.m_fSoundVolume;
            this.sndRain.volume = param1 * 0.25 * GUIEscMenu.m_fSoundVolume;
         }
         if(PlayState.m_objInstance.tilCurrentHex != null && PlayState.m_objInstance.tilCurrentHex.index == 20)
         {
            if(this.sndDaytime.active)
            {
               this.sndDaytime.fadeOut(0.5);
            }
            if(this.sndNighttime.active)
            {
               this.sndNighttime.fadeOut(0.5);
            }
            if(!this.sndCity.active)
            {
               this.sndCity.fadeIn(0.5);
            }
         }
         else if(this.GetTimeOfDay(this.objWeatherLast.objDate) != this.vTimeStrings.length - 1)
         {
            if(!this.sndDaytime.active)
            {
               this.sndDaytime.fadeIn(0.5);
            }
            if(this.sndNighttime.active)
            {
               this.sndNighttime.fadeOut(0.5);
            }
            if(this.sndCity.active)
            {
               this.sndCity.fadeOut(0.5);
            }
         }
         else
         {
            if(this.sndDaytime.active)
            {
               this.sndDaytime.fadeOut(0.5);
            }
            if(!this.sndNighttime.active)
            {
               this.sndNighttime.fadeIn(0.5);
            }
            if(this.sndCity.active)
            {
               this.sndCity.fadeOut(0.5);
            }
         }
         if(this.objWeatherLast.bPrecip)
         {
            if(!this.sndRain.active)
            {
               this.sndRain.fadeIn(0.5);
            }
         }
         else if(this.sndRain.active)
         {
            this.sndRain.fadeOut(0.5);
         }
      }
      
      public function StopSounds() : void
      {
         if(this.sndDaytime.active)
         {
            this.sndDaytime.fadeOut(0.5);
         }
         if(this.sndNighttime.active)
         {
            this.sndNighttime.fadeOut(0.5);
         }
         if(this.sndCity.active)
         {
            this.sndCity.fadeOut(0.5);
         }
         if(this.sndRain.active)
         {
            this.sndRain.fadeOut(0.5);
         }
      }
      
      private function AddRain() : void
      {
         var _loc3_:Number = NaN;
         var _loc4_:Number = NaN;
         var _loc5_:FlxPoint = null;
         var _loc6_:Number = NaN;
         var _loc7_:FlxSprite = null;
         var _loc1_:Array = new Array();
         var _loc2_:int = 0;
         while(_loc2_ < this.nRaindrops)
         {
            _loc3_ = -this.bmpVFXRain.height;
            _loc4_ = FlxG.width + Math.tan(this.fRainAngle) * FlxG.height + this.bmpVFXRain.width;
            _loc5_ = new FlxPoint(Math.random() * _loc4_,_loc3_);
            _loc6_ = 0.25 + 0.5 * _loc2_ / this.nRaindrops;
            (_loc7_ = new FlxSprite(_loc5_.x,_loc5_.y,null)).pixels = this.bmpVFXRain.bitmapData;
            _loc7_.alpha = _loc6_;
            _loc1_.push(_loc7_);
            _loc2_++;
         }
         this.grpVFX = new VFX(_loc1_,null,this.VFXRainPerFrame,this.RemovePrecip);
         this.grpVFXLayer.add(this.grpVFX);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[FlxG.camera]);
      }
      
      private function RemovePrecip() : void
      {
         this.grpVFXLayer.remove(this.grpVFX);
         this.grpVFX = null;
      }
      
      private function VFXRainPerFrame(param1:VFX) : void
      {
         var _loc4_:FlxSprite = null;
         var _loc5_:Number = NaN;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc2_:Array = param1.aSprites;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_.length)
         {
            _loc4_ = FlxSprite(_loc2_[_loc3_]);
            _loc5_ = _loc3_ / _loc2_.length;
            if(_loc4_.y > FlxG.height)
            {
               _loc6_ = -_loc4_.height;
               _loc7_ = FlxG.width + Math.tan(this.fRainAngle) * FlxG.height + _loc4_.width;
               this.ptTemp.x = Math.random() * _loc7_;
               this.ptTemp.y = _loc6_;
               _loc4_.x = this.ptTemp.x;
               _loc4_.y = this.ptTemp.y;
            }
            else
            {
               _loc4_.x += this.ptRain.x * (0.25 + 0.5 * _loc5_) * DataHandler.nFPSModifier;
               _loc4_.y += this.ptRain.y * (0.25 + 0.5 * _loc5_) * DataHandler.nFPSModifier;
            }
            _loc3_++;
         }
      }
      
      private function AddSnow() : void
      {
         var _loc4_:Number = NaN;
         var _loc5_:Number = NaN;
         var _loc6_:FlxPoint = null;
         var _loc7_:FlxSprite = null;
         var _loc8_:Number = NaN;
         var _loc1_:Array = new Array();
         var _loc2_:Array = new Array();
         var _loc3_:int = 0;
         while(_loc3_ < this.nSnowdrops)
         {
            _loc2_.push(new Array(0.05 + Math.random() * 0.1,1 + Math.random() * 3,0.1 + Math.random() * 2));
            _loc4_ = Math.random() * 2 * FlxG.height - FlxG.height;
            _loc5_ = FlxG.width + Math.tan(this.fSnowAngle) * FlxG.height + this.bmpVFXSnow.width;
            _loc6_ = new FlxPoint(Math.random() * _loc5_,_loc4_);
            (_loc7_ = new FlxSprite(_loc6_.x,_loc6_.y,null)).pixels = this.bmpVFXSnow.bitmapData;
            _loc8_ = 0.2 + Math.random();
            _loc7_.scale = new FlxPoint(_loc8_,_loc8_);
            _loc7_.alpha = 0.1 + Math.random();
            _loc1_.push(_loc7_);
            _loc3_++;
         }
         this.grpVFX = new VFX(_loc1_,_loc2_,this.VFXSnowPerFrame,this.RemovePrecip);
         this.grpVFXLayer.add(this.grpVFX);
         this.grpVFXLayer.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpVFXLayer.setAll("cameras",[FlxG.camera]);
      }
      
      private function VFXSnowPerFrame(param1:VFX) : void
      {
         var _loc5_:FlxSprite = null;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc2_:Array = param1.aSprites;
         var _loc3_:Array = param1.aParams;
         var _loc4_:int = 0;
         while(_loc4_ < _loc2_.length)
         {
            if((_loc5_ = FlxSprite(_loc2_[_loc4_])).y > FlxG.height)
            {
               _loc6_ = -_loc5_.height;
               _loc7_ = FlxG.width + Math.tan(this.fSnowAngle) * (FlxG.height + _loc6_) + _loc5_.width;
               this.ptTemp.x = Math.random() * _loc7_;
               this.ptTemp.y = _loc6_;
               _loc5_.x = this.ptTemp.x;
               _loc5_.y = this.ptTemp.y;
            }
            else
            {
               this.ptTemp.x = Math.cos(_loc5_.y * _loc3_[_loc4_][0] * DataHandler.nFPSModifier) * _loc3_[_loc4_][2];
               this.ptTemp.y = _loc3_[_loc4_][1] * DataHandler.nFPSModifier;
               _loc5_.x += this.ptTemp.x;
               _loc5_.y += this.ptTemp.y;
            }
            _loc4_++;
         }
      }
      
      public function GetTemperature(param1:Date) : Number
      {
         var _loc2_:Number = this.aTemps[0] - this.aTemps[3];
         var _loc3_:Number = this.aTemps[1] - this.aTemps[4];
         var _loc4_:Number = this.aTemps[2] - this.aTemps[5];
         var _loc5_:Number = Math.cos(2 * Math.PI * (param1.getMonth() / 12));
         var _loc6_:Number = (-_loc2_ * _loc5_ + Math.abs(_loc2_)) / 2 + Math.min(this.aTemps[0],this.aTemps[3]);
         var _loc7_:Number = (-_loc3_ * _loc5_ + Math.abs(_loc3_)) / 2 + Math.min(this.aTemps[1],this.aTemps[4]);
         var _loc8_:Number = (-_loc4_ * _loc5_ + Math.abs(_loc4_)) / 2 + Math.min(this.aTemps[2],this.aTemps[5]);
         var _loc10_:Number;
         var _loc9_:Number;
         return (_loc10_ = (-(_loc9_ = _loc6_ - _loc7_) * Math.cos(2 * Math.PI * (param1.getHours() / 24)) + Math.abs(_loc9_)) / 2 + Math.min(_loc6_,_loc7_)) + (2 * Math.random() - 1) * _loc8_;
      }
      
      public function GetPrecipChance(param1:Date) : Number
      {
         var _loc2_:Number = this.aPrecips[0] - this.aPrecips[1];
         var _loc3_:Number = (-_loc2_ * Math.cos(2 * Math.PI * (param1.getMonth() / 12)) + Math.abs(_loc2_)) / 2 + Math.min(this.aPrecips[0],this.aPrecips[1]);
         return _loc3_ + (2 * Math.random() - 1) * this.aPrecips[2];
      }
      
      public function GetOvercastChance(param1:Date) : Number
      {
         var _loc2_:Number = this.aOvercast[0] - this.aOvercast[1];
         var _loc3_:Number = (-_loc2_ * Math.cos(2 * Math.PI * (param1.getMonth() / 12)) + Math.abs(_loc2_)) / 2 + Math.min(this.aOvercast[0],this.aOvercast[1]);
         return _loc3_ + (2 * Math.random() - 1) * this.aOvercast[2];
      }
      
      public function SetRes() : void
      {
         var _loc1_:String = "";
         if(GUIValues.GetInt("Item.zoom") == 2)
         {
            _loc1_ = DataHandler.m_strZoomPrefix;
         }
         this.sprSkyDay.pixels = DataHandler.GetImage("SkyDay.png",_loc1_);
         this.sprSkyNight.pixels = DataHandler.GetImage("SkyNight.png",_loc1_);
         this.sprSkyCloud.pixels = DataHandler.GetImage("SkyCloud.png",_loc1_);
         this.sprSkyRain.pixels = DataHandler.GetImage("SkyRain.png",_loc1_);
         this.sprSkySnow.pixels = DataHandler.GetImage("SkySnow.png",_loc1_);
         this.ptTemp = GUIValues.GetPoint("WeatherNode");
         this.sprSkyDay.x = this.ptTemp.x;
         this.sprSkyDay.y = this.ptTemp.y;
         this.sprSkyNight.x = this.ptTemp.x;
         this.sprSkyNight.y = this.ptTemp.y;
         this.sprSkyCloud.x = this.ptTemp.x;
         this.sprSkyCloud.y = this.ptTemp.y;
         this.sprSkyRain.x = this.ptTemp.x;
         this.sprSkyRain.y = this.ptTemp.y;
         this.sprSkySnow.x = this.ptTemp.x;
         this.sprSkySnow.y = this.ptTemp.y;
         this.x = this.ptTemp.x;
         this.y = this.ptTemp.y;
         this.width = this.sprSkyCloud.width;
         this.height = this.sprSkyCloud.height;
         this.bmpVFXRain = new Bitmap(DataHandler.GetImage(this.strVFXRain));
         this.bmpVFXSnow = new Bitmap(DataHandler.GetImage(this.strVFXSnow));
      }
   }
}

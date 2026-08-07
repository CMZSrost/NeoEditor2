package
{
   import flash.display.BitmapData;
   import flash.events.*;
   import flash.geom.Point;
   import org.flixel.*;
   
   public class GUIInventoryWound extends GUIInventorySlot implements IConditionOwner
   {
       
      
      private var m_vWoundImages:Vector.<BitmapData>;
      
      private var m_vBloodImages:Vector.<BitmapData>;
      
      private var m_vInfectedImages:Vector.<BitmapData>;
      
      private var m_vBluntImages:Vector.<BitmapData>;
      
      private var m_vWoundImageNames:Vector.<String>;
      
      private var m_vBloodImageNames:Vector.<String>;
      
      private var m_vInfectedImageNames:Vector.<String>;
      
      private var m_vBluntImageNames:Vector.<String>;
      
      private var m_vBluntStrings:Vector.<String>;
      
      private var m_vCutStrings:Vector.<String>;
      
      private var m_aBluntConditions:Array;
      
      private var m_aCutConditions:Array;
      
      private var m_aInfectConditions:Array;
      
      private var m_sprPain:FlxSprite;
      
      private var m_strPain:String;
      
      private var m_bmpPain:BitmapData;
      
      public var m_fBluntSeverity:Number;
      
      public var m_fCutSeverity:Number;
      
      public var m_fBluntSeverityOld:Number;
      
      public var m_fCutSeverityOld:Number;
      
      public var m_fInfectRateOld:Number;
      
      public var m_fInfectRate:Number;
      
      public var m_fBleedRate:Number;
      
      public var m_bStaunched:Boolean;
      
      public var m_bSplinted:Boolean;
      
      public var m_fPain:Number;
      
      public var m_bIgnoreImage:Boolean;
      
      public var m_fPainCoeff:Number;
      
      public var m_fFractureMin:Number;
      
      public var m_bHealed:Boolean;
      
      public var m_bVital:Boolean;
      
      public var m_vBluntDescriptors:Vector.<String>;
      
      public var m_vWoundDescriptors:Vector.<String>;
      
      public var m_vPainDescriptors:Vector.<String>;
      
      public var m_vInfectionDescriptors:Vector.<String>;
      
      public var m_vBleedingDescriptors:Vector.<String>;
      
      public var m_fProbability:Number;
      
      public var aCurrentStates:Array;
      
      public var m_fCutArmor:Number;
      
      public var m_fBluntArmor:Number;
      
      public var m_nSlotOverlap:int;
      
      public function GUIInventoryWound(param1:Creature, param2:String, param3:Number, param4:int, param5:FlxPoint, param6:int, param7:Vector.<String>, param8:Vector.<String>, param9:Vector.<String>, param10:Vector.<String>, param11:Vector.<String>, param12:Vector.<String>, param13:String, param14:Boolean, param15:Array, param16:Array, param17:Boolean = false, param18:Number = 1)
      {
         var _loc19_:Array = null;
         var _loc20_:int = 0;
         var _loc21_:PlayerCondition = null;
         super(param1,param2,param4,param5,"blank.png","blank.png",param6,true,param17);
         btnSlot.nMask = 17;
         if(param14)
         {
            this.m_bIgnoreImage = true;
         }
         else
         {
            this.m_bIgnoreImage = false;
            this.m_vWoundImages = new Vector.<BitmapData>();
            this.m_vWoundImageNames = new Vector.<String>();
            this.m_vWoundDescriptors = new Vector.<String>();
            _loc20_ = 0;
            while(_loc20_ < param7.length)
            {
               this.m_vWoundImages.push(DataHandler.GetImage(param7[_loc20_]));
               this.m_vWoundImageNames.push(param7[_loc20_]);
               _loc20_++;
               this.m_vWoundDescriptors.push(param7[_loc20_]);
               _loc20_++;
            }
            this.m_vBloodImages = new Vector.<BitmapData>();
            this.m_vBloodImageNames = new Vector.<String>();
            this.m_vBleedingDescriptors = new Vector.<String>();
            _loc20_ = 0;
            while(_loc20_ < param8.length)
            {
               this.m_vBloodImages.push(DataHandler.GetImage(param8[_loc20_]));
               this.m_vBloodImageNames.push(param8[_loc20_]);
               _loc20_++;
               this.m_vBleedingDescriptors.push(param8[_loc20_]);
               _loc20_++;
            }
            this.m_vInfectedImages = new Vector.<BitmapData>();
            this.m_vInfectedImageNames = new Vector.<String>();
            this.m_vInfectionDescriptors = new Vector.<String>();
            _loc20_ = 0;
            while(_loc20_ < param9.length)
            {
               this.m_vInfectedImages.push(DataHandler.GetImage(param9[_loc20_]));
               this.m_vInfectedImageNames.push(param9[_loc20_]);
               _loc20_++;
               this.m_vInfectionDescriptors.push(param9[_loc20_]);
               _loc20_++;
            }
            this.m_vBluntImages = new Vector.<BitmapData>();
            this.m_vBluntImageNames = new Vector.<String>();
            this.m_vBluntDescriptors = new Vector.<String>();
            _loc20_ = 0;
            while(_loc20_ < param10.length)
            {
               this.m_vBluntImages.push(DataHandler.GetImage(param10[_loc20_]));
               this.m_vBluntImageNames.push(param10[_loc20_]);
               _loc20_++;
               this.m_vBluntDescriptors.push(param10[_loc20_]);
               _loc20_++;
            }
            this.m_sprPain = new FlxSprite(param5.x,param5.y);
            this.m_strPain = param13;
            this.m_bmpPain = DataHandler.GetImage(param13);
            if(bMirrored)
            {
               this.m_sprPain.pixels = ItemInstance.MirrorImage(this.m_bmpPain.clone());
            }
            else
            {
               this.m_sprPain.pixels = this.m_bmpPain.clone();
            }
            this.m_sprPain.alpha = 0;
            add(this.m_sprPain);
         }
         this.m_vPainDescriptors = Vector.<String>(["轻度疼痛\n","中度疼痛\n","重度疼痛\n"]);
         this.m_aBluntConditions = param15.concat();
         this.m_aCutConditions = param16.concat();
         this.m_aInfectConditions = [[0.25,219],[0.5,220],[0.75,221]];
         this.m_vBluntStrings = param11.concat();
         this.m_vCutStrings = param12.concat();
         this.aCurrentStates = new Array();
         this.m_nSlotOverlap = 0;
         for each(_loc19_ in this.m_aBluntConditions)
         {
            if(this.m_bVital)
            {
               break;
            }
            _loc20_ = 1;
            while(_loc20_ < _loc19_.length)
            {
               if((_loc21_ = DataHandler.GetCondition(_loc19_[_loc20_])).bFatal)
               {
                  this.m_bVital = true;
                  break;
               }
               _loc20_++;
            }
         }
         for each(_loc19_ in this.m_aCutConditions)
         {
            if(this.m_bVital)
            {
               break;
            }
            _loc20_ = 1;
            while(_loc20_ < _loc19_.length)
            {
               if((_loc21_ = DataHandler.GetCondition(_loc19_[_loc20_])).bFatal)
               {
                  this.m_bVital = true;
                  break;
               }
               _loc20_++;
            }
         }
         this.m_fInfectRate = 0;
         this.m_fBleedRate = 0;
         this.m_fPain = 0;
         this.m_fCutSeverity = 0;
         this.m_fBluntSeverity = 0;
         this.m_fCutSeverityOld = this.m_fCutSeverity;
         this.m_fBluntSeverityOld = this.m_fBluntSeverity;
         this.m_bStaunched = false;
         this.m_bSplinted = false;
         this.m_fPainCoeff = 0.33;
         this.m_fFractureMin = param18;
         this.m_bHealed = true;
         this.m_fProbability = param3;
         this.m_fCutArmor = 0;
         this.m_fBluntArmor = 0;
      }
      
      override public function destroy() : void
      {
         var _loc1_:BitmapData = null;
         var _loc2_:String = null;
         var _loc3_:int = 0;
         for each(_loc1_ in this.m_vWoundImages)
         {
            _loc1_ = null;
         }
         this.m_vWoundImages = null;
         for each(_loc1_ in this.m_vBloodImages)
         {
            _loc1_ = null;
         }
         this.m_vBloodImages = null;
         for each(_loc1_ in this.m_vInfectedImages)
         {
            _loc1_ = null;
         }
         this.m_vInfectedImages = null;
         for each(_loc1_ in this.m_vBluntImages)
         {
            _loc1_ = null;
         }
         this.m_vBluntImages = null;
         for each(_loc2_ in this.m_vWoundImageNames)
         {
            _loc2_ = null;
         }
         this.m_vWoundImageNames = null;
         for each(_loc2_ in this.m_vBloodImageNames)
         {
            _loc2_ = null;
         }
         this.m_vBloodImageNames = null;
         for each(_loc2_ in this.m_vInfectedImageNames)
         {
            _loc2_ = null;
         }
         this.m_vInfectedImageNames = null;
         for each(_loc2_ in this.m_vBluntImageNames)
         {
            _loc2_ = null;
         }
         this.m_vBluntImageNames = null;
         for each(_loc2_ in this.m_vBluntStrings)
         {
            _loc2_ = null;
         }
         this.m_vBluntStrings = null;
         for each(_loc2_ in this.m_vCutStrings)
         {
            _loc2_ = null;
         }
         this.m_vCutStrings = null;
         if(this.m_aBluntConditions != null)
         {
            _loc3_ = 0;
            while(_loc3_ < this.m_aBluntConditions.length)
            {
               this.m_aBluntConditions[_loc3_] = null;
               _loc3_++;
            }
            this.m_aBluntConditions = null;
         }
         if(this.m_aCutConditions != null)
         {
            _loc3_ = 0;
            while(_loc3_ < this.m_aCutConditions.length)
            {
               this.m_aCutConditions[_loc3_] = null;
               _loc3_++;
            }
            this.m_aCutConditions = null;
         }
         if(this.m_aInfectConditions != null)
         {
            _loc3_ = 0;
            while(_loc3_ < this.m_aInfectConditions.length)
            {
               this.m_aInfectConditions[_loc3_] = null;
               _loc3_++;
            }
            this.m_aInfectConditions = null;
         }
         this.m_sprPain = DataHandler.DestroyObject(this.m_sprPain);
         this.m_strPain = null;
         this.m_bmpPain = null;
         for each(_loc2_ in this.m_vBluntDescriptors)
         {
            _loc2_ = null;
         }
         this.m_vBluntDescriptors = null;
         for each(_loc2_ in this.m_vWoundDescriptors)
         {
            _loc2_ = null;
         }
         this.m_vWoundDescriptors = null;
         for each(_loc2_ in this.m_vPainDescriptors)
         {
            _loc2_ = null;
         }
         this.m_vPainDescriptors = null;
         for each(_loc2_ in this.m_vInfectionDescriptors)
         {
            _loc2_ = null;
         }
         this.m_vInfectionDescriptors = null;
         for each(_loc2_ in this.m_vBleedingDescriptors)
         {
            _loc2_ = null;
         }
         this.m_vBleedingDescriptors = null;
         if(this.aCurrentStates != null)
         {
            _loc3_ = 0;
            while(_loc3_ < this.aCurrentStates.length)
            {
               PlayerCondition(this.aCurrentStates[_loc3_]).destroy();
               this.aCurrentStates[_loc3_] = null;
               _loc3_++;
            }
            this.aCurrentStates = null;
         }
         super.destroy();
      }
      
      public function EndTurn(param1:Number, param2:Date) : void
      {
         var _loc4_:PlayerCondition = null;
         var _loc5_:Number = NaN;
         var _loc3_:Array = this.aCurrentStates.concat();
         for each(_loc4_ in _loc3_)
         {
            _loc4_.Update(this,param2);
         }
         _loc5_ = m_sprOwner.HealPerHour;
         if(this.m_fBluntSeverity >= this.m_fFractureMin && !this.m_bSplinted)
         {
            _loc5_ *= DM.Rand(DM.RAND_MID) - 0.5;
         }
         this.m_fInfectRate += this.m_fInfectRate * param1 * this.m_fCutSeverity * 0.1;
         this.m_fCutSeverity -= this.m_fCutSeverity * param1 * m_sprOwner.HealPerHour * (1 - this.m_fInfectRate) * 0.5;
         this.m_fBluntSeverity -= this.m_fBluntSeverity * param1 * _loc5_ * (1 - this.m_fInfectRate) * 0.5;
         if(this.m_fCutSeverity < 0.01)
         {
            this.m_fCutSeverity = 0;
         }
         if(this.m_fBluntSeverity < 0.01)
         {
            this.m_fBluntSeverity = 0;
         }
         if(this.m_fCutSeverity <= 0)
         {
            this.m_fInfectRate = 0;
         }
         if(this.m_fBleedRate >= 0.1)
         {
            this.m_fBleedRate -= 0.5 * this.m_fBleedRate * param1 * m_sprOwner.HealPerHour * 10;
         }
         else
         {
            this.m_fBleedRate = 0;
         }
         var _loc6_:Number = this.m_fPain;
         this.m_fPain = Math.max(this.m_fCutSeverity + this.m_fBluntSeverity + this.m_fInfectRate);
         this.ValidateStats();
         if(!this.m_bStaunched && this.m_fBleedRate > 0)
         {
            m_sprOwner.m_fBloodLeft -= this.m_fBleedRate * param1;
            if(!m_sprOwner.HasCondition(106))
            {
               m_sprOwner.AddCondition(m_sprOwner.GetCondition(106),true,false);
            }
         }
         m_sprOwner.m_fImmuneLeft -= this.m_fInfectRate * this.m_fCutSeverity * param1;
         m_sprOwner.m_fPainLeft += (_loc6_ - this.m_fPain) * this.m_fPainCoeff;
         this.ApplyConditionThresholds();
         if(param1 > 0)
         {
            this.UpdateImage();
         }
      }
      
      public function ApplyConditionThresholds() : void
      {
         var _loc1_:Array = null;
         var _loc2_:Boolean = false;
         var _loc3_:Boolean = false;
         var _loc4_:int = 0;
         var _loc5_:PlayerCondition = null;
         for each(_loc1_ in this.m_aBluntConditions)
         {
            _loc2_ = this.m_fBluntSeverity >= _loc1_[0] && this.m_fBluntSeverityOld < _loc1_[0];
            _loc3_ = this.m_fBluntSeverity < _loc1_[0] && this.m_fBluntSeverityOld >= _loc1_[0];
            _loc4_ = 1;
            while(_loc4_ < _loc1_.length)
            {
               if(_loc2_ && _loc1_[_loc4_] < 0)
               {
                  _loc2_ = false;
                  _loc3_ = true;
               }
               else if(_loc3_ && _loc1_[_loc4_] < 0)
               {
                  _loc2_ = true;
                  _loc3_ = false;
               }
               if(_loc2_)
               {
                  m_sprOwner.AddCondition(m_sprOwner.GetCondition(_loc1_[_loc4_]),true,false);
               }
               else if(_loc3_)
               {
                  if(!m_sprOwner.GetCondition(_loc1_[_loc4_]).bFatal)
                  {
                     m_sprOwner.RemoveCondition(m_sprOwner.GetCondition(_loc1_[_loc4_]));
                  }
               }
               _loc4_++;
            }
         }
         for each(_loc1_ in this.m_aCutConditions)
         {
            _loc2_ = this.m_fCutSeverity >= _loc1_[0] && this.m_fCutSeverityOld < _loc1_[0];
            _loc3_ = this.m_fCutSeverity < _loc1_[0] && this.m_fCutSeverityOld >= _loc1_[0];
            _loc4_ = 1;
            while(_loc4_ < _loc1_.length)
            {
               if(_loc2_ && _loc1_[_loc4_] < 0)
               {
                  _loc2_ = false;
                  _loc3_ = true;
               }
               else if(_loc3_ && _loc1_[_loc4_] < 0)
               {
                  _loc2_ = true;
                  _loc3_ = false;
               }
               if(_loc2_)
               {
                  m_sprOwner.AddCondition(m_sprOwner.GetCondition(_loc1_[_loc4_]),true,false);
               }
               else if(_loc3_)
               {
                  if(!m_sprOwner.GetCondition(_loc1_[_loc4_]).bFatal)
                  {
                     m_sprOwner.RemoveCondition(m_sprOwner.GetCondition(_loc1_[_loc4_]));
                  }
               }
               _loc4_++;
            }
         }
         for each(_loc1_ in this.m_aInfectConditions)
         {
            _loc2_ = this.m_fInfectRate >= _loc1_[0] && this.m_fInfectRateOld < _loc1_[0];
            _loc3_ = this.m_fInfectRate < _loc1_[0] && this.m_fInfectRateOld >= _loc1_[0];
            _loc4_ = 1;
            while(_loc4_ < _loc1_.length)
            {
               if(_loc2_ && _loc1_[_loc4_] < 0)
               {
                  _loc2_ = false;
                  _loc3_ = true;
               }
               else if(_loc3_ && _loc1_[_loc4_] < 0)
               {
                  _loc2_ = true;
                  _loc3_ = false;
               }
               if(_loc2_)
               {
                  _loc5_ = m_sprOwner.GetCondition(_loc1_[_loc4_]);
                  _loc5_.strDesc = _loc5_.strDesc.replace(/<wound>/gi,strName);
                  m_sprOwner.AddCondition(_loc5_,true,false);
                  m_sprOwner.Resting = false;
               }
               else if(_loc3_)
               {
                  if(!(_loc5_ = m_sprOwner.GetCondition(_loc1_[_loc4_])).bFatal)
                  {
                     m_sprOwner.RemoveCondition(_loc5_);
                  }
               }
               _loc4_++;
            }
         }
         this.m_fCutSeverityOld = this.m_fCutSeverity;
         this.m_fBluntSeverityOld = this.m_fBluntSeverity;
         this.m_fInfectRateOld = this.m_fInfectRate;
      }
      
      public function GetCondition(param1:uint) : PlayerCondition
      {
         var _loc2_:PlayerCondition = null;
         for each(_loc2_ in this.aCurrentStates)
         {
            if(_loc2_.m_nID == param1)
            {
               return _loc2_;
            }
         }
         return DataHandler.GetCondition(param1);
      }
      
      public function HasCondition(param1:uint) : Boolean
      {
         var _loc2_:PlayerCondition = null;
         for each(_loc2_ in this.aCurrentStates)
         {
            if(_loc2_.m_nID == param1)
            {
               return true;
            }
         }
         return false;
      }
      
      public function AddCondition(param1:PlayerCondition, param2:Boolean = true, param3:Boolean = true) : void
      {
         if(param1 == null)
         {
            return;
         }
         param1.ApplyConditionEffects(this);
      }
      
      public function RemoveCondition(param1:PlayerCondition) : void
      {
         if(param1 == null)
         {
            return;
         }
         param1.RemoveConditionEffects(this);
      }
      
      public function UpdateImage() : void
      {
         if(this.m_bIgnoreImage)
         {
            return;
         }
         this.m_bHealed = true;
         btnSlot.m_strPopUpText = "";
         var _loc1_:BitmapData = new BitmapData(this.m_vWoundImages[0].width,this.m_vWoundImages[0].height,true,0);
         var _loc2_:uint = uint(this.GetIndex(this.m_fCutSeverity,this.m_vWoundImages.length));
         var _loc3_:String = "";
         if(this.m_fCutSeverity > 0)
         {
            _loc1_.copyPixels(this.m_vWoundImages[_loc2_],this.m_vWoundImages[_loc2_].rect,new Point(),null,null,true);
            _loc3_ = this.m_vWoundDescriptors[this.GetIndex(this.m_fCutSeverity,this.m_vWoundDescriptors.length)];
            if(_loc3_ != "")
            {
               btnSlot.m_strPopUpText += _loc3_;
            }
            this.m_bHealed = false;
         }
         _loc2_ = uint(this.GetIndex(this.m_fBluntSeverity,this.m_vBluntImages.length));
         if(this.m_fBluntSeverity > 0)
         {
            _loc1_.copyPixels(this.m_vBluntImages[_loc2_],this.m_vBluntImages[_loc2_].rect,new Point(),null,null,true);
            _loc3_ = this.m_vBluntDescriptors[this.GetIndex(this.m_fBluntSeverity,this.m_vBluntDescriptors.length)];
            if(_loc3_ != "")
            {
               btnSlot.m_strPopUpText += _loc3_;
            }
            this.m_bHealed = false;
         }
         if(!this.m_bStaunched && this.m_fBleedRate > 0)
         {
            _loc2_ = uint(this.GetIndex(this.m_fBleedRate,this.m_vBloodImages.length));
            _loc1_.copyPixels(this.m_vBloodImages[_loc2_],this.m_vBloodImages[_loc2_].rect,new Point(),null,null,true);
            _loc3_ = this.m_vBleedingDescriptors[this.GetIndex(this.m_fBleedRate,this.m_vBleedingDescriptors.length)];
            if(_loc3_ != "")
            {
               btnSlot.m_strPopUpText += _loc3_;
            }
            this.m_bHealed = false;
         }
         _loc2_ = uint(this.GetIndex(this.m_fInfectRate,this.m_vInfectedImages.length));
         if(this.m_fInfectRate > 0)
         {
            _loc1_.copyPixels(this.m_vInfectedImages[_loc2_],this.m_vInfectedImages[_loc2_].rect,new Point(),null,null,true);
            _loc3_ = this.m_vInfectionDescriptors[this.GetIndex(this.m_fInfectRate,this.m_vInfectionDescriptors.length)];
            if(_loc3_ != "")
            {
               btnSlot.m_strPopUpText += _loc3_;
            }
            this.m_bHealed = false;
         }
         this.m_sprPain.alpha = this.m_fPain;
         if(this.m_fPain > 0)
         {
            _loc3_ = this.m_vPainDescriptors[this.GetIndex(this.m_fPain,this.m_vPainDescriptors.length)];
            if(_loc3_ != "")
            {
               btnSlot.m_strPopUpText += _loc3_;
            }
            this.m_bHealed = false;
         }
         if(bMirrored)
         {
            _loc1_ = ItemInstance.MirrorImage(_loc1_);
         }
         btnSlot.bmpImgOut = _loc1_;
         btnSlot.bmpImgOn = _loc1_;
         btnSlot.bmpImgDown = _loc1_;
         btnSlot.bmpImgOver = _loc1_;
         btnSlot.UpdateImage();
      }
      
      private function GetIndex(param1:Number, param2:int) : int
      {
         var _loc3_:Number = Math.floor(param1 * param2);
         if(_loc3_ >= param2)
         {
            _loc3_ = param2 - 1;
         }
         return _loc3_;
      }
      
      public function Damage(param1:Number, param2:Number, param3:Number, param4:String, param5:String, param6:Boolean = true) : void
      {
         var _loc7_:String = null;
         var _loc8_:* = null;
         var _loc13_:ItemInstance = null;
         if(param1 < 0 || param2 < 0)
         {
            return;
         }
         var _loc9_:String = "未知目标";
         if(PlayState.m_objInstance.sprPlayer.PlayerCanSee(m_sprOwner))
         {
            _loc9_ = m_sprOwner.Name;
         }
         var _loc10_:Number = 0;
         if(this.m_fCutArmor >= 1)
         {
            if((_loc10_ = param3 / this.m_fCutArmor) <= 1)
            {
               param1 += _loc10_ * param2;
               param2 = 0;
            }
            else
            {
               param1 += 1 / _loc10_ * param2;
               param2 -= 1 / _loc10_ * param2;
            }
         }
         if(this.m_nSlotOverlap > 0)
         {
            if((_loc13_ = GUIInventorySlot(m_sprOwner.m_dictSlots[this.m_nSlotOverlap]).SocketedItem()) != null)
            {
               if(_loc10_ > 0)
               {
                  _loc13_.EquipDegrade(_loc10_ * 30);
               }
               if(param1 > 0 && this.m_fBluntArmor > 0)
               {
                  _loc13_.EquipDegrade(Math.min(this.m_fBluntArmor,param1) * 30);
               }
            }
         }
         if(this.m_fBluntArmor > 0)
         {
            param1 -= this.m_fBluntArmor;
         }
         param2 *= m_sprOwner.DefDmgMult;
         param1 *= m_sprOwner.DefDmgMult;
         if(param1 < 0)
         {
            param1 = 0;
         }
         if(param2 < 0)
         {
            param2 = 0;
         }
         this.m_fCutSeverity += param2;
         this.m_fBluntSeverity += param1;
         if(this.m_fCutSeverity < 0)
         {
            this.m_fCutSeverity = 0;
         }
         else if(this.m_fCutSeverity > 1)
         {
            this.m_fCutSeverity = 1;
         }
         if(this.m_fBluntSeverity < 0)
         {
            this.m_fBluntSeverity = 0;
         }
         else if(this.m_fBluntSeverity > 1)
         {
            this.m_fBluntSeverity = 1;
         }
         if(param2 > 0)
         {
            if(param6 && SocketedItem() != null)
            {
               this.UnSocketItem(true);
            }
            this.m_fInfectRate += 0.05 * Math.random();
            if(this.m_fBleedRate < this.m_fCutSeverity)
            {
               this.m_fBleedRate = this.m_fCutSeverity;
            }
         }
         else if(param1 > 0 && this.m_fBluntSeverity > 0.5 && this.m_vCutStrings.length > 0)
         {
            this.m_fBleedRate += this.m_fBluntSeverity - 0.5;
         }
         var _loc11_:Number = this.m_fPain;
         if(this.m_fPain < this.m_fCutSeverity)
         {
            this.m_fPain = this.m_fCutSeverity;
         }
         if(this.m_fPain < this.m_fBluntSeverity)
         {
            this.m_fPain = this.m_fBluntSeverity;
         }
         var _loc12_:String = strName;
         if(param1 <= 0 && param2 <= 0 && this.m_fBluntArmor > 0 && this.m_fCutArmor > 0)
         {
            _loc12_ = "护甲";
            _loc7_ = "几乎不受影响";
         }
         else if(param1 > param2 && this.m_vBluntStrings.length > 0 || this.m_vCutStrings.length == 0)
         {
            _loc7_ = this.m_vBluntStrings[this.GetIndex(param1,this.m_vBluntStrings.length)];
         }
         else
         {
            _loc7_ = this.m_vCutStrings[this.GetIndex(param2,this.m_vCutStrings.length)];
         }
         if(_loc7_ == null)
         {
         }
         if(param4 == "")
         {
            _loc8_ = _loc9_ + "的 " + _loc12_ + " 被 " + _loc7_;
         }
         else
         {
            _loc8_ = param4 + " " + _loc7_ + " " + _loc9_ + "的 " + _loc12_;
         }
         if(param5 != "")
         {
            _loc8_ += " 用 " + param5;
         }
         _loc8_ += ".";
         m_sprOwner.MessageFloaty(_loc8_,false,null,GUIMessageWindow.COLOR_BAD);
         this.ValidateStats();
         this.ApplyConditionThresholds();
         this.UpdateImage();
         m_sprOwner.m_fPainLeft += (_loc11_ - this.m_fPain) * this.m_fPainCoeff;
         if(param2 + param1 >= 0.5)
         {
            m_sprOwner.AddCondition(m_sprOwner.GetCondition(145));
         }
      }
      
      override public function SocketItem(param1:ItemInstance) : ItemInstance
      {
         var _loc4_:int = 0;
         var _loc2_:int = int(param1.StackCount);
         var _loc3_:ItemInstance = super.SocketItem(param1);
         if(_loc3_ != param1)
         {
            if(_loc3_ != null)
            {
               _loc2_ -= _loc3_.StackCount;
            }
            _loc4_ = 0;
            while(_loc4_ < _loc2_)
            {
               UpdateEquipConditions(this,param1);
               _loc4_++;
            }
            this.UpdateImage();
            this.ValidateStats();
         }
         return _loc3_;
      }
      
      override public function UnSocketItem(param1:Boolean = false, param2:ItemInstance = null, param3:Boolean = true) : ItemInstance
      {
         var _loc4_:ItemInstance = SocketedItem();
         if(param2 != null)
         {
            _loc4_ = param2;
         }
         if(_loc4_ == null)
         {
            return null;
         }
         var _loc5_:ItemInstance;
         if((_loc5_ = super.UnSocketItem(param1,param2,param3)) != null)
         {
            UpdateEquipConditions(this,_loc5_,true);
            this.UpdateImage();
            this.ValidateStats();
         }
         return _loc5_;
      }
      
      override public function UseItem(param1:ItemInstance) : void
      {
         var _loc2_:Array = null;
         var _loc3_:PlayerCondition = null;
         for each(_loc2_ in param1.ItemDefinition.m_aUseConditions)
         {
            if(!(_loc2_[0] != "" && _loc2_[0] != nSlotIndex))
            {
               _loc3_ = DataHandler.GetCondition(_loc2_[1]);
               _loc3_.ApplyConditionEffects(this);
            }
         }
         this.UpdateImage();
         this.ValidateStats();
         super.UseItem(param1);
      }
      
      private function ValidateStats() : void
      {
         if(this.m_fInfectRate < 0)
         {
            this.m_fInfectRate = 0;
         }
         else if(this.m_fInfectRate > 1)
         {
            this.m_fInfectRate = 1;
         }
         if(this.m_fBleedRate < 0)
         {
            this.m_fBleedRate = 0;
         }
         else if(this.m_fBleedRate > 1)
         {
            this.m_fBleedRate = 1;
         }
         if(this.m_fPain < 0)
         {
            this.m_fPain = 0;
         }
         else if(this.m_fPain > 1)
         {
            this.m_fPain = 1;
         }
      }
      
      public function get Bandaged() : int
      {
         return 0;
      }
      
      public function set Bandaged(param1:int) : void
      {
         if(param1 == 1)
         {
            this.m_bStaunched = true;
         }
         else
         {
            this.m_bStaunched = false;
         }
      }
      
      public function get Splinted() : int
      {
         return 0;
      }
      
      public function set Splinted(param1:int) : void
      {
         if(param1 == 1)
         {
            this.m_bSplinted = true;
         }
         else
         {
            this.m_bSplinted = false;
         }
      }
      
      public function get Infected() : Number
      {
         return this.m_fInfectRate;
      }
      
      public function set Infected(param1:Number) : void
      {
         if(this.m_fCutSeverity > 0)
         {
            this.m_fInfectRate = param1;
         }
         this.ValidateStats();
      }
      
      public function get Disinfected() : Number
      {
         return 0;
      }
      
      public function set Disinfected(param1:Number) : void
      {
         this.m_fInfectRate *= 1 - param1;
         this.ValidateStats();
      }
      
      public function get ApplyCutDamage() : Number
      {
         return 0;
      }
      
      public function set ApplyCutDamage(param1:Number) : void
      {
         this.Damage(0,param1,0,"","",false);
         m_sprOwner.UpdateStatus();
      }
      
      public function get ApplyBluntDamage() : Number
      {
         return 0;
      }
      
      public function set ApplyBluntDamage(param1:Number) : void
      {
         this.Damage(param1,0,0,"","",false);
         m_sprOwner.UpdateStatus();
      }
      
      override public function IsSlotDepthFree(param1:int) : Boolean
      {
         return vSocketedItems[0].length < m_vStackAllowances[0];
      }
      
      override public function SetRes(param1:Number, param2:String = "", param3:String = "") : void
      {
         var _loc6_:Vector.<ItemInstance> = null;
         var _loc7_:ItemInstance = null;
         if(this.m_bIgnoreImage)
         {
            return;
         }
         var _loc4_:String = "";
         if(param1 == 2)
         {
            _loc4_ = DataHandler.m_strZoomPrefix;
         }
         var _loc5_:int = 0;
         while(_loc5_ < this.m_vWoundImages.length)
         {
            this.m_vWoundImages[_loc5_] = DataHandler.GetImage(this.m_vWoundImageNames[_loc5_],_loc4_);
            _loc5_++;
         }
         _loc5_ = 0;
         while(_loc5_ < this.m_vBloodImages.length)
         {
            this.m_vBloodImages[_loc5_] = DataHandler.GetImage(this.m_vBloodImageNames[_loc5_],_loc4_);
            _loc5_++;
         }
         _loc5_ = 0;
         while(_loc5_ < this.m_vInfectedImages.length)
         {
            this.m_vInfectedImages[_loc5_] = DataHandler.GetImage(this.m_vInfectedImageNames[_loc5_],_loc4_);
            _loc5_++;
         }
         _loc5_ = 0;
         while(_loc5_ < this.m_vBluntImages.length)
         {
            this.m_vBluntImages[_loc5_] = DataHandler.GetImage(this.m_vBluntImageNames[_loc5_],_loc4_);
            _loc5_++;
         }
         GUIValues.SetPosition(this.m_sprPain,param2);
         this.m_bmpPain = DataHandler.GetImage(this.m_strPain,_loc4_);
         if(bMirrored)
         {
            this.m_sprPain.pixels = ItemInstance.MirrorImage(this.m_bmpPain.clone());
         }
         else
         {
            this.m_sprPain.pixels = this.m_bmpPain.clone();
         }
         super.SetRes(param1,param2,param3);
         this.UpdateImage();
         for each(_loc6_ in vSocketedItems)
         {
            for each(_loc7_ in _loc6_)
            {
               AlignImageToSocket(_loc7_);
            }
         }
      }
      
      public function set SaveData(param1:SaveGameWound) : void
      {
         var _loc2_:SaveGameCondition = null;
         var _loc3_:PlayerCondition = null;
         var _loc4_:int = 0;
         for each(_loc2_ in param1.vCurrentStates)
         {
            _loc3_ = this.GetCondition(_loc2_.m_nID);
            _loc3_.m_objDate = new Date();
            _loc3_.m_objDate.setTime(_loc2_.m_fDate);
            _loc4_ = _loc3_.m_nStacked;
            while(_loc4_ < _loc2_.m_nStacked)
            {
               this.AddCondition(_loc3_);
               _loc4_++;
            }
         }
         this.m_fBluntSeverity = param1.m_fBluntSeverity;
         this.m_fCutSeverity = param1.m_fCutSeverity;
         this.m_fBluntSeverityOld = param1.m_fBluntSeverityOld;
         this.m_fCutSeverityOld = param1.m_fCutSeverityOld;
         this.m_fBluntArmor = param1.m_fBluntArmor;
         this.m_fCutArmor = param1.m_fCutArmor;
         this.m_fInfectRate = param1.m_fInfectRate;
         this.m_fBleedRate = param1.m_fBleedRate;
         this.m_bStaunched = param1.m_bStaunched;
         this.m_bSplinted = param1.m_bSplinted;
         this.m_fPain = param1.m_fPain;
         this.UpdateImage();
      }
   }
}

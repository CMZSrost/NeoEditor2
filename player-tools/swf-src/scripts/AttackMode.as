package
{
   import flash.display.BitmapData;
   import org.flixel.FlxG;
   import org.flixel.FlxPoint;
   import org.flixel.FlxSprite;
   
   public class AttackMode
   {
      
      public static const ATTACK_TYPE_MELEE:uint = 0;
      
      public static const ATTACK_TYPE_RANGED:uint = 1;
       
      
      public var m_nID:uint;
      
      public var m_strName:String;
      
      public var m_strIMG:String;
      
      public var m_nRange:uint;
      
      public var m_fDamageCut:Number;
      
      public var m_fDamageBlunt:Number;
      
      public var m_nPenetration:uint;
      
      public var m_nType:uint;
      
      public var m_strSnd:String;
      
      public var m_bTransfer:Boolean;
      
      public var m_vAttackerConditions:Vector.<Vector.<Number>>;
      
      public var m_bmpIMG:BitmapData;
      
      public var m_strChargeIMG:String;
      
      public var m_bmpChargeIMG:BitmapData;
      
      public var m_objItem:ItemInstance;
      
      public var m_fMorale:Number;
      
      public var m_vChargeProfiles:Vector.<ChargeProfile>;
      
      public var m_vChargesUsed:Vector.<ItemInstance>;
      
      public function AttackMode(param1:uint, param2:String, param3:uint, param4:Number, param5:Number, param6:Vector.<ChargeProfile>, param7:uint, param8:uint, param9:String, param10:Boolean, param11:Vector.<Vector.<Number>>, param12:String, param13:Number)
      {
         var _loc14_:Item = null;
         super();
         this.m_nID = param1;
         this.m_strName = param2;
         this.m_nRange = param3;
         this.m_fDamageCut = param4;
         this.m_fDamageBlunt = param5;
         this.m_nPenetration = param7;
         this.m_nType = param8;
         this.m_strSnd = param9;
         this.m_bTransfer = param10;
         this.m_vAttackerConditions = param11.concat();
         this.m_strIMG = param12;
         this.m_bmpIMG = DataHandler.GetImage(this.m_strIMG);
         this.m_fMorale = param13;
         this.m_vChargeProfiles = param6.concat();
         this.m_vChargesUsed = new Vector.<ItemInstance>();
         if(param6.length > 0)
         {
            _loc14_ = DataHandler.GetItemDef(this.m_vChargeProfiles[0].m_strItemID);
            this.m_strChargeIMG = _loc14_.vImageListNames[0];
            this.m_bmpChargeIMG = _loc14_.vImageList[0];
         }
      }
      
      public function IsLoaded() : Boolean
      {
         var _loc2_:Vector.<ItemInstance> = null;
         var _loc3_:ChargeProfile = null;
         var _loc4_:ItemInstance = null;
         if(this.m_vChargeProfiles.length == 0)
         {
            return true;
         }
         var _loc1_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:* = this.m_vChargeProfiles;
         while(true)
         {
            for each(_loc3_ in _loc6_)
            {
               _loc1_ = _loc3_.GetQuantity(1,0,0,false);
               if(_loc1_ != 0)
               {
                  _loc2_ = this.m_objItem.GetCharges(_loc3_.m_strItemID);
                  for each(_loc4_ in _loc2_)
                  {
                     _loc1_ -= _loc4_.StackCount;
                     if(_loc1_ <= 0)
                     {
                        break;
                     }
                  }
                  if(_loc1_ > 0)
                  {
                     break;
                  }
               }
            }
            return true;
         }
         return false;
      }
      
      public function GetWoundLevel(param1:Creature) : FlxPoint
      {
         var _loc2_:Number = 1;
         if(this.m_nType == 0)
         {
            _loc2_ = param1.AttDmgMult;
         }
         return new FlxPoint(DM.Rand(DM.RAND_MID) * this.m_fDamageCut * _loc2_,DM.Rand(DM.RAND_MID) * this.m_fDamageBlunt * _loc2_);
      }
      
      public function ApplyConditions(param1:Creature, param2:Creature) : void
      {
         var _loc3_:Vector.<Number> = null;
         var _loc4_:PlayerCondition = null;
         for each(_loc3_ in this.m_vAttackerConditions)
         {
            if(Math.random() < _loc3_[1])
            {
               if(_loc3_[0] > 0)
               {
                  _loc4_ = param1.GetCondition(_loc3_[0]);
                  param1.AddCondition(_loc4_);
               }
               else
               {
                  param1.RemoveCondition(param1.GetCondition(-_loc3_[0]));
               }
            }
         }
      }
      
      public function ChargesLeft() : uint
      {
         var _loc2_:ChargeProfile = null;
         var _loc3_:int = 0;
         var _loc4_:int = 0;
         var _loc5_:Vector.<ItemInstance> = null;
         var _loc6_:ItemInstance = null;
         var _loc7_:int = 0;
         if(this.m_vChargeProfiles.length == 0)
         {
            return 0;
         }
         var _loc1_:int = -1;
         for each(_loc2_ in this.m_vChargeProfiles)
         {
            _loc3_ = _loc2_.GetQuantity(1,0,0,false);
            _loc4_ = 0;
            if(_loc3_ != 0)
            {
               _loc5_ = this.m_objItem.GetCharges(_loc2_.m_strItemID);
               for each(_loc6_ in _loc5_)
               {
                  _loc4_ += _loc6_.StackCount;
               }
               _loc7_ = Math.floor(_loc4_ / _loc3_);
               if(_loc1_ < 0 || _loc7_ < _loc1_)
               {
                  _loc1_ = _loc7_;
               }
            }
         }
         if(_loc1_ < 0)
         {
            _loc1_ = 0;
         }
         return _loc1_;
      }
      
      public function UseDegrade() : void
      {
         if(this.m_objItem == null)
         {
            return;
         }
         this.m_objItem.UseDegrade();
      }
      
      public function Discharge() : void
      {
         var _loc1_:ChargeProfile = null;
         var _loc2_:int = 0;
         var _loc3_:ItemInstance = null;
         var _loc4_:Creature = null;
         var _loc5_:uint = 0;
         var _loc6_:Vector.<ItemInstance> = null;
         if(this.m_objItem == null)
         {
            return;
         }
         if(this.m_vChargeProfiles.length == 0)
         {
            return;
         }
         for each(_loc1_ in this.m_vChargeProfiles)
         {
            _loc2_ = _loc1_.GetQuantity(1,0,0,false);
            _loc5_ = 0;
            while(_loc5_ < _loc2_)
            {
               if((_loc6_ = this.m_objItem.GetCharges(_loc1_.m_strItemID)).length == 0)
               {
                  break;
               }
               if(this.m_objItem.Slot != null)
               {
                  _loc4_ = this.m_objItem.Slot.m_sprOwner;
               }
               if(this.m_objItem.bSocketed)
               {
                  if(_loc1_.m_strItemID == this.m_objItem.IDString)
                  {
                     if(this.m_objItem.m_vStack.length > 0)
                     {
                        _loc3_ = this.m_objItem.Slot.UnSocketItem(true,this.m_objItem.m_vStack[this.m_objItem.m_vStack.length - 1],false);
                     }
                     else
                     {
                        _loc3_ = this.m_objItem.Slot.UnSocketItem(true,this.m_objItem,false);
                     }
                  }
                  else
                  {
                     _loc3_ = this.m_objItem.Slot.RemoveItem(_loc6_[0],false);
                  }
               }
               else
               {
                  _loc3_ = this.m_objItem.RemoveItem(_loc6_[0],false);
               }
               if(this.m_bTransfer)
               {
                  this.m_vChargesUsed.push(_loc3_);
               }
               _loc5_++;
            }
         }
      }
      
      public function Link(param1:ItemInstance) : void
      {
         this.m_objItem = param1;
         this.m_objItem.m_vAttackModes.push(this);
      }
   }
}

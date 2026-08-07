package
{
   import org.flixel.*;
   
   public class TreasureGroup
   {
       
      
      private var vTreasures:Vector.<Vector.<Treasure>>;
      
      public var m_strName:String;
      
      private var m_bNested:Boolean;
      
      private var m_bSuppress:Boolean;
      
      private var m_bIdentify:Boolean;
      
      public function TreasureGroup(param1:String, param2:Vector.<Vector.<Treasure>>, param3:Boolean, param4:Boolean, param5:Boolean)
      {
         super();
         this.m_strName = param1;
         this.vTreasures = param2;
         this.m_bNested = param3;
         this.m_bSuppress = param4;
         this.m_bIdentify = param5;
      }
      
      public function GenerateTreasure(param1:Boolean = false, param2:Boolean = false, param3:Boolean = false) : Vector.<ItemInstance>
      {
         var _loc5_:Vector.<Treasure> = null;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc8_:Treasure = null;
         var _loc9_:ItemInstance = null;
         var _loc10_:int = 0;
         var _loc11_:int = 0;
         var _loc12_:ItemInstance = null;
         var _loc13_:ItemInstance = null;
         var _loc14_:GUIInventory = null;
         var _loc15_:int = 0;
         var _loc16_:Boolean = false;
         var _loc17_:ItemHardware = null;
         var _loc18_:Boolean = false;
         var _loc4_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         for each(_loc5_ in this.vTreasures)
         {
            _loc6_ = 0;
            _loc7_ = DM.Rand(DM.RAND_FLAT);
            for each(_loc8_ in _loc5_)
            {
               if(param2 && _loc5_.length == 1)
               {
                  _loc6_ += DM.m_fRewardMult * _loc8_.fChance;
               }
               else
               {
                  _loc6_ += _loc8_.fChance;
               }
               if(_loc7_ < _loc6_)
               {
                  _loc9_ = null;
                  _loc10_ = _loc8_.ptAmount.x + Math.round(Math.random() * _loc8_.ptAmount.y);
                  _loc11_ = 0;
                  while(_loc11_ < _loc10_)
                  {
                     if(_loc8_.strItemID.indexOf(".") < 0)
                     {
                        _loc4_ = _loc4_.concat(DataHandler.GetTreasure(int(_loc8_.strItemID)).GenerateTreasure(param1,param2,param3 || this.m_bIdentify));
                     }
                     else if((_loc12_ = DataHandler.GetItem(_loc8_.strItemID,!this.m_bSuppress)) != null)
                     {
                        if(this.m_bSuppress)
                        {
                           _loc12_.vItems.length = 0;
                        }
                        if(param1 && _loc12_.m_bDegrades)
                        {
                           _loc12_.fDurability = Math.random() * _loc12_.fDurability;
                           if(isNaN(_loc12_.fDurability))
                           {
                              _loc12_.fDurability = 1;
                           }
                           for each(_loc13_ in _loc12_.m_vComponents)
                           {
                              if(_loc13_.m_bDegrades)
                              {
                                 _loc13_.fDurability = _loc12_.fDurability;
                              }
                           }
                        }
                        if(this.m_bIdentify || param3)
                        {
                           _loc12_.Identified = true;
                           _loc12_.IdentifyContents();
                        }
                        if(_loc9_ == null)
                        {
                           _loc9_ = _loc12_;
                           _loc4_.push(_loc9_);
                        }
                        else if(_loc9_.StackCount < _loc9_.ItemDefinition.m_nStackLimit)
                        {
                           _loc9_.m_vStack.push(_loc12_);
                        }
                        else
                        {
                           if(_loc9_ != null)
                           {
                              _loc9_.UpdateStackImage();
                           }
                           _loc9_ = _loc12_;
                           _loc4_.push(_loc9_);
                        }
                     }
                     _loc11_++;
                  }
                  if(_loc9_ != null)
                  {
                     _loc9_.UpdateStackImage();
                  }
                  break;
               }
            }
         }
         if(this.m_bNested)
         {
            _loc14_ = PlayState.m_objInstance.grpInventoryUI;
            _loc11_ = int(_loc4_.length - 1);
            while(_loc11_ > 0)
            {
               _loc15_ = _loc11_ - 1;
               while(_loc15_ >= 0)
               {
                  _loc16_ = false;
                  if(_loc4_[_loc15_] is ItemHardware)
                  {
                     _loc18_ = (_loc17_ = ItemHardware(_loc4_[_loc15_])).bSoftwareAccessible;
                     _loc17_.bSoftwareAccessible = true;
                     _loc16_ = true;
                  }
                  _loc4_[_loc11_] = _loc14_.AddItemToCapBox(_loc4_[_loc11_],_loc4_[_loc15_]);
                  if(_loc16_)
                  {
                     _loc17_.CheckSoftwareAccessible();
                  }
                  if(_loc4_[_loc11_] == null)
                  {
                     _loc4_.splice(_loc11_,1);
                     break;
                  }
                  _loc15_--;
               }
               _loc11_--;
            }
         }
         return _loc4_;
      }
   }
}

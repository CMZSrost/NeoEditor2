package
{
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class Recipe
   {
      
      public static const REVERSE_FALSE:uint = 0;
      
      public static const REVERSE_TRUE:uint = 1;
      
      public static const REVERSE_TOOLS:uint = 2;
       
      
      public var m_nID:int;
      
      public var m_strName:String;
      
      public var m_strSecretName:String;
      
      public var m_nOutput:uint;
      
      public var m_nOutputTemp:uint;
      
      public var m_fHours:Number;
      
      private var m_dictTools:Dictionary;
      
      private var m_dictConsumed:Dictionary;
      
      public var m_vDestroyed:Vector.<int>;
      
      public var m_vAlsoTry:Vector.<int>;
      
      public var m_nComplexity:int;
      
      public var m_bIdentify:Boolean;
      
      public var m_bTransferComponents:Boolean;
      
      public var m_bKnown:Boolean;
      
      public var m_nHiddenID:int;
      
      public var m_bOutputTemp:Boolean;
      
      public var m_nReverse:uint;
      
      public var m_strText:String;
      
      public var m_bReverseTemp:Boolean;
      
      public var m_bDegradeOutput:Boolean;
      
      private var m_vTreasure:Vector.<ItemInstance>;
      
      public function Recipe(param1:int, param2:String, param3:String, param4:int, param5:Number, param6:Dictionary, param7:Dictionary, param8:Vector.<int>, param9:uint, param10:Boolean, param11:Boolean, param12:int, param13:Vector.<int>, param14:int, param15:Boolean)
      {
         var _loc16_:Object = null;
         super();
         this.m_nID = param1;
         this.m_nOutput = param4;
         this.m_nOutputTemp = param14;
         this.m_strName = param2;
         this.m_strSecretName = param3;
         this.m_fHours = param5;
         this.m_dictTools = param6;
         this.m_dictConsumed = param7;
         this.m_vDestroyed = param8;
         this.m_vAlsoTry = param13;
         this.m_bKnown = false;
         this.m_bReverseTemp = false;
         this.m_strText = "";
         this.m_nReverse = param9;
         this.m_bIdentify = param10;
         this.m_bTransferComponents = param11;
         this.m_bOutputTemp = this.m_nOutputTemp != 3;
         this.m_nHiddenID = param12;
         this.m_bDegradeOutput = param15;
         for(_loc16_ in this.m_dictTools)
         {
            this.m_nComplexity += this.m_dictTools[_loc16_];
         }
         for(_loc16_ in this.m_dictConsumed)
         {
            this.m_nComplexity += this.m_dictConsumed[_loc16_];
         }
      }
      
      public static function PreSortItems(param1:Vector.<ItemInstance>) : Vector.<ItemInstance>
      {
         var _loc2_:uint = 0;
         while(_loc2_ < param1.length)
         {
            if(param1[_loc2_].m_vStack.length > 0)
            {
               param1 = param1.concat(param1[_loc2_].m_vStack);
            }
            _loc2_++;
         }
         return param1.sort(CompareItemsByPrice);
      }
      
      public static function CompareItemsByPrice(param1:ItemInstance, param2:ItemInstance) : int
      {
         if(param1.m_objProxy == null || param2.m_objProxy == null)
         {
            return 0;
         }
         var _loc3_:Number = param1.m_objProxy.GetTotalValue(false);
         var _loc4_:Number = param2.m_objProxy.GetTotalValue(false);
         if(param1.m_objProxy.bSocketed)
         {
            _loc3_ += 1000;
         }
         if(param2.m_objProxy.bSocketed)
         {
            _loc4_ += 1000;
         }
         if(param1.Slot == null || param1.Slot.nSlotIndex != 205)
         {
            _loc3_ += 1000;
         }
         if(param2.Slot == null || param2.Slot.nSlotIndex != 205)
         {
            _loc4_ += 1000;
         }
         if(param1.m_objProxy.Identified)
         {
            _loc3_ += 10;
         }
         if(param2.m_objProxy.Identified)
         {
            _loc4_ += 10;
         }
         _loc3_ += param1.ItemDefinition.m_nUsefulness * 100;
         _loc4_ += param2.ItemDefinition.m_nUsefulness * 100;
         if(_loc3_ > _loc4_)
         {
            return 1;
         }
         if(_loc3_ < _loc4_)
         {
            return -1;
         }
         return 0;
      }
      
      public function destroy() : void
      {
         this.m_strName = null;
         this.m_strSecretName = null;
         this.m_strText = null;
         this.m_dictTools = null;
         this.m_dictConsumed = null;
         this.m_vDestroyed = null;
         this.m_vAlsoTry = null;
      }
      
      public function Validate(param1:Vector.<ItemInstance>, param2:Boolean, param3:Vector.<ItemInstance> = null, param4:Vector.<ItemInstance> = null) : Vector.<ItemInstance>
      {
         var _loc7_:Vector.<ItemInstance> = null;
         var _loc8_:ItemInstance = null;
         var _loc9_:Ingredient = null;
         var _loc10_:Object = null;
         var _loc11_:Vector.<ItemInstance> = null;
         var _loc12_:Vector.<ItemInstance> = null;
         var _loc13_:Vector.<ItemInstance> = null;
         var _loc14_:int = 0;
         var _loc15_:int = 0;
         var _loc16_:int = 0;
         var _loc17_:ItemInstance = null;
         var _loc18_:ItemInstance = null;
         var _loc19_:ItemInstance = null;
         this.m_bReverseTemp = false;
         var _loc5_:int = this.m_nComplexity;
         if(param3 == null)
         {
            param3 = new Vector.<ItemInstance>();
         }
         if(param4 == null)
         {
            param4 = new Vector.<ItemInstance>();
         }
         var _loc6_:Number = 1;
         for(_loc10_ in this.m_dictConsumed)
         {
            _loc15_ = 0;
            _loc16_ = 0;
            while(_loc16_ < this.m_dictConsumed[_loc10_])
            {
               _loc15_ = _loc15_;
               while(_loc15_ < param1.length)
               {
                  _loc8_ = param1[_loc15_];
                  if((_loc9_ = Ingredient(DataHandler.GetIngredient(int(_loc10_)))).ItemMatches(_loc8_) && param4.indexOf(_loc8_) < 0)
                  {
                     if(_loc6_ > _loc8_.fDurability)
                     {
                        if(isNaN(_loc6_))
                        {
                           _loc6_ = 1;
                        }
                        _loc6_ = _loc8_.fDurability;
                     }
                     param4.push(_loc8_);
                     _loc5_--;
                     _loc15_++;
                     if(_loc5_ <= 0)
                     {
                        if(!param2 && this.m_bOutputTemp)
                        {
                           _loc7_ = DataHandler.GetTreasure(this.m_nOutputTemp).GenerateTreasure(false,false,this.m_bIdentify);
                        }
                        else
                        {
                           _loc7_ = DataHandler.GetTreasure(this.m_nOutput).GenerateTreasure(false,false,this.m_bIdentify);
                        }
                        for each(_loc17_ in _loc7_)
                        {
                           if(Boolean(_loc17_.m_bDegrades) && this.m_bDegradeOutput)
                           {
                              _loc17_.fDurability = _loc6_;
                              for each(_loc18_ in _loc17_.m_vStack)
                              {
                                 _loc18_.fDurability = _loc6_;
                              }
                           }
                        }
                        if(_loc7_.length > 0)
                        {
                           return _loc7_;
                        }
                     }
                     break;
                  }
                  _loc15_++;
               }
               _loc16_++;
            }
         }
         _loc11_ = new Vector.<ItemInstance>();
         _loc12_ = new Vector.<ItemInstance>();
         _loc13_ = new Vector.<ItemInstance>();
         _loc14_ = 0;
         for(_loc10_ in this.m_dictTools)
         {
            _loc15_ = 0;
            if(this.m_nReverse == REVERSE_TOOLS)
            {
               _loc14_ += this.m_dictTools[_loc10_];
            }
            _loc16_ = 0;
            while(_loc16_ < this.m_dictTools[_loc10_])
            {
               _loc15_ = _loc15_;
               while(_loc15_ < param1.length)
               {
                  _loc8_ = param1[_loc15_];
                  if((_loc9_ = Ingredient(DataHandler.GetIngredient(int(_loc10_)))).ItemMatches(_loc8_) && _loc8_.m_objProxy.IsCharged(1,0,0))
                  {
                     _loc5_--;
                     _loc14_--;
                     if((_loc19_ = _loc8_.Clone()).m_vStack.length > 0)
                     {
                        _loc19_.m_vStack.length = 0;
                        _loc19_.UpdateStackImage();
                     }
                     _loc19_.m_objProxy = _loc8_.m_objProxy;
                     _loc19_.Discharge(1,0,0);
                     _loc19_.UseDegrade();
                     if(param3.indexOf(_loc8_) < 0)
                     {
                        param3.push(_loc8_);
                        if(this.m_nReverse == REVERSE_TOOLS && _loc13_.indexOf(_loc19_) < 0)
                        {
                           _loc13_.push(_loc19_);
                        }
                        if(_loc19_.fDurability <= 0)
                        {
                           _loc12_ = _loc19_.GetDegradedItems(1);
                        }
                        else if(_loc11_.indexOf(_loc19_) < 0 && param4.indexOf(_loc8_) < 0)
                        {
                           _loc11_.push(_loc19_);
                        }
                     }
                     _loc15_++;
                     if(_loc5_ <= 0)
                     {
                        if(!param2 && this.m_bOutputTemp)
                        {
                           _loc7_ = DataHandler.GetTreasure(this.m_nOutputTemp).GenerateTreasure(false,false,this.m_bIdentify);
                        }
                        else
                        {
                           _loc7_ = DataHandler.GetTreasure(this.m_nOutput).GenerateTreasure(false,false,this.m_bIdentify);
                        }
                        for each(_loc17_ in _loc7_)
                        {
                           if(Boolean(_loc17_.m_bDegrades) && this.m_bDegradeOutput)
                           {
                              _loc17_.fDurability = _loc6_;
                              for each(_loc18_ in _loc17_.m_vStack)
                              {
                                 _loc18_.fDurability = _loc6_;
                              }
                           }
                        }
                        if(param2)
                        {
                           return _loc7_.concat(_loc12_);
                        }
                        return _loc11_.concat(_loc7_.concat(_loc12_));
                     }
                     break;
                  }
                  _loc15_++;
               }
               _loc16_++;
            }
         }
         if(this.m_vTreasure == null)
         {
            this.m_vTreasure = DataHandler.GetTreasure(this.m_nOutput).GenerateTreasure();
         }
         param4.length = 0;
         if(param2)
         {
            _loc7_ = new Vector.<ItemInstance>();
         }
         else
         {
            _loc7_ = _loc13_;
         }
         if(this.m_nReverse != Recipe.REVERSE_FALSE && _loc14_ <= 0)
         {
            _loc16_ = 0;
            while(_loc16_ < this.m_vTreasure.length)
            {
               _loc15_ = 0;
               while(_loc15_ < param1.length)
               {
                  if(param1[_loc15_].IDString == this.m_vTreasure[_loc16_].IDString)
                  {
                     this.m_bReverseTemp = true;
                     if(param4.indexOf(param1[_loc15_]) < 0)
                     {
                        param4.push(param1[_loc15_]);
                     }
                     for each(_loc8_ in param1[_loc15_].m_vComponents)
                     {
                        _loc7_.push(_loc8_.Clone());
                     }
                     return _loc7_.concat();
                  }
                  _loc15_++;
               }
               _loc16_++;
            }
         }
         return new Vector.<ItemInstance>();
      }
      
      public function get RecipeText() : String
      {
         var _loc1_:Object = null;
         if(this.m_strText != "")
         {
            return this.m_strText;
         }
         for(_loc1_ in this.m_dictTools)
         {
            if(this.m_strText.length > 0)
            {
               this.m_strText += "\n";
            }
            this.m_strText += this.m_dictTools[_loc1_] + "x " + Ingredient(DataHandler.GetIngredient(int(_loc1_))).m_strName;
         }
         for(_loc1_ in this.m_dictConsumed)
         {
            if(this.m_strText.length > 0)
            {
               this.m_strText += "\n";
            }
            this.m_strText += this.m_dictConsumed[_loc1_] + "x " + Ingredient(DataHandler.GetIngredient(int(_loc1_))).m_strName;
         }
         return this.m_strText;
      }
   }
}

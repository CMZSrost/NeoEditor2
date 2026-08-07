package
{
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class PlayerResponse
   {
       
      
      public var m_aItemIDs:Array;
      
      public var m_aItemAmounts:Array;
      
      public var m_aIngredientIDs:Array;
      
      public var m_aIngredientAmounts:Array;
      
      public var m_nItemsLeftToFill:int;
      
      public var m_fComplexity:Number;
      
      public var m_nOutput:uint;
      
      public var m_fChance:Number;
      
      public var m_nChargeUses:int;
      
      public var m_fChargeHours:Number;
      
      public var m_nChargeHexes:int;
      
      public function PlayerResponse(param1:Array, param2:uint, param3:Number, param4:Array)
      {
         var _loc5_:Array = null;
         super();
         this.m_nOutput = param2;
         this.m_aItemIDs = new Array();
         this.m_aItemAmounts = new Array();
         this.m_aIngredientIDs = new Array();
         this.m_aIngredientAmounts = new Array();
         this.m_nItemsLeftToFill = 0;
         this.m_fComplexity = 0;
         this.m_fChance = param3;
         this.m_fChargeHours = 0;
         for each(_loc5_ in param1)
         {
            if(String(_loc5_[0]).indexOf(".") >= 0)
            {
               this.m_aItemIDs.push(_loc5_[0]);
               this.m_aItemAmounts.push(_loc5_[1]);
               this.m_nItemsLeftToFill += _loc5_[1];
               this.m_fComplexity += _loc5_[1];
            }
            else if(String(_loc5_[0]).length > 0)
            {
               this.m_aIngredientIDs.push(_loc5_[0]);
               this.m_aIngredientAmounts.push(_loc5_[1]);
               this.m_fComplexity += _loc5_[1];
            }
         }
         if(param4.length > 0)
         {
            this.m_nChargeUses = param4[0];
            this.m_fChargeHours = param4[1];
            this.m_nChargeHexes = param4[2];
            this.m_fComplexity += param4[0];
            this.m_fComplexity += param4[1];
            this.m_fComplexity += param4[2];
         }
      }
      
      public function destroy() : void
      {
         this.m_aItemIDs = null;
         this.m_aItemAmounts = null;
      }
      
      public function Validate(param1:Creature, param2:Vector.<ItemInstance>, param3:Vector.<ItemInstance> = null) : Boolean
      {
         var _loc8_:int = 0;
         var _loc9_:ItemInstance = null;
         var _loc10_:int = 0;
         var _loc11_:ItemInstance = null;
         var _loc12_:Dictionary = null;
         var _loc13_:Vector.<ItemInstance> = null;
         var _loc14_:Vector.<ItemInstance> = null;
         var _loc15_:Vector.<ItemInstance> = null;
         var _loc16_:int = 0;
         var _loc17_:Recipe = null;
         var _loc4_:int = this.m_nItemsLeftToFill;
         var _loc5_:* = this.m_aIngredientIDs.length == 0;
         if(param3 == null)
         {
            param3 = new Vector.<ItemInstance>();
         }
         var _loc6_:Encounter;
         if(!(_loc6_ = DataHandler.GetEncounter(this.m_nOutput)).PreconditionsOK(param1))
         {
            return false;
         }
         var _loc7_:Vector.<int> = new Vector.<int>();
         for each(_loc8_ in this.m_aItemAmounts)
         {
            _loc7_.push(_loc8_);
         }
         for each(_loc9_ in param2)
         {
            _loc10_ = int(this.m_aItemIDs.indexOf(_loc9_.ItemDefinition.nGroupID + "." + _loc9_.ItemDefinition.nSubgroupID));
            _loc11_ = _loc9_;
            if(_loc9_.m_objProxy != null)
            {
               _loc11_ = _loc9_.m_objProxy;
            }
            if(_loc10_ >= 0 && int(_loc7_[_loc10_]) > 0 && _loc11_.IsCharged(this.m_nChargeUses,this.m_fChargeHours,this.m_nChargeHexes))
            {
               _loc7_[_loc10_] -= _loc9_.StackCount;
               _loc4_ -= _loc9_.StackCount;
               if(param3.indexOf(_loc9_) < 0)
               {
                  param3.push(_loc9_);
               }
            }
         }
         if(_loc5_ == false)
         {
            _loc12_ = new Dictionary();
            _loc13_ = new Vector.<ItemInstance>();
            _loc14_ = new Vector.<ItemInstance>();
            _loc15_ = new Vector.<ItemInstance>();
            _loc16_ = 0;
            while(_loc16_ < this.m_aIngredientIDs.length)
            {
               _loc12_[this.m_aIngredientIDs[_loc16_]] = this.m_aIngredientAmounts[_loc16_];
               for each(_loc9_ in param2)
               {
                  _loc11_ = _loc9_;
                  if(_loc9_.m_objProxy != null)
                  {
                     _loc11_ = _loc9_.m_objProxy;
                  }
                  if(Ingredient(DataHandler.GetIngredient(this.m_aIngredientIDs[_loc16_])).ItemMatches(_loc9_) && _loc11_.IsCharged(this.m_nChargeUses,this.m_fChargeHours,this.m_nChargeHexes))
                  {
                     _loc15_.push(_loc9_);
                  }
               }
               _loc16_++;
            }
            _loc17_ = new Recipe(0,"Encounter Test","",427,0,new Dictionary(),_loc12_,new Vector.<int>(),0,false,false,0,new Vector.<int>(),3,true);
            param2 = Recipe.PreSortItems(param2);
            _loc5_ = _loc17_.Validate(param2,true,_loc13_,_loc14_).length > 0;
            for each(_loc9_ in _loc15_)
            {
               if(param3.indexOf(_loc9_) < 0)
               {
                  param3.push(_loc9_);
               }
            }
         }
         return _loc4_ <= 0 && _loc5_;
      }
      
      public function ToString() : String
      {
         var _loc1_:* = "";
         var _loc2_:uint = 0;
         while(_loc2_ < this.m_aItemIDs.length)
         {
            if(_loc2_ > 0)
            {
               _loc1_ += "+";
            }
            if(this.m_aItemAmounts[_loc2_] > 0)
            {
               _loc1_ += this.m_aItemIDs[_loc2_] + "x" + this.m_aItemAmounts[_loc2_];
            }
            _loc2_++;
         }
         _loc1_ += "=" + this.m_nOutput.toString();
         _loc1_ += "x" + this.m_fChance.toString();
         _loc1_ += "x" + this.m_nChargeUses.toString();
         _loc1_ += "x" + this.m_fChargeHours.toString();
         return _loc1_ + ("x" + this.m_nChargeHexes.toString());
      }
   }
}

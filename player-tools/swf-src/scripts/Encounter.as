package
{
   import flash.display.BitmapData;
   import flash.geom.Point;
   import flash.utils.Dictionary;
   import flash.utils.getTimer;
   import org.flixel.*;
   
   public class Encounter
   {
      
      public static const TYPE_NORMAL:uint = 0;
      
      public static const TYPE_SCAVENGE:uint = 1;
      
      public static const TYPE_COMBAT:uint = 2;
      
      public static const TYPE_HACKING:uint = 3;
       
      
      public var m_nID:uint;
      
      public var m_strName:String;
      
      public var m_strDesc:String;
      
      public var m_strImg:String;
      
      public var m_nItemsID:uint;
      
      public var m_vTreasure:Vector.<ItemInstance>;
      
      public var m_vRemoveTreasure:Vector.<ItemInstance>;
      
      public var m_dictUsedItems:Dictionary;
      
      public var m_nTreasureID:uint;
      
      public var m_nRemoveTreasureID:uint;
      
      public var m_aResponses:Array;
      
      public var m_aMinimapHexes:Array;
      
      public var m_bRemoveCreatures:Boolean;
      
      public var m_aConditions:Array;
      
      public var m_aPreConditions:Array;
      
      public var m_fPrice:Number;
      
      public var m_objSourceCreature:SourceCreature;
      
      public var m_ptCreatureHex:FlxPoint;
      
      public var m_ptTeleport:FlxPoint;
      
      public var m_nTeleportRange:uint;
      
      public var m_ptEditor:FlxPoint;
      
      public var m_strNameBase:String;
      
      public var m_strDescBase:String;
      
      public var m_fAccidentChance:Number;
      
      public var m_fAccidentChanceTemp:Number;
      
      public var m_fLootChance:Number;
      
      public var m_fLootChanceTemp:Number;
      
      public var m_fCreatureChance:Number;
      
      public var m_fCreatureChanceTemp:Number;
      
      public var m_vAccidents:Vector.<int>;
      
      public var m_vBonusAccidents:Vector.<int>;
      
      public var m_vLoot:Vector.<int>;
      
      public var m_vBonusLoot:Vector.<int>;
      
      public var m_bScavenge:Boolean;
      
      public var m_bRemoveUsed:Boolean;
      
      public var m_nType:uint;
      
      public function Encounter(param1:uint, param2:String = "insert name here", param3:String = "insert description here", param4:String = "EncBlank.png", param5:uint = 3, param6:Array = null, param7:Array = null, param8:Array = null, param9:uint = 3, param10:uint = 3, param11:SourceCreature = null, param12:FlxPoint = null, param13:FlxPoint = null, param14:uint = 0, param15:Array = null, param16:Boolean = false, param17:FlxPoint = null, param18:uint = 0, param19:Number = 0, param20:Number = 0, param21:Number = 0, param22:Vector.<int> = null, param23:Vector.<int> = null, param24:Number = 0, param25:Boolean = false)
      {
         super();
         this.m_nID = param1;
         this.m_strName = param2;
         this.m_strDesc = param3;
         this.m_strImg = param4;
         this.m_nItemsID = param5;
         if(param6 == null || param6.length == 0)
         {
            this.m_aResponses = new Array(new PlayerResponse(new Array(),DM.m_nNullEnc,1,new Array()));
         }
         else
         {
            this.m_aResponses = param6;
         }
         if(param7 == null)
         {
            this.m_aConditions = new Array();
            this.m_aConditions.push(1);
         }
         else
         {
            this.m_aConditions = param7;
         }
         if(param8 == null || param8[0] == "")
         {
            this.m_aPreConditions = new Array();
         }
         else
         {
            this.m_aPreConditions = param8;
         }
         this.m_nTreasureID = param9;
         this.m_nRemoveTreasureID = param10;
         if(param11 == null)
         {
            this.m_objSourceCreature = DataHandler.GetCreatureSource(1);
         }
         else
         {
            this.m_objSourceCreature = param11;
         }
         this.m_fPrice = param24;
         if(param12 == null)
         {
            this.m_ptCreatureHex = new FlxPoint();
         }
         else
         {
            this.m_ptCreatureHex = param12;
         }
         if(param13 == null)
         {
            this.m_ptTeleport = new FlxPoint();
         }
         else
         {
            this.m_ptTeleport = param13;
         }
         this.m_nTeleportRange = param14;
         if(param17 == null)
         {
            this.m_ptEditor = new FlxPoint();
         }
         else
         {
            this.m_ptEditor = param17;
         }
         if(param15 == null)
         {
            this.m_aMinimapHexes = new Array(1);
         }
         else
         {
            this.m_aMinimapHexes = param15;
         }
         this.m_bRemoveCreatures = param16;
         this.m_vTreasure = new Vector.<ItemInstance>();
         this.m_vRemoveTreasure = new Vector.<ItemInstance>();
         this.m_dictUsedItems = new Dictionary();
         this.m_strNameBase = this.m_strName;
         this.m_strDescBase = this.m_strDesc;
         this.m_fLootChance = param19;
         this.m_fAccidentChance = param20;
         this.m_fCreatureChance = param21;
         if(param22 == null)
         {
            this.m_vAccidents = new Vector.<int>();
         }
         else
         {
            this.m_vAccidents = param22.concat();
         }
         if(param23 == null)
         {
            this.m_vLoot = new Vector.<int>();
         }
         else
         {
            this.m_vLoot = param23.concat();
         }
         this.m_nType = param18;
         this.m_bRemoveUsed = param25;
      }
      
      public static function MinimapStrToArray(param1:String) : Array
      {
         var _loc4_:Array = null;
         var _loc5_:Array = null;
         var _loc2_:Array = param1.split(",");
         if(_loc2_[0] == "")
         {
            _loc2_ = [];
         }
         var _loc3_:uint = 0;
         while(_loc3_ < _loc2_.length)
         {
            _loc4_ = String(_loc2_[_loc3_]).split("=");
            (_loc5_ = String(_loc4_[0]).split("x"))[0] = int(_loc5_[0]);
            _loc5_[1] = int(_loc5_[1]);
            _loc5_.push(_loc4_[1]);
            if(_loc4_.length > 2)
            {
               _loc5_.push(int(_loc4_[2]));
            }
            else
            {
               _loc5_.push(1);
            }
            _loc2_[_loc3_] = _loc5_;
            _loc3_++;
         }
         return _loc2_;
      }
      
      public function destroy() : void
      {
         var _loc1_:int = 0;
         var _loc2_:Object = null;
         var _loc3_:ItemInstance = null;
         this.m_strName = null;
         this.m_strDesc = null;
         this.m_strImg = null;
         if(this.m_vTreasure != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vTreasure.length)
            {
               if(this.m_vTreasure[_loc1_] != null)
               {
                  this.m_vTreasure[_loc1_].destroy();
                  this.m_vTreasure[_loc1_] = null;
               }
               _loc1_++;
            }
            this.m_vTreasure = null;
         }
         if(this.m_vRemoveTreasure != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vRemoveTreasure.length)
            {
               if(this.m_vRemoveTreasure[_loc1_] != null)
               {
                  this.m_vRemoveTreasure[_loc1_].destroy();
                  this.m_vRemoveTreasure[_loc1_] = null;
               }
               _loc1_++;
            }
            this.m_vRemoveTreasure = null;
         }
         if(this.m_dictUsedItems != null)
         {
            for(_loc2_ in this.m_dictUsedItems)
            {
               _loc3_ = ItemInstance(_loc2_);
               this.m_dictUsedItems[_loc3_] = null;
               _loc3_.destroy();
            }
            this.m_dictUsedItems = null;
         }
         if(this.m_aResponses != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aResponses.length)
            {
               if(this.m_aResponses[_loc1_] != null)
               {
                  PlayerResponse(this.m_aResponses[_loc1_]).destroy();
                  this.m_aResponses[_loc1_] = null;
               }
               _loc1_++;
            }
            this.m_aResponses = null;
         }
         if(this.m_aMinimapHexes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_aMinimapHexes.length)
            {
               if(this.m_aMinimapHexes[_loc1_] != null)
               {
                  (this.m_aMinimapHexes[_loc1_] as Array)[2] = null;
                  this.m_aMinimapHexes[_loc1_] = null;
               }
               _loc1_++;
            }
            this.m_aMinimapHexes = null;
         }
         this.m_aConditions = null;
         this.m_aPreConditions = null;
         this.m_objSourceCreature = DataHandler.DestroyObject(this.m_objSourceCreature);
         this.m_ptCreatureHex = null;
         this.m_ptTeleport = null;
         this.m_ptEditor = null;
         this.m_strNameBase = null;
         this.m_strDescBase = null;
         this.m_vAccidents = null;
         this.m_vBonusAccidents = null;
         this.m_vLoot = null;
         this.m_vBonusLoot = null;
      }
      
      public function PreconditionsOK(param1:Creature) : Boolean
      {
         var _loc5_:int = 0;
         var _loc6_:PlayerCondition = null;
         var _loc7_:int = 0;
         if(this.m_aPreConditions.length == 0 && this.m_fPrice <= 0)
         {
            return true;
         }
         if(this.m_fPrice > 0)
         {
            if(param1 is Player == false)
            {
               return false;
            }
            if(Player(param1).Money < this.m_fPrice)
            {
               return false;
            }
         }
         var _loc2_:Array = param1.aCurrentStates;
         _loc2_.sortOn("m_nID",Array.NUMERIC);
         var _loc3_:Vector.<int> = new Vector.<int>(_loc2_.length);
         var _loc4_:uint = 0;
         while(_loc4_ < _loc2_.length)
         {
            _loc6_ = _loc2_[_loc4_];
            _loc3_[_loc4_] = _loc6_.m_nID;
            _loc4_++;
         }
         for each(_loc5_ in this.m_aPreConditions)
         {
            if(_loc5_ < 0)
            {
               _loc5_ = -_loc5_;
               if((_loc7_ = int(_loc3_.indexOf(_loc5_))) >= 0)
               {
                  return false;
               }
            }
            else if((_loc7_ = int(_loc3_.indexOf(_loc5_))) < 0)
            {
               return false;
            }
         }
         return true;
      }
      
      public function HandleResponse(param1:Creature, param2:Vector.<ItemInstance>, param3:Boolean = false) : Encounter
      {
         var _loc10_:PlayerResponse = null;
         var _loc11_:Number = NaN;
         var _loc12_:int = 0;
         var _loc14_:ItemInstance = null;
         switch(this.m_nType)
         {
            case TYPE_SCAVENGE:
               return this.HandleScavengeResponse(param2,param3);
            case TYPE_COMBAT:
               return this.HandleCombatResponse(param2,param3);
            default:
               var _loc4_:Number = -1;
               var _loc5_:uint = 1;
               var _loc6_:Vector.<PlayerResponse> = new Vector.<PlayerResponse>();
               var _loc7_:Vector.<Vector.<ItemInstance>> = new Vector.<Vector.<ItemInstance>>();
               var _loc8_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
               var _loc9_:Dictionary = new Dictionary();
               for each(_loc10_ in this.m_aResponses)
               {
                  _loc8_.length = 0;
                  if(_loc10_.Validate(param1,param2,_loc8_))
                  {
                     if(_loc10_.m_fComplexity > _loc4_)
                     {
                        _loc6_ = Vector.<PlayerResponse>([_loc10_]);
                        _loc7_ = Vector.<Vector.<ItemInstance>>([_loc8_.concat()]);
                        _loc4_ = _loc10_.m_fComplexity;
                     }
                     else if(_loc10_.m_fComplexity == _loc4_)
                     {
                        _loc6_.push(_loc10_);
                        _loc7_.push(_loc8_.concat());
                     }
                  }
               }
               _loc11_ = 0;
               _loc8_.length = 0;
               _loc12_ = 0;
               while(_loc12_ < _loc6_.length)
               {
                  _loc10_ = _loc6_[_loc12_];
                  if(Math.random() < _loc11_ + _loc10_.m_fChance)
                  {
                     _loc5_ = _loc10_.m_nOutput;
                     _loc8_ = _loc7_[_loc12_];
                     for each(_loc14_ in _loc8_)
                     {
                        if(_loc14_.m_objProxy != null)
                        {
                           _loc9_[_loc14_] = _loc10_;
                        }
                     }
                     break;
                  }
                  _loc11_ += _loc10_.m_fChance;
                  _loc12_++;
               }
               var _loc13_:Encounter;
               (_loc13_ = DataHandler.GetEncounter(_loc5_)).m_vTreasure = Vector.<ItemInstance>(DataHandler.GetTreasure(_loc13_.m_nTreasureID).GenerateTreasure());
               _loc13_.m_vRemoveTreasure = Vector.<ItemInstance>(DataHandler.GetTreasure(_loc13_.m_nRemoveTreasureID).GenerateTreasure());
               _loc13_.m_dictUsedItems = _loc9_;
               if(_loc13_.m_bRemoveUsed)
               {
                  _loc13_.m_vRemoveTreasure = _loc13_.m_vRemoveTreasure.concat(_loc8_);
               }
               for each(_loc14_ in _loc13_.m_vRemoveTreasure)
               {
                  _loc13_.m_vRemoveTreasure = _loc13_.m_vRemoveTreasure.concat(_loc14_.m_vStack);
               }
               return _loc13_;
         }
      }
      
      public function HandleCombatResponse(param1:Vector.<ItemInstance>, param2:Boolean = false) : Encounter
      {
         return GUIBattleScreen.grpInstance.HandleCombatResponse(param1,param2);
      }
      
      public function HandleScavengeResponse(param1:Vector.<ItemInstance>, param2:Boolean = false) : Encounter
      {
         var _loc11_:PlayerResponse = null;
         var _loc12_:Encounter = null;
         var _loc13_:ItemInstance = null;
         var _loc14_:* = false;
         var _loc15_:Boolean = false;
         var _loc16_:int = 0;
         var _loc17_:int = 0;
         var _loc18_:Vector.<int> = null;
         var _loc19_:int = 0;
         var _loc20_:Number = NaN;
         var _loc21_:Vector.<FlxHexTile> = null;
         var _loc22_:Boolean = false;
         var _loc23_:FlxHexTile = null;
         var _loc24_:Creature = null;
         var _loc3_:Encounter = DataHandler.GetEncounter(DM.m_nNullEnc);
         var _loc4_:PlayState;
         var _loc5_:Number = (_loc4_ = PlayState.m_objInstance).tilCurrentHex.m_vLightLevels[_loc4_.nTimeOfDay];
         var _loc6_:Number = 1;
         if(_loc5_ < _loc4_.sprPlayer.MinLightLevel)
         {
            _loc6_ = _loc5_ / _loc4_.sprPlayer.MinLightLevel;
         }
         this.m_fLootChanceTemp = this.m_fLootChance * _loc6_;
         this.m_fAccidentChanceTemp = this.m_fAccidentChance * (2 - _loc6_);
         this.m_fCreatureChanceTemp = this.m_fCreatureChance * (2 - _loc6_);
         this.m_vBonusLoot = new Vector.<int>();
         this.m_vBonusAccidents = new Vector.<int>();
         var _loc7_:Array = new Array();
         var _loc8_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc9_:Vector.<PlayerResponse> = new Vector.<PlayerResponse>();
         var _loc10_:Dictionary = new Dictionary();
         for each(_loc11_ in this.m_aResponses)
         {
            _loc8_.length = 0;
            if(_loc11_.m_nItemsLeftToFill == 0 && _loc11_.m_aIngredientIDs.length == 0)
            {
               _loc3_ = DataHandler.GetEncounter(_loc11_.m_nOutput).Clone();
               _loc3_.m_strImg = this.m_strImg;
            }
            else if(_loc9_.indexOf(_loc11_) < 0 && _loc11_.Validate(PlayState.m_objInstance.sprPlayer,param1,_loc8_))
            {
               _loc9_.push(_loc11_);
               _loc12_ = DataHandler.GetEncounter(_loc11_.m_nOutput);
               for each(_loc13_ in _loc8_)
               {
                  if(_loc13_.m_objProxy != null)
                  {
                     _loc10_[_loc13_] = _loc11_;
                  }
               }
               _loc7_ = _loc7_.concat(_loc12_.m_aConditions);
               if(_loc12_.m_nType != Encounter.TYPE_SCAVENGE)
               {
                  this.m_nType = TYPE_NORMAL;
                  _loc3_ = this.HandleResponse(PlayState.m_objInstance.sprPlayer,_loc8_);
                  this.m_nType = TYPE_SCAVENGE;
                  _loc3_.m_fAccidentChanceTemp = this.m_fAccidentChance * (2 - _loc6_);
                  _loc3_.m_fCreatureChanceTemp = this.m_fCreatureChance * (2 - _loc6_);
                  _loc3_.m_fLootChanceTemp = this.m_fLootChance * _loc6_;
                  _loc3_.m_dictUsedItems = _loc10_;
                  return _loc3_;
               }
               this.m_fLootChanceTemp += _loc12_.m_fLootChance;
               this.m_fAccidentChanceTemp += _loc12_.m_fAccidentChance;
               this.m_fCreatureChanceTemp += _loc12_.m_fCreatureChance;
               this.m_vBonusLoot = this.m_vBonusLoot.concat(_loc12_.m_vLoot);
               this.m_vBonusAccidents = this.m_vBonusAccidents.concat(_loc12_.m_vAccidents);
            }
         }
         this.m_fLootChanceTemp *= _loc4_.tilCurrentHex.m_fDMCCoeff;
         this.m_fCreatureChanceTemp *= _loc4_.tilCurrentHex.m_fDMCCoeff;
         this.RangeCap(this.m_fLootChanceTemp);
         this.RangeCap(this.m_fAccidentChanceTemp);
         this.RangeCap(this.m_fCreatureChanceTemp);
         _loc3_.m_nTreasureID = 3;
         _loc3_.m_vTreasure = Vector.<ItemInstance>([]);
         _loc3_.m_strDesc = _loc3_.m_strDescBase;
         if(param2)
         {
            _loc14_ = (getTimer() - DM.m_fLastReward) * _loc4_.tilCurrentHex.m_fDMCCoeff > DM.m_fRewardThreshold;
            _loc15_ = false;
            if(this.m_vAccidents.length > 0 && this.m_vAccidents[0] == 1)
            {
               _loc15_ = true;
            }
            if(_loc14_)
            {
               _loc3_.m_nTreasureID = this.m_vLoot[0];
               _loc3_.m_vTreasure = DataHandler.GetTreasure(this.m_vLoot[0]).GenerateTreasure(true,_loc14_);
               for each(_loc17_ in this.m_vBonusLoot)
               {
                  _loc3_.m_vTreasure = _loc3_.m_vTreasure.concat(DataHandler.GetTreasure(_loc17_).GenerateTreasure(true,_loc14_));
               }
               DM.m_fLastReward = getTimer();
            }
            else if(!_loc15_ && Math.random() < this.m_fAccidentChanceTemp)
            {
               _loc18_ = this.m_vAccidents.concat(this.m_vBonusAccidents);
               _loc19_ = Math.floor(Math.random() * _loc18_.length);
               _loc3_ = DataHandler.GetEncounter(_loc18_[_loc19_]).Clone();
               DM.m_fLastReward -= 0.5 * 60 * 1000;
            }
            else if((_loc20_ = DM.Rand(DM.RAND_FLAT)) < this.m_fLootChanceTemp)
            {
               _loc3_.m_nTreasureID = this.m_vLoot[0];
               _loc3_.m_vTreasure = DataHandler.GetTreasure(this.m_vLoot[0]).GenerateTreasure(true);
               for each(_loc17_ in this.m_vBonusLoot)
               {
                  _loc3_.m_vTreasure = _loc3_.m_vTreasure.concat(DataHandler.GetTreasure(_loc17_).GenerateTreasure(true));
               }
            }
            else
            {
               DM.m_fLastReward -= 0.5 * 60 * 1000;
            }
            if(_loc3_.m_vTreasure.length > 0)
            {
               _loc3_.m_strDesc = "你发现了一些东西！";
               if(_loc3_.m_vTreasure.length > 1)
               {
                  _loc3_.m_strDesc += "\n(查看物品菜单去看看它是什么)";
               }
               for each(_loc13_ in _loc3_.m_vTreasure)
               {
                  if(_loc13_.ItemDefinition.nGroupID == 12 && _loc4_.sprPlayer.m_tilCurrentHex.m_vCampItems.length < _loc4_.sprPlayer.m_tilCurrentHex.nCampItems)
                  {
                     _loc3_.m_strDesc += "\n\n你找到了一个露营地！";
                     break;
                  }
               }
            }
            else
            {
               if(_loc3_.m_strDesc != "")
               {
                  _loc3_.m_strDesc += "\n\n";
               }
               _loc3_.m_strDesc += "这次什么也没找到。";
            }
            if(Math.random() < this.m_fCreatureChanceTemp)
            {
               _loc21_ = (_loc21_ = (_loc21_ = (_loc21_ = (_loc21_ = Vector.<FlxHexTile>([PlayState.m_objInstance.sprPlayer.m_tilCurrentHex])).concat(MapUtils.GetHexRing(PlayState.m_objInstance.sprPlayer.m_tilCurrentHex.GetHexCoords(),1))).concat(MapUtils.GetHexRing(PlayState.m_objInstance.sprPlayer.m_tilCurrentHex.GetHexCoords(),2))).concat(MapUtils.GetHexRing(PlayState.m_objInstance.sprPlayer.m_tilCurrentHex.GetHexCoords(),3))).concat(MapUtils.GetHexRing(PlayState.m_objInstance.sprPlayer.m_tilCurrentHex.GetHexCoords(),4));
               _loc22_ = false;
               for each(_loc23_ in _loc21_)
               {
                  if(_loc23_ != null)
                  {
                     for each(_loc24_ in _loc23_.m_vOccupants)
                     {
                        if(_loc24_ is AICreature && _loc24_.m_fMoraleHidden >= 0)
                        {
                           AICreature(_loc24_).m_tilHome = PlayState.m_objInstance.sprPlayer.m_tilCurrentHex;
                           _loc22_ = true;
                        }
                     }
                  }
               }
               if(_loc22_ == false)
               {
                  DM.CreateTension();
               }
               _loc3_.m_strDesc += "\n\n你的行动可能警醒了附近的生物！";
            }
            _loc3_.m_aConditions.push(99);
            _loc3_.m_aConditions = _loc3_.m_aConditions.concat(_loc7_);
            if((_loc16_ = int(_loc4_.sprPlayer.m_tilCurrentHex.m_vScavengeItems.indexOf(_loc4_.grpInventoryUI.m_objScavLoc))) >= 0)
            {
               _loc4_.sprPlayer.m_tilCurrentHex.m_vScavengeItems.splice(_loc16_,1);
            }
            MapUtils.tmapHexes.setDirty();
         }
         _loc3_.m_strName = this.m_strName;
         _loc3_.m_fAccidentChanceTemp = this.m_fAccidentChanceTemp;
         _loc3_.m_fCreatureChanceTemp = this.m_fCreatureChanceTemp;
         _loc3_.m_fLootChanceTemp = this.m_fLootChanceTemp;
         _loc3_.m_dictUsedItems = _loc10_;
         return _loc3_;
      }
      
      private function RangeCap(param1:Number) : Number
      {
         if(param1 > 1)
         {
            param1 = 1;
         }
         else if(param1 < 0)
         {
            param1 = 0;
         }
         return param1;
      }
      
      public function Clone() : Encounter
      {
         return new Encounter(this.m_nID,this.m_strName,this.m_strDesc,this.m_strImg,this.m_nItemsID,this.m_aResponses.concat(),this.m_aConditions.concat(),this.m_aPreConditions.concat(),this.m_nTreasureID,this.m_nRemoveTreasureID,this.m_objSourceCreature,new FlxPoint(int(this.m_ptCreatureHex.x),int(this.m_ptCreatureHex.y)),new FlxPoint(int(this.m_ptTeleport.x),int(this.m_ptTeleport.y)),this.m_nTeleportRange,this.m_aMinimapHexes.concat(),this.m_bRemoveCreatures,new FlxPoint(int(this.m_ptEditor.x),int(this.m_ptEditor.y)),this.m_nType,this.m_fLootChance,this.m_fAccidentChance,this.m_fCreatureChance,this.m_vAccidents.concat(),this.m_vLoot.concat(),this.m_fPrice,this.m_bRemoveUsed);
      }
   }
}

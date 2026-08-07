package
{
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class AICreature extends Creature
   {
       
      
      public var m_strNameCopy:String;
      
      public var m_vEncounterIDs:Vector.<int>;
      
      public var m_nTreasureID:uint;
      
      public var m_nCorpseID:uint;
      
      public var m_tilHome:FlxHexTile;
      
      private var m_bHeadHomeSleep:Boolean = false;
      
      private var m_bHeadHomeLoot:Boolean = false;
      
      private var m_tilLastFail:FlxHexTile;
      
      private var m_nLastFailTimer:int = 2;
      
      public var m_strActivity:String;
      
      private var vDirections:Vector.<String>;
      
      public var m_vActivities:Vector.<String>;
      
      public var m_nID:int;
      
      public var grpGroundSlotOrig:GUIInventorySlot;
      
      public var m_vWaypoints:Vector.<Vector.<int>>;
      
      public var m_vEncQueue:Vector.<int>;
      
      public function AICreature(param1:String, param2:String, param3:Vector.<int>, param4:String, param5:uint, param6:uint, param7:uint, param8:Vector.<AttackMode>, param9:int, param10:Vector.<Vector.<Number>>, param11:int, param12:Vector.<String>, param13:Dictionary)
      {
         super(param1,param2,param4,param5,param7,param8,param10,param13);
         this.m_strNameCopy = param1;
         this.m_vEncounterIDs = param3.concat();
         this.m_nTreasureID = param6;
         this.m_nCorpseID = param11;
         this.m_nID = param9;
         this.vDirections = Vector.<String>(["staying put","heading north","heading northeast","heading southeast","heading south","heading southwest","heading northwest"]);
         this.m_vActivities = param12;
         this.m_vWaypoints = new Vector.<Vector.<int>>();
         this.m_vEncQueue = new Vector.<int>();
      }
      
      public static function CompareItemsByPrice(param1:ItemInstance, param2:ItemInstance) : int
      {
         if(param1 == null || param2 == null)
         {
            return 0;
         }
         var _loc3_:Number = param1.GetTotalValue();
         var _loc4_:Number = param2.GetTotalValue();
         if(_loc3_ < _loc4_)
         {
            return 1;
         }
         if(_loc3_ > _loc4_)
         {
            return -1;
         }
         return 0;
      }
      
      override public function Initialize(param1:Vector.<Vector.<Number>> = null, param2:Boolean = true) : void
      {
         var _loc3_:Vector.<int> = null;
         var _loc4_:FlxPoint = null;
         var _loc5_:Vector.<String> = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:ItemInstance = null;
         var _loc10_:int = 0;
         for each(_loc3_ in this.m_vWaypoints)
         {
            _loc3_.length = 0;
         }
         this.m_vWaypoints.length = 0;
         this.m_vEncQueue.length = 0;
         super.Initialize(null,param2);
         _loc4_ = new FlxPoint();
         AddSlot(null,vInvCategories,2,"LEFT SHOE","blank.png","blank.png",_loc4_,20,true,true);
         AddSlot(null,vInvCategories,3,"RIGHT SHOE","blank.png","blank.png",_loc4_,30,true);
         AddSlot(null,vInvCategories,4,"LEGS","blank.png","blank.png",_loc4_,40,true,false,_loc4_);
         AddSlot(null,vInvCategories,5,"LEFT WRIST","blank.png","blank.png",_loc4_,50,true,true);
         AddSlot(null,vInvCategories,6,"RIGHT WRIST","blank.png","blank.png",_loc4_,60,true);
         AddSlot(null,vInvCategories,7,"LEFT HAND","blank.png","blank.png",_loc4_,130,true,true);
         AddSlot(null,vInvCategories,8,"RIGHT HAND","blank.png","blank.png",_loc4_,130,true);
         AddSlot(null,vInvCategories,11,"TORSO","blank.png","blank.png",_loc4_,120,true,false,_loc4_,Vector.<int>([3,1,1,1]));
         AddSlot(null,vInvCategories,12,"BELT","blank.png","blank.png",_loc4_,110,true,false,_loc4_);
         AddSlot(null,vInvCategories,13,"LEFT SHOULDER","blank.png","blank.png",_loc4_,20,true,true,_loc4_);
         AddSlot(null,vInvCategories,14,"RIGHT SHOULDER","blank.png","blank.png",_loc4_,20,true,false,_loc4_);
         AddSlot(null,vInvCategories,17,"HEAD","blank.png","blank.png",_loc4_,170,true,false,null,Vector.<int>([1,1,1]));
         AddSlot(null,vInvCategories,22,"BACKPACK","blank.png","blank.png",_loc4_,10,true,false,_loc4_);
         AddSlot(null,vInvCategories,20,"HOLD IN LEFT HAND","blank.png","blank.png",_loc4_,240,true,true,_loc4_);
         vInvCategories[vInvCategories.length - 1].bHoldSlot = true;
         vInvCategories[vInvCategories.length - 1].m_bAllowStacks = true;
         AddSlot(null,vInvCategories,21,"HOLD IN RIGHT HAND","blank.png","blank.png",_loc4_,240,true,false,_loc4_);
         vInvCategories[vInvCategories.length - 1].bHoldSlot = true;
         vInvCategories[vInvCategories.length - 1].m_bAllowStacks = true;
         AddSlot(null,vInvCategories,23,"NECK #1","blank.png","blank.png",_loc4_,230,true,false,null,Vector.<int>([3]));
         grpGroundSlot = AddSlot(null,null,200,"GROUND","blank.png","blank.png",_loc4_,0,false,false,GUIValues.GetPoint("GUIInventory.grpGroundSlot.Cap"));
         this.grpGroundSlotOrig = grpGroundSlot;
         grpCampSlot = AddSlot(null,null,208,"CAMP","blank.png","blank.png",_loc4_,0,false,false,GUIValues.GetPoint("GUIInventory.grpCampSlot.Cap"));
         AddSlot(null,vInvCategories,207,"VEHICLE","blank.png","blank.png",_loc4_,0,true,false,_loc4_);
         sort("ID",FlxGroup.ASCENDING);
         _loc5_ = new Vector.<String>();
         if(HasCondition(725))
         {
            AddWound(null,104,"（飞机的）机身",1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[1,726]],[[1,726]]);
         }
         else
         {
            AddWound(null,100,"左上臂",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,186]],[[0.95,186]]);
            AddWound(null,111,"右上臂",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,187]],[[0.95,187]],true);
            AddWound(null,101,"头部",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.33,195,145],[0.5,189],[0.66,195,145],[0.9,194]],[[0.9,194]]);
            AddWound(null,102,"右下臂",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,187]],[[0.95,187]]);
            AddWound(null,112,"左下臂",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,186]],[[0.95,186]],true);
            AddWound(null,103,"左臂",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,_loc5_,"",true,[[0.5,189],[0.67,186]],[],false,0.67);
            AddWound(null,113,"右臂",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,_loc5_,"",true,[[0.5,189],[0.67,187]],[],true,0.67);
            AddWound(null,104,"上胸部",0.25,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.4,205,204,198],[0.5,189],[0.67,205,204,198],[0.9,193]],[[0.4,204,198],[0.67,204,198],[0.9,193]]);
            AddWound(null,105,"下胸部",0.35,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.4,205,204,198],[0.5,189],[0.67,205,204,198],[0.9,197]],[[0.4,204,198],[0.67,204,198],[0.9,197]]);
            AddWound(null,106,"上腹部",0.3,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.5,189,204]],[[0.5,204]]);
            AddWound(null,107,"下腹部",0.25,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.5,189,204]],[[0.5,204]]);
            AddWound(null,108,"左大腿",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,190]],[[0.95,190]]);
            AddWound(null,114,"右大腿",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,191]],[[0.95,191]],true);
            AddWound(null,109,"左小腿",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,190]],[[0.95,190]]);
            AddWound(null,115,"右小腿",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,DM.m_vCutVerbs,"",true,[[0.8,191]],[[0.95,191]],true);
            AddWound(null,110,"左腿",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,_loc5_,"",true,[[0.5,189],[0.67,190]],[],false,0.67);
            AddWound(null,116,"右腿",0.1,_loc4_,0,null,null,null,null,DM.m_vBluntVerbs,_loc5_,"",true,[[0.5,189],[0.67,191]],[],true,0.67);
            GUIInventoryWound(m_dictSlots[100]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[111]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[101]).m_nSlotOverlap = 17;
            GUIInventoryWound(m_dictSlots[102]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[112]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[103]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[113]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[104]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[105]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[106]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[107]).m_nSlotOverlap = 11;
            GUIInventoryWound(m_dictSlots[108]).m_nSlotOverlap = 4;
            GUIInventoryWound(m_dictSlots[114]).m_nSlotOverlap = 4;
            GUIInventoryWound(m_dictSlots[109]).m_nSlotOverlap = 4;
            GUIInventoryWound(m_dictSlots[115]).m_nSlotOverlap = 4;
            GUIInventoryWound(m_dictSlots[110]).m_nSlotOverlap = 4;
            GUIInventoryWound(m_dictSlots[116]).m_nSlotOverlap = 4;
         }
         m_vBluntWoundSlots = SortWounds(m_vBluntWoundSlots);
         m_vCutWoundSlots = SortWounds(m_vCutWoundSlots);
         var _loc6_:Vector.<Vector.<Number>> = m_vBaseConditions.concat();
         if(param1 != null)
         {
            _loc6_ = m_vBaseConditions.concat(param1);
         }
         if(_loc6_ != null)
         {
            _loc10_ = 0;
            while(_loc10_ < _loc6_.length)
            {
               if(Math.random() <= _loc6_[_loc10_][1])
               {
                  if(_loc6_[_loc10_][0] > 0)
                  {
                     this.AddCondition(GetCondition(_loc6_[_loc10_][0]));
                  }
                  else
                  {
                     RemoveCondition(GetCondition(_loc6_[_loc10_][0]));
                  }
               }
               _loc10_++;
            }
         }
         var _loc9_:Vector.<ItemInstance> = DataHandler.GetTreasure(this.m_nTreasureID).GenerateTreasure(HasCondition(535));
         _loc10_ = 0;
         while(_loc10_ < _loc9_.length)
         {
            _loc7_ = _loc9_[_loc10_];
            if((_loc8_ = this.TakeItem(_loc7_)) != _loc7_ && _loc8_ != null)
            {
               _loc9_.push(_loc8_);
            }
            else
            {
               DropItem(_loc8_,true,true);
            }
            _loc10_++;
         }
         if(HasCondition(473))
         {
            fSleepDebt = DM.Rand(DM.RAND_LOW) * aRestedStates[2][0];
            if(HasCondition(492))
            {
               fSleepDebt *= 2;
            }
            fFoodDebt = DM.Rand(DM.RAND_LOW) * aHungerStates[2][0];
            fWaterDebt = DM.Rand(DM.RAND_LOW) * aThirstStates[2][0];
            if(Math.random() > 0.95)
            {
               CauseWound(DM.Rand(DM.RAND_LOW),DM.Rand(DM.RAND_LOW),0,"","");
            }
         }
      }
      
      override public function destroy() : void
      {
         var _loc1_:String = null;
         var _loc2_:Vector.<int> = null;
         this.m_strNameCopy = null;
         this.m_tilHome = null;
         this.m_tilLastFail = null;
         this.m_strActivity = null;
         for each(_loc1_ in this.vDirections)
         {
            _loc1_ = null;
         }
         this.vDirections = null;
         this.grpGroundSlotOrig = null;
         if(this.m_vWaypoints != null)
         {
            for each(_loc2_ in this.m_vWaypoints)
            {
               _loc2_.length = 0;
            }
            this.m_vWaypoints.length = 0;
            this.m_vWaypoints = null;
         }
         if(this.m_vEncQueue != null)
         {
            this.m_vEncQueue.length = 0;
            this.m_vEncQueue = null;
         }
         super.destroy();
      }
      
      override public function Spawn(param1:FlxPoint) : void
      {
         super.Spawn(param1);
         this.m_tilHome = m_tilCurrentHex;
         this.Move(false);
      }
      
      public function EquipBestWeapon() : void
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc8_:ItemInstance = null;
         var _loc9_:Array = null;
         var _loc10_:AttackMode = null;
         var _loc11_:Array = null;
         var _loc12_:Vector.<ItemInstance> = null;
         var _loc13_:Boolean = false;
         var _loc14_:ItemInstance = null;
         var _loc15_:GUIInventorySlot = null;
         var _loc16_:ItemInstance = null;
         if(HasCondition(151) == false)
         {
            return;
         }
         var _loc1_:AttackMode = CurrentAttackMode;
         if(CurrentAttackMode.IsLoaded() == false)
         {
            _loc1_ = m_vAttackModes[0];
         }
         var _loc2_:Vector.<ItemInstance> = GetItems();
         var _loc3_:Boolean = false;
         var _loc7_:GUIInventory = PlayState.m_objInstance.grpInventoryUI;
         for each(_loc8_ in _loc2_)
         {
            for each(_loc9_ in _loc8_.ItemDefinition.m_aAttackModes)
            {
               (_loc10_ = DataHandler.GetAttackMode(_loc9_[1])).Link(_loc8_);
               if(!(_loc10_.m_nType == AttackMode.ATTACK_TYPE_RANGED && m_objPair != null && m_objPair.nRange == 0))
               {
                  if(_loc10_.m_fDamageCut + _loc10_.m_fDamageBlunt > _loc1_.m_fDamageCut + _loc1_.m_fDamageBlunt)
                  {
                     if(_loc10_.IsLoaded())
                     {
                        _loc1_ = _loc10_;
                     }
                     else
                     {
                        _loc11_ = _loc10_.m_vChargeProfiles[0].m_strItemID.split(".");
                        if((_loc12_ = GetItems(true,true,true,false,_loc11_[0],_loc11_[1])).length != 0)
                        {
                           _loc6_ = _loc12_[0].Slot;
                           if((_loc4_ = RemoveItem(_loc12_[0],true)) != null)
                           {
                              _loc5_ = _loc7_.AddItemToCapBox(_loc12_[0],_loc8_,null,true);
                              if(_loc12_[0] != _loc5_)
                              {
                                 _loc1_ = _loc10_;
                              }
                              if(_loc5_ != null)
                              {
                                 _loc7_.AddItemToSlot(_loc5_,_loc6_,true);
                              }
                           }
                           else
                           {
                              _loc7_.AddItemToSlot(_loc4_,_loc6_,true);
                           }
                        }
                     }
                  }
               }
            }
         }
         if(_loc1_ != CurrentAttackMode)
         {
            _loc13_ = false;
            if(_loc1_.m_objItem != null)
            {
               _loc14_ = _loc1_.m_objItem;
               for each(_loc9_ in _loc14_.ItemDefinition.m_aAttackModes)
               {
                  if(_loc9_[1] == _loc1_.m_nID)
                  {
                     if(!_loc14_.bSocketed || _loc14_.grpItemPanelSlot == null || _loc14_.grpItemPanelSlot.nSlotIndex != _loc9_[0])
                     {
                        if(!(_loc15_ = m_dictSlots[_loc9_[0]]).IsSlotDepthFree(_loc14_.nSlotDepth))
                        {
                           _loc16_ = _loc15_.SocketedItem(_loc14_.nSlotDepth);
                           _loc5_ = DropItem(_loc16_,false,true);
                           _loc3_ = true;
                           if(_loc5_ != null)
                           {
                              _loc7_.AddItemToSlot(_loc5_,_loc15_,true);
                              break;
                           }
                        }
                        _loc4_ = RemoveItem(_loc14_,true);
                        if(_loc14_ != _loc4_)
                        {
                           break;
                        }
                        if((_loc5_ = _loc15_.SocketItem(_loc14_)) != _loc14_)
                        {
                           for each(_loc10_ in m_vAttackModes)
                           {
                              if(_loc10_.m_nID == _loc1_.m_nID)
                              {
                                 _loc1_ = _loc10_;
                                 break;
                              }
                           }
                           _loc13_ = true;
                           _loc15_.m_bLocked = true;
                        }
                        else
                        {
                           this.TakeItem(_loc14_);
                           if(_loc14_ != _loc5_)
                           {
                              this.TakeItem(_loc5_);
                           }
                           _loc3_ = true;
                        }
                        if(_loc16_ != null)
                        {
                           _loc4_ = grpGroundSlot.RemoveItem(_loc16_,true);
                           if((_loc5_ = this.TakeItem(_loc4_)) == _loc4_)
                           {
                              DropItem(_loc4_,true,true);
                           }
                           _loc3_ = true;
                        }
                        _loc15_.m_bLocked = false;
                     }
                     else
                     {
                        for each(_loc10_ in m_vAttackModes)
                        {
                           if(_loc10_.m_nID == _loc1_.m_nID)
                           {
                              _loc1_ = _loc10_;
                              break;
                           }
                        }
                        _loc13_ = true;
                     }
                     break;
                  }
               }
            }
            else
            {
               _loc13_ = true;
            }
            if(_loc13_)
            {
               ChangeAttackMode(m_vAttackModes.indexOf(_loc1_));
            }
            if(_loc3_)
            {
               m_tilCurrentHex.CalculateValue();
            }
         }
      }
      
      private function TakeItem(param1:ItemInstance) : ItemInstance
      {
         var _loc2_:ItemInstance = null;
         var _loc3_:ItemInstance = null;
         var _loc4_:ItemInstance = null;
         var _loc5_:GUIInventorySlot = null;
         var _loc6_:int = 0;
         if(param1 == null)
         {
            return param1;
         }
         for each(_loc6_ in param1.ItemDefinition.vEquipSlots)
         {
            if((_loc5_ = m_dictSlots[_loc6_]) != null)
            {
               _loc2_ = _loc5_.SocketItem(param1);
               if(param1 != _loc2_)
               {
                  return _loc2_;
               }
               if(!((_loc4_ = _loc5_.SocketedItem(param1.nSlotDepth)) == null || _loc5_.AcceptsItem(param1) == false || param1.GetTotalValue() <= _loc4_.GetTotalValue()))
               {
                  _loc3_ = _loc5_.UnSocketItem(false,_loc4_);
                  if(_loc3_ != null)
                  {
                     _loc2_ = _loc5_.SocketItem(param1);
                     if(param1 != _loc2_)
                     {
                        _loc3_ = this.TakeItem(_loc3_);
                        if(_loc3_ != null)
                        {
                           DropItem(_loc3_,true,true);
                        }
                        return _loc2_;
                     }
                     _loc5_.SocketItem(_loc3_);
                  }
               }
            }
         }
         for each(_loc5_ in vInvCategories)
         {
            _loc2_ = PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(param1,_loc5_,true);
            if(param1 != _loc2_)
            {
               return _loc2_;
            }
         }
         return _loc2_;
      }
      
      override public function EndTurn(param1:Number, param2:Weather) : void
      {
         super.EndTurn(param1,param2);
         --this.m_nLastFailTimer;
      }
      
      override public function AddCondition(param1:PlayerCondition, param2:Boolean = true, param3:Boolean = true) : void
      {
         var _loc4_:CombatPair = null;
         var _loc5_:String = null;
         if(param1 == null || m_vImmunities.indexOf(param1.m_nID) >= 0)
         {
            return;
         }
         super.AddCondition(param1);
         if(param2 && param1.m_bDisplayOther && (param1.m_nStacked < 2 || param1.m_bStackable))
         {
            if((_loc4_ = m_dictCombatPairs[PlayState.m_objInstance.sprPlayer]) != null && _loc4_.UsSpotted)
            {
               _loc5_ = param1.strDesc.replace(/<us>/gi,Name);
               MessageFloaty(_loc5_,param3,null,param1.m_nColor);
            }
         }
      }
      
      private function ExchangeFactionInfo(param1:Creature) : void
      {
         var _loc3_:Object = null;
         var _loc4_:Number = NaN;
         var _loc2_:Dictionary = DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictFactions;
         for(_loc3_ in _loc2_)
         {
            _loc4_ = (GetFactionRep(int(_loc3_)) * m_fLeader + param1.GetFactionRep(int(_loc3_)) * param1.m_fLeader) / (m_fLeader + param1.m_fLeader);
            param1.ChangeFactionRep([int(_loc3_),_loc4_ - param1.GetFactionRep(int(_loc3_))],true,false);
         }
      }
      
      public function GetBattleMove2(param1:Battle) : void
      {
         var _loc12_:CombatPair = null;
         var _loc13_:CombatPair = null;
         var _loc16_:CombatPair = null;
         var _loc17_:Boolean = false;
         var _loc18_:Boolean = false;
         var _loc19_:Boolean = false;
         var _loc20_:Boolean = false;
         var _loc21_:Boolean = false;
         var _loc22_:Boolean = false;
         var _loc23_:Boolean = false;
         var _loc24_:* = null;
         var _loc25_:Number = NaN;
         var _loc26_:Number = NaN;
         var _loc27_:CombatPair = null;
         var _loc28_:Vector.<BattleMove> = null;
         var _loc29_:int = 0;
         m_bSpied = false;
         this.m_strActivity = "?";
         m_bLeader = true;
         if(Alive == false)
         {
            this.m_strActivity = "Dead";
            m_fMovesLeft = 0;
            return;
         }
         if(Asleep)
         {
            Asleep = false;
         }
         if(Asleep)
         {
            this.m_strActivity = "Unconscious";
            m_fMovesLeft = 0;
            m_fOrder = 0.5;
            m_objPair = null;
            m_objMove = null;
            return;
         }
         var _loc2_:AttackMode = CurrentAttackMode;
         this.EquipBestWeapon();
         if(_loc2_ != CurrentAttackMode)
         {
            _loc24_ = Name + " 拿起了 ";
            if(CurrentAttackMode.m_objItem != null)
            {
               _loc24_ += CurrentAttackMode.m_objItem.strDesc;
            }
            else
            {
               _loc24_ += CurrentAttackMode.m_strName;
            }
            _loc24_ += ".";
            MessageFloaty(_loc24_,false);
         }
         m_fOrder = 0.5;
         m_objPair = null;
         m_objMove = null;
         var _loc3_:int = PlayState.m_objInstance.nTimeOfDay;
         var _loc4_:Number = m_tilCurrentHex.m_vLightLevels[_loc3_];
         var _loc5_:Boolean = HasCondition(151);
         var _loc6_:Boolean = HasCondition(109);
         if(_loc4_ + m_fLightLevel < MinLightLevel)
         {
            this.AddCondition(GetCondition(109));
            if(m_fMovesLeft - m_tilCurrentHex.nTerrainCost > 1)
            {
               m_fMovesLeft = 1 + m_tilCurrentHex.nTerrainCost;
            }
         }
         else if(_loc6_)
         {
            RemoveCondition(GetCondition(109));
         }
         var _loc7_:int = param1.NumberOpponents(this);
         var _loc8_:int = 0;
         var _loc9_:Number = (DM.Rand(DM.RAND_MID) - 0.5) * 0.125;
         m_fMoraleSitu = m_fMorale + CurrentAttackMode.m_fMorale;
         m_fMoraleSituHidden = m_fMoraleSitu + m_fMoraleHidden + _loc9_;
         var _loc10_:Number = 0;
         var _loc11_:Number = 0;
         var _loc14_:Boolean = false;
         var _loc15_:Boolean = false;
         if(_loc7_ == param1.NumberCombatants() - 1)
         {
            m_bLeader = false;
         }
         for each(_loc16_ in m_dictCombatPairs)
         {
            if(_loc12_ == null)
            {
               _loc12_ = _loc16_;
            }
            if(_loc16_.sprThem.m_nFaction != m_nFaction)
            {
               _loc12_ = _loc16_;
            }
            if(HasCondition(367))
            {
               if((_loc27_ = m_dictCombatPairs[PlayState.m_objInstance.sprPlayer]) == null)
               {
                  m_objPair = _loc16_;
                  m_objMove = DataHandler.GetBattleMove("90.110");
                  m_fOrder = m_objMove.m_fOrder;
                  return;
               }
               if(PlayState.m_objInstance.sprPlayer.HasCondition(136))
               {
                  Despawn([1],true);
                  PlayState.m_objInstance.sprPlayer.RemoveCondition(PlayState.m_objInstance.sprPlayer.GetCondition(567));
                  _loc7_--;
                  continue;
               }
               if(_loc16_ == _loc27_)
               {
                  m_objPair = _loc16_;
                  _loc11_ = m_fMoraleSituHidden - 1;
                  break;
               }
            }
            else
            {
               if(HasCondition(464) && (_loc16_.sprThem.HasCondition(460) || _loc16_.sprThem.Asleep == false))
               {
                  _loc7_--;
                  continue;
               }
               if(_loc16_.sprThem.m_nFaction == m_nFaction)
               {
                  if(_loc16_.sprThem.Asleep == false)
                  {
                     m_fMoraleSituHidden += 0.5;
                     this.ExchangeFactionInfo(_loc16_.sprThem);
                     if(_loc16_.sprThem.m_fLeader > m_fLeader && _loc16_.sprThem.m_objMove != null)
                     {
                        if(_loc16_.sprThem.m_objMove.m_bPassive)
                        {
                           _loc14_ = true;
                           _loc13_ = m_dictCombatPairs[_loc16_.sprThem.m_objPair.sprThem];
                        }
                        else
                        {
                           _loc15_ = true;
                        }
                        m_bLeader = false;
                     }
                     else
                     {
                        _loc16_.sprThem.m_bLeader = false;
                     }
                  }
                  continue;
               }
               if(GetFactionRep(_loc16_.sprThem.m_nFaction) > 0.5 - DM.Rand(DM.RAND_FLAT))
               {
                  m_fMoraleSituHidden += 0.1;
                  _loc7_--;
                  _loc8_++;
                  if(_loc13_ == null)
                  {
                     _loc13_ = _loc16_;
                  }
                  continue;
               }
            }
            _loc25_ = 0.5;
            _loc26_ = 0.5;
            if(_loc16_.ThemSpotted)
            {
               _loc25_ = _loc16_.sprThem.m_fMorale;
               if(_loc5_)
               {
                  if(_loc16_.sprThem.HasCondition(401))
                  {
                     _loc25_ = 0;
                     _loc7_--;
                  }
                  else
                  {
                     _loc25_ += _loc16_.sprThem.CurrentAttackMode.m_fMorale;
                  }
               }
               if(!_loc16_.UsSpotted)
               {
                  _loc25_--;
               }
               if(_loc16_.nRange > 0)
               {
                  _loc26_ = _loc25_ * 1 / _loc16_.nRange;
               }
               else
               {
                  _loc26_ = _loc25_;
               }
            }
            if(m_objPair == null)
            {
               m_objPair = _loc16_;
               _loc10_ = _loc25_;
               _loc11_ = _loc26_;
            }
            else if(_loc26_ > _loc11_)
            {
               m_objPair = _loc16_;
               _loc10_ = _loc25_;
               _loc11_ = _loc26_;
            }
         }
         _loc17_ = false;
         _loc18_ = false;
         _loc19_ = false;
         _loc20_ = false;
         _loc21_ = false;
         _loc22_ = false;
         _loc23_ = false;
         if(m_objPair == null)
         {
            m_objPair = _loc12_;
         }
         if(_loc14_ || _loc7_ <= 0 && _loc15_ == false)
         {
            if(_loc8_ > 0)
            {
               _loc19_ = true;
               m_objPair = _loc13_;
            }
            else
            {
               _loc17_ = true;
               m_objPair = _loc12_;
            }
         }
         else if(m_fMoraleSituHidden >= _loc11_ && GetFactionRep(m_objPair.sprThem.m_nFaction) <= 0)
         {
            _loc18_ = true;
         }
         else
         {
            _loc17_ = true;
         }
         if(_loc17_)
         {
            this.m_tilLastFail = m_tilCurrentHex;
            this.m_nLastFailTimer = 2;
            if(!m_objPair.ThemSpotted)
            {
               if(!HasCondition(109))
               {
                  _loc21_ = true;
               }
            }
            else if(m_objPair.sprThem.CurrentAttackMode.m_nType == AttackMode.ATTACK_TYPE_MELEE && m_objPair.GetInverse().InRange())
            {
               if(m_objPair.sprThem.HasCondition(143) || m_objPair.sprThem.HasCondition(144) || m_objPair.sprThem.HasCondition(145) || m_objPair.sprThem.HasCondition(146) || m_objPair.sprThem.HasCondition(148) || Math.random() < 0.5)
               {
                  _loc22_ = true;
               }
               else
               {
                  _loc21_ = true;
               }
            }
            else
            {
               _loc22_ = true;
            }
         }
         else if(_loc18_)
         {
            if(!m_objPair.ThemSpotted)
            {
               if(!HasCondition(109))
               {
                  _loc21_ = true;
               }
            }
            else if(m_objPair.InRange())
            {
               _loc20_ = true;
            }
            else
            {
               _loc23_ = true;
            }
         }
         if(_loc19_)
         {
            if(m_objPair.vPassiveMoves.length > 0)
            {
               m_objMove = this.GetRandomMove(m_objPair.vPassiveMoves);
            }
            else
            {
               _loc22_ = true;
            }
         }
         else
         {
            _loc28_ = new Vector.<BattleMove>();
            _loc29_ = 0;
            while(_loc29_ < m_objPair.vAllMoves.length)
            {
               if(m_objPair.vAllMoves[_loc29_].m_bPassive == false)
               {
                  _loc28_.push(m_objPair.vAllMoves[_loc29_]);
               }
               _loc29_++;
            }
            m_objPair.vAllMoves.length = 0;
            m_objPair.vAllMoves = _loc28_;
         }
         if(_loc21_)
         {
            m_objMove = this.GetRandomMove(m_objPair.vPositionMoves);
         }
         else if(_loc22_)
         {
            if(m_objPair.vRetreatMoves.length > 0)
            {
               m_objMove = this.GetRandomMove(m_objPair.vRetreatMoves);
            }
            else
            {
               m_objMove = this.GetRandomMove(m_objPair.vFallBackMoves);
            }
         }
         else if(_loc23_)
         {
            m_objMove = this.GetRandomMove(m_objPair.vApproachMoves);
         }
         else if(_loc20_)
         {
            m_objMove = this.GetRandomMove(m_objPair.vOffenseMoves);
         }
         if(m_objMove == null)
         {
            m_objMove = this.GetRandomMove(m_objPair.vAllMoves);
         }
         if(m_objMove != null)
         {
            m_fOrder = m_objMove.m_fOrder;
         }
      }
      
      private function GetRandomMove(param1:Vector.<BattleMove>) : BattleMove
      {
         var _loc2_:Number = 0;
         var _loc3_:int = 0;
         while(_loc3_ < param1.length)
         {
            _loc2_ += param1[_loc3_].m_fPriority;
            _loc3_++;
         }
         var _loc4_:Number = DM.Rand(DM.RAND_HIGH) * _loc2_;
         var _loc5_:Number = _loc2_;
         _loc3_ = int(param1.length - 1);
         while(_loc3_ >= 0)
         {
            _loc5_ -= param1[_loc3_].m_fPriority;
            if(_loc4_ >= _loc5_)
            {
               return param1[_loc3_];
            }
            _loc3_--;
         }
         return null;
      }
      
      public function Move(param1:Boolean = true) : void
      {
         var _loc10_:FlxHexTile = null;
         var _loc11_:Creature = null;
         var _loc12_:FlxHexTile = null;
         var _loc13_:FlxHexTile = null;
         var _loc14_:Creature = null;
         var _loc26_:FlxHexTile = null;
         var _loc28_:Number = NaN;
         var _loc29_:Creature = null;
         var _loc30_:int = 0;
         var _loc31_:Number = NaN;
         var _loc32_:int = 0;
         var _loc33_:String = null;
         m_bSpied = false;
         m_bLeader = true;
         if(Alive == false)
         {
            this.m_strActivity = "Dead";
            m_fMovesLeft = 0;
            return;
         }
         var _loc2_:int = Math.floor(DM.Rand(DM.RAND_FLAT) * this.m_vActivities.length);
         this.m_strActivity = this.m_vActivities[_loc2_];
         if(Asleep)
         {
            Asleep = false;
         }
         if(Asleep)
         {
            this.m_strActivity = "Unconscious";
            m_fMovesLeft = 0;
            return;
         }
         var _loc3_:int = 0;
         if(_loc3_ >= this.m_vEncQueue.length)
         {
            if(param1)
            {
               this.EquipBestWeapon();
            }
            var _loc4_:int = PlayState.m_objInstance.nTimeOfDay;
            var _loc5_:Number = m_tilCurrentHex.m_vLightLevels[_loc4_];
            var _loc6_:Number = (_loc6_ = _loc6_ = Math.max(_loc5_ * VisionRange,0)) + m_tilCurrentHex.nVizIncrease;
            var _loc7_:Boolean = HasCondition(109);
            if(_loc5_ + m_fLightLevel < MinLightLevel)
            {
               if(!_loc7_)
               {
                  this.AddCondition(GetCondition(109));
               }
               if(m_fMovesLeft - m_tilCurrentHex.nTerrainCost > 1)
               {
                  m_fMovesLeft = 1 + m_tilCurrentHex.nTerrainCost;
               }
            }
            else if(_loc7_)
            {
               RemoveCondition(GetCondition(109));
            }
            var _loc8_:Vector.<FlxHexTile> = MapUtils.GetVisibleHexes(m_tilCurrentHex.GetHexCoords(),_loc6_,MinLightLevel,false);
            var _loc9_:Vector.<FlxHexTile> = Vector.<FlxHexTile>([m_tilCurrentHex]).concat(MapUtils.GetHexRing(m_tilCurrentHex.GetHexCoords(),1));
            var _loc15_:Number = 0;
            var _loc16_:Number = 0;
            var _loc17_:Number = 0;
            var _loc18_:Number = m_fLeader;
            var _loc19_:Vector.<FlxHexTile> = new Vector.<FlxHexTile>();
            var _loc20_:int = 1;
            var _loc21_:int = 0;
            var _loc22_:int = 0;
            var _loc23_:int = 0;
            var _loc24_:Number = (DM.Rand(DM.RAND_MID) - 0.5) * 0.5;
            var _loc25_:Boolean = HasCondition(151);
            _loc3_ = 1;
            while(_loc3_ < _loc9_.length)
            {
               if(this.CanEnterHex(_loc9_[_loc3_]))
               {
                  _loc19_.push(_loc9_[_loc3_]);
               }
               _loc3_++;
            }
            if(HasCondition(367))
            {
               if(PlayState.m_objInstance.sprPlayer.HasCondition(136) == false)
               {
                  _loc11_ = PlayState.m_objInstance.sprPlayer;
               }
               else if(param1)
               {
                  Despawn([1],true);
                  PlayState.m_objInstance.sprPlayer.RemoveCondition(PlayState.m_objInstance.sprPlayer.GetCondition(567));
                  m_fMovesLeft = 0;
               }
            }
            else if(HasCondition(464) && PlayState.m_objInstance.sprPlayer.HasCondition(461) && PlayState.m_objInstance.sprPlayer.Asleep == true)
            {
               _loc11_ = PlayState.m_objInstance.sprPlayer;
            }
            for each(_loc26_ in _loc8_)
            {
               if(_loc26_ != null)
               {
                  _loc28_ = 0;
                  for each(_loc29_ in _loc26_.m_vOccupants)
                  {
                     if(_loc29_ != this)
                     {
                        if(!(HasCondition(367) && (_loc29_.HasCondition(136) || _loc29_ is AICreature)))
                        {
                           if(!(HasCondition(464) && (_loc29_.HasCondition(460) || _loc29_.Asleep == false)))
                           {
                              if(_loc29_.m_nFaction == m_nFaction)
                              {
                                 _loc20_++;
                                 if(param1 && _loc26_ == m_tilCurrentHex)
                                 {
                                    this.ExchangeFactionInfo(_loc29_);
                                 }
                                 if(_loc29_.m_fLeader > m_fLeader && (_loc14_ == null || _loc29_.m_fLeader > _loc14_.m_fLeader))
                                 {
                                    if(!HasCondition(495))
                                    {
                                       _loc14_ = _loc29_;
                                       if(_loc29_ is AICreature)
                                       {
                                          this.m_tilHome = AICreature(_loc14_).m_tilHome;
                                       }
                                       else
                                       {
                                          this.m_tilHome = m_tilCurrentHex;
                                       }
                                       m_bLeader = false;
                                    }
                                 }
                                 else
                                 {
                                    _loc29_.m_bLeader = false;
                                 }
                              }
                              else if(CanSeeCreature(_loc29_))
                              {
                                 if(GetFactionRep(_loc29_.m_nFaction) > 0)
                                 {
                                    _loc21_++;
                                 }
                                 else
                                 {
                                    _loc22_++;
                                    _loc30_ = int(MapUtils.GetHexDistance(_loc26_.GetHexCoords(),m_tilCurrentHex.GetHexCoords()));
                                    _loc28_ = _loc29_.m_fMorale;
                                    if(_loc25_)
                                    {
                                       _loc28_ = _loc29_.CurrentAttackMode.m_fMorale;
                                    }
                                    if(!_loc29_.CanSeeCreature(this))
                                    {
                                       _loc28_--;
                                    }
                                    _loc31_ = 1 + Math.max(3 - _loc30_,0) / 3;
                                    if(_loc28_ < 0)
                                    {
                                       _loc31_ = -_loc31_;
                                    }
                                    if((_loc28_ *= 1 + _loc31_) > _loc16_ && _loc11_ is AICreature || _loc11_ == null)
                                    {
                                       _loc11_ = _loc29_;
                                       _loc16_ = _loc28_;
                                    }
                                 }
                              }
                           }
                        }
                     }
                  }
                  if(_loc28_ <= 0)
                  {
                     if(_loc26_.Scent > 0 && (_loc26_.m_objScentOwner == null || _loc26_.m_objScentOwner.m_nFaction != m_nFaction) && _loc26_.Scent >= m_fTrackingThreshold && Math.random() < 0.85)
                     {
                        _loc12_ = _loc26_;
                        _loc17_ = _loc26_.Scent;
                     }
                     if(_loc26_ != this.m_tilHome && _loc26_.m_fTotalValue > _loc15_)
                     {
                        _loc10_ = _loc26_;
                        _loc15_ = _loc26_.m_fTotalValue;
                     }
                  }
               }
            }
            if(_loc20_ == 1)
            {
               m_bLeader = false;
               for each(_loc29_ in PlayState.m_objInstance.m_aCreatures)
               {
                  if(_loc29_ != this && _loc29_.m_nFaction == m_nFaction && _loc29_.m_fLeader > m_fLeader && (_loc14_ == null || _loc29_.m_fLeader > _loc14_.m_fLeader) && !HasCondition(495) && MapUtils.GetHexDistance(m_tilCurrentHex.GetHexCoords(),_loc29_.m_tilCurrentHex.GetHexCoords()) <= 3)
                  {
                     _loc14_ = _loc29_;
                     if(_loc29_ is AICreature)
                     {
                        this.m_tilHome = AICreature(_loc14_).m_tilHome;
                     }
                     else
                     {
                        this.m_tilHome = m_tilCurrentHex;
                     }
                     _loc20_++;
                  }
               }
            }
            m_fMoraleSitu = m_fMorale + CurrentAttackMode.m_fMorale + _loc20_ * 0.2 + _loc21_ * 0.1 - _loc22_ * 0.1;
            m_fMoraleSituHidden = m_fMoraleSitu + m_fMoraleHidden + _loc24_;
            if(_loc11_ != null)
            {
               if(m_fMoraleSituHidden - _loc16_ >= 0)
               {
                  if(_loc11_.m_tilCurrentHex == m_tilCurrentHex)
                  {
                     _loc13_ = m_tilCurrentHex;
                     this.m_strActivity = "engaging " + _loc11_.Name;
                     m_fMovesLeft = 0;
                  }
                  else
                  {
                     _loc13_ = this.ApproachHex(new FlxPoint(_loc11_.m_tilCurrentHex.x,_loc11_.m_tilCurrentHex.y),_loc9_);
                     this.m_strActivity = "following " + _loc11_.Name;
                  }
               }
               else
               {
                  if((_loc13_ = this.ApproachHex(new FlxPoint(_loc11_.m_tilCurrentHex.x,_loc11_.m_tilCurrentHex.y),_loc9_,true)) == m_tilCurrentHex)
                  {
                     _loc32_ = Math.floor(Math.random() * _loc19_.length);
                     _loc13_ = _loc19_[_loc32_];
                  }
                  this.m_strActivity = "fleeing " + _loc11_.Name;
               }
            }
            else if(this.m_bHeadHomeSleep || this.TimeToSleep())
            {
               if(m_tilCurrentHex == this.m_tilHome)
               {
                  this.m_strActivity = "unconscious.";
                  Asleep = true;
                  m_fMovesLeft = 0;
                  this.m_bHeadHomeSleep = false;
                  return;
               }
               this.m_bHeadHomeSleep = true;
               _loc13_ = this.ApproachHex(new FlxPoint(this.m_tilHome.x,this.m_tilHome.y),_loc9_);
               this.m_strActivity = this.GetHeading(_loc9_,_loc13_);
            }
            else if(this.m_vWaypoints.length > 0)
            {
               if(this.m_vWaypoints[0].length < 3)
               {
                  this.m_vWaypoints.splice(0,1);
                  this.Move();
                  return;
               }
               _loc26_ = MapUtils.GetTileByCoords(new FlxPoint(this.m_vWaypoints[0][0],this.m_vWaypoints[0][1]));
               if(m_tilCurrentHex == _loc26_)
               {
                  this.m_vEncQueue.push(this.m_vWaypoints[0][2]);
                  this.m_vWaypoints.splice(0,1);
                  this.Move();
                  return;
               }
               _loc13_ = this.ApproachHex(new FlxPoint(_loc26_.x,_loc26_.y),_loc9_);
               this.m_strActivity = this.GetHeading(_loc9_,_loc13_);
            }
            else if(_loc14_ != null)
            {
               if(m_tilCurrentHex == _loc14_.m_tilCurrentHex)
               {
                  this.m_strActivity = "waiting for " + _loc14_.Name + ".";
                  m_fMovesLeft = 0;
                  return;
               }
               this.m_strActivity = "following " + _loc14_.Name + ".";
               _loc13_ = this.ApproachHex(new FlxPoint(_loc14_.m_tilCurrentHex.x,_loc14_.m_tilCurrentHex.y),_loc9_);
            }
            else
            {
               if(HasCondition(493) && _loc12_ != null)
               {
                  _loc13_ = _loc12_;
                  _loc33_ = "";
                  if(_loc12_.m_objScentOwner)
                  {
                     _loc33_ = " of " + _loc12_.m_objScentOwner.Name;
                  }
                  this.m_strActivity = "following tracks" + _loc33_;
               }
               else if(HasCondition(494))
               {
                  if(this.m_bHeadHomeLoot)
                  {
                     if(m_tilCurrentHex != this.m_tilHome)
                     {
                        _loc13_ = this.ApproachHex(new FlxPoint(this.m_tilHome.x,this.m_tilHome.y),_loc9_);
                        this.m_strActivity = this.GetHeading(_loc9_,_loc13_);
                     }
                     else
                     {
                        _loc13_ = m_tilCurrentHex;
                     }
                  }
                  else if(_loc10_ != null)
                  {
                     _loc13_ = this.ApproachHex(new FlxPoint(_loc10_.x,_loc10_.y),_loc9_);
                     this.m_strActivity = this.GetHeading(_loc9_,_loc13_);
                  }
               }
               if(_loc13_ == null)
               {
                  _loc32_ = Math.floor(Math.random() * _loc19_.length);
                  _loc13_ = _loc19_[_loc32_];
               }
            }
            var _loc27_:int;
            if((_loc27_ = int(MapUtils.GetHexDistance(_loc13_.GetHexCoords(),m_tilCurrentHex.GetHexCoords()))) > 1)
            {
               _loc13_ = this.ApproachHex(new FlxPoint(_loc13_.x,_loc13_.y),_loc9_);
            }
            if(!this.CanEnterHex(_loc13_))
            {
               _loc32_ = Math.floor(Math.random() * _loc19_.length);
               _loc13_ = _loc19_[_loc32_];
            }
            if(!this.CanEnterHex(_loc13_))
            {
               _loc13_ = m_tilCurrentHex;
            }
            if(!param1)
            {
               return;
            }
            m_fMovesLeft -= _loc13_.nTerrainCost;
            fSleepDebt += _loc13_.nTerrainCost * m_fFatigueModifier;
            if(_loc13_ != m_tilCurrentHex && _loc29_.HasCondition(120) == false && _loc29_.HasCondition(121) == false)
            {
               JustMoved = 1;
            }
            PlayState.m_objInstance.AlignCreatureToHex(this,_loc13_);
            if(this.m_nLastFailTimer < 0)
            {
               this.m_tilLastFail = null;
            }
            for each(_loc29_ in m_tilCurrentHex.m_vOccupants)
            {
               if(_loc29_.m_nFaction != m_nFaction)
               {
                  m_fMovesLeft = 0;
                  break;
               }
            }
            if(HasCondition(494))
            {
               if(m_tilCurrentHex == this.m_tilHome)
               {
                  this.DepositLoot();
               }
               else if(m_tilCurrentHex.m_fTotalValue > 0 && (m_tilCurrentHex.m_objScentOwner != null && (m_tilCurrentHex.m_objScentOwner.m_nFaction == m_nFaction || GetFactionRep(m_tilCurrentHex.m_objScentOwner.m_nFaction) <= 0)))
               {
                  this.LootHex();
               }
            }
            return;
         }
         this.HandleEncounter();
         --m_fMovesLeft;
      }
      
      private function GetHeading(param1:Vector.<FlxHexTile>, param2:FlxHexTile) : String
      {
         var _loc3_:int = int(param1.indexOf(param2));
         if(_loc3_ >= 0)
         {
            return this.vDirections[_loc3_];
         }
         return "can\'t tell";
      }
      
      private function DepositLoot() : void
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc4_:ItemInstance = null;
         this.m_strActivity = "depositing loot.";
         var _loc1_:Vector.<GUIInventorySlot> = Vector.<GUIInventorySlot>([m_dictSlots[20],m_dictSlots[21],m_dictSlots[22]]);
         var _loc2_:Boolean = false;
         for each(_loc3_ in _loc1_)
         {
            if((_loc4_ = _loc3_.SocketedItem()) != null && CurrentAttackMode.m_objItem != _loc4_)
            {
               DropItem(_loc4_,false,true);
               _loc2_ = true;
            }
         }
         if(_loc2_)
         {
            m_tilCurrentHex.CalculateValue();
         }
         this.m_bHeadHomeLoot = false;
      }
      
      private function LootHex() : void
      {
         var _loc3_:ItemInstance = null;
         var _loc4_:Number = NaN;
         var _loc5_:Number = NaN;
         var _loc6_:ItemInstance = null;
         var _loc10_:ItemCamp = null;
         var _loc11_:* = false;
         var _loc12_:GUIInventorySlot = null;
         var _loc13_:ItemInstance = null;
         if(m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
         {
            return;
         }
         var _loc1_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc2_:Boolean = false;
         for each(_loc3_ in m_tilCurrentHex.GroundObject.vItems)
         {
            if(_loc3_.ItemDefinition.fMonetaryValue > 0 && PlayState.m_objInstance.grpInventoryUI.vForbidDeleteIDs.indexOf(_loc3_.ItemDefinition.nGroupID) < 0)
            {
               _loc1_.push(_loc3_);
            }
         }
         _loc4_ = m_tilCurrentHex.m_vLightLevels[PlayState.m_objInstance.nTimeOfDay] - MinLightLevel;
         _loc5_ = BaseDetectionLevel + m_fDetectionLevel;
         if(m_tilCurrentHex.m_vCampItems != null)
         {
            for each(_loc10_ in m_tilCurrentHex.m_vCampItems)
            {
               if(_loc5_ < _loc10_.m_fVisibility + _loc4_)
               {
                  for each(_loc3_ in _loc10_.vItems)
                  {
                     if(_loc3_.ItemDefinition.fMonetaryValue > 0 && PlayState.m_objInstance.grpInventoryUI.vForbidDeleteIDs.indexOf(_loc3_.ItemDefinition.nGroupID) < 0)
                     {
                        _loc1_.push(_loc3_);
                     }
                  }
               }
            }
         }
         var _loc7_:Boolean = false;
         var _loc8_:Boolean = false;
         if(m_tilCurrentHex.m_vOccupants.indexOf(PlayState.m_objInstance.sprPlayer) >= 0)
         {
            _loc8_ = true;
         }
         if(_loc8_ && PlayState.m_objInstance.sprPlayer.CanSeeCreature(this))
         {
            _loc7_ = true;
         }
         var _loc9_:int = 1;
         while(_loc9_ < _loc1_.length)
         {
            AICreature.CompareItemsByPrice(_loc1_[0],_loc1_[1]);
            _loc9_++;
         }
         for each(_loc3_ in _loc1_)
         {
            if(this.m_bHeadHomeLoot)
            {
               break;
            }
            if(_loc3_.ItemDefinition.m_vProperties.indexOf(77) < 0)
            {
               if(!(_loc8_ && _loc3_.ItemDefinition.m_vProperties.indexOf(78) >= 0))
               {
                  if(Encumberance + _loc3_.WeightPlusContents < m_fEncumberanceLimit)
                  {
                     _loc11_ = false;
                     if(_loc3_.m_objParentContainer is ItemCamp && _loc3_.m_objParentContainer.Slot == null)
                     {
                        _loc11_ = grpCampSlot.SocketItem(_loc3_.m_objParentContainer) == null;
                     }
                     _loc12_ = _loc3_.Slot;
                     if(_loc3_.Slot != null)
                     {
                        _loc6_ = _loc3_.Slot.RemoveItem(_loc3_,true);
                     }
                     if(_loc6_ != null)
                     {
                        if((_loc13_ = this.TakeItem(_loc6_)) != null)
                        {
                           if((_loc13_ = DropItem(_loc13_,false,true)) != null && _loc12_ != null)
                           {
                              PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(_loc13_,_loc12_,true);
                           }
                           this.m_bHeadHomeLoot = true;
                        }
                        else if(_loc7_)
                        {
                           MessageFloaty(Name + " 捡起了 " + _loc6_.strDesc);
                        }
                        _loc2_ = true;
                     }
                     if(_loc11_)
                     {
                        grpCampSlot.UnSocketItem(true,null,true);
                     }
                     if(_loc8_ && PlayState.m_objInstance.sprPlayer.Asleep && Math.random() / 1 <= PlayState.m_objInstance.sprPlayer.m_fSleepAwareness)
                     {
                        PlayState.m_objInstance.sprPlayer.MessageFloaty(Name + " 惊醒了 " + PlayState.m_objInstance.sprPlayer.Name + " 在偷东西的时候 " + _loc6_.strDesc);
                        PlayState.m_objInstance.sprPlayer.ForceAwake();
                        break;
                     }
                  }
               }
            }
         }
         if(_loc2_)
         {
            m_tilCurrentHex.CalculateValue();
         }
         if(_loc10_ != null)
         {
            this.m_strActivity = "looting " + _loc10_.strDesc;
         }
         else
         {
            this.m_strActivity = "looting area.";
         }
      }
      
      public function TimeToSleep() : Boolean
      {
         if(fSleepDebt > aRestedStates[1][0])
         {
            return true;
         }
         if(fSleepDebt < aRestedStates[2][0])
         {
            return false;
         }
         var _loc1_:Number = 0;
         var _loc2_:int = int(PlayState.m_objInstance.objDate.getHours());
         if(HasCondition(492))
         {
            if(_loc2_ > 5 && _loc2_ < 21)
            {
               _loc1_ = 0.5 + (_loc2_ - 5) / 8;
            }
         }
         else if(_loc2_ < 6)
         {
            _loc1_ = 0.75 + _loc2_ / 8;
         }
         else if(_loc2_ > 22)
         {
            _loc1_ = 0.5 + (_loc2_ - 22) / 8;
         }
         return Math.random() < _loc1_;
      }
      
      private function ApproachHex(param1:FlxPoint, param2:Vector.<FlxHexTile>, param3:Boolean = false) : FlxHexTile
      {
         var _loc4_:Number = param1.x - x;
         var _loc5_:Number = param1.y - y;
         var _loc6_:int = 0;
         if(param3)
         {
            _loc4_ = -_loc4_;
            _loc5_ = -_loc5_;
         }
         if(_loc5_ < 0)
         {
            if(_loc4_ == 0 && this.CanEnterHex(param2[1]))
            {
               _loc6_ = 1;
            }
            else if(_loc4_ > 0 && this.CanEnterHex(param2[2]))
            {
               _loc6_ = 2;
            }
            else if(this.CanEnterHex(param2[6]))
            {
               _loc6_ = 6;
            }
         }
         else if(_loc4_ == 0 && this.CanEnterHex(param2[4]))
         {
            _loc6_ = 4;
         }
         else if(_loc4_ > 0 && this.CanEnterHex(param2[3]))
         {
            _loc6_ = 3;
         }
         else if(this.CanEnterHex(param2[5]))
         {
            _loc6_ = 5;
         }
         return param2[_loc6_];
      }
      
      override public function CanEnterHex(param1:FlxHexTile) : Boolean
      {
         if(param1 == null || param1.m_nBarterTile != BarterHex.BARTER_NONE || !param1.bPassable || param1 == this.m_tilLastFail && this.m_nLastFailTimer >= 0)
         {
            return false;
         }
         var _loc2_:String = param1.GetHexCoords().x + "," + param1.GetHexCoords().y;
         if(DataHandler.IsHexForbidden(_loc2_))
         {
            return false;
         }
         if(param1 == m_tilCurrentHex)
         {
            return true;
         }
         return true;
      }
      
      override public function KillCreature(param1:String, param2:String, param3:String = "") : void
      {
         var _loc6_:ItemInstance = null;
         super.KillCreature(param1,param2,param3);
         var _loc4_:Vector.<ItemInstance> = GetItems(true,true,false,true);
         _loc4_ = DataHandler.GetTreasure(this.m_nCorpseID).GenerateTreasure().concat(_loc4_);
         var _loc5_:Boolean = false;
         for each(_loc6_ in _loc4_)
         {
            if(DropItem(_loc6_,true,true) == null)
            {
               _loc5_ = true;
            }
         }
         if(_loc5_)
         {
            m_tilCurrentHex.CalculateValue();
         }
      }
      
      override public function GetPopUpText(param1:Boolean = false) : String
      {
         var _loc2_:* = super.GetPopUpText(param1);
         if(m_bSpied)
         {
            _loc2_ += "\nActivity: " + this.m_strActivity;
            if(param1)
            {
               _loc2_ += "\nSees Player: ";
               if(CanSeeCreature(PlayState.m_objInstance.sprPlayer))
               {
                  _loc2_ += "Yes";
               }
               else
               {
                  _loc2_ += "Not yet";
               }
            }
         }
         return _loc2_;
      }
      
      override public function get LootTarget() : int
      {
         return 0;
      }
      
      override public function set LootTarget(param1:int) : void
      {
         var _loc3_:ItemInstance = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:Boolean = false;
         this.LootHex();
         var _loc2_:Creature = null;
         var _loc4_:Boolean = false;
         var _loc5_:Vector.<GUIInventorySlot> = new Vector.<GUIInventorySlot>();
         if(m_objPair != null)
         {
            _loc2_ = m_objPair.sprThem;
         }
         if(_loc2_ == null)
         {
            return;
         }
         _loc5_.push(_loc2_.m_dictSlots[207]);
         _loc5_.push(_loc2_.m_dictSlots[20]);
         _loc5_.push(_loc2_.m_dictSlots[21]);
         _loc5_.push(_loc2_.m_dictSlots[22]);
         _loc5_.push(_loc2_.m_dictSlots[13]);
         _loc5_.push(_loc2_.m_dictSlots[14]);
         _loc5_.push(_loc2_.m_dictSlots[2]);
         _loc5_.push(_loc2_.m_dictSlots[3]);
         _loc5_.push(_loc2_.m_dictSlots[4]);
         _loc5_.push(_loc2_.m_dictSlots[5]);
         _loc5_.push(_loc2_.m_dictSlots[6]);
         _loc5_.push(_loc2_.m_dictSlots[7]);
         _loc5_.push(_loc2_.m_dictSlots[8]);
         _loc5_.push(_loc2_.m_dictSlots[11]);
         _loc5_.push(_loc2_.m_dictSlots[12]);
         _loc5_.push(_loc2_.m_dictSlots[23]);
         _loc5_ = _loc5_.concat(m_vAllWoundSlots);
         for each(_loc6_ in _loc5_)
         {
            _loc3_ = _loc6_.UnSocketItem();
            if(_loc3_ != null)
            {
               if(Encumberance + _loc3_.WeightPlusContents >= m_fEncumberanceLimit)
               {
                  _loc7_ = _loc3_;
               }
               else
               {
                  _loc7_ = this.TakeItem(_loc3_);
               }
               _loc8_ = false;
               if(_loc7_ != _loc3_)
               {
                  _loc8_ = true;
               }
               if(_loc7_ != null)
               {
                  if((_loc7_ = DropItem(_loc7_,false,true)) != null && _loc6_ != null)
                  {
                     PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(_loc7_,_loc6_,true);
                  }
                  this.m_bHeadHomeLoot = true;
               }
               _loc4_ = true;
               if(_loc8_ && DM.Rand(DM.RAND_FLAT) < 0.4)
               {
                  this.m_bHeadHomeLoot = true;
                  break;
               }
            }
         }
         if(_loc4_)
         {
            m_tilCurrentHex.CalculateValue();
         }
      }
      
      override public function set TriggerEncounter(param1:int) : void
      {
         if(PlayState.m_objInstance.m_nGameState < PlayState.GAMESTATE_GAMEREADY)
         {
            return;
         }
         if(param1 < 0 || param1 == DM.m_nNullEnc)
         {
            return;
         }
         var _loc2_:Encounter = DataHandler.GetEncounter(param1);
         if(_loc2_.PreconditionsOK(this))
         {
            this.m_vEncQueue.push(_loc2_.m_nID);
         }
      }
      
      private function HandleEncounter() : void
      {
         var _loc5_:int = 0;
         var _loc6_:ItemInstance = null;
         var _loc9_:int = 0;
         var _loc10_:int = 0;
         var _loc11_:Vector.<FlxHexTile> = null;
         var _loc12_:Vector.<ItemInstance> = null;
         var _loc13_:ItemInstance = null;
         var _loc14_:Vector.<int> = null;
         var _loc1_:Encounter = DataHandler.GetEncounter(this.m_vEncQueue.pop());
         var _loc2_:FlxHexTile = PlayState.m_objInstance.tilCurrentHex;
         var _loc3_:GUIInventory = PlayState.m_objInstance.grpInventoryUI;
         if(_loc1_.m_ptTeleport.x != 0 || _loc1_.m_ptTeleport.y != 0)
         {
            _loc2_ = MapUtils.GetTileByCoords(_loc1_.m_ptTeleport);
            PlayState.m_objInstance.AlignCreatureToHex(this,_loc2_);
         }
         else if(_loc1_.m_nTeleportRange > 0)
         {
            DM.TeleportRange(this,_loc2_,_loc1_.m_nTeleportRange);
         }
         var _loc4_:AICreature;
         if((_loc4_ = DataHandler.GetCreature(_loc1_.m_objSourceCreature.m_nCreatureID)) != null)
         {
            _loc9_ = _loc1_.m_objSourceCreature.m_nMin + Math.random() * _loc1_.m_objSourceCreature.m_nMax;
            _loc10_ = 0;
            while(_loc10_ < _loc9_)
            {
               if((_loc11_ = MapUtils.GetHexRing(_loc2_.GetHexCoords(),_loc1_.m_ptCreatureHex.x))[_loc1_.m_ptCreatureHex.y] != null)
               {
                  PlayState.m_objInstance.AddCreature(_loc4_,_loc11_[_loc1_.m_ptCreatureHex.y].GetHexCoords());
                  if(_loc1_.m_ptCreatureHex.x <= 1)
                  {
                     _loc4_.m_tilCurrentHex.nExploredState = 0;
                     _loc4_.visible = true;
                     MapUtils.tmapHexes.vVisibleHexes.push(_loc4_.m_tilCurrentHex);
                     _loc4_.AddCondition(_loc4_.GetCondition(113));
                  }
                  _loc4_ = DataHandler.GetCreature(_loc1_.m_objSourceCreature.m_nCreatureID);
               }
               _loc10_++;
            }
         }
         for each(_loc5_ in _loc1_.m_aConditions)
         {
            if(_loc5_ > 1)
            {
               this.AddCondition(GetCondition(_loc5_));
            }
            else if(_loc5_ < -1)
            {
               RemoveCondition(GetCondition(-_loc5_));
            }
         }
         _loc1_.m_vTreasure = Vector.<ItemInstance>(DataHandler.GetTreasure(_loc1_.m_nTreasureID).GenerateTreasure());
         _loc1_.m_vRemoveTreasure = Vector.<ItemInstance>(DataHandler.GetTreasure(_loc1_.m_nRemoveTreasureID).GenerateTreasure());
         if(_loc1_.m_vRemoveTreasure.length > 0)
         {
            for each(_loc13_ in _loc1_.m_vRemoveTreasure)
            {
               _loc6_ = null;
               if(_loc13_.m_objProxy != null)
               {
                  _loc6_ = _loc13_.m_objProxy;
               }
               else
               {
                  _loc12_ = GetItems(true,true,true,true,_loc13_.ItemDefinition.nGroupID,_loc13_.ItemDefinition.nSubgroupID);
                  if(grpCampSlot.SocketedItem() != null)
                  {
                     _loc12_ = _loc12_.concat(grpCampSlot.SocketedItem().GetItems(_loc13_.ItemDefinition.nGroupID,_loc13_.ItemDefinition.nSubgroupID));
                  }
                  if(_loc12_.length == 0)
                  {
                     _loc12_ = grpGroundSlot.SocketedItem().GetItems(_loc13_.ItemDefinition.nGroupID,_loc13_.ItemDefinition.nSubgroupID);
                  }
                  if(_loc12_.length == 0)
                  {
                     _loc12_ = grpCampSlot.SocketedItem().GetItems(_loc13_.ItemDefinition.nGroupID,_loc13_.ItemDefinition.nSubgroupID);
                  }
                  if(_loc12_.length > 0)
                  {
                     _loc6_ = _loc12_[0];
                  }
               }
               if(_loc6_ != null)
               {
                  if(_loc6_.bSocketed)
                  {
                     _loc6_.Slot.UnSocketItem(true,_loc6_,false);
                  }
                  else
                  {
                     _loc6_.Slot.RemoveItem(_loc6_);
                  }
               }
            }
         }
         var _loc7_:ItemCamp = m_tilCurrentHex.GetCampObject();
         if(_loc1_.m_vTreasure.length > 0)
         {
            _loc14_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_FIT_SUB]);
            for each(_loc13_ in _loc1_.m_vTreasure)
            {
               _loc13_.CreateAppearance();
               if(_loc13_.ItemDefinition.nGroupID == 12 && m_tilCurrentHex.m_vCampItems.length < m_tilCurrentHex.nCampItems)
               {
                  m_tilCurrentHex.m_vCampItems.push(_loc13_);
                  RememberCamp(m_tilCurrentHex,ItemCamp(_loc13_));
               }
               else
               {
                  _loc3_.AddItemToCapBox(_loc13_,m_tilCurrentHex.GroundObject,_loc14_);
               }
            }
         }
         var _loc8_:Encounter = _loc1_.HandleResponse(this,new Vector.<ItemInstance>(),true);
         this.TriggerEncounter = _loc8_.m_nID;
      }
      
      public function SetWaypoint(param1:Array, param2:Boolean) : void
      {
         if(param1 == null || param1.length < 4 || param2 == false)
         {
            return;
         }
         var _loc3_:Vector.<int> = Vector.<int>(param1.slice(0,3));
         var _loc4_:Boolean;
         if(_loc4_ = Boolean(param1[3]))
         {
            this.m_tilHome = MapUtils.GetTileByCoords(new FlxPoint(_loc3_[0],_loc3_[1]));
         }
         this.m_vWaypoints.push(_loc3_);
      }
      
      public function SetPlayerCondition(param1:Array, param2:Boolean) : void
      {
         if(param1 == null || param1.length < 1)
         {
            return;
         }
         var _loc3_:int = int(param1[0]);
         var _loc4_:int = 1;
         if(_loc3_ < 0)
         {
            _loc3_ = -_loc3_;
            _loc4_ = -1;
         }
         if(_loc4_ > 0)
         {
            PlayState.m_objInstance.sprPlayer.AddCondition(PlayState.m_objInstance.sprPlayer.GetCondition(_loc3_),false,false);
         }
         else
         {
            PlayState.m_objInstance.sprPlayer.RemoveCondition(PlayState.m_objInstance.sprPlayer.GetCondition(_loc3_));
         }
      }
      
      override public function get SaveData() : SaveGameCreature
      {
         var _loc1_:SaveGameCreature = super.SaveData;
         var _loc2_:int = 0;
         while(_loc2_ < this.m_vWaypoints.length)
         {
            _loc1_.m_vWaypoints.push(this.m_vWaypoints[_loc2_]);
            _loc2_++;
         }
         _loc2_ = 0;
         while(_loc2_ < this.m_vEncQueue.length)
         {
            _loc1_.m_vEncQueue.push(this.m_vEncQueue[_loc2_]);
            _loc2_++;
         }
         return _loc1_;
      }
      
      override public function set SaveData(param1:SaveGameCreature) : void
      {
         super.SaveData = param1;
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = true;
         this.m_vWaypoints.length = 0;
         var _loc2_:int = 0;
         while(_loc2_ < param1.m_vWaypoints.length)
         {
            if(!(param1.m_vWaypoints[_loc2_] == null || param1.m_vWaypoints[_loc2_].length < 3))
            {
               this.m_vWaypoints.push(param1.m_vWaypoints[_loc2_].concat());
            }
            _loc2_++;
         }
         this.m_vEncQueue.length = 0;
         _loc2_ = 0;
         while(_loc2_ < param1.m_vEncQueue.length)
         {
            this.m_vEncQueue.push(param1.m_vEncQueue[_loc2_]);
            _loc2_++;
         }
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = false;
      }
   }
}

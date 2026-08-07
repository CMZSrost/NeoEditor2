package
{
   import flash.net.URLRequest;
   import flash.net.navigateToURL;
   import flash.utils.getTimer;
   import org.flixel.*;
   
   public class DM
   {
      
      public static var m_aEventQueue:Array;
      
      public static var m_nNullEnc:uint = 1;
      
      public static var m_ptDMC:FlxPoint;
      
      public static var m_vDMCHexes:Vector.<int>;
      
      public static var m_nDMCRadius:int;
      
      private static var m_nLastTick:uint;
      
      private static var m_nCreatureIndex:int;
      
      private static var m_fCreatureMoveDelay:Number;
      
      private static var m_nStartingCreatureCount:uint;
      
      public static var m_fLastTension:Number;
      
      public static var m_fLastReward:Number;
      
      private static var m_fTensionSpacing:Number;
      
      public static var m_fRewardThreshold:Number;
      
      public static var m_fRewardMult:Number;
      
      public static var m_grpCreature:AICreature;
      
      public static var m_nLocalFactionPopCap:int;
      
      public static var m_nLocalFactionRadius:int;
      
      public static var m_nGlobalPopCap:int;
      
      public static var m_vBluntVerbs:Vector.<String> = Vector.<String>(["打肿","抽打","虐待","剥皮","掌掴","震聋","碾碎","撞烂","重压","摧毁"]);
      
      public static var m_vCutVerbs:Vector.<String> = Vector.<String>(["擦伤","抓伤","切伤","砍伤","扯伤","划伤","刺伤","撕伤","打伤","碾碎"]);
      
      public static const MOVERESULT_MOVED:uint = 0;
      
      public static const MOVERESULT_WAIT:uint = 1;
      
      public static const MOVERESULT_DONE:uint = 2;
      
      public static const RAND_FLAT:uint = 0;
      
      public static const RAND_LOW:uint = 1;
      
      public static const RAND_MID:uint = 2;
      
      public static const RAND_HIGH:uint = 3;
      
      private static var nSafeX:uint;
      
      private static var nSafeY:uint;
      
      public static var ENCOUNTER_CRAFT_ID:uint = 657;
      
      private static var nCombatInQueue:int = 0;
       
      
      public function DM()
      {
         super();
      }
      
      public static function Initialize() : void
      {
         var _loc1_:GUIInventory = null;
         var _loc2_:String = null;
         var _loc3_:Item = null;
         m_aEventQueue = new Array();
         m_fCreatureMoveDelay = 0.5;
         m_nCreatureIndex = -1;
         m_nStartingCreatureCount = 0;
         m_ptDMC = new FlxPoint(57,192);
         m_vDMCHexes = Vector.<int>([13]);
         m_nDMCRadius = 20;
         m_fTensionSpacing = 1 * 60 * 1000;
         m_fRewardThreshold = 4 * 60 * 1000;
         m_fRewardMult = 1.5;
         nSafeX = 57;
         nSafeY = 194;
         m_nGlobalPopCap = 66;
         m_nLocalFactionPopCap = 6;
         m_nLocalFactionRadius = 3;
         _loc1_ = PlayState.m_objInstance.grpInventoryUI;
         _loc1_.UpdateSkillItems(DataHandler.GetTreasure(587).GenerateTreasure(),DataHandler.GetTreasure(588).GenerateTreasure());
         _loc1_.UpdateCraftingItems(false);
         _loc1_.m_nState = GUIInventory.STATE_SKILL_EXCLUSIVE;
         _loc1_.m_bAvailSkills = true;
         _loc1_.UpdateScreens(GUIInventory.PANEL_SKILLS);
         if(false == false)
         {
            _loc2_ = FlxG.stage.loaderInfo.parameters["strBonusItem"];
            if(_loc2_ == "88.1")
            {
               _loc3_ = DataHandler.GetItemDef("88.1");
               _loc3_.m_aEquipConditions = [[23,81],[23,461]];
            }
            else
            {
               DataHandler.GetItemDef("88.1").fDegradePerHour = 100;
            }
         }
      }
      
      public static function destroy() : void
      {
         var _loc1_:int = 0;
         while(_loc1_ < m_aEventQueue.length)
         {
            if(DataHandler.IsEncounterOriginal(m_aEventQueue[_loc1_]) == false)
            {
               Encounter(m_aEventQueue[_loc1_]).destroy();
            }
            m_aEventQueue[_loc1_] = null;
            _loc1_++;
         }
         m_aEventQueue = null;
         m_ptDMC = null;
         m_vDMCHexes = null;
         m_grpCreature = null;
      }
      
      public static function StartGame() : void
      {
         var _loc5_:Array = null;
         var _loc6_:FlxPoint = null;
         var _loc7_:Array = null;
         var _loc8_:uint = 0;
         var _loc9_:GUIInventorySlot = null;
         var _loc10_:FlxHexTile = null;
         var _loc11_:SourceCreature = null;
         var _loc12_:int = 0;
         var _loc13_:int = 0;
         var _loc14_:int = 0;
         m_fLastTension = getTimer();
         m_fLastReward = getTimer();
         var _loc1_:GUIInventory = PlayState.m_objInstance.grpInventoryUI;
         var _loc2_:Array = new Array();
         _loc2_.push(new Array(DataHandler.GetItem("78.4"),11));
         _loc2_.push(new Array(DataHandler.GetItem("88.0"),23));
         _loc2_.push(new Array(DataHandler.GetItem("89.0"),5));
         var _loc3_:uint = uint(int(FlxG.stage.loaderInfo.parameters["strBonusItemSlot"]));
         var _loc4_:String = FlxG.stage.loaderInfo.parameters["strBonusItem"];
         FlxG.log("Slot: " + _loc3_ + "; Item: " + _loc4_);
         if(_loc3_ != 0)
         {
            _loc2_.push(new Array(DataHandler.GetItem(_loc4_),_loc3_));
         }
         for each(_loc5_ in _loc2_)
         {
            _loc9_ = PlayState.m_objInstance.sprPlayer.m_dictSlots[_loc5_[1]];
            _loc1_.AddItemToSlot(_loc5_[0],_loc9_,true);
         }
         _loc6_ = new FlxPoint();
         _loc7_ = new Array();
         _loc8_ = 0;
         while(_loc8_ < m_nStartingCreatureCount)
         {
            _loc6_.x = Math.random() * (MapUtils.tmapHexes.widthInTiles - 1);
            _loc6_.y = Math.random() * (MapUtils.tmapHexes.heightInTiles - 1);
            _loc10_ = MapUtils.GetTileByCoords(_loc6_);
            _loc11_ = DataHandler.GetRandomCreature(_loc10_);
            if((_loc12_ = int(_loc7_.indexOf(_loc10_))) < 0 && _loc10_.bPassable && MapUtils.GetHexDistance(new FlxPoint(nSafeX,nSafeY),_loc6_) > 10)
            {
               _loc13_ = _loc11_.m_nMin + Math.random() * _loc11_.m_nMax;
               _loc14_ = 0;
               while(_loc14_ < _loc13_)
               {
                  PlayState.m_objInstance.AddCreature(DataHandler.GetCreature(_loc11_.m_nCreatureID),_loc6_);
                  _loc14_++;
               }
               _loc7_.push(_loc10_);
            }
            else
            {
               _loc8_--;
            }
            _loc8_++;
         }
         RestockShops();
      }
      
      public static function RestockShops() : void
      {
         var _loc2_:BarterHex = null;
         var _loc3_:FlxHexTile = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:GUIInventorySlot = null;
         var _loc6_:ItemInstance = null;
         var _loc7_:Vector.<int> = null;
         var _loc1_:Vector.<BarterHex> = DataHandler.GetAllBarterHexes();
         for each(_loc2_ in _loc1_)
         {
            _loc3_ = MapUtils.GetTileByCoords(new FlxPoint(_loc2_.m_nX,_loc2_.m_nY));
            _loc3_.m_nBarterTile = BarterHex.BARTER_NONE;
            _loc4_ = _loc3_.GroundObject.GetItems();
            _loc5_ = _loc3_.GroundObject.Slot;
            for each(_loc6_ in _loc4_)
            {
               if(_loc5_ != null)
               {
                  _loc5_.RemoveItem(_loc6_,true);
               }
               else
               {
                  _loc3_.GroundObject.RemoveItem(_loc6_,true);
               }
            }
            _loc4_ = DataHandler.GetTreasure(_loc2_.m_nTreasureID).GenerateTreasure();
            _loc7_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_STACK_PARTIAL,GUIFitItemResult.RESULT_CAN_FIT_SUB]);
            for each(_loc6_ in _loc4_)
            {
               PlayState.m_objInstance.grpInventoryUI.AddItemToCapBox(_loc6_,_loc3_.GroundObject,_loc7_);
            }
            _loc3_.m_nBarterTile = BarterHex.BARTER_SELL;
            if(_loc2_.m_bBuys)
            {
               _loc3_.m_nBarterTile = BarterHex.BARTER_BUYSELL;
            }
         }
      }
      
      public static function CreateTension() : void
      {
         var _loc4_:String = null;
         var _loc5_:FlxHexTile = null;
         var _loc6_:Vector.<FlxHexTile> = null;
         var _loc7_:FlxHexTile = null;
         var _loc8_:Creature = null;
         var _loc9_:int = 0;
         var _loc10_:uint = 0;
         var _loc11_:FlxPoint = null;
         var _loc12_:SourceCreature = null;
         var _loc13_:int = 0;
         var _loc14_:AICreature = null;
         var _loc1_:PlayState = PlayState.m_objInstance;
         var _loc2_:Array = new Array();
         var _loc3_:Vector.<int> = new Vector.<int>();
         for each(_loc5_ in MapUtils.tmapHexes.vVisibleHexes)
         {
            if(_loc5_.nExploredState == 0)
            {
               _loc6_ = MapUtils.GetHexRing(_loc5_.GetHexCoords(),1);
               for each(_loc7_ in _loc6_)
               {
                  if(_loc7_ != null)
                  {
                     _loc4_ = _loc7_.GetHexCoords().x + "," + _loc7_.GetHexCoords().y;
                     if(_loc7_.nExploredState > 0 && _loc7_.bPassable && DataHandler.IsHexForbidden(_loc4_) == false)
                     {
                        _loc2_.push(_loc7_);
                     }
                     for each(_loc8_ in _loc7_.m_vOccupants)
                     {
                        _loc9_ = int(_loc3_.length);
                        while(_loc9_ < _loc8_.m_nFaction + 1)
                        {
                           _loc3_.push(0);
                           _loc9_++;
                        }
                        ++_loc3_[_loc8_.m_nFaction];
                     }
                  }
               }
            }
         }
         if(_loc2_.length > 0)
         {
            _loc10_ = Math.floor(Math.random() * _loc2_.length);
            _loc11_ = FlxHexTile(_loc2_[_loc10_]).GetHexCoords();
            if(MapUtils.GetHexDistance(new FlxPoint(nSafeX,nSafeY),_loc11_) > 1)
            {
               _loc13_ = (_loc12_ = DataHandler.GetRandomCreature(_loc2_[_loc10_])).m_nMin + Math.random() * (_loc12_.m_nMax - _loc12_.m_nMax);
               _loc9_ = 0;
               while(_loc9_ < _loc13_)
               {
                  _loc14_ = DataHandler.GetCreature(_loc12_.m_nCreatureID);
                  if(_loc3_.length > _loc14_.m_nFaction && _loc3_[_loc14_.m_nFaction] >= m_nLocalFactionPopCap || PlayState.m_objInstance.m_aCreatures.length >= m_nGlobalPopCap)
                  {
                     break;
                  }
                  _loc1_.AddCreature(_loc14_,_loc11_);
                  m_fLastTension = getTimer();
                  m_fLastReward -= 0.5 * 60 * 1000;
                  _loc9_++;
               }
            }
         }
         _loc3_.length = 0;
         _loc3_ = null;
         _loc2_.length = 0;
         _loc2_ = null;
         _loc12_ = null;
         _loc11_ = null;
         _loc8_ = null;
         _loc14_ = null;
      }
      
      public static function PrependEncounter(param1:Encounter) : void
      {
         if(param1.m_nID == m_nNullEnc)
         {
            return;
         }
         var _loc2_:Array = new Array(param1);
         m_aEventQueue = _loc2_.concat(m_aEventQueue);
         if(param1.m_nType == Encounter.TYPE_COMBAT)
         {
            ++nCombatInQueue;
         }
      }
      
      public static function AppendEncounter(param1:Encounter) : void
      {
         if(param1.m_nID == m_nNullEnc)
         {
            return;
         }
         m_aEventQueue.push(param1);
         if(param1.m_nType == Encounter.TYPE_COMBAT)
         {
            ++nCombatInQueue;
         }
      }
      
      public static function NextEncounter() : void
      {
         var _loc1_:Encounter = DataHandler.GetEncounter(m_nNullEnc);
         if(m_aEventQueue.length > 0)
         {
            _loc1_ = m_aEventQueue.pop();
            if(_loc1_.m_nType == Encounter.TYPE_COMBAT)
            {
               var _loc2_:*;
               var _loc3_:* = (_loc2_ = §§findproperty(nCombatInQueue)).nCombatInQueue - 1;
               _loc2_.nCombatInQueue = _loc3_;
               if(PlayState.m_objInstance.tilCurrentHex.m_objBattle == null)
               {
                  NextEncounter();
                  return;
               }
            }
            ApplyEncounter(_loc1_);
            PlayState.m_objInstance.Mode(PlayState.GAMESTATE_INVENTORY);
         }
         else
         {
            ApplyEncounter(_loc1_);
            if(PlayState.m_objInstance.tilCurrentHex.index == 20)
            {
               PlayState.m_objInstance.Mode(PlayState.GAMESTATE_INVENTORY);
            }
            else
            {
               PlayState.m_objInstance.Mode(PlayState.GAMESTATE_DMRUNNING);
            }
         }
      }
      
      public static function ApplyEncounter(param1:Encounter) : void
      {
         var _loc4_:Array = null;
         var _loc5_:URLRequest = null;
         var _loc6_:AICreature = null;
         var _loc7_:int = 0;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:Vector.<FlxHexTile> = null;
         var _loc11_:ItemInstance = null;
         var _loc2_:FlxHexTile = PlayState.m_objInstance.tilCurrentHex;
         var _loc3_:GUIInventory = PlayState.m_objInstance.grpInventoryUI;
         if(param1.m_nID == 96)
         {
            _loc5_ = new URLRequest(DataHandler.strProductURL);
            navigateToURL(_loc5_);
         }
         if(PlayState.m_objInstance.bPlayerReady || PlayState.m_objInstance.m_nGameState != 2)
         {
            if(param1.m_ptTeleport.x != 0 || param1.m_ptTeleport.y != 0)
            {
               _loc2_ = MapUtils.GetTileByCoords(param1.m_ptTeleport);
               PlayState.m_objInstance.AlignPlayerToHex(_loc2_);
               EncounterCheck(_loc2_);
            }
            else if(param1.m_nTeleportRange > 0)
            {
               TeleportRange(PlayState.m_objInstance.sprPlayer,_loc2_,param1.m_nTeleportRange);
            }
            if(param1.m_fPrice != 0)
            {
               PlayState.m_objInstance.sprPlayer.Money -= param1.m_fPrice;
            }
            if((_loc6_ = DataHandler.GetCreature(param1.m_objSourceCreature.m_nCreatureID)) != null)
            {
               _loc8_ = param1.m_objSourceCreature.m_nMin + Math.random() * param1.m_objSourceCreature.m_nMax;
               _loc9_ = 0;
               while(_loc9_ < _loc8_)
               {
                  if((_loc10_ = MapUtils.GetHexRing(_loc2_.GetHexCoords(),param1.m_ptCreatureHex.x))[param1.m_ptCreatureHex.y] != null)
                  {
                     PlayState.m_objInstance.AddCreature(_loc6_,_loc10_[param1.m_ptCreatureHex.y].GetHexCoords());
                     if(param1.m_ptCreatureHex.x <= 1)
                     {
                        _loc6_.m_tilCurrentHex.nExploredState = 0;
                        _loc6_.visible = true;
                        MapUtils.tmapHexes.vVisibleHexes.push(_loc6_.m_tilCurrentHex);
                        _loc6_.AddCondition(_loc6_.GetCondition(113));
                     }
                     _loc6_ = DataHandler.GetCreature(param1.m_objSourceCreature.m_nCreatureID);
                  }
                  _loc9_++;
               }
               m_fLastReward -= 0.5 * 60 * 1000;
            }
            for each(_loc7_ in param1.m_aConditions)
            {
               if(_loc7_ > 1)
               {
                  PlayState.m_objInstance.sprPlayer.AddCondition(PlayState.m_objInstance.sprPlayer.GetCondition(_loc7_));
               }
               else if(_loc7_ < -1)
               {
                  PlayState.m_objInstance.sprPlayer.RemoveCondition(PlayState.m_objInstance.sprPlayer.GetCondition(-_loc7_));
               }
            }
         }
         _loc3_.UpdateEncounterItems(param1);
         if(param1.m_nID == 42)
         {
            for each(_loc11_ in _loc2_.m_vScavengeItems)
            {
               if(_loc11_.m_fZoom != GUIValues.m_fItemZoom)
               {
                  _loc11_.SetRes(GUIValues.m_fItemZoom);
               }
               _loc3_.AddItemToCapBox(_loc11_,_loc3_.grpAvailEncounterSlot.SocketedItem());
            }
         }
         for each(_loc4_ in param1.m_aMinimapHexes)
         {
            PlayState.m_objInstance.RevealHex(_loc4_[0],_loc4_[1],_loc4_[2],_loc4_[3]);
         }
         if(param1.m_nID != m_nNullEnc)
         {
            m_fLastTension = getTimer();
         }
         if(param1.m_nType == Encounter.TYPE_COMBAT)
         {
            _loc3_.m_nPanel = GUIInventory.PANEL_BATTLE;
         }
         else if(_loc2_.index == 20 && param1.m_nID == m_nNullEnc)
         {
            _loc3_.m_nPanel = GUIInventory.PANEL_DMC;
         }
         else
         {
            _loc3_.m_nPanel = GUIInventory.PANEL_RESPONSE;
         }
      }
      
      public static function TeleportRange(param1:Creature, param2:FlxHexTile, param3:int) : void
      {
         if(param3 <= 0)
         {
            return;
         }
         var _loc4_:Vector.<FlxHexTile> = MapUtils.GetHexRing(param2.GetHexCoords(),param3);
         var _loc5_:int = Math.floor(Math.random() * _loc4_.length);
         var _loc6_:FlxHexTile = _loc4_[_loc5_];
         var _loc7_:int = 0;
         while(_loc7_ < _loc4_.length)
         {
            if(!(!param1.CanEnterHex(_loc6_) || _loc6_ == param2))
            {
               break;
            }
            _loc5_++;
            if(_loc5_ >= _loc4_.length)
            {
               _loc5_ = 0;
            }
            _loc6_ = _loc4_[_loc5_];
            _loc7_++;
         }
         if(param1 is Player)
         {
            PlayState.m_objInstance.AlignPlayerToHex(_loc6_);
            EncounterCheck(_loc6_);
         }
         else
         {
            PlayState.m_objInstance.AlignCreatureToHex(param1,_loc6_);
         }
      }
      
      public static function EncounterCheck(param1:FlxHexTile, param2:Boolean = false, param3:Boolean = true) : void
      {
         var _loc10_:EncounterTrigger = null;
         var _loc11_:int = 0;
         var _loc12_:Boolean = false;
         var _loc13_:Boolean = false;
         var _loc14_:Creature = null;
         var _loc15_:int = 0;
         if(param1 == null || PlayState.m_objInstance.m_nGameState == PlayState.GAMESTATE_MAPEDITOR)
         {
            return;
         }
         var _loc4_:Encounter = DataHandler.GetEncounter(m_nNullEnc);
         var _loc5_:FlxPoint = param1.GetHexCoords();
         var _loc6_:Array = PlayState.m_objInstance.m_aCreatures;
         if(param1.m_vOccupants.length > 1 && param1.m_objBattle == null)
         {
            _loc12_ = false;
            _loc13_ = true;
            for each(_loc14_ in param1.m_vOccupants)
            {
               if(_loc14_.HasCondition(500) == false && _loc14_.Asleep == false)
               {
                  _loc12_ = true;
               }
               if(_loc14_ is AICreature)
               {
                  if(_loc14_.CanSeeCreature(PlayState.m_objInstance.sprPlayer) || PlayState.m_objInstance.sprPlayer.CanSeeCreature(_loc14_) == false)
                  {
                     _loc13_ = false;
                  }
               }
            }
            if(_loc12_)
            {
               param1.m_objBattle = new Battle(param1);
            }
         }
         if(param1.m_objBattle != null && (m_aEventQueue.length == 0 || m_aEventQueue.length > 0 && Encounter(m_aEventQueue[m_aEventQueue.length - 1]).m_nType != Encounter.TYPE_COMBAT))
         {
            _loc4_ = DataHandler.GetEncounter(236);
            AppendEncounter(_loc4_);
            if(PlayState.m_objInstance.sprPlayer.Asleep)
            {
               AppendEncounter(DataHandler.GetEncounter(1542));
            }
            else if(_loc13_)
            {
               AppendEncounter(DataHandler.GetEncounter(13));
            }
         }
         var _loc7_:Vector.<Creature> = param1.m_vOccupants.concat();
         _loc7_ = Battle.SortLeaders(_loc7_);
         var _loc8_:Boolean = false;
         for each(_loc14_ in _loc7_)
         {
            if(_loc8_)
            {
               break;
            }
            if(_loc14_ is AICreature)
            {
               for each(_loc15_ in AICreature(_loc14_).m_vEncounterIDs)
               {
                  if((_loc4_ = DataHandler.GetEncounter(_loc15_)).PreconditionsOK(PlayState.m_objInstance.sprPlayer))
                  {
                     AppendEncounter(_loc4_);
                     _loc8_ = true;
                     m_grpCreature = AICreature(_loc14_);
                     break;
                  }
               }
            }
            if(param1.m_objBattle != null)
            {
               param1.m_objBattle.AddCreature(_loc14_);
            }
         }
         if(param2 || param1.m_objBattle != null)
         {
            return;
         }
         var _loc9_:Array = new Array();
         for each(_loc10_ in DataHandler.EncounterTriggersRemaining)
         {
            if(_loc10_.Triggered(PlayState.m_objInstance.objDate,_loc5_,param1.index))
            {
               _loc4_ = DataHandler.GetEncounter(_loc10_.m_nEncounterID);
               AppendEncounter(_loc4_);
               if(_loc10_.m_bUnique)
               {
                  _loc9_.push(_loc10_);
               }
            }
         }
         for each(_loc10_ in _loc9_)
         {
            DataHandler.RemoveEncounterTrigger(_loc10_);
         }
         _loc11_ = getTimer();
         if(_loc4_.m_nID == m_nNullEnc && _loc11_ - m_fLastTension > m_fTensionSpacing)
         {
            CreateTension();
         }
         if(param3 && PlayState.m_objInstance.objOldDate.getDay() != PlayState.m_objInstance.objDate.getDay() && PlayState.m_objInstance.sprPlayer.HasCondition(807) == false)
         {
            RestockShops();
            PlayState.m_objInstance.sprPlayer.AddCondition(PlayState.m_objInstance.sprPlayer.GetCondition(807));
         }
      }
      
      public static function RefreshCreatures(param1:Array) : void
      {
         m_nCreatureIndex = param1.length - 1;
      }
      
      public static function UpdateCreatures(param1:Array, param2:Number, param3:Weather) : void
      {
         var _loc5_:AICreature = null;
         var _loc6_:Boolean = false;
         var _loc7_:int = 0;
         var _loc8_:Battle = null;
         var _loc9_:Boolean = false;
         var _loc10_:Creature = null;
         var _loc11_:Date = null;
         var _loc12_:Number = NaN;
         var _loc13_:Encounter = null;
         m_nLastTick = FlxU.getTicks();
         var _loc4_:Vector.<Battle> = new Vector.<Battle>();
         for each(_loc5_ in param1)
         {
            _loc6_ = true;
            if(_loc5_.m_tilCurrentHex == PlayState.m_objInstance.tilCurrentHex)
            {
               if((_loc7_ = int(_loc4_.indexOf(_loc5_.m_tilCurrentHex.m_objBattle))) >= 0)
               {
                  _loc4_.splice(_loc7_,1);
               }
            }
            else if(_loc5_.m_tilCurrentHex.m_objBattle != null)
            {
               (_loc8_ = _loc5_.m_tilCurrentHex.m_objBattle).AddCreature(_loc5_);
               if(_loc4_.indexOf(_loc8_) < 0)
               {
                  _loc4_.push(_loc8_);
               }
               _loc6_ = false;
            }
            else if(_loc5_.m_tilCurrentHex.m_vOccupants.length > 1)
            {
               _loc9_ = false;
               for each(_loc10_ in _loc5_.m_tilCurrentHex.m_vOccupants)
               {
                  if(_loc10_.m_nFaction != _loc5_.m_nFaction && _loc10_.HasCondition(500) == false)
                  {
                     _loc9_ = true;
                     break;
                  }
               }
               if(_loc9_)
               {
                  _loc8_ = new Battle(_loc5_.m_tilCurrentHex);
                  _loc5_.m_tilCurrentHex.m_objBattle = _loc8_;
                  if(_loc4_.indexOf(_loc8_) < 0)
                  {
                     _loc4_.push(_loc8_);
                  }
                  _loc6_ = false;
               }
            }
            if(_loc6_)
            {
               _loc5_.EndTurn(param2,param3);
            }
         }
         for each(_loc8_ in _loc4_)
         {
            _loc11_ = PlayState.m_objInstance.objDate;
            _loc12_ = 0;
            while(_loc12_ < param2)
            {
               _loc8_.GetOptions();
               _loc13_ = _loc8_.HandleCombatResponse(null,null,true);
               _loc8_.AdvanceTurns();
               if(_loc13_.m_nID == m_nNullEnc)
               {
                  break;
               }
               PlayState.m_objInstance.objDate.setTime(PlayState.m_objInstance.objDate.getTime() + PlayState.HOURS_PER_COMBAT_TURN * 60 * 60 * 1000);
               _loc12_ += PlayState.HOURS_PER_COMBAT_TURN;
            }
            PlayState.m_objInstance.objDate.setTime(_loc11_.getTime());
         }
      }
      
      public static function MoveCreatures(param1:Array) : uint
      {
         var _loc2_:uint = 0;
         var _loc3_:AICreature = null;
         if(m_nCreatureIndex >= 0 && param1.length > m_nCreatureIndex)
         {
            _loc2_ = FlxU.getTicks();
            _loc3_ = AICreature(param1[m_nCreatureIndex]);
            if(_loc3_.visible == false || _loc2_ - m_nLastTick > m_fCreatureMoveDelay * 1000 || PlayState.m_objInstance.sprPlayer.Asleep)
            {
               m_nLastTick = _loc2_;
               if(_loc3_.CanMove())
               {
                  _loc3_.Move();
                  return MOVERESULT_MOVED;
               }
               --m_nCreatureIndex;
               return MOVERESULT_WAIT;
            }
            return MOVERESULT_WAIT;
         }
         return MOVERESULT_DONE;
      }
      
      public static function Rand(param1:uint) : Number
      {
         var _loc2_:Number = Math.random();
         switch(param1)
         {
            case RAND_LOW:
               return _loc2_ * _loc2_;
            case RAND_HIGH:
               return 1 - _loc2_ * _loc2_;
            case RAND_MID:
               _loc2_ = 2 * _loc2_ - 1;
               _loc2_ = _loc2_ * _loc2_ * _loc2_;
               return 0.5 * _loc2_ + 0.5;
            default:
               return _loc2_;
         }
      }
   }
}

package
{
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class Player extends Creature
   {
       
      
      public var ptMaxSkillCap:FlxPoint;
      
      public var ptSkillCap:FlxPoint;
      
      private var m_fMoney:Number;
      
      public var m_strVersion:String;
      
      public var m_vKnownRecipes:Vector.<Recipe>;
      
      public function Player()
      {
         var _loc1_:AICreature = DataHandler.GetCreature(23);
         super(_loc1_.m_strNamePrivate,_loc1_.m_strNamePublic,_loc1_.m_strImage,_loc1_.m_nMovesPerTurn,_loc1_.m_nFaction,_loc1_.m_vBaseAttackModes,_loc1_.m_vBaseConditions,new Dictionary());
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         this.ptMaxSkillCap = null;
         this.ptSkillCap = null;
         this.m_strVersion = null;
         if(this.m_vKnownRecipes != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vKnownRecipes.length)
            {
               this.m_vKnownRecipes[_loc1_] = null;
               _loc1_++;
            }
            this.m_vKnownRecipes = null;
         }
         super.destroy();
      }
      
      override public function Initialize(param1:Vector.<Vector.<Number>> = null, param2:Boolean = false) : void
      {
         if(param1 == null)
         {
            param1 = new Vector.<Vector.<Number>>();
         }
         if(PlayState.m_objInstance.bScavTut)
         {
            param1.push(Vector.<Number>([338,1]));
         }
         this.m_vKnownRecipes = new Vector.<Recipe>();
         super.Initialize(param1);
         this.ptMaxSkillCap = new FlxPoint(24 * GUIInventorySlot.CapacityPixel,14 * GUIInventorySlot.CapacityPixel);
         this.ptSkillCap = new FlxPoint(12 * GUIInventorySlot.CapacityPixel,4 * GUIInventorySlot.CapacityPixel);
         this.m_fMoney = 0;
         m_bSpied = true;
      }
      
      override public function AddCondition(param1:PlayerCondition, param2:Boolean = true, param3:Boolean = true) : void
      {
         var _loc4_:String = null;
         if(m_vImmunities.indexOf(param1.m_nID) >= 0)
         {
            return;
         }
         super.AddCondition(param1);
         if(m_bCondQueue && param1.bFatal == false)
         {
            return;
         }
         if(param2 && param1.m_bDisplay && (param1.m_nStacked == 1 || param1.m_bStackable))
         {
            _loc4_ = param1.strDesc.replace(/<us>/gi,Name);
            this.MessageFloaty(_loc4_,param3,null,param1.m_nColor);
         }
         if(m_bCondQueue)
         {
            return;
         }
         PlayState.m_objInstance.UpdatePlayerUI();
      }
      
      override public function RemoveCondition(param1:PlayerCondition) : void
      {
         super.RemoveCondition(param1);
         if(m_bCondQueue)
         {
            return;
         }
         PlayState.m_objInstance.UpdatePlayerUI();
      }
      
      override public function AddAttackMode(param1:AttackMode) : void
      {
         var _loc3_:int = 0;
         super.AddAttackMode(param1);
         var _loc2_:* = param1.m_fDamageCut + param1.m_fDamageBlunt > CurrentAttackMode.m_fDamageCut + CurrentAttackMode.m_fDamageBlunt;
         if(_loc2_)
         {
            _loc2_ = param1.IsLoaded();
         }
         if(_loc2_ && CurrentAttackMode.m_vChargeProfiles.length > 0 && CurrentAttackMode.m_vChargeProfiles[0].m_strItemID == CurrentAttackMode.m_objItem.IDString && CurrentAttackMode.IsLoaded())
         {
            _loc2_ = false;
         }
         if(_loc2_)
         {
            _loc3_ = int(m_vAttackModes.indexOf(param1));
            if(_loc3_ >= 0)
            {
               this.ChangeAttackMode(_loc3_);
            }
         }
      }
      
      override public function AddWound(param1:FlxGroup, param2:uint, param3:String, param4:Number, param5:FlxPoint, param6:int, param7:Vector.<String>, param8:Vector.<String>, param9:Vector.<String>, param10:Vector.<String>, param11:Vector.<String>, param12:Vector.<String>, param13:String, param14:Boolean, param15:Array, param16:Array, param17:Boolean = false, param18:Number = 1) : GUIInventoryWound
      {
         var _loc19_:GUIInventoryWound = super.AddWound(param1,param2,param3,param4,param5,param6,param7,param8,param9,param10,param11,param12,param13,param14,param15,param16,param17,param18);
         PlayState.m_objInstance.m_aMouseOverItems.push(_loc19_);
         return _loc19_;
      }
      
      override public function RemoveAttackMode(param1:AttackMode) : void
      {
         var _loc2_:int = int(m_nAttackMode);
         super.RemoveAttackMode(param1);
         if(_loc2_ != m_nAttackMode)
         {
            PlayState.m_objInstance.UpdatePlayerUI();
         }
      }
      
      override public function ChangeAttackMode(param1:int) : void
      {
         var _loc2_:int = int(m_nAttackMode);
         super.ChangeAttackMode(param1);
         PlayState.m_objInstance.UpdatePlayerUI();
         if(m_tilCurrentHex != null && PlayState.m_objInstance.grpInventoryUI.grpBattleLayer.GetBattle() != null)
         {
            PlayState.m_objInstance.grpInventoryUI.UpdateCombatItems();
         }
      }
      
      override public function KillCreature(param1:String, param2:String, param3:String = "") : void
      {
         m_bCondQueue = true;
         super.KillCreature(param1,param2,param3);
         PlayState.m_objInstance.UpdatePlayer();
      }
      
      public function PlayerCanSee(param1:Creature) : Boolean
      {
         var _loc2_:Boolean = false;
         if((Alive == false || m_bKillQueue) && param1 != this)
         {
            return false;
         }
         var _loc3_:CombatPair = m_dictCombatPairs[param1];
         if(param1 == this)
         {
            _loc2_ = true;
         }
         else if(_loc3_ != null)
         {
            _loc2_ = _loc3_.ThemSpotted;
         }
         else if(m_tilCurrentHex != null && m_tilCurrentHex.m_objBattle == null && param1.m_tilCurrentHex != null && param1.m_tilCurrentHex.nExploredState == 0)
         {
            _loc2_ = CanSeeCreature(param1);
         }
         return _loc2_;
      }
      
      override public function set JustMoved(param1:int) : void
      {
         if(HasCondition(120) || HasCondition(121))
         {
            PlayState.m_objInstance.PlayerHide();
         }
         super.JustMoved = param1;
      }
      
      override public function set Asleep(param1:Boolean) : void
      {
         var _loc2_:Boolean = bAsleep;
         super.Asleep = param1;
         if(_loc2_)
         {
            if(!bAsleep)
            {
               PlayState.m_objInstance.Mode(PlayState.GAMESTATE_GAMEREADY);
               PlayState.m_objInstance.UpdatePlayer();
            }
         }
         else if(bAsleep)
         {
            if(PlayState.m_objInstance.m_nGameState > PlayState.GAMESTATE_READINGMAPCOMPLETE)
            {
               PlayState.m_objInstance.Mode(PlayState.GAMESTATE_SLEEPING);
            }
         }
      }
      
      override public function set VisionRange(param1:Number) : void
      {
         super.VisionRange = param1;
         PlayState.m_objInstance.UpdateVisibility(m_tilCurrentHex);
      }
      
      override public function set MinLightLevel(param1:Number) : void
      {
         super.MinLightLevel = param1;
         PlayState.m_objInstance.UpdateVisibility(m_tilCurrentHex);
      }
      
      override public function set BaseDetectionLevel(param1:Number) : void
      {
         super.BaseDetectionLevel = param1;
         PlayState.m_objInstance.UpdateVisibility(m_tilCurrentHex);
      }
      
      override public function set LightLevel(param1:Number) : void
      {
         super.LightLevel = param1;
         PlayState.m_objInstance.UpdateVisibility(m_tilCurrentHex);
      }
      
      public function get Money() : Number
      {
         return this.m_fMoney;
      }
      
      public function set Money(param1:Number) : void
      {
         if(isNaN(param1))
         {
            return;
         }
         this.m_fMoney = param1;
         PlayState.m_objInstance.UpdatePlayerUI();
      }
      
      override public function Spawn(param1:FlxPoint) : void
      {
         m_tilCurrentHex = MapUtils.GetTileByCoords(param1);
         PlayState.m_objInstance.AlignPlayerToHex(m_tilCurrentHex);
      }
      
      override public function MessageFloaty(param1:String, param2:Boolean = true, param3:FlxPoint = null, param4:int = -1) : void
      {
         if(param4 == -1)
         {
            param4 = int(GUIMessageWindow.COLOR_DEFAULT);
         }
         if(param3 == null && members.length > 0)
         {
            param3 = FlxSprite(members[0]).getMidpoint();
            param3.y -= FlxSprite(members[0]).height / 2;
         }
         PlayState.m_objInstance.grpMsg.MessageFloaty(param1,param2,param3,param4);
      }
      
      override public function UpdateStatus() : void
      {
         super.UpdateStatus();
      }
      
      override public function set Encumberance(param1:Number) : void
      {
         super.Encumberance = param1;
         PlayState.m_objInstance.UpdatePlayerUI();
      }
      
      override public function set TriggerEncounter(param1:int) : void
      {
         if(PlayState.m_objInstance.m_nGameState < PlayState.GAMESTATE_GAMEREADY)
         {
            return;
         }
         if(param1 < 0)
         {
            return;
         }
         var _loc2_:Encounter = DataHandler.GetEncounter(param1);
         if(!_loc2_.PreconditionsOK(this))
         {
            return;
         }
         DM.AppendEncounter(_loc2_);
         var _loc3_:Boolean = true;
         if(PlayState.m_objInstance.m_nGameState == PlayState.GAMESTATE_INVENTORY && PlayState.m_objInstance.grpInventoryUI.m_nState != GUIInventory.STATE_NORMAL)
         {
            _loc3_ = false;
         }
         if(_loc3_)
         {
            DM.NextEncounter();
         }
      }
      
      override public function set AddRecipe(param1:int) : void
      {
         var _loc5_:Array = null;
         var _loc6_:Recipe = null;
         var _loc2_:Recipe = DataHandler.GetRecipe(param1);
         if(_loc2_ == null)
         {
            return;
         }
         if(_loc2_.m_nHiddenID > 0)
         {
            this.AddRecipe = _loc2_.m_nHiddenID;
            return;
         }
         var _loc3_:int = int(this.m_vKnownRecipes.indexOf(_loc2_));
         var _loc4_:GUIInventory = PlayState.m_objInstance.grpInventoryUI;
         if(_loc3_ < 0)
         {
            this.m_vKnownRecipes.push(_loc2_);
            _loc2_.m_bKnown = true;
            _loc5_ = [];
            for each(_loc6_ in this.m_vKnownRecipes)
            {
               _loc5_.push(_loc6_);
            }
            _loc5_ = _loc5_.sortOn("m_strName");
            this.m_vKnownRecipes = Vector.<Recipe>(_loc5_);
         }
      }
      
      public function AddTrait(param1:Array, param2:Boolean) : void
      {
         if(param1.length <= 0)
         {
            return;
         }
         var _loc3_:String = param1[0];
         if(param2 == false)
         {
            return this.RemoveTrait(param1,true);
         }
         PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(DataHandler.GetItem(_loc3_),PlayState.m_objInstance.grpInventoryUI.grpTraitSlot,true);
         var _loc4_:ItemInstance = DataHandler.GetItem("96.4");
         if(PlayState.m_objInstance.grpInventoryUI.FindSpaceInCapBox(_loc4_,PlayState.m_objInstance.grpInventoryUI.grpTraitSlot.SocketedItem()).m_nResult == GUIFitItemResult.RESULT_CANNOT_FIT)
         {
            this.AddCondition(GetCondition(355),false,false);
         }
      }
      
      public function RemoveTrait(param1:Array, param2:Boolean) : void
      {
         if(param1.length <= 0)
         {
            return;
         }
         var _loc3_:String = param1[0];
         if(param2 == false)
         {
            return this.AddTrait(param1,true);
         }
         var _loc4_:ItemInstance = PlayState.m_objInstance.grpInventoryUI.grpTraitSlot.SocketedItem();
         var _loc5_:Array = _loc3_.split(".");
         var _loc6_:Vector.<ItemInstance>;
         if((_loc6_ = _loc4_.GetItems(_loc5_[0],_loc5_[1])).length > 0)
         {
            if((_loc4_ = PlayState.m_objInstance.grpInventoryUI.grpTraitSlot.RemoveItem(_loc6_[0],true)) != null && HasCondition(355))
            {
               this.RemoveCondition(GetCondition(355));
            }
         }
      }
      
      public function AddSkill(param1:Array, param2:Boolean) : void
      {
         if(param1.length <= 0)
         {
            return;
         }
         var _loc3_:String = param1[0];
         if(param2 == false)
         {
            return this.RemoveSkill(param1,true);
         }
         PlayState.m_objInstance.grpInventoryUI.AddItemToSlot(DataHandler.GetItem(_loc3_),PlayState.m_objInstance.grpInventoryUI.grpSkillSlot,true);
         var _loc4_:ItemInstance = DataHandler.GetItem("91.0");
         if(PlayState.m_objInstance.grpInventoryUI.FindSpaceInCapBox(_loc4_,PlayState.m_objInstance.grpInventoryUI.grpSkillSlot.SocketedItem()).m_nResult == GUIFitItemResult.RESULT_CANNOT_FIT)
         {
            this.AddCondition(GetCondition(809),false,false);
         }
      }
      
      public function RemoveSkill(param1:Array, param2:Boolean) : void
      {
         if(param1.length <= 0)
         {
            return;
         }
         var _loc3_:String = param1[0];
         if(param2 == false)
         {
            return this.AddSkill(param1,true);
         }
         var _loc4_:ItemInstance = PlayState.m_objInstance.grpInventoryUI.grpSkillSlot.SocketedItem();
         var _loc5_:Array = _loc3_.split(".");
         var _loc6_:Vector.<ItemInstance>;
         if((_loc6_ = _loc4_.GetItems(_loc5_[0],_loc5_[1])).length > 0)
         {
            if((_loc4_ = PlayState.m_objInstance.grpInventoryUI.grpSkillSlot.RemoveItem(_loc6_[0],true)) != null && HasCondition(809))
            {
               this.RemoveCondition(GetCondition(809));
            }
         }
      }
      
      public function get UseGPS() : int
      {
         return 0;
      }
      
      public function set UseGPS(param1:int) : void
      {
         var _loc3_:Vector.<FlxHexTile> = null;
         var _loc9_:FlxHexTile = null;
         var _loc2_:int = param1;
         var _loc4_:Date = PlayState.m_objInstance.objDate;
         var _loc5_:Date = new Date();
         var _loc6_:Number = 0.25;
         var _loc7_:int = 0;
         _loc5_.setTime(_loc4_.getTime() - 24 * 60 * 60 * 1000);
         var _loc8_:int = 1;
         while(_loc8_ < _loc2_)
         {
            _loc3_ = MapUtils.GetHexRing(m_tilCurrentHex.GetHexCoords(),_loc8_);
            for each(_loc9_ in _loc3_)
            {
               if(_loc9_.m_objLastScavengeUpdate == null)
               {
                  _loc9_.m_objLastScavengeUpdate = new Date();
               }
               _loc9_.m_objLastScavengeUpdate.setTime(_loc5_.getTime());
               _loc9_.UpdateScavengeItems(PlayState.m_objInstance.objDate,true);
               if(Math.random() < _loc6_)
               {
                  PlayState.m_objInstance.RevealHex(_loc9_.GetHexCoords().x,_loc9_.GetHexCoords().y);
                  _loc7_++;
               }
            }
            _loc8_++;
         }
         this.MessageFloaty("Discovered " + _loc7_ + " hexes.",false);
      }
      
      public function get ResetTemp() : int
      {
         return 0;
      }
      
      public function set ResetTemp(param1:int) : void
      {
         fCoreTemp = fNormalBodyTemp;
      }
      
      public function get GetDiagnostic() : int
      {
         return 0;
      }
      
      public function set GetDiagnostic(param1:int) : void
      {
         var _loc5_:PlayerCondition = null;
         var _loc2_:* = "病情诊断:\n\n";
         var _loc3_:Vector.<int> = Vector.<int>([40,41,42,48,49,65,66,77,78,79,106,132,133,134,135,174,175,182,183,186,187,189,190,191,193,195,198,199,200,205,292,295,296,297,303,304,305,306,307,308,316,317,318,319,468,609,610,611,613,616,618,619,620,621]);
         var _loc4_:int = 0;
         for each(_loc5_ in aCurrentStates)
         {
            if(_loc3_.indexOf(_loc5_.m_nID) >= 0)
            {
               if(_loc4_ > 0)
               {
                  _loc2_ += ", ";
               }
               _loc2_ += _loc5_.strName;
               _loc4_++;
            }
         }
         _loc2_ += ".";
         if(_loc4_ == 0)
         {
            _loc2_ = "病情诊断:\n\n非常健康。";
         }
         var _loc6_:Encounter;
         (_loc6_ = new Encounter(100000,"Patient Diagnosis",_loc2_)).m_aResponses = [new PlayerResponse([],param1,1,new Array())];
         DM.AppendEncounter(_loc6_);
      }
      
      public function ChangeGlobalFactionRep(param1:Array, param2:Boolean) : void
      {
         var _loc7_:Creature = null;
         var _loc3_:Number = Number(param1[1]);
         if(_loc3_ == 0)
         {
            return;
         }
         var _loc4_:int = int(param1[0]);
         var _loc5_:Boolean = DataHandler.StrToBoolean(param1[2]);
         var _loc6_:Number = -1;
         if(param2)
         {
            _loc6_ = 1;
         }
         if(DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictFactions[_loc4_][0] == undefined)
         {
            DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictFactions[_loc4_][0] = GetFactionRep(0);
         }
         DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictFactions[_loc4_][0] = DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictFactions[_loc4_][0] + _loc6_ * _loc3_;
         if(_loc5_)
         {
            for each(_loc7_ in PlayState.m_objInstance.m_aCreatures)
            {
               if(_loc7_.m_nFaction == _loc4_)
               {
                  _loc7_.m_dictFactions[0] += _loc6_ * _loc3_;
               }
            }
         }
      }
      
      public function Confiscate(param1:Array, param2:Boolean) : void
      {
         var _loc7_:ItemInstance = null;
         var _loc8_:Ingredient = null;
         var _loc9_:Vector.<ItemInstance> = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:Vector.<ItemInstance> = null;
         var _loc12_:Vector.<ItemInstance> = null;
         var _loc13_:GUIInventorySlot = null;
         var _loc14_:ItemInstance = null;
         if(param1 == null || param1.length < 2)
         {
            return;
         }
         var _loc3_:int = int(param1[0]);
         var _loc4_:Boolean = DataHandler.StrToBoolean(param1[1]);
         var _loc5_:Boolean = DataHandler.StrToBoolean(param1[2]);
         var _loc6_:GUIInventory = PlayState.m_objInstance.grpInventoryUI;
         if(param2 == false)
         {
            return;
         }
         if(_loc4_)
         {
            if(m_objLocker == null)
            {
               return;
            }
            _loc6_.AddItemToSlot(m_objLocker,grpGroundSlot);
            m_objLocker.ItemDefinition.fDegradePerHour = 100;
            m_objLocker.ItemDefinition.fEquipDegradePerHour = 100;
            m_objLocker = null;
         }
         else
         {
            _loc7_ = m_objLocker;
            _loc8_ = DataHandler.GetIngredient(_loc3_);
            _loc9_ = new Vector.<ItemInstance>();
            if(_loc7_ == null)
            {
               for each(_loc13_ in _loc6_.vItemSlots)
               {
                  for each(_loc14_ in _loc13_.GetAllSocketedItems())
                  {
                     if(_loc14_.IDString == "26.11")
                     {
                        _loc9_.push(_loc14_);
                     }
                     _loc9_ = _loc9_.concat(_loc14_.GetItems(26,11));
                  }
               }
               if(_loc9_.length > 0)
               {
                  if((_loc7_ = _loc9_[0]).bSocketed)
                  {
                     _loc7_.Slot.UnSocketItem(true,_loc7_);
                  }
                  else if(_loc7_.Slot != null)
                  {
                     _loc7_.Slot.RemoveItem(_loc7_,true);
                  }
                  else
                  {
                     _loc7_.m_objParentContainer.RemoveItem(_loc7_,true);
                  }
               }
            }
            if(_loc7_ == null)
            {
               _loc7_ = DataHandler.GetItem("26.11");
            }
            if(_loc5_)
            {
               _loc7_.vItems.length = 0;
            }
            for each(_loc13_ in _loc6_.vItemSlots)
            {
               for each(_loc14_ in _loc13_.GetAllSocketedItems())
               {
                  _loc9_.push(_loc14_);
                  _loc9_ = _loc9_.concat(_loc14_.GetItems());
               }
            }
            _loc11_ = new Vector.<ItemInstance>();
            _loc12_ = new Vector.<ItemInstance>();
            for each(_loc14_ in _loc9_)
            {
               if(_loc12_.indexOf(_loc14_) < 0 && _loc8_.ItemMatches(_loc14_))
               {
                  if((_loc10_ = RemoveItem(_loc14_,true)) != null)
                  {
                     _loc11_.push(_loc10_);
                     _loc12_.push(_loc10_);
                     _loc12_ = _loc12_.concat(_loc10_.GetItems());
                  }
               }
            }
            _loc11_ = _loc11_.sort(PlayState.m_objInstance.grpInventoryUI.CompareItems);
            for each(_loc14_ in _loc11_)
            {
               _loc6_.AddItemToCapBox(_loc14_,_loc7_,null,true);
            }
            _loc7_.fDurability = 1;
            _loc7_.ItemDefinition.fDegradePerHour = 0;
            _loc7_.ItemDefinition.fEquipDegradePerHour = 0;
            m_objLocker = _loc7_;
         }
      }
      
      override public function GetPopUpText(param1:Boolean = false) : String
      {
         var _loc2_:String = super.GetPopUpText(param1);
         return _loc2_ + ("\n潜行： " + (HasCondition(120) || HasCondition(121)));
      }
      
      override public function set ExitBattle(param1:int) : void
      {
         GUIBattleScreen.grpInstance.ClearBattle();
         super.ExitBattle = param1;
      }
      
      public function EndGame(param1:Array, param2:Boolean) : void
      {
         if(param1 == null || param1.length < 1 || param2 == false)
         {
            return;
         }
         PlayState.m_objInstance.m_nEndingID = param1[0];
         PlayState.m_objInstance.EndGame();
      }
   }
}

package
{
   import org.flixel.*;
   
   public class GUIBattleScreen extends FlxGroup
   {
      
      public static var grpInstance:GUIBattleScreen;
       
      
      private var sprPlayer:Player;
      
      private var grpNext:FlxGroup;
      
      private var grpPrev:FlxGroup;
      
      private var sprPlayerImg:FlxSprite;
      
      private var sprPlayerTargetImg:FlxSprite;
      
      private var btnPlayerMoveImg:ImgButton;
      
      private var sprPlayerPanel:FlxSprite;
      
      private var txtPlayerName:FlxText;
      
      private var txtPlayerFields:FlxText;
      
      private var txtPlayerVals:FlxText;
      
      private var txtPlayerFields2:FlxText;
      
      private var txtPlayerVals2:FlxText;
      
      private var txtPlayerVals3:FlxText;
      
      private var txtPlayerVs:FlxText;
      
      private var sprEnemyPanel:FlxSprite;
      
      public var sprEnemy:FlxSprite;
      
      public var sprEnemyTarget:FlxSprite;
      
      private var btnEnemyMove:ImgButton;
      
      private var sprEnemyNext:FlxSprite;
      
      private var sprEnemyPrev:FlxSprite;
      
      private var txtEnemyName:FlxText;
      
      private var txtEnemyFields:FlxText;
      
      private var txtEnemyVals:FlxText;
      
      private var txtEnemyFields2:FlxText;
      
      private var txtEnemyVals2:FlxText;
      
      private var txtEnemyVals3:FlxText;
      
      private var txtEnemyVs:FlxText;
      
      private var btnPrevEnemy:ImgButton;
      
      private var btnNextEnemy:ImgButton;
      
      private var sprHex:FlxSprite;
      
      private var txtHexFields:FlxText;
      
      private var txtHexVals:FlxText;
      
      private var sprCurrentTarget:Creature;
      
      private var m_tilHex:FlxHexTile;
      
      public function GUIBattleScreen()
      {
         super();
         this.sprPlayer = PlayState.m_objInstance.sprPlayer;
         grpInstance = this;
         this.grpPrev = new FlxGroup();
         this.grpNext = new FlxGroup();
         this.sprPlayerPanel = new FlxSprite();
         this.sprPlayerImg = new FlxSprite();
         this.sprPlayerTargetImg = new FlxSprite();
         this.btnPlayerMoveImg = new ImgButton("ItmEncTime.png","ItmEncTime.png","ItmEncTime.png","ItmEncTime.png",0,0);
         this.btnPlayerMoveImg.m_strPopUpText = "";
         this.txtPlayerName = new FlxText(0,0,10,"Player");
         this.txtPlayerFields = new FlxText(0,0,10,"");
         this.txtPlayerFields.text = "行动:\n" + "\n\n" + "遮挡:\n" + "可见:\n" + "武器:\n";
         this.txtPlayerVals = new FlxText(0,0,10,"");
         this.txtPlayerFields2 = new FlxText(0,0,10,"状态:");
         this.txtPlayerVals2 = new FlxText(0,0,10,"");
         this.txtPlayerVals3 = new FlxText(0,0,10,"");
         this.txtPlayerVs = new FlxText(0,0,10,"vs.");
         this.sprEnemyPanel = new FlxSprite();
         this.sprEnemy = new FlxSprite();
         this.sprEnemyTarget = new FlxSprite();
         this.btnEnemyMove = new ImgButton("ItmEncTime.png","ItmEncTime.png","ItmEncTime.png","ItmEncTime.png",0,0);
         this.btnEnemyMove.m_strPopUpText = "";
         this.sprEnemyNext = new FlxSprite();
         this.sprEnemyPrev = new FlxSprite();
         this.txtEnemyName = new FlxText(0,0,10,"Enemy");
         this.txtEnemyFields = new FlxText(0,0,10,"");
         this.txtEnemyFields.text = "行动:\n" + "\n\n" + "遮挡:\n" + "可见:\n" + "武器:\n";
         this.txtEnemyVals = new FlxText(0,0,10,"");
         this.txtEnemyFields2 = new FlxText(0,0,10,"状态:");
         this.txtEnemyVals2 = new FlxText(0,0,10,"");
         this.txtEnemyVals3 = new FlxText(0,0,10,"");
         this.txtEnemyVs = new FlxText(0,0,10,"vs.");
         this.btnPrevEnemy = new ImgButton("btn_cbt_prev_dn.png","btn_cbt_prev.png","btn_cbt_prev_on.png","btn_cbt_prev_on.png",0,0,this.PreviousEnemy);
         this.btnNextEnemy = new ImgButton("btn_cbt_next_dn.png","btn_cbt_next.png","btn_cbt_next_on.png","btn_cbt_next_on.png",0,0,this.NextEnemy);
         this.sprHex = new FlxSprite();
         this.txtHexFields = new FlxText(0,0,10,"");
         this.txtHexFields.text = "距离:\n" + "\n" + "隐蔽:\n" + "地形:\n";
         this.txtHexVals = new FlxText(0,0,10,"");
         this.grpPrev.add(this.sprEnemyPrev);
         this.grpPrev.add(this.btnPrevEnemy);
         this.grpNext.add(this.sprEnemyNext);
         this.grpNext.add(this.btnNextEnemy);
         PlayState.m_objInstance.m_aMouseOverItems.push(this.btnPlayerMoveImg);
         PlayState.m_objInstance.m_aMouseOverItems.push(this.btnEnemyMove);
         add(this.sprPlayerPanel);
         add(this.sprEnemyPanel);
         add(this.sprPlayerImg);
         add(this.sprPlayerTargetImg);
         add(this.btnPlayerMoveImg);
         add(this.txtPlayerName);
         add(this.txtPlayerFields);
         add(this.txtPlayerVals);
         add(this.txtPlayerFields2);
         add(this.txtPlayerVals2);
         add(this.txtPlayerVals3);
         add(this.txtPlayerVs);
         add(this.grpPrev);
         add(this.grpNext);
         add(this.sprEnemy);
         add(this.sprEnemyTarget);
         add(this.btnEnemyMove);
         add(this.txtEnemyName);
         add(this.txtEnemyFields);
         add(this.txtEnemyVals);
         add(this.txtEnemyFields2);
         add(this.txtEnemyVals2);
         add(this.txtEnemyVals3);
         add(this.txtEnemyVs);
         add(this.sprHex);
         add(this.txtHexFields);
         add(this.txtHexVals);
         setAll("scrollFactor",new FlxPoint(0,0));
         setAll("cameras",[FlxG.camera]);
         this.SetRes();
      }
      
      override public function destroy() : void
      {
         grpInstance = null;
         this.sprPlayer = null;
         this.grpNext = DataHandler.DestroyObject(this.grpNext);
         this.grpPrev = DataHandler.DestroyObject(this.grpPrev);
         this.sprPlayerImg = DataHandler.DestroyObject(this.sprPlayerImg);
         this.sprPlayerTargetImg = DataHandler.DestroyObject(this.sprPlayerTargetImg);
         var _loc1_:int = int(PlayState.m_objInstance.m_aMouseOverItems.indexOf(this.btnPlayerMoveImg));
         if(_loc1_ >= 0)
         {
            PlayState.m_objInstance.m_aMouseOverItems.splice(_loc1_,1);
         }
         this.btnPlayerMoveImg = DataHandler.DestroyObject(this.btnPlayerMoveImg);
         this.sprPlayerPanel = DataHandler.DestroyObject(this.sprPlayerPanel);
         this.txtPlayerName = DataHandler.DestroyObject(this.txtPlayerName);
         this.txtPlayerFields = DataHandler.DestroyObject(this.txtPlayerFields);
         this.txtPlayerVals = DataHandler.DestroyObject(this.txtPlayerVals);
         this.txtPlayerFields2 = DataHandler.DestroyObject(this.txtPlayerFields2);
         this.txtPlayerVals2 = DataHandler.DestroyObject(this.txtPlayerVals2);
         this.txtPlayerVals3 = DataHandler.DestroyObject(this.txtPlayerVals3);
         this.txtPlayerVs = DataHandler.DestroyObject(this.txtPlayerVs);
         this.sprEnemyPanel = DataHandler.DestroyObject(this.sprEnemyPanel);
         this.sprEnemy = DataHandler.DestroyObject(this.sprEnemy);
         this.sprEnemyTarget = DataHandler.DestroyObject(this.sprEnemyTarget);
         _loc1_ = int(PlayState.m_objInstance.m_aMouseOverItems.indexOf(this.btnEnemyMove));
         if(_loc1_ >= 0)
         {
            PlayState.m_objInstance.m_aMouseOverItems.splice(_loc1_,1);
         }
         this.btnEnemyMove = DataHandler.DestroyObject(this.btnEnemyMove);
         this.sprEnemyNext = DataHandler.DestroyObject(this.sprEnemyNext);
         this.sprEnemyPrev = DataHandler.DestroyObject(this.sprEnemyPrev);
         this.txtEnemyName = DataHandler.DestroyObject(this.txtEnemyName);
         this.txtEnemyFields = DataHandler.DestroyObject(this.txtEnemyFields);
         this.txtEnemyVals = DataHandler.DestroyObject(this.txtEnemyVals);
         this.txtEnemyFields2 = DataHandler.DestroyObject(this.txtEnemyFields2);
         this.txtEnemyVals2 = DataHandler.DestroyObject(this.txtEnemyVals2);
         this.txtEnemyVals3 = DataHandler.DestroyObject(this.txtEnemyVals3);
         this.txtEnemyVs = DataHandler.DestroyObject(this.txtEnemyVs);
         this.btnPrevEnemy = DataHandler.DestroyObject(this.btnPrevEnemy);
         this.btnNextEnemy = DataHandler.DestroyObject(this.btnNextEnemy);
         this.sprHex = DataHandler.DestroyObject(this.sprHex);
         this.txtHexFields = DataHandler.DestroyObject(this.txtHexFields);
         this.txtHexVals = DataHandler.DestroyObject(this.txtHexVals);
         this.sprCurrentTarget = null;
         this.m_tilHex = null;
      }
      
      public function DisplayCombatPair() : void
      {
         var _loc8_:Creature = null;
         var _loc9_:CombatPair = null;
         var _loc1_:CombatPair = this.CurrentCombatPair;
         if(_loc1_ == null)
         {
            return;
         }
         var _loc2_:PlayState = PlayState.m_objInstance;
         var _loc3_:* = "";
         if(_loc1_.ThemSpotted)
         {
            _loc3_ += _loc1_.nRange + "\n";
         }
         else
         {
            _loc3_ += "?\n";
         }
         _loc3_ += "\n" + this.GetDescriptor(this.m_tilHex.m_objBattle.fCover) + "\n" + this.GetTerrainDescriptor(this.m_tilHex.m_objBattle.fGround) + "\n";
         this.txtHexVals.text = _loc3_;
         this.txtPlayerName.text = _loc1_.sprUs.Name;
         this.UpdateCreatureSprite(_loc1_.sprUs);
         if(_loc1_.ThemSpotted)
         {
            this.txtEnemyName.text = _loc1_.sprThem.Name;
         }
         else
         {
            this.txtEnemyName.text = "?";
         }
         this.UpdateCreatureSprite(_loc1_.sprThem);
         this.grpPrev.kill();
         this.grpPrev.visible = false;
         this.grpNext.kill();
         this.grpNext.visible = false;
         if(this.m_tilHex.m_objBattle.NumberCombatants() > 2)
         {
            if((_loc8_ = this.m_tilHex.m_objBattle.PrevCombatant(this.sprCurrentTarget,this.sprPlayer)) != null)
            {
               this.grpPrev.revive();
               this.grpPrev.visible = true;
               this.UpdateCreatureSprite(_loc8_);
            }
            if((_loc8_ = this.m_tilHex.m_objBattle.NextCombatant(this.sprCurrentTarget,this.sprPlayer)) != null)
            {
               this.grpNext.revive();
               this.grpNext.visible = true;
               this.UpdateCreatureSprite(_loc8_);
            }
         }
         if(!_loc1_.ThemSpotted)
         {
            _loc3_ = "?";
         }
         else if(_loc1_.UsSpotted)
         {
            _loc3_ = "是";
         }
         else
         {
            _loc3_ = "否";
         }
         var _loc4_:String = _loc1_.sprUs.CurrentAttackMode.m_strName;
         if(_loc1_.sprUs.CurrentAttackMode.m_objItem != null)
         {
            _loc4_ = _loc1_.sprUs.CurrentAttackMode.m_objItem.strDesc;
         }
         if(this.sprPlayer.m_objMoveLast != null)
         {
            if((_loc9_ = CombatPair(this.sprPlayer.m_dictCombatPairs[this.sprPlayer.m_objTargetLast])) != null)
            {
               this.sprPlayerTargetImg.pixels = this.sprPlayer.m_objTargetLast.GetCreatureImage(_loc9_.ThemSpotted);
               this.sprPlayerTargetImg.visible = true;
            }
            this.txtPlayerVs.visible = true;
            this.btnPlayerMoveImg.visible = true;
            this.btnPlayerMoveImg.strImgDown = this.btnPlayerMoveImg.strImgOn = this.btnPlayerMoveImg.strImgOut = this.btnPlayerMoveImg.strImgOver = this.sprPlayer.m_objMoveLast.ItemRef.ItemDefinition.vImageListNames[0];
            this.btnPlayerMoveImg.pixels = this.btnPlayerMoveImg.bmpImgDown = this.btnPlayerMoveImg.bmpImgOn = this.btnPlayerMoveImg.bmpImgOut = this.btnPlayerMoveImg.bmpImgOver = this.sprPlayer.m_objMoveLast.ItemRef.ItemDefinition.vImageList[0];
            this.btnPlayerMoveImg.m_strPopUpText = this.sprPlayer.m_objMoveLast.m_strPopUp;
         }
         else
         {
            this.txtPlayerVs.visible = false;
            this.sprPlayerTargetImg.visible = false;
            this.btnPlayerMoveImg.visible = false;
         }
         this.txtPlayerVals.text = "\n\n\n" + this.GetDescriptor(_loc1_.sprUs.m_fCover) + "\n" + _loc3_ + "\n" + _loc4_;
         var _loc5_:Array = _loc1_.sprUs.GetStatus().split("\n");
         var _loc6_:int = GUIValues.GetInt("GUIBattleScreen.NumLines");
         this.txtPlayerFields2.text = "状态:\n";
         this.txtPlayerVals2.text = "";
         this.txtPlayerVals3.text = "";
         var _loc7_:int = 0;
         while(_loc7_ < _loc5_.length)
         {
            if(_loc7_ + 1 > _loc6_)
            {
               this.txtPlayerVals3.text += _loc5_[_loc7_] + "\n";
            }
            else
            {
               this.txtPlayerFields2.text += _loc5_[_loc7_] + "\n";
            }
            _loc7_++;
         }
         if(_loc1_.ThemSpotted)
         {
            if(_loc1_.sprThem.m_objMoveLast != null)
            {
               this.sprEnemyTarget.pixels = _loc1_.sprThem.m_objPair.sprThem.GetCreatureImage(_loc1_.sprThem.m_objPair.ThemSpotted);
               this.sprEnemyTarget.visible = true;
               this.txtEnemyVs.visible = true;
               this.btnEnemyMove.visible = true;
               this.btnEnemyMove.strImgDown = this.btnEnemyMove.strImgOn = this.btnEnemyMove.strImgOut = this.btnEnemyMove.strImgOver = _loc1_.sprThem.m_objMoveLast.ItemRef.ItemDefinition.vImageListNames[0];
               this.btnEnemyMove.pixels = this.btnEnemyMove.bmpImgDown = this.btnEnemyMove.bmpImgOn = this.btnEnemyMove.bmpImgOut = this.btnEnemyMove.bmpImgOver = _loc1_.sprThem.m_objMoveLast.ItemRef.ItemDefinition.vImageList[0];
               this.btnEnemyMove.m_strPopUpText = _loc1_.sprThem.m_objMoveLast.m_strPopUp;
            }
            else
            {
               this.btnEnemyMove.visible = false;
               this.sprEnemyTarget.visible = false;
               this.txtEnemyVs.visible = false;
            }
            _loc4_ = _loc1_.sprThem.CurrentAttackMode.m_strName;
            if(_loc1_.sprThem.CurrentAttackMode.m_objItem != null)
            {
               _loc4_ = _loc1_.sprThem.CurrentAttackMode.m_objItem.Description();
            }
            this.txtEnemyVals.text = "\n\n\n" + this.GetDescriptor(_loc1_.sprThem.m_fCover) + "\n" + "是\n" + _loc4_;
            _loc5_ = _loc1_.sprThem.GetStatus().split("\n");
            this.txtEnemyFields2.text = "状态:\n";
            this.txtEnemyVals2.text = "";
            this.txtEnemyVals3.text = "";
            _loc7_ = 0;
            while(_loc7_ < _loc5_.length)
            {
               if(_loc7_ + 1 > _loc6_)
               {
                  this.txtEnemyVals3.text += _loc5_[_loc7_] + "\n";
               }
               else
               {
                  this.txtEnemyFields2.text += _loc5_[_loc7_] + "\n";
               }
               _loc7_++;
            }
         }
         else
         {
            this.btnEnemyMove.visible = false;
            this.sprEnemyTarget.visible = false;
            this.txtEnemyVs.visible = false;
            this.txtEnemyFields2.text = "状态: ?\n";
            this.txtEnemyVals.text = "\n\n\n" + "?\n" + "否\n" + "?";
            this.txtEnemyVals2.text = "";
            this.txtEnemyVals3.text = "";
         }
      }
      
      public function UpdateCreatureSprite(param1:Creature) : void
      {
         var _loc4_:FlxSprite = null;
         var _loc2_:Battle = this.GetBattle();
         var _loc3_:CombatPair = this.CurrentCombatPair;
         var _loc5_:int = GUIValues.GetInt("GUIBattleScreen.zoom");
         if(_loc2_ == null || _loc3_ == null || param1 == null || _loc2_.IsCombatant(param1) == false)
         {
            return;
         }
         if(param1 is Player)
         {
            this.sprPlayerImg.pixels = param1.GetCreatureImage(true);
            _loc4_ = this.sprPlayerImg;
         }
         else if(param1 == this.sprCurrentTarget)
         {
            this.sprEnemy.pixels = _loc3_.sprThem.GetCreatureImage(_loc3_.ThemSpotted);
            _loc4_ = this.sprEnemy;
         }
         else if(param1 == _loc2_.PrevCombatant(this.sprCurrentTarget,this.sprPlayer))
         {
            this.sprEnemyPrev.pixels = param1.GetCreatureImage(CombatPair(this.sprPlayer.m_dictCombatPairs[param1]).ThemSpotted);
            _loc4_ = this.sprEnemyPrev;
         }
         else if(param1 == _loc2_.NextCombatant(this.sprCurrentTarget,this.sprPlayer))
         {
            this.sprEnemyNext.pixels = param1.GetCreatureImage(CombatPair(this.sprPlayer.m_dictCombatPairs[param1]).ThemSpotted);
            _loc4_ = this.sprEnemyNext;
         }
         if(_loc4_ != null)
         {
            _loc4_.fZoom = 1;
            _loc4_.Zoom(_loc5_);
         }
      }
      
      public function HandleCombatResponse(param1:Vector.<ItemInstance>, param2:Boolean = false) : Encounter
      {
         var _loc3_:ItemInstance = null;
         if(param1.length > 0)
         {
            _loc3_ = param1[0];
         }
         if(this.m_tilHex == null || this.m_tilHex.m_objBattle == null)
         {
            return DataHandler.GetEncounter(DM.m_nNullEnc);
         }
         return this.m_tilHex.m_objBattle.HandleCombatResponse(this.CurrentCombatPair,_loc3_,param2);
      }
      
      private function GetDescriptor(param1:Number) : String
      {
         if(param1 < 0.05)
         {
            return "无";
         }
         if(param1 < 0.33)
         {
            return "低";
         }
         if(param1 > 0.95)
         {
            return "完全";
         }
         if(param1 > 0.66)
         {
            return "高";
         }
         return "中";
      }
      
      private function GetTerrainDescriptor(param1:Number) : String
      {
         if(param1 > 0.8)
         {
            return "险恶";
         }
         if(param1 > 0.4)
         {
            return "艰难";
         }
         if(param1 > 0.15)
         {
            return "不平";
         }
         return "平坦";
      }
      
      public function get CurrentCombatPair() : CombatPair
      {
         var _loc1_:CombatPair = null;
         if(this.m_tilHex == null || this.m_tilHex.m_objBattle == null)
         {
            return null;
         }
         if(this.m_tilHex.m_objBattle.IsCombatant(this.sprCurrentTarget))
         {
            _loc1_ = this.sprPlayer.m_dictCombatPairs[this.sprCurrentTarget];
         }
         else
         {
            for each(_loc1_ in this.sprPlayer.m_dictCombatPairs)
            {
               if(this.m_tilHex.m_objBattle.IsCombatant(_loc1_.sprThem))
               {
                  this.sprCurrentTarget = _loc1_.sprThem;
                  break;
               }
            }
         }
         return _loc1_;
      }
      
      public function PreviousEnemy() : void
      {
         this.sprCurrentTarget = this.m_tilHex.m_objBattle.PrevCombatant(this.sprCurrentTarget,this.sprPlayer);
         this.DisplayCombatPair();
         this.btnPrevEnemy.status = FlxButton.NORMAL;
         this.btnPrevEnemy.on = false;
         PlayState.m_objInstance.grpInventoryUI.UpdateCombatItems();
      }
      
      public function NextEnemy() : void
      {
         this.sprCurrentTarget = this.m_tilHex.m_objBattle.NextCombatant(this.sprCurrentTarget,this.sprPlayer);
         this.DisplayCombatPair();
         this.btnNextEnemy.status = FlxButton.NORMAL;
         this.btnNextEnemy.on = false;
         PlayState.m_objInstance.grpInventoryUI.UpdateCombatItems();
      }
      
      public function GetBattle() : Battle
      {
         if(this.m_tilHex != null)
         {
            return this.m_tilHex.m_objBattle;
         }
         return null;
      }
      
      public function SetBattle(param1:FlxHexTile) : void
      {
         if(this.m_tilHex == param1)
         {
            return;
         }
         this.m_tilHex = param1;
         if(this.m_tilHex.m_objBattle == null)
         {
            this.m_tilHex = null;
            return;
         }
         this.sprHex.pixels = GUIValues.ScaleBitmapData(MapUtils.tmapHexes.GetImage(this.m_tilHex.index),this.sprHex.fZoom);
         if(this.m_tilHex.m_objBattle.vAddCreatures.indexOf(this.sprPlayer) >= 0)
         {
            this.m_tilHex.m_objBattle.ProcessAddQueue();
         }
         this.sprCurrentTarget = this.m_tilHex.m_objBattle.PrevCombatant(this.sprPlayer,this.sprPlayer);
         if(this.sprCurrentTarget == null)
         {
            this.sprCurrentTarget = this.m_tilHex.m_objBattle.NextCombatant(this.sprPlayer,this.sprPlayer);
         }
         this.DisplayCombatPair();
      }
      
      public function ClearBattle() : void
      {
         this.m_tilHex = null;
         this.sprCurrentTarget = null;
      }
      
      public function SetRes() : void
      {
         var _loc4_:CombatPair = null;
         var _loc5_:ItemInstance = null;
         var _loc1_:int = GUIValues.GetInt("Item.zoom");
         var _loc2_:int = GUIValues.GetInt("GUIBattleScreen.zoom");
         this.sprPlayerPanel.pixels = DataHandler.GetImage(GUIValues.GetString("GUIBattleScreen.sprPlayerPanel.image"));
         this.sprEnemyPanel.pixels = DataHandler.GetImage(GUIValues.GetString("GUIBattleScreen.sprPlayerPanel.image"));
         GUIValues.SetPosition(this.sprPlayerPanel,"GUIBattleScreen.sprPlayerPanel");
         GUIValues.SetPosition(this.sprPlayerImg,"GUIBattleScreen.sprPlayerImg");
         GUIValues.SetPosition(this.sprPlayerTargetImg,"GUIBattleScreen.sprPlayerTargetImg");
         GUIValues.SetPosition(this.btnPlayerMoveImg,"GUIBattleScreen.sprPlayerMoveImg");
         this.sprPlayerImg.Zoom(_loc2_);
         this.btnPlayerMoveImg.Zoom(_loc1_);
         GUIValues.SetPosition(this.txtPlayerName,"GUIBattleScreen.txtPlayerName");
         this.txtPlayerName.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerName.width"));
         this.txtPlayerName.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"center",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtPlayerFields,"GUIBattleScreen.txtPlayerFields");
         this.txtPlayerFields.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerFields.width"));
         this.txtPlayerFields.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtPlayerVals,"GUIBattleScreen.txtPlayerVals");
         this.txtPlayerVals.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerVals.width"));
         this.txtPlayerVals.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtPlayerFields2,"GUIBattleScreen.txtPlayerFields2");
         this.txtPlayerFields2.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerFields2.width"));
         this.txtPlayerFields2.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtPlayerVals2,"GUIBattleScreen.txtPlayerVals2");
         this.txtPlayerVals2.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerVals2.width"));
         this.txtPlayerVals2.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtPlayerVs,"GUIBattleScreen.txtPlayerVs");
         this.txtPlayerVs.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerVals2.width"));
         this.txtPlayerVs.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         var _loc3_:FlxPoint = GUIValues.GetPoint("GUIBattleScreen.txtPlayerFields2");
         this.txtPlayerVals3.x = _loc3_.x + GUIValues.GetInt("GUIBattleScreen.txtPlayerFields2.width") + 2;
         this.txtPlayerVals3.y = _loc3_.y;
         this.txtPlayerVals3.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerVals2.width"));
         this.txtPlayerVals3.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtPlayerVs,"GUIBattleScreen.txtPlayerVs");
         this.txtPlayerVs.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtPlayerVals2.width"));
         this.txtPlayerVs.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.sprEnemyPanel,"GUIBattleScreen.sprEnemyPanel");
         GUIValues.SetPosition(this.sprEnemy,"GUIBattleScreen.sprEnemy");
         GUIValues.SetPosition(this.sprEnemyTarget,"GUIBattleScreen.sprEnemyTarget");
         GUIValues.SetPosition(this.btnEnemyMove,"GUIBattleScreen.sprEnemyMove");
         GUIValues.SetPosition(this.sprEnemyNext,"GUIBattleScreen.sprEnemyNext");
         GUIValues.SetPosition(this.sprEnemyPrev,"GUIBattleScreen.sprEnemyPrev");
         this.sprEnemy.Zoom(_loc2_);
         this.btnEnemyMove.Zoom(_loc1_);
         this.sprEnemyNext.Zoom(_loc2_);
         this.sprEnemyPrev.Zoom(_loc2_);
         this.sprEnemyNext.scale = new FlxPoint(this.sprEnemy.scale.x / 2,this.sprEnemy.scale.y / 2);
         this.sprEnemyPrev.scale = new FlxPoint(this.sprEnemy.scale.x / 2,this.sprEnemy.scale.y / 2);
         GUIValues.SetPosition(this.txtEnemyName,"GUIBattleScreen.txtEnemyName");
         this.txtEnemyName.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyName.width"));
         this.txtEnemyName.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"center",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtEnemyFields,"GUIBattleScreen.txtEnemyFields");
         this.txtEnemyFields.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyFields.width"));
         this.txtEnemyFields.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtEnemyVals,"GUIBattleScreen.txtEnemyVals");
         this.txtEnemyVals.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyVals.width"));
         this.txtEnemyVals.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtEnemyFields2,"GUIBattleScreen.txtEnemyFields2");
         this.txtEnemyFields2.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyFields2.width"));
         this.txtEnemyFields2.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtEnemyVals2,"GUIBattleScreen.txtEnemyVals2");
         this.txtEnemyVals2.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyVals2.width"));
         this.txtEnemyVals2.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         _loc3_ = GUIValues.GetPoint("GUIBattleScreen.txtEnemyFields2");
         this.txtEnemyVals3.x = _loc3_.x + GUIValues.GetInt("GUIBattleScreen.txtEnemyFields2.width") + 2;
         this.txtEnemyVals3.y = _loc3_.y;
         this.txtEnemyVals3.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyVals2.width"));
         this.txtEnemyVals3.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtEnemyVs,"GUIBattleScreen.txtEnemyVs");
         this.txtEnemyVs.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtEnemyVals2.width"));
         this.txtEnemyVs.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.btnPrevEnemy,"GUIBattleScreen.btnPrevEnemy");
         GUIValues.SetPosition(this.btnNextEnemy,"GUIBattleScreen.btnNextEnemy");
         this.btnPrevEnemy.Zoom(_loc2_);
         this.btnNextEnemy.Zoom(_loc2_);
         GUIValues.SetPosition(this.sprHex,"GUIBattleScreen.sprHex");
         this.sprHex.Zoom(_loc2_);
         GUIValues.SetPosition(this.txtHexFields,"GUIBattleScreen.txtHexFields");
         this.txtHexFields.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtHexFields.width"));
         this.txtHexFields.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         GUIValues.SetPosition(this.txtHexVals,"GUIBattleScreen.txtHexVals");
         this.txtHexVals.SetWidth(GUIValues.GetInt("GUIBattleScreen.txtHexVals.width"));
         this.txtHexVals.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         for each(_loc4_ in this.sprPlayer.m_dictCombatPairs)
         {
            for each(_loc5_ in _loc4_.vAllItems)
            {
               _loc5_.SetRes(_loc1_);
            }
         }
      }
   }
}

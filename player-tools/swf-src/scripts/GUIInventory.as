package
{
   import flash.display.BitmapData;
   import flash.events.*;
   import flash.geom.Point;
   import flash.utils.Dictionary;
   import flash.utils.getQualifiedClassName;
   import org.flixel.*;
   
   public class GUIInventory extends FlxGroup
   {
      
      public static const STATE_NORMAL:int = 0;
      
      public static const STATE_SKILL_EXCLUSIVE:int = 1;
      
      public static const STATE_ENCOUNTER:int = 2;
      
      public static const STATE_ENCOUNTER_EXCLUSIVE:int = 3;
      
      public static const STATE_ENCOUNTER_TREASURE:int = 4;
      
      public static const STATE_ENCOUNTER_EXCLUSIVETREASURE:int = 5;
      
      public static const STATE_COMBAT:int = 6;
      
      public static const STATE_COMBAT_TREASURE:int = 7;
      
      public static const PANEL_ITEMS:int = 0;
      
      public static const PANEL_VEHICLE:int = 1;
      
      public static const PANEL_RESPONSE:int = 2;
      
      public static const PANEL_SKILLS:int = 3;
      
      public static const PANEL_CAMP:int = 4;
      
      public static const PANEL_BATTLE:int = 5;
      
      public static const PANEL_HEALTH:int = 6;
      
      public static const PANEL_DMC:int = 7;
      
      public static const PANEL_CRAFT:int = 8;
      
      public static const MOUSE_TAKE:int = 4;
      
      public static const MOUSE_DRAG:int = 6;
      
      public static const MOUSE_USE:int = 8;
      
      public static const MOUSE_DELETE:int = 10;
       
      
      private var sprBG:FlxSprite;
      
      private var sprCraftCapBG:FlxSprite;
      
      private var sprYieldCapBG:FlxSprite;
      
      private var sprTint:FlxSprite;
      
      private var sprCraftArrowRight:FlxSprite;
      
      private var sprCraftArrowDown:FlxSprite;
      
      private var sprSkillArrowRight:FlxSprite;
      
      private var sprTraitArrowRight:FlxSprite;
      
      private var sprEncounter:FlxSprite;
      
      private var sprBody:FlxSprite;
      
      private var txtEncounter:FlxText;
      
      private var txtEncResponse:FlxText;
      
      private var txtEncAvail:FlxText;
      
      private var txtEncResponseLabel:FlxText;
      
      private var txtCraIngredients:FlxText;
      
      private var txtCraYield:FlxText;
      
      private var txtSklAvail:FlxText;
      
      private var txtTraitAvail:FlxText;
      
      private var txtTraitInstruct:FlxText;
      
      private var txtSkillSpace1:FlxText;
      
      private var txtTraitSlot1:FlxText;
      
      private var txtSkillTotal:FlxText;
      
      private var txtGround:FlxText;
      
      private var txtAvailCamp:FlxText;
      
      private var txtCamp:FlxText;
      
      private var txtVehicle:FlxText;
      
      public var txtCraftMoves:FlxText;
      
      private var txtCondTitle:FlxText;
      
      private var txtCondStatsWarning:FlxText;
      
      private var grpPopUp:TextPopUp;
      
      public var grpItemsLayer:FlxGroup;
      
      private var grpGroundLayer:FlxGroup;
      
      private var grpEncounterLayer:FlxGroup;
      
      private var grpSkillsLayer:FlxGroup;
      
      private var grpVehicleLayer:FlxGroup;
      
      private var grpCampLayer:FlxGroup;
      
      public var grpBattleLayer:GUIBattleScreen;
      
      public var grpHealthLayer:FlxGroup;
      
      public var grpDMCLayer:GUIDMC;
      
      private var grpCraftLayer:FlxGroup;
      
      private var aPanels:Array;
      
      private var grpScavengeLoot:GUITintBar;
      
      private var grpScavengeAccident:GUITintBar;
      
      private var grpScavengeCreature:GUITintBar;
      
      private var grpCampConcealment:GUITintBar;
      
      private var grpCampShelter:GUITintBar;
      
      private var grpCampAlertness:GUITintBar;
      
      private var grpCampHealRate:GUITintBar;
      
      private var grpCampSleepQuality:GUITintBar;
      
      private var grpHealthBlood:GUITintBar;
      
      private var grpHealthInfection:GUITintBar;
      
      private var grpHealthPain:GUITintBar;
      
      public var m_nState:uint = 0;
      
      public var m_nPanel:uint = 0;
      
      public var m_nMouseMode:int = 0;
      
      public var m_nMouseModeLast:int = 0;
      
      public var m_nMouseModeRestore:int = -1;
      
      public var bMouseWholeStack:Boolean = false;
      
      private var btnEncConfirm:ImgButton;
      
      private var btnEncViewItems:ImgButton;
      
      public var btnSkillsConfirm:ImgButton;
      
      public var btnSkillsRandom:ImgButton;
      
      public var btnCraftConfirm:ImgButton;
      
      public var btnCraftClear:ImgButton;
      
      private var btnContextUse:ImgButton;
      
      private var btnContextDelete:ImgButton;
      
      private var btnContextCraft:ImgButton;
      
      private var btnContextTake:ImgButton;
      
      private var btnContextEmpty:ImgButton;
      
      private var btnContextPad:ImgButton;
      
      private var btnCraftPrev:ImgButton;
      
      private var btnCraftNext:ImgButton;
      
      private var btnYieldPrev:ImgButton;
      
      private var btnYieldNext:ImgButton;
      
      private var btnCraftRecipes:ImgButton;
      
      private var btnCraftAvail:ImgButton;
      
      private var btnCursor:ImgButton;
      
      public var m_bAvailSkills:Boolean = false;
      
      public var m_vKnownRecipes:Vector.<ImgButton>;
      
      private var m_aConditions:Array;
      
      public var vItemSlots:Vector.<GUIInventorySlot>;
      
      public var vSaveSlots:Vector.<GUIInventorySlot>;
      
      private var vCheckSlots:Vector.<GUIInventorySlot>;
      
      public var grpCraftingIngredientsSlot:GUIInventorySlot;
      
      private var grpCraftingYieldSlot:GUIInventorySlot;
      
      private var grpAvailCraftItemsSlot:GUIInventorySlot;
      
      private var grpEncounterSlot:GUIInventorySlot;
      
      public var grpAvailEncounterSlot:GUIInventorySlot;
      
      public var grpAvailTraitSlot:GUIInventorySlot;
      
      private var grpAvailSkillSlot:GUIInventorySlot;
      
      public var grpTraitSlot:GUIInventorySlot;
      
      public var grpSkillSlot:GUIInventorySlot;
      
      private var grpVehicleSlot:GUIInventorySlot;
      
      private var grpUseSlot:GUIInventorySlot;
      
      public var grpTempSlot:GUIInventorySlot;
      
      public var grpAvailCampSlot:GUIInventorySlot;
      
      public var objDragging:ItemInstance;
      
      private var objContext:ItemInstance;
      
      private var ptDragOrigin:FlxPoint;
      
      private var ptDragOffset:FlxPoint;
      
      private var grpSlotSource:GUIInventorySlot;
      
      private var objParentItem:ItemInstance;
      
      private var fRotationOrig:Number;
      
      public var objEncounter:Encounter;
      
      private var objCurrentRecipe:Recipe;
      
      private var ptMouse:FlxPoint;
      
      private var objMouseOverItem:ItemInstance;
      
      private var objMouseOverItemLast:ItemInstance;
      
      private var m_nRecipeFirstIndex:int;
      
      public var m_objScavLoc:ItemInstance;
      
      private var m_strCraftOutput:String;
      
      private var vDegradeCleanup:Vector.<ItemInstance>;
      
      private var vAvailCraftingPages:Vector.<ItemInstance>;
      
      private var vYieldPages:Vector.<ItemInstance>;
      
      private var vCurrentRecipes:Vector.<Recipe>;
      
      private var sprPlayer:Player;
      
      private var objPlayState:PlayState;
      
      public var m_nSkillSlots:uint = 14;
      
      public var vForbidDeleteIDs:Vector.<int>;
      
      private var m_vContextButtons:Vector.<ImgButton>;
      
      private var objLoopFitResult:GUIFitItemResult;
      
      private var dictContextButtons:Dictionary;
      
      private var bUnspentNotify:Boolean;
      
      private var ptTemp1:FlxPoint;
      
      private var ptTemp2:FlxPoint;
      
      private var nDebugCounter:int = 0;
      
      public function GUIInventory(param1:Player, param2:Function)
      {
         var _loc5_:GUIInventorySlot = null;
         var _loc6_:ItemInstance = null;
         this.vForbidDeleteIDs = Vector.<int>([12,25,26,49,90,91,96,103]);
         super();
         this.sprPlayer = param1;
         this.objPlayState = PlayState.m_objInstance;
         this.vItemSlots = new Vector.<GUIInventorySlot>();
         this.vSaveSlots = new Vector.<GUIInventorySlot>();
         this.m_vKnownRecipes = new Vector.<ImgButton>();
         this.vDegradeCleanup = new Vector.<ItemInstance>();
         this.vAvailCraftingPages = new Vector.<ItemInstance>();
         this.vYieldPages = new Vector.<ItemInstance>();
         this.vCurrentRecipes = new Vector.<Recipe>();
         this.m_aConditions = new Array();
         this.m_vContextButtons = new Vector.<ImgButton>();
         this.objLoopFitResult = new GUIFitItemResult();
         this.dictContextButtons = new Dictionary();
         this.m_nRecipeFirstIndex = 0;
         this.m_strCraftOutput = "上方物品全部制作";
         this.bUnspentNotify = false;
         var _loc3_:FlxPoint = GUIValues.GetPoint("GUIInventory.sprBG");
         var _loc4_:FlxPoint = GUIValues.GetPoint("GUIInventory.sprBG.size");
         this.sprBG = new FlxSprite(_loc3_.x,_loc3_.y);
         this.sprBG.pixels = DataHandler.GetImage("GUIBG.png");
         this.sprBG.pixels = GUIValues.ScaleBitmapData(DataHandler.GetImage("GUIBG.png"),_loc4_.x / this.sprBG.width,_loc4_.y / this.sprBG.height);
         this.grpPopUp = new TextPopUp();
         this.txtEncounter = new FlxText(GUIValues.GetPoint("GUIInventory.txtEncounter").x,GUIValues.GetPoint("GUIInventory.txtEncounter").y,GUIValues.GetInt("GUIInventory.txtEncounter.size"),"遭遇情况在此显示.");
         this.txtEncResponse = new FlxText(GUIValues.GetPoint("GUIInventory.txtEncResponse").x,GUIValues.GetPoint("GUIInventory.txtEncResponse").y,GUIValues.GetInt("GUIInventory.txtEncResponse.size"),"遭遇结果在此显示.");
         this.txtEncAvail = new FlxText(GUIValues.GetPoint("GUIInventory.txtEncAvail").x,GUIValues.GetPoint("GUIInventory.txtEncAvail").y,GUIValues.GetInt("GUIInventory.txtEncAvail.size"),"遭遇选项.");
         this.txtEncResponseLabel = new FlxText(GUIValues.GetPoint("GUIInventory.txtEncResponseLabel").x,GUIValues.GetPoint("GUIInventory.txtEncResponseLabel").y,GUIValues.GetInt("GUIInventory.txtEncResponseLabel.size"),"选择好的项目放在这.");
         this.txtCraIngredients = new FlxText(GUIValues.GetPoint("GUIInventory.txtCraIngredients").x,GUIValues.GetPoint("GUIInventory.txtCraIngredients").y,GUIValues.GetInt("GUIInventory.txtCraIngredients.size"),"把材料放在此处进行融合");
         this.txtCraYield = new FlxText(GUIValues.GetPoint("GUIInventory.txtCraYield").x,GUIValues.GetPoint("GUIInventory.txtCraYield").y,GUIValues.GetInt("GUIInventory.txtCraYield.size"),this.m_strCraftOutput);
         this.txtCraftMoves = new FlxText(GUIValues.GetPoint("GUIInventory.txtCraftMoves").x,GUIValues.GetPoint("GUIInventory.txtCraftMoves").y,GUIValues.GetInt("GUIInventory.txtCraftMoves.size"),"");
         this.txtSklAvail = new FlxText(GUIValues.GetPoint("GUIInventory.txtSklAvail").x,GUIValues.GetPoint("GUIInventory.txtSklAvail").y,GUIValues.GetInt("GUIInventory.txtSklAvail.size"),"你可以选择的人物天赋.");
         this.txtTraitAvail = new FlxText(GUIValues.GetPoint("GUIInventory.txtTraitAvail").x,GUIValues.GetPoint("GUIInventory.txtTraitAvail").y,GUIValues.GetInt("GUIInventory.txtTraitAvail.size"),"你可以选择的人物缺陷.");
         this.txtTraitInstruct = new FlxText(GUIValues.GetPoint("GUIInventory.txtTraitInstruct").x,GUIValues.GetPoint("GUIInventory.txtTraitInstruct").y,GUIValues.GetInt("GUIInventory.txtTraitInstruct.size"),"这里可以选择更多的天赋，但是你要选择更多的缺陷.");
         this.txtSkillSpace1 = new FlxText(GUIValues.GetPoint("GUIInventory.txtSkillSpace1").x,GUIValues.GetPoint("GUIInventory.txtSkillSpace1").y,GUIValues.GetInt("GUIInventory.txtSkillSpace1.size"),"已选天赋");
         this.txtTraitSlot1 = new FlxText(GUIValues.GetPoint("GUIInventory.txtTraitSlot1").x,GUIValues.GetPoint("GUIInventory.txtTraitSlot1").y,GUIValues.GetInt("GUIInventory.txtTraitSlot1.size"),"已选缺陷");
         this.txtSkillTotal = new FlxText(GUIValues.GetPoint("GUIInventory.txtSkillTotal").x,GUIValues.GetPoint("GUIInventory.txtSkillTotal").y,GUIValues.GetInt("GUIInventory.txtSkillSpace1.size"),"合计: ");
         this.txtGround = new FlxText(GUIValues.GetPoint("GUIInventory.txtGroundAvail").x,GUIValues.GetPoint("GUIInventory.txtGroundAvail").y,GUIValues.GetInt("GUIInventory.txtSklAvail.size"),"此处显示地面上的物品.");
         this.txtCamp = new FlxText(GUIValues.GetPoint("GUIInventory.txtCamp").x,GUIValues.GetPoint("GUIInventory.txtCamp").y,GUIValues.GetInt("GUIInventory.txtCamp.size"),"当前使用的营地.");
         this.txtAvailCamp = new FlxText(GUIValues.GetPoint("GUIInventory.txtAvailCamp").x,GUIValues.GetPoint("GUIInventory.txtAvailCamp").y,GUIValues.GetInt("GUIInventory.txtAvailCamp.size"),"可用的营地.");
         this.txtCondTitle = new FlxText(GUIValues.GetPoint("GUIInventory.txtCondTitle").x,GUIValues.GetPoint("GUIInventory.txtCondTitle").y,GUIValues.GetInt("GUIInventory.txtCamp.size"),"玩家状态.");
         this.txtCondStatsWarning = new FlxText(GUIValues.GetPoint("GUIInventory.txtCondStatsWarning").x,GUIValues.GetPoint("GUIInventory.txtCondStatsWarning").y,GUIValues.GetInt("GUIInventory.txtCondStatsWarning.size"),"医疗天赋可以看到详细的健康状况.");
         this.txtVehicle = new FlxText(GUIValues.GetPoint("GUIInventory.txtCamp").x,GUIValues.GetPoint("GUIInventory.txtCamp").y,GUIValues.GetInt("GUIInventory.txtCamp.size"),"当前的载具.");
         this.sprTint = new FlxSprite(0,0);
         this.sprTint.makeGraphic(1,1,1157627903);
         this.ptDragOffset = new FlxPoint();
         this.sprCraftArrowDown = new FlxSprite(GUIValues.GetPoint("GUIInventory.sprCraftArrowDown").x,GUIValues.GetPoint("GUIInventory.sprCraftArrowDown").y);
         this.sprCraftArrowDown.pixels = DataHandler.GetImage("GUIArrowDown.png");
         this.sprCraftArrowRight = new FlxSprite(GUIValues.GetPoint("GUIInventory.sprCraftArrowRight").x,GUIValues.GetPoint("GUIInventory.sprCraftArrowRight").y);
         this.sprCraftArrowRight.pixels = DataHandler.GetImage("GUIArrowRight.png");
         this.sprSkillArrowRight = new FlxSprite(GUIValues.GetPoint("GUIInventory.sprSkillArrowRight").x,GUIValues.GetPoint("GUIInventory.sprSkillArrowRight").y);
         this.sprSkillArrowRight.pixels = DataHandler.GetImage("GUIArrowRight.png");
         this.sprTraitArrowRight = new FlxSprite(GUIValues.GetPoint("GUIInventory.sprTraitArrowRight").x,GUIValues.GetPoint("GUIInventory.sprTraitArrowRight").y);
         this.sprTraitArrowRight.pixels = DataHandler.GetImage("GUIArrowRight.png");
         this.sprEncounter = new FlxSprite(GUIValues.GetPoint("GUIInventory.grpEncounterSlot").x,GUIValues.GetPoint("GUIInventory.grpEncounterSlot").y);
         this.sprEncounter.pixels = DataHandler.GetImage("EncBlank.png");
         this.grpItemsLayer = new FlxGroup();
         this.grpGroundLayer = new FlxGroup();
         this.grpEncounterLayer = new FlxGroup();
         this.grpSkillsLayer = new FlxGroup();
         this.grpVehicleLayer = new FlxGroup();
         this.grpCampLayer = new FlxGroup();
         this.grpBattleLayer = new GUIBattleScreen();
         this.grpHealthLayer = new FlxGroup();
         this.grpDMCLayer = new GUIDMC();
         this.grpCraftLayer = new FlxGroup();
         this.btnEncConfirm = new ImgButton("btn_confirm_dn.png","btn_confirm.png","btn_confirm_on.png","btn_confirm_on.png",GUIValues.GetPoint("GUIInventory.btnEncConfirm").x,GUIValues.GetPoint("GUIInventory.btnEncConfirm").y,this.ConfirmResponse);
         this.btnEncViewItems = new ImgButton("btn_inv_menu_items_on.png","btn_inv_menu_items.png","btn_inv_menu_items_on.png","btn_inv_menu_items_on.png",GUIValues.GetPoint("GUIInventory.btnEncConfirm").x,GUIValues.GetPoint("GUIInventory.btnEncConfirm").y,PlayState.m_objInstance.ShowItems);
         this.btnSkillsConfirm = new ImgButton("btn_confirm_dn.png","btn_confirm.png","btn_confirm_on.png","btn_confirm_on.png",GUIValues.GetPoint("GUIInventory.btnSkillsConfirm").x,GUIValues.GetPoint("GUIInventory.btnSkillsConfirm").y,param2);
         this.btnSkillsRandom = new ImgButton("btn_random_dn.png","btn_random_off.png","btn_random_on.png","btn_random_on.png",GUIValues.GetPoint("GUIInventory.btnSkillsRandom").x,GUIValues.GetPoint("GUIInventory.btnSkillsRandom").y,this.RandomSkills);
         this.btnCraftConfirm = new ImgButton("btn_confirm_dn.png","btn_confirm.png","btn_confirm_on.png","btn_confirm_on.png",GUIValues.GetPoint("GUIInventory.btnCraftConfirm").x,GUIValues.GetPoint("GUIInventory.btnCraftConfirm").y,this.ConfirmCraft);
         this.btnCraftClear = new ImgButton("btn_clear_dn.png","btn_clear.png","btn_clear_on.png","btn_clear_on.png",GUIValues.GetPoint("GUIInventory.btnCraftConfirm").x,GUIValues.GetPoint("GUIInventory.btnCraftConfirm").y,this.ClearCraftYield);
         this.btnCursor = new ImgButton("btn_cursors_off.png","btn_cursors_off.png","btn_cursors_off.png","btn_cursors_off.png",GUIValues.GetPoint("PlayState.btnWait").x,GUIValues.GetPoint("PlayState.btnWait").y,this.MouseModeToggle);
         this.btnEncConfirm.m_strPopUpText = "确认目前的遭遇情况(空格键)";
         this.btnEncViewItems.m_strPopUpText = "查看物品(Q)";
         this.btnSkillsConfirm.m_strPopUpText = "确认选择好的技能(空格键)";
         this.btnSkillsRandom.m_strPopUpText = "随机选择技能和特征";
         this.btnCraftConfirm.m_strPopUpText = "制造这个物品(空格键)";
         this.btnCraftClear.m_strPopUpText = "清空制造区域(空格键)";
         this.btnCursor.m_strPopUpText = "光标模式.拿起/丢掉(1),使用(2),摧毁(3),分割(Shift)";
         this.objPlayState.m_aMouseOverItems.push(this.btnEncConfirm);
         this.objPlayState.m_aMouseOverItems.push(this.btnEncViewItems);
         this.objPlayState.m_aMouseOverItems.push(this.btnSkillsConfirm);
         this.objPlayState.m_aMouseOverItems.push(this.btnSkillsRandom);
         this.objPlayState.m_aMouseOverItems.push(this.btnCraftConfirm);
         this.objPlayState.m_aMouseOverItems.push(this.btnCraftClear);
         this.objPlayState.m_aMouseOverItems.push(this.btnCursor);
         this.aPanels = new Array(this.objPlayState.btnItems,this.objPlayState.btnVehicle,this.objPlayState.btnEncounter,this.objPlayState.btnSkills,this.objPlayState.btnCamp,this.objPlayState.btnEncounter,this.objPlayState.btnConditions,this.objPlayState.btnEncounter,this.objPlayState.btnCraft);
         this.btnContextTake = new ImgButton("btn_context_take_dn.png","btn_context_take_up.png","btn_context_take_on.png","btn_context_take_on.png",0,0,this.ContextTakeDrop);
         this.btnContextUse = new ImgButton("btn_context_use_dn.png","btn_context_use_up.png","btn_context_use_on.png","btn_context_use_on.png",0,0,this.ContextUse);
         this.btnContextDelete = new ImgButton("btn_context_delete_dn.png","btn_context_delete_up.png","btn_context_delete_on.png","btn_context_delete_on.png",0,0,this.ContextDelete);
         this.btnContextCraft = new ImgButton("btn_context_craft_dn.png","btn_context_craft_up.png","btn_context_craft_on.png","btn_context_craft_on.png",0,0,this.ContextCraft);
         this.btnContextPad = new ImgButton("btn_context_pad.png","btn_context_pad.png","btn_context_pad.png","btn_context_pad.png",0,0);
         this.btnContextEmpty = new ImgButton("btn_context_empty_dn.png","btn_context_empty_up.png","btn_context_empty_on.png","btn_context_empty_on.png",0,0,this.ContextEmpty);
         this.btnContextUse.m_strPopUpText = "Use";
         this.btnContextDelete.m_strPopUpText = "Delete";
         this.btnContextCraft.scrollFactor = new FlxPoint();
         this.btnContextDelete.scrollFactor = new FlxPoint();
         this.btnContextUse.scrollFactor = new FlxPoint();
         this.btnContextTake.scrollFactor = new FlxPoint();
         this.btnContextEmpty.scrollFactor = new FlxPoint();
         this.btnContextPad.scrollFactor = new FlxPoint();
         this.txtEncounter.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtEncResponse.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),13421568,"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtEncAvail.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtCraIngredients.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtCraYield.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtCraftMoves.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),4294944768,"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtEncResponseLabel.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtSklAvail.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtTraitAvail.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtTraitInstruct.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtSkillSpace1.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtTraitSlot1.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtSkillTotal.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtGround.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtCamp.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtAvailCamp.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtCondTitle.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtCondStatsWarning.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtVehicle.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.sprTint.scrollFactor = new FlxPoint();
         add(this.sprBG);
         add(this.grpItemsLayer);
         add(this.grpGroundLayer);
         add(this.grpEncounterLayer);
         add(this.grpBattleLayer);
         add(this.grpHealthLayer);
         add(this.grpDMCLayer);
         add(this.grpSkillsLayer);
         add(this.grpVehicleLayer);
         add(this.grpCampLayer);
         add(this.grpCraftLayer);
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,2,"LEFT SHOE","btn_inv_body_shoel.png","btn_inv_body_shoel_on.png",GUIValues.GetPoint("GUIInventory.Body"),20,true,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,3,"RIGHT SHOE","btn_inv_body_shoer.png","btn_inv_body_shoer_on.png",GUIValues.GetPoint("GUIInventory.Body"),30,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,4,"LEGS","btn_inv_body_legs.png","btn_inv_body_legs_on.png",GUIValues.GetPoint("GUIInventory.Body"),40,true,false,GUIValues.GetPoint("GUIInventory.Body.CapLegs")));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,5,"LEFT WRIST","btn_inv_body_wristl.png","btn_inv_body_wristl_on.png",GUIValues.GetPoint("GUIInventory.Body"),50,true,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,6,"RIGHT WRIST","btn_inv_body_wristr.png","btn_inv_body_wristr_on.png",GUIValues.GetPoint("GUIInventory.Body"),60,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,7,"LEFT HAND","btn_inv_body_handl.png","btn_inv_body_handl_on.png",GUIValues.GetPoint("GUIInventory.Body"),130,true,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,8,"RIGHT HAND","btn_inv_body_handr.png","btn_inv_body_handr_on.png",GUIValues.GetPoint("GUIInventory.Body"),130,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,11,"TORSO","btn_inv_body_torso.png","btn_inv_body_torso_on.png",GUIValues.GetPoint("GUIInventory.Body"),120,true,false,GUIValues.GetPoint("GUIInventory.Body.CapTorso"),Vector.<int>([3,1,1,1])));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,12,"BELT","btn_inv_body_belt.png","btn_inv_body_belt_on.png",GUIValues.GetPoint("GUIInventory.Body"),110,true,false,GUIValues.GetPoint("GUIInventory.Body.CapBelt")));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,13,"LEFT SHOULDER","btn_inv_body_shoulderl.png","btn_inv_body_shoulderl_on.png",GUIValues.GetPoint("GUIInventory.Body"),20,true,true,GUIValues.GetPoint("GUIInventory.Body.CapLeftShoulder"),null,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,14,"RIGHT SHOULDER","btn_inv_body_shoulderr.png","btn_inv_body_shoulderr_on.png",GUIValues.GetPoint("GUIInventory.Body"),20,true,false,GUIValues.GetPoint("GUIInventory.Body.CapRightShoulder")));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,17,"HEAD","btn_inv_body_head.png","btn_inv_body_head_on.png",GUIValues.GetPoint("GUIInventory.Body"),170,true,false,new FlxPoint(-1000,-1000),Vector.<int>([1,1,1])));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,22,"BACKPACK","btn_inv_backpack.png","btn_inv_backpack_on.png",GUIValues.GetPoint("GUIInventory.Backpack"),10,true,false,GUIValues.GetPoint("GUIInventory.Backpack.Cap"),null,true));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,23,"NECK #1","btn_inv_neck.png","btn_inv_neck_on.png",GUIValues.GetPoint("GUIInventory.Neck"),230,true,false,null,Vector.<int>([3])));
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,20,"HOLD IN LEFT HAND","btn_inv_body_holdl.png","btn_inv_body_holdl_on.png",GUIValues.GetPoint("GUIInventory.HoldLeft"),240,true,false,GUIValues.GetPoint("GUIInventory.HoldLeft.Cap"),null,true));
         this.sprPlayer.vInvCategories[this.sprPlayer.vInvCategories.length - 1].bHoldSlot = true;
         this.sprPlayer.vInvCategories[this.sprPlayer.vInvCategories.length - 1].m_bAllowStacks = true;
         this.sprPlayer.vInvCategories.push(this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,21,"HOLD IN RIGHT HAND","btn_inv_body_holdr.png","btn_inv_body_holdr_on.png",GUIValues.GetPoint("GUIInventory.HoldRight"),240,true,false,GUIValues.GetPoint("GUIInventory.HoldRight.Cap"),null,true));
         this.sprPlayer.vInvCategories[this.sprPlayer.vInvCategories.length - 1].bHoldSlot = true;
         this.sprPlayer.vInvCategories[this.sprPlayer.vInvCategories.length - 1].m_bAllowStacks = true;
         this.grpItemsLayer.sort("nZSort",FlxGroup.ASCENDING);
         this.grpItemsLayer.add(this.txtGround);
         this.sprPlayer.sort("ID",FlxGroup.ASCENDING);
         this.sprPlayer.grpGroundSlot = this.sprPlayer.AddSlot(this.grpItemsLayer,this.vItemSlots,200,"GROUND","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpGroundSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpGroundSlot.Cap"),null,true);
         this.sprPlayer.grpGroundSlot.btnSlot.visible = false;
         this.grpItemsLayer.add(this.sprPlayer.grpGroundSlot);
         this.grpItemsLayer.add(this.txtGround);
         this.grpItemsLayer.add(this.btnCursor);
         this.grpEncounterSlot = this.sprPlayer.AddSlot(this.grpEncounterLayer,this.vItemSlots,201,"ENCOUNTER RESPONSE","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpEncounterSlot"),0,false,false,GUIValues.GetPoint("GUIInventory.grpEncounterSlot.Cap"));
         this.grpAvailEncounterSlot = this.sprPlayer.AddSlot(this.grpEncounterLayer,this.vItemSlots,202,"ENCOUNTER ITEMS","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpEncounterSlot"),0,false,false,GUIValues.GetPoint("GUIInventory.grpAvailEncounterSlot.Cap"));
         this.grpScavengeLoot = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpScavengeLoot").x,GUIValues.GetPoint("GUIInventory.grpScavengeLoot").y,59,"收获:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpScavengeAccident = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpScavengeAccident").x,GUIValues.GetPoint("GUIInventory.grpScavengeAccident").y,59,"安全:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpScavengeCreature = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpScavengeCreature").x,GUIValues.GetPoint("GUIInventory.grpScavengeCreature").y,59,"潜行:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpScavengeLoot.m_txtLabel.width = this.grpScavengeAccident.m_txtLabel.width = this.grpScavengeCreature.m_txtLabel.width = GUIValues.GetInt("GUIInventory.grpScavengeLoot.size");
         this.grpCampConcealment = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpCampConcealment").x,GUIValues.GetPoint("GUIInventory.grpCampConcealment").y,59,"隐蔽处:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpCampShelter = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpCampShelter").x,GUIValues.GetPoint("GUIInventory.grpCampShelter").y,59,"庇护所:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpCampAlertness = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpCampAlertness").x,GUIValues.GetPoint("GUIInventory.grpCampAlertness").y,59,"警戒:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpCampHealRate = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpCampHealRate").x,GUIValues.GetPoint("GUIInventory.grpCampHealRate").y,59,"治疗:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpCampSleepQuality = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpCampSleepQuality").x,GUIValues.GetPoint("GUIInventory.grpCampSleepQuality").y,59,"睡眠:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpEncounterLayer.add(this.txtEncounter);
         this.grpEncounterLayer.add(this.txtEncResponse);
         this.grpEncounterLayer.add(this.txtEncResponseLabel);
         this.grpEncounterLayer.add(this.txtEncAvail);
         this.grpEncounterLayer.add(this.btnEncConfirm);
         this.grpEncounterLayer.add(this.btnEncViewItems);
         this.grpEncounterLayer.add(this.grpScavengeAccident);
         this.grpEncounterLayer.add(this.grpScavengeCreature);
         this.grpEncounterLayer.add(this.grpScavengeLoot);
         this.grpEncounterLayer.add(this.btnCursor);
         this.grpEncounterLayer.add(this.sprEncounter);
         this.grpAvailTraitSlot = this.sprPlayer.AddSlot(this.grpSkillsLayer,this.vItemSlots,203,"AVAILABLE TRAITS","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpAvailTraitSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpAvailTraitSlot.Cap"));
         this.vSaveSlots.push(this.grpAvailTraitSlot);
         this.grpAvailSkillSlot = this.sprPlayer.AddSlot(this.grpSkillsLayer,this.vItemSlots,204,"AVAILABLE SKILLS","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpAvailSkillSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpAvailSkillSlot.Cap"));
         this.vSaveSlots.push(this.grpAvailSkillSlot);
         _loc3_ = GUIValues.GetPoint("GUIInventory.Traits");
         this.grpTraitSlot = this.sprPlayer.AddSlot(this.grpSkillsLayer,this.vItemSlots,213,"Traits","blank.png","blank.png",_loc3_,0,false,false,_loc3_);
         this.vSaveSlots.push(this.grpTraitSlot);
         _loc3_ = GUIValues.GetPoint("GUIInventory.Skills");
         this.grpSkillSlot = this.sprPlayer.AddSlot(this.grpSkillsLayer,this.vItemSlots,214,"Skills","blank.png","blank.png",_loc3_,0,false,false,_loc3_);
         this.vSaveSlots.push(this.grpSkillSlot);
         this.grpSkillsLayer.add(this.btnSkillsConfirm);
         this.grpSkillsLayer.add(this.btnSkillsRandom);
         this.grpSkillsLayer.add(this.txtSklAvail);
         this.grpSkillsLayer.add(this.txtTraitAvail);
         this.grpSkillsLayer.add(this.txtTraitInstruct);
         this.grpSkillsLayer.add(this.txtSkillSpace1);
         this.grpSkillsLayer.add(this.txtTraitSlot1);
         this.grpSkillsLayer.add(this.txtSkillTotal);
         this.grpSkillsLayer.add(this.sprSkillArrowRight);
         this.grpSkillsLayer.add(this.sprTraitArrowRight);
         this.grpSkillsLayer.add(this.btnCursor);
         this.sprCraftCapBG = new FlxSprite(GUIValues.GetPoint("GUIInventory.sprCraftCapBG").x,GUIValues.GetPoint("GUIInventory.sprCraftCapBG").y);
         this.sprCraftCapBG.pixels = DataHandler.GetImage(GUIValues.GetString("GUIInventory.sprCraftCapBG.image"));
         this.sprCraftCapBG.m_strImg = GUIValues.GetString("GUIInventory.sprCraftCapBG.image");
         this.grpCraftLayer.add(this.sprCraftCapBG);
         this.sprYieldCapBG = new FlxSprite(GUIValues.GetPoint("GUIInventory.sprYieldCapBG").x,GUIValues.GetPoint("GUIInventory.sprYieldCapBG").y);
         this.sprYieldCapBG.pixels = DataHandler.GetImage("GUIYieldCapBG.png");
         this.sprYieldCapBG.m_strImg = "GUIYieldCapBG.png";
         this.grpCraftLayer.add(this.sprYieldCapBG);
         this.grpCraftingIngredientsSlot = this.sprPlayer.AddSlot(this.grpCraftLayer,this.vItemSlots,205,"INGREDIENTS","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpCraftingIngredientsSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpCraftingIngredientsSlot.Cap"));
         this.vSaveSlots.push(this.grpCraftingIngredientsSlot);
         this.grpCraftingYieldSlot = this.sprPlayer.AddSlot(this.grpCraftLayer,this.vItemSlots,206,"YIELD","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpCraftingYieldSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpCraftingYieldSlot.Cap"));
         this.grpAvailCraftItemsSlot = this.sprPlayer.AddSlot(this.grpCraftLayer,this.vItemSlots,210,"AVAILABLE CRAFTING ITEMS","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpAvailCraftItemsSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpAvailCraftItemsSlot.Cap"));
         this.btnCraftPrev = new ImgButton("btn_craft_prev_dn.png","btn_craft_prev.png","btn_craft_prev_on.png","btn_craft_prev_on.png",GUIValues.GetPoint("GUIInventory.btnPrevCraft").x,GUIValues.GetPoint("GUIInventory.btnPrevCraft").y,this.PrevCraft);
         this.btnCraftNext = new ImgButton("btn_craft_next_dn.png","btn_craft_next.png","btn_craft_next_on.png","btn_craft_next_on.png",GUIValues.GetPoint("GUIInventory.btnNextCraft").x,GUIValues.GetPoint("GUIInventory.btnNextCraft").y,this.NextCraft);
         this.btnCraftRecipes = new ImgButton("btn_craft_recipes.png","btn_craft_recipes.png","btn_craft_recipes_on.png","btn_craft_recipes_on.png",GUIValues.GetPoint("GUIInventory.btnCraftRecipes").x,GUIValues.GetPoint("GUIInventory.btnCraftRecipes").y,this.ShowQuickRecipes);
         this.btnCraftAvail = new ImgButton("btn_craft_avail.png","btn_craft_avail.png","btn_craft_avail_on.png","btn_craft_avail_on.png",GUIValues.GetPoint("GUIInventory.btnCraftAvail").x,GUIValues.GetPoint("GUIInventory.btnCraftAvail").y,this.ShowAvailIngredients);
         this.btnYieldPrev = new ImgButton("btn_craft_prev_dn.png","btn_craft_prev.png","btn_craft_prev_on.png","btn_craft_prev_on.png",GUIValues.GetPoint("GUIInventory.btnPrevYield").x,GUIValues.GetPoint("GUIInventory.btnPrevYield").y,this.PrevYield);
         this.btnYieldNext = new ImgButton("btn_craft_next_dn.png","btn_craft_next.png","btn_craft_next_on.png","btn_craft_next_on.png",GUIValues.GetPoint("GUIInventory.btnNextYield").x,GUIValues.GetPoint("GUIInventory.btnNextYield").y,this.NextYield);
         this.btnCraftNext.m_strPopUpText = "下一页";
         this.btnCraftPrev.m_strPopUpText = "上一页";
         this.btnYieldNext.m_strPopUpText = "下一个可用物品.";
         this.btnYieldPrev.m_strPopUpText = "上一个可用物品.";
         this.btnCraftRecipes.m_strPopUpText = "查看快速配方列表.";
         this.btnCraftAvail.m_strPopUpText = "查看可制造的部件.";
         this.objPlayState.m_aMouseOverItems.push(this.btnCraftNext);
         this.objPlayState.m_aMouseOverItems.push(this.btnCraftPrev);
         this.objPlayState.m_aMouseOverItems.push(this.btnYieldNext);
         this.objPlayState.m_aMouseOverItems.push(this.btnYieldPrev);
         this.objPlayState.m_aMouseOverItems.push(this.btnCraftRecipes);
         this.objPlayState.m_aMouseOverItems.push(this.btnCraftAvail);
         this.grpCraftLayer.add(this.txtCraftMoves);
         this.grpCraftLayer.add(this.txtCraIngredients);
         this.grpCraftLayer.add(this.txtCraYield);
         this.grpCraftLayer.add(this.btnCraftConfirm);
         this.grpCraftLayer.add(this.btnCraftClear);
         this.grpCraftLayer.add(this.sprCraftArrowDown);
         this.grpCraftLayer.add(this.sprCraftArrowRight);
         this.grpCraftLayer.add(this.btnCraftPrev);
         this.grpCraftLayer.add(this.btnCraftNext);
         this.grpCraftLayer.add(this.btnYieldPrev);
         this.grpCraftLayer.add(this.btnYieldNext);
         this.grpCraftLayer.add(this.btnCraftRecipes);
         this.grpCraftLayer.add(this.btnCraftAvail);
         this.grpCraftLayer.add(this.btnCursor);
         this.txtCraftMoves.visible = false;
         this.grpCraftLayer.kill();
         var _loc7_:int = 0;
         while(_loc7_ < 10)
         {
            _loc6_ = DataHandler.GetItem("93.8");
            this.vAvailCraftingPages.push(_loc6_);
            _loc6_ = DataHandler.GetItem("93.7");
            this.vYieldPages.push(_loc6_);
            _loc7_++;
         }
         this.grpVehicleSlot = this.sprPlayer.AddSlot(this.grpVehicleLayer,this.vItemSlots,207,"VEHICLE","btn_vehicle.png","btn_vehicle_on.png",GUIValues.GetPoint("GUIInventory.grpVehicleSlot"),0,true,false,GUIValues.GetPoint("GUIInventory.grpVehicleSlot.Cap"),null,true);
         this.sprPlayer.vInvCategories.push(this.grpVehicleSlot);
         this.grpVehicleLayer.add(this.sprPlayer.grpGroundSlot);
         this.grpVehicleLayer.add(this.txtGround);
         this.grpVehicleLayer.add(this.txtVehicle);
         this.grpVehicleLayer.add(this.btnCursor);
         this.grpAvailCampSlot = this.sprPlayer.AddSlot(this.grpCampLayer,this.vItemSlots,209,"AVAILABLE CAMP SITES","blank.png","blank.png",GUIValues.GetPoint("GUIInventory.grpAvailCampSlot.Cap"),0,false,false,GUIValues.GetPoint("GUIInventory.grpAvailCampSlot.Cap"));
         this.sprPlayer.grpCampSlot = this.sprPlayer.AddSlot(this.grpCampLayer,this.vItemSlots,208,"CAMP","btn_camp.png","btn_camp_on.png",GUIValues.GetPoint("GUIInventory.grpCampSlot"),0,false,false,GUIValues.GetPoint("GUIInventory.grpCampSlot.Cap"),null,true);
         this.grpCampLayer.add(this.sprPlayer.grpGroundSlot);
         this.grpCampLayer.add(this.txtGround);
         this.grpCampLayer.add(this.txtCamp);
         this.grpCampLayer.add(this.txtAvailCamp);
         this.grpCampLayer.add(this.grpCampConcealment);
         this.grpCampLayer.add(this.grpCampShelter);
         this.grpCampLayer.add(this.grpCampSleepQuality);
         this.grpCampLayer.add(this.grpCampHealRate);
         this.grpCampLayer.add(this.grpCampAlertness);
         this.grpCampLayer.add(this.objPlayState.btnSleep);
         this.grpCampLayer.add(this.objPlayState.btnRest);
         this.grpCampLayer.add(this.btnCursor);
         this.grpHealthBlood = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpHealthBlood").x,GUIValues.GetPoint("GUIInventory.grpHealthBlood").y,59,"血液供应:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpHealthInfection = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpHealthInfection").x,GUIValues.GetPoint("GUIInventory.grpHealthInfection").y,59,"免疫系统:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.grpHealthPain = new GUITintBar(GUIValues.GetPoint("GUIInventory.grpHealthPain").x,GUIValues.GetPoint("GUIInventory.grpHealthPain").y,59,"耐痛阈:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.sprBody = new FlxSprite(GUIValues.GetPoint("GUIInventory.Body").x,GUIValues.GetPoint("GUIInventory.Body").y);
         this.sprBody.pixels = DataHandler.GetImage("btn_inv_body.png");
         this.grpHealthLayer.add(this.sprPlayer.m_dictSlots[20]);
         this.grpHealthLayer.add(this.sprPlayer.m_dictSlots[21]);
         this.grpHealthLayer.add(this.sprPlayer.m_dictSlots[22]);
         this.grpHealthLayer.add(this.sprBody);
         this.grpHealthLayer.add(this.sprPlayer.grpGroundSlot);
         this.grpHealthLayer.add(this.txtGround);
         this.grpHealthLayer.add(this.txtCondTitle);
         this.grpHealthLayer.add(this.txtCondStatsWarning);
         this.grpHealthLayer.add(this.grpHealthBlood);
         this.grpHealthLayer.add(this.grpHealthInfection);
         this.grpHealthLayer.add(this.grpHealthPain);
         this.grpHealthLayer.add(this.btnCursor);
         this.grpItemsLayer.add(this.txtCondTitle);
         this.grpItemsLayer.add(this.txtCondStatsWarning);
         this.grpItemsLayer.add(this.grpHealthBlood);
         this.grpItemsLayer.add(this.grpHealthInfection);
         this.grpItemsLayer.add(this.grpHealthPain);
         var _loc8_:Vector.<String> = new Vector.<String>();
         _loc3_ = new FlxPoint(GUIValues.GetPoint("GUIInventory.Body").x,GUIValues.GetPoint("GUIInventory.Body").y);
         this.sprPlayer.AddWound(this.grpHealthLayer,100,"左上臂",0.1,_loc3_,0,Vector.<String>(["WoundUACCutMild.png","轻度割伤\n","WoundUACCut.png","中度割伤\n","WoundUACCutSevere.png","深度割伤\n"]),Vector.<String>(["WoundUACBloodMild.png","轻度流血\n","WoundUACBlood.png","中度流血\n","WoundUACBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundUACInfect.png","轻度感染\n","WoundUACInfect.png","中度感染\n","WoundUACInfect.png","重度感染\n"]),Vector.<String>(["WoundUACBruise.png","轻度擦伤\n","WoundUACBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundUACPain.png",false,[[0.8,186]],[[0.95,186]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,111,"右上臂",0.1,_loc3_,0,Vector.<String>(["WoundUACCutMild.png","轻度割伤\n","WoundUACCut.png","中度割伤\n","WoundUACCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundUACBloodMild.png","轻度流血\n","WoundUACBlood.png","中度流血\n","WoundUACBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundUACInfect.png","轻度感染\n","WoundUACInfect.png","中度感染\n","WoundUACInfect.png","重度感染\n"]),Vector.<String>(["WoundUACBruise.png","轻度擦伤\n","WoundUACBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundUACPain.png",false,[[0.8,187]],[[0.95,187]],true);
         this.sprPlayer.AddWound(this.grpHealthLayer,101,"头部",0.1,_loc3_,0,Vector.<String>(["WoundHCCutMild.png","轻度割伤\n","WoundHCCut.png","中度割伤\n","WoundHCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundHCBloodMild.png","轻度流血\n","WoundHCBlood.png","中度流血\n","WoundHCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundHCInfect.png","轻度感染\n","WoundHCInfect.png","中度感染\n","WoundHCInfect.png","重度感染\n"]),Vector.<String>(["WoundHCBruise.png","轻度擦伤\n","WoundHCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundHCPain.png",false,[[0.33,195,145],[0.5,189],[0.66,195,145],[0.9,194]],[[0.9,194]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,102,"左下臂",0.1,_loc3_,0,Vector.<String>(["WoundLACCutMild.png","轻度割伤\n","WoundLACCut.png","中度割伤\n","WoundLACCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundLACBloodMild.png","轻度流血\n","WoundLACBlood.png","中度流血\n","WoundLACBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundLACInfect.png","轻度感染\n","WoundLACInfect.png","中度感染\n","WoundLACInfect.png","重度感染\n"]),Vector.<String>(["WoundLACBruise.png","轻度擦伤\n","WoundLACBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundLACPain.png",false,[[0.8,187]],[[0.95,187]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,112,"右下臂",0.1,_loc3_,0,Vector.<String>(["WoundLACCutMild.png","轻度割伤\n","WoundLACCut.png","中度割伤\n","WoundLACCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundLACBloodMild.png","轻度流血\n","WoundLACBlood.png","中度流血\n","WoundLACBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundLACInfect.png","轻度感染\n","WoundLACInfect.png","中度感染\n","WoundLACInfect.png","重度感染\n"]),Vector.<String>(["WoundLACBruise.png","轻度擦伤\n","WoundLACBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundLACPain.png",false,[[0.8,186]],[[0.95,186]],true);
         this.sprPlayer.AddWound(this.grpHealthLayer,103,"左臂",0.1,_loc3_,0,Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundAFBruiseMild.png","轻度擦伤\n","WoundAFBruise.png","重度擦伤\n","WoundAFBruiseSevere.png","Broken bone\n"]),DM.m_vBluntVerbs,_loc8_,"WoundAFPain.png",false,[[0.5,189],[0.67,186]],[],false,0.67);
         this.sprPlayer.AddWound(this.grpHealthLayer,113,"右臂",0.1,_loc3_,0,Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundAFBruiseMild.png","轻度擦伤\n","WoundAFBruise.png","重度擦伤\n","WoundAFBruiseSevere.png","Broken bone\n"]),DM.m_vBluntVerbs,_loc8_,"WoundAFPain.png",false,[[0.5,189],[0.67,187]],[],true,0.67);
         this.sprPlayer.AddWound(this.grpHealthLayer,104,"上胸部",0.25,_loc3_,0,Vector.<String>(["WoundUCCCutMild.png","轻度割伤\n","WoundUCCCut.png","中度割伤\n","WoundUCCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundUCCBloodMild.png","轻度流血\n","WoundUCCBlood.png","中度流血\n","WoundUCCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundUCCInfect.png","轻度感染\n","WoundUCCInfect.png","中度感染\n","WoundUCCInfect.png","重度感染\n"]),Vector.<String>(["WoundUCCBruise.png","轻度擦伤\n","WoundUCCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundUCCPain.png",false,[[0.4,205,204,198],[0.5,189],[0.67,205,204,198],[0.9,193]],[[0.4,204,198],[0.67,204,198],[0.9,193]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,105,"下胸部",0.35,_loc3_,0,Vector.<String>(["WoundLCCCutMild.png","轻度割伤\n","WoundLCCCut.png","中度割伤\n","WoundLCCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundLCCBloodMild.png","轻度流血\n","WoundLCCBlood.png","中度流血\n","WoundLCCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundLCCInfect.png","轻度感染\n","WoundLCCInfect.png","中度感染\n","WoundLCCInfect.png","重度感染\n"]),Vector.<String>(["WoundLCCBruise.png","轻度擦伤\n","WoundLCCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundLCCPain.png",false,[[0.4,205,204,198],[0.5,189],[0.67,205,204,198],[0.9,197]],[[0.4,204,198],[0.67,204,198],[0.9,197]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,106,"上腹部",0.3,_loc3_,0,Vector.<String>(["WoundUSCCutMild.png","轻度割伤\n","WoundUSCCut.png","中度割伤\n","WoundUSCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundUSCBloodMild.png","轻度流血\n","WoundUSCBlood.png","中度流血\n","WoundUSCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundUSCInfect.png","轻度感染\n","WoundUSCInfect.png","中度感染\n","WoundUSCInfect.png","重度感染\n"]),Vector.<String>(["WoundUSCBruise.png","轻度擦伤\n","WoundUSCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundUSCPain.png",false,[[0.5,189,204]],[[0.5,204]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,107,"下腹部",0.25,_loc3_,0,Vector.<String>(["WoundLSCCutMild.png","轻度割伤\n","WoundLSCCut.png","中度割伤\n","WoundLSCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundLSCBloodMild.png","轻度流血\n","WoundLSCBlood.png","中度流血\n","WoundLSCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundLSCInfect.png","轻度感染\n","WoundLSCInfect.png","中度感染\n","WoundLSCInfect.png","重度感染\n"]),Vector.<String>(["WoundLSCBruise.png","轻度擦伤\n","WoundLSCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundLSCPain.png",false,[[0.5,189,204]],[[0.5,204]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,108,"左大腿",0.25,_loc3_,0,Vector.<String>(["WoundULCCutMild.png","轻度割伤\n","WoundULCCut.png","中度割伤\n","WoundULCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundULCBloodMild.png","轻度流血\n","WoundULCBlood.png","中度流血\n","WoundULCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundULCInfect.png","轻度感染\n","WoundULCInfect.png","中度感染\n","WoundULCInfect.png","重度感染\n"]),Vector.<String>(["WoundULCBruise.png","轻度擦伤\n","WoundULCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundULCPain.png",false,[[0.8,190]],[[0.95,190]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,114,"右大腿",0.1,_loc3_,0,Vector.<String>(["WoundULCCutMild.png","轻度割伤\n","WoundULCCut.png","中度割伤\n","WoundULCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundULCBloodMild.png","轻度流血\n","WoundULCBlood.png","中度流血\n","WoundULCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundULCInfect.png","轻度感染\n","WoundULCInfect.png","中度感染\n","WoundULCInfect.png","重度感染\n"]),Vector.<String>(["WoundULCBruise.png","轻度擦伤\n","WoundULCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundULCPain.png",false,[[0.8,191]],[[0.95,191]],true);
         this.sprPlayer.AddWound(this.grpHealthLayer,109,"左小腿",0.1,_loc3_,0,Vector.<String>(["WoundLLCCutMild.png","轻度割伤\n","WoundLLCCut.png","中度割伤\n","WoundLLCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundLLCBloodMild.png","轻度流血\n","WoundLLCBlood.png","中度流血\n","WoundLLCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundLLCInfect.png","轻度感染\n","WoundLLCInfect.png","中度感染\n","WoundLLCInfect.png","重度感染\n"]),Vector.<String>(["WoundLLCBruise.png","轻度擦伤\n","WoundLLCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundLLCPain.png",false,[[0.8,190]],[[0.95,190]]);
         this.sprPlayer.AddWound(this.grpHealthLayer,115,"右小腿",0.1,_loc3_,0,Vector.<String>(["WoundLLCCutMild.png","轻度割伤\n","WoundLLCCut.png","中度割伤\n","WoundLLCCutSevere.png","重度割伤\n"]),Vector.<String>(["WoundLLCBloodMild.png","轻度流血\n","WoundLLCBlood.png","中度流血\n","WoundLLCBloodSevere.png","重度流血\n"]),Vector.<String>(["WoundBlank.png","","WoundLLCInfect.png","轻度感染\n","WoundLLCInfect.png","中度感染\n","WoundLLCInfect.png","重度感染\n"]),Vector.<String>(["WoundLLCBruise.png","轻度擦伤\n","WoundLLCBruiseSevere.png","重度擦伤\n"]),DM.m_vBluntVerbs,DM.m_vCutVerbs,"WoundLLCPain.png",false,[[0.8,191]],[[0.95,191]],true);
         this.sprPlayer.AddWound(this.grpHealthLayer,110,"左腿",0.1,_loc3_,0,Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundLFBruiseMild.png","轻度擦伤\n","WoundLFBruise.png","重度擦伤\n","WoundLFBruiseSevere.png","Broken bone\n"]),DM.m_vBluntVerbs,_loc8_,"WoundLFPain.png",false,[[0.5,189],[0.67,190]],[],false,0.67);
         this.sprPlayer.AddWound(this.grpHealthLayer,116,"右腿",0.1,_loc3_,0,Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundBlank.png",""]),Vector.<String>(["WoundLFBruiseMild.png","轻度擦伤\n","WoundLFBruise.png","重度擦伤\n","WoundLFBruiseSevere.png","Broken bone\n"]),DM.m_vBluntVerbs,_loc8_,"WoundLFPain.png",false,[[0.5,189],[0.67,191]],[],true,0.67);
         GUIInventoryWound(this.sprPlayer.m_dictSlots[100]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[111]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[101]).m_nSlotOverlap = 17;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[102]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[112]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[103]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[113]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[104]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[105]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[106]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[107]).m_nSlotOverlap = 11;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[108]).m_nSlotOverlap = 4;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[114]).m_nSlotOverlap = 4;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[109]).m_nSlotOverlap = 4;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[115]).m_nSlotOverlap = 4;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[110]).m_nSlotOverlap = 4;
         GUIInventoryWound(this.sprPlayer.m_dictSlots[116]).m_nSlotOverlap = 4;
         this.sprPlayer.m_vBluntWoundSlots = this.sprPlayer.SortWounds(this.sprPlayer.m_vBluntWoundSlots);
         this.sprPlayer.m_vCutWoundSlots = this.sprPlayer.SortWounds(this.sprPlayer.m_vCutWoundSlots);
         this.grpUseSlot = this.sprPlayer.AddSlot(this,this.vItemSlots,211,"USE","blank.png","blank.png",new FlxPoint(-50,-50),0,false,false,new FlxPoint(-10,-10));
         this.grpUseSlot.m_bAllowStacks = true;
         this.grpTempSlot = this.sprPlayer.AddSlot(this,null,212,"TEMP","blank.png","blank.png",new FlxPoint(-10,-10),0,false,false,new FlxPoint(-10,-10),Vector.<int>([1,1,1]));
         this.grpUseSlot.m_bAllowStacks = true;
         this.MouseMode(MOUSE_DRAG);
         this.vCheckSlots = this.vItemSlots.concat();
         setAll("scrollFactor",new FlxPoint(0,0));
         setAll("cameras",[FlxG.camera]);
      }
      
      override public function destroy() : void
      {
         var _loc1_:ImgButton = null;
         var _loc2_:GUIInventorySlot = null;
         var _loc3_:ItemInstance = null;
         var _loc4_:Object = null;
         var _loc5_:int = 0;
         this.sprBG = DataHandler.DestroyObject(this.sprBG);
         this.sprCraftCapBG = DataHandler.DestroyObject(this.sprCraftCapBG);
         this.sprYieldCapBG = DataHandler.DestroyObject(this.sprYieldCapBG);
         this.sprTint = DataHandler.DestroyObject(this.sprTint);
         this.sprCraftArrowRight = DataHandler.DestroyObject(this.sprCraftArrowRight);
         this.sprCraftArrowDown = DataHandler.DestroyObject(this.sprCraftArrowDown);
         this.sprSkillArrowRight = DataHandler.DestroyObject(this.sprSkillArrowRight);
         this.sprTraitArrowRight = DataHandler.DestroyObject(this.sprTraitArrowRight);
         this.sprEncounter = DataHandler.DestroyObject(this.sprEncounter);
         this.sprBody = DataHandler.DestroyObject(this.sprBody);
         this.txtEncounter = DataHandler.DestroyObject(this.txtEncounter);
         this.txtEncResponse = DataHandler.DestroyObject(this.txtEncResponse);
         this.txtEncAvail = DataHandler.DestroyObject(this.txtEncAvail);
         this.txtEncResponseLabel = DataHandler.DestroyObject(this.txtEncResponseLabel);
         this.txtCraIngredients = DataHandler.DestroyObject(this.txtCraIngredients);
         this.txtCraYield = DataHandler.DestroyObject(this.txtCraYield);
         this.txtSklAvail = DataHandler.DestroyObject(this.txtSklAvail);
         this.txtTraitAvail = DataHandler.DestroyObject(this.txtTraitAvail);
         this.txtTraitInstruct = DataHandler.DestroyObject(this.txtTraitInstruct);
         this.txtSkillSpace1 = DataHandler.DestroyObject(this.txtSkillSpace1);
         this.txtTraitSlot1 = DataHandler.DestroyObject(this.txtTraitSlot1);
         this.txtSkillTotal = DataHandler.DestroyObject(this.txtSkillTotal);
         this.txtGround = DataHandler.DestroyObject(this.txtGround);
         this.txtAvailCamp = DataHandler.DestroyObject(this.txtAvailCamp);
         this.txtCamp = DataHandler.DestroyObject(this.txtCamp);
         this.txtVehicle = DataHandler.DestroyObject(this.txtVehicle);
         this.txtCraftMoves = DataHandler.DestroyObject(this.txtCraftMoves);
         this.txtCondTitle = DataHandler.DestroyObject(this.txtCondTitle);
         this.txtCondStatsWarning = DataHandler.DestroyObject(this.txtCondStatsWarning);
         this.grpPopUp = DataHandler.DestroyObject(this.grpPopUp);
         this.grpItemsLayer = DataHandler.DestroyObject(this.grpItemsLayer);
         this.grpGroundLayer = DataHandler.DestroyObject(this.grpGroundLayer);
         this.grpEncounterLayer = DataHandler.DestroyObject(this.grpEncounterLayer);
         this.grpSkillsLayer = DataHandler.DestroyObject(this.grpSkillsLayer);
         this.grpVehicleLayer = DataHandler.DestroyObject(this.grpVehicleLayer);
         this.grpCampLayer = DataHandler.DestroyObject(this.grpCampLayer);
         this.grpBattleLayer = DataHandler.DestroyObject(this.grpBattleLayer);
         this.grpHealthLayer = DataHandler.DestroyObject(this.grpHealthLayer);
         this.grpDMCLayer = DataHandler.DestroyObject(this.grpDMCLayer);
         this.grpCraftLayer = DataHandler.DestroyObject(this.grpCraftLayer);
         if(this.aPanels != null)
         {
            _loc5_ = 0;
            while(_loc5_ < this.aPanels.length)
            {
               this.aPanels[_loc5_] = null;
               _loc5_++;
            }
            this.aPanels = null;
         }
         this.grpScavengeLoot = DataHandler.DestroyObject(this.grpScavengeLoot);
         this.grpScavengeAccident = DataHandler.DestroyObject(this.grpScavengeAccident);
         this.grpScavengeCreature = DataHandler.DestroyObject(this.grpScavengeCreature);
         this.grpCampConcealment = DataHandler.DestroyObject(this.grpCampConcealment);
         this.grpCampShelter = DataHandler.DestroyObject(this.grpCampShelter);
         this.grpCampAlertness = DataHandler.DestroyObject(this.grpCampAlertness);
         this.grpCampHealRate = DataHandler.DestroyObject(this.grpCampHealRate);
         this.grpCampSleepQuality = DataHandler.DestroyObject(this.grpCampSleepQuality);
         this.grpHealthBlood = DataHandler.DestroyObject(this.grpHealthBlood);
         this.grpHealthInfection = DataHandler.DestroyObject(this.grpHealthInfection);
         this.grpHealthPain = DataHandler.DestroyObject(this.grpHealthPain);
         this.btnEncConfirm = DataHandler.DestroyObject(this.btnEncConfirm);
         this.btnEncViewItems = DataHandler.DestroyObject(this.btnEncViewItems);
         this.btnSkillsConfirm = DataHandler.DestroyObject(this.btnSkillsConfirm);
         this.btnSkillsRandom = DataHandler.DestroyObject(this.btnSkillsRandom);
         this.btnCraftConfirm = DataHandler.DestroyObject(this.btnCraftConfirm);
         this.btnCraftClear = DataHandler.DestroyObject(this.btnCraftClear);
         this.btnContextUse = DataHandler.DestroyObject(this.btnContextUse);
         this.btnContextDelete = DataHandler.DestroyObject(this.btnContextDelete);
         this.btnContextCraft = DataHandler.DestroyObject(this.btnContextCraft);
         this.btnContextTake = DataHandler.DestroyObject(this.btnContextTake);
         this.btnContextEmpty = DataHandler.DestroyObject(this.btnContextEmpty);
         this.btnContextPad = DataHandler.DestroyObject(this.btnContextPad);
         this.btnCraftPrev = DataHandler.DestroyObject(this.btnCraftPrev);
         this.btnCraftNext = DataHandler.DestroyObject(this.btnCraftNext);
         this.btnYieldPrev = DataHandler.DestroyObject(this.btnYieldPrev);
         this.btnYieldNext = DataHandler.DestroyObject(this.btnYieldNext);
         this.btnCraftRecipes = DataHandler.DestroyObject(this.btnCraftRecipes);
         this.btnCraftAvail = DataHandler.DestroyObject(this.btnCraftAvail);
         this.btnCursor = DataHandler.DestroyObject(this.btnCursor);
         for each(_loc1_ in this.m_vKnownRecipes)
         {
            _loc1_.destroy();
         }
         this.m_vKnownRecipes = null;
         if(this.m_aConditions != null)
         {
            _loc5_ = 0;
            while(_loc5_ < this.m_aConditions.length)
            {
               FlxText(this.m_aConditions[_loc5_]).destroy();
               _loc5_++;
            }
            this.m_aConditions = null;
         }
         for each(_loc2_ in this.vItemSlots)
         {
            _loc2_.destroy();
         }
         this.vItemSlots = null;
         for each(_loc2_ in this.vSaveSlots)
         {
            _loc2_.destroy();
         }
         this.vSaveSlots = null;
         this.grpCraftingIngredientsSlot = DataHandler.DestroyObject(this.grpCraftingIngredientsSlot);
         this.grpCraftingYieldSlot = DataHandler.DestroyObject(this.grpCraftingYieldSlot);
         this.grpAvailCraftItemsSlot = DataHandler.DestroyObject(this.grpAvailCraftItemsSlot);
         this.grpEncounterSlot = DataHandler.DestroyObject(this.grpEncounterSlot);
         this.grpAvailEncounterSlot = DataHandler.DestroyObject(this.grpAvailEncounterSlot);
         this.grpAvailTraitSlot = DataHandler.DestroyObject(this.grpAvailTraitSlot);
         this.grpAvailSkillSlot = DataHandler.DestroyObject(this.grpAvailSkillSlot);
         this.grpTraitSlot = DataHandler.DestroyObject(this.grpTraitSlot);
         this.grpSkillSlot = DataHandler.DestroyObject(this.grpSkillSlot);
         this.grpVehicleSlot = DataHandler.DestroyObject(this.grpVehicleSlot);
         this.grpUseSlot = DataHandler.DestroyObject(this.grpUseSlot);
         this.grpTempSlot = DataHandler.DestroyObject(this.grpTempSlot);
         this.grpAvailCampSlot = DataHandler.DestroyObject(this.grpAvailCampSlot);
         this.objDragging = DataHandler.DestroyObject(this.objDragging);
         this.objContext = null;
         this.ptDragOrigin = null;
         this.ptDragOffset = null;
         this.grpSlotSource = null;
         this.objParentItem = null;
         this.objEncounter = null;
         this.objCurrentRecipe = null;
         this.ptMouse = null;
         this.objMouseOverItem = null;
         this.objMouseOverItemLast = null;
         this.m_objScavLoc = DataHandler.DestroyObject(this.m_objScavLoc);
         for each(_loc3_ in this.vDegradeCleanup)
         {
            _loc3_.destroy();
         }
         this.vDegradeCleanup = null;
         for each(_loc3_ in this.vAvailCraftingPages)
         {
            _loc3_.destroy();
         }
         this.vAvailCraftingPages = null;
         for each(_loc3_ in this.vYieldPages)
         {
            _loc3_.destroy();
         }
         this.vYieldPages = null;
         this.vCurrentRecipes = null;
         this.sprPlayer = null;
         this.objPlayState = null;
         for each(_loc1_ in this.m_vContextButtons)
         {
            _loc1_.destroy();
         }
         this.m_vContextButtons = null;
         for(_loc4_ in this.dictContextButtons)
         {
            delete this.dictContextButtons[_loc4_];
            ImgButton(_loc4_).destroy();
         }
         this.dictContextButtons = null;
      }
      
      override public function update() : void
      {
         var _loc2_:Boolean = false;
         var _loc3_:* = null;
         var _loc4_:Number = NaN;
         var _loc5_:ItemInstance = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:FlxPoint = null;
         var _loc9_:Vector.<int> = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:int = 0;
         var _loc12_:Vector.<ImgButton> = null;
         var _loc13_:int = 0;
         var _loc14_:ImgButton = null;
         super.update();
         if(this.m_nPanel == PANEL_HEALTH)
         {
            if(this.vCheckSlots.length == this.vItemSlots.length)
            {
               this.vCheckSlots = this.vCheckSlots.concat(this.sprPlayer.m_vCutWoundSlots);
               this.vCheckSlots = this.vCheckSlots.concat(this.sprPlayer.m_vBluntWoundSlots);
            }
         }
         else if(this.vCheckSlots.length > this.vItemSlots.length)
         {
            this.vCheckSlots.length = this.vItemSlots.length;
         }
         if(this.objDragging != null && (this.m_nPanel == PANEL_RESPONSE || this.m_nPanel == PANEL_BATTLE) && (this.grpAvailEncounterSlot.SocketedItem().AcceptsItem(this.objDragging) == false || this.grpEncounterSlot.SocketedItem().AcceptsItem(this.objDragging) == false))
         {
            this.sprPlayer.DropItem(this.objDragging,true,true);
            this.StopDragging();
         }
         this.ptMouse = FlxG.mouse.getScreenPosition(null,this.ptMouse);
         this.objMouseOverItemLast = this.objMouseOverItem;
         this.objMouseOverItem = this.GetItemUnderPoint(this.ptMouse,this.vCheckSlots);
         if(this.objMouseOverItem != null)
         {
            _loc2_ = true;
            if(this.objDragging == null)
            {
               this.grpSlotSource = this.objMouseOverItem.grpItemPanelSlot;
               this.objParentItem = this.objMouseOverItem.m_objParentContainer;
               this.fRotationOrig = this.objMouseOverItem.m_fAngle;
            }
            if(this.objMouseOverItem == this.grpAvailEncounterSlot.SocketedItem())
            {
               _loc2_ = false;
            }
            if(_loc2_ && this.objMouseOverItemLast == null)
            {
               this.AddGUIChild(this.grpPopUp);
               this.grpPopUp.Show();
            }
            if(this.objMouseOverItem != this.objMouseOverItemLast)
            {
               _loc3_ = "";
               if(this.m_nPanel == PANEL_BATTLE)
               {
                  _loc3_ = DataHandler.GetBattleMove(this.objMouseOverItem.IDString).m_strPopUp;
               }
               else
               {
                  _loc3_ = this.objMouseOverItem.Description();
                  _loc3_ += "\n";
                  if(this.objMouseOverItem is ItemHardware)
                  {
                     if(this.grpSlotSource == this.sprPlayer.grpGroundSlot && this.sprPlayer.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
                     {
                        _loc3_ += "\n价值: $" + ItemHardware(this.objMouseOverItem).GetTotalValueHidden().toFixed(2);
                     }
                     else
                     {
                        _loc3_ += "\n价值: $" + this.objMouseOverItem.GetTotalValue().toFixed(2);
                     }
                  }
                  else if(this.objMouseOverItem.GetTotalValue() > 0)
                  {
                     _loc3_ += "\n价值: $" + this.objMouseOverItem.GetTotalValue().toFixed(2);
                  }
                  if(this.objMouseOverItem.m_bDegrades || this.objMouseOverItem.fDurability < 1)
                  {
                     if((_loc4_ = this.objMouseOverItem.fDurability * 100) < 0.1 && _loc4_ > 0)
                     {
                        _loc4_ = 0.1;
                     }
                     _loc3_ += "\n耐久: " + Number(_loc4_).toFixed(1) + "%";
                  }
                  if(this.objMouseOverItem.ItemDefinition.m_vChargeProfiles.length > 0 && _loc3_.indexOf("\n次数: ") < 0)
                  {
                     _loc3_ += "\n次数: " + this.objMouseOverItem.GetRemainingChargeInfo();
                  }
                  if(this.objMouseOverItem.ItemDefinition.aCapacities.length > 0 && this.objMouseOverItem.ItemDefinition.nGroupID != 96)
                  {
                     if(this.objMouseOverItem.vItems.length > 0)
                     {
                        _loc3_ += "\n装载: " + this.objMouseOverItem.vItems.length + "  道具. ";
                     }
                     else
                     {
                        _loc3_ += "\n装载: 空.";
                     }
                  }
                  if(this.objMouseOverItem.WeightPlusContents > 0 && this.objMouseOverItem.ItemDefinition.m_vProperties.indexOf(86) < 0)
                  {
                     _loc3_ += "\n重量: " + Number(this.objMouseOverItem.WeightPlusContents).toFixed(2) + "公斤.";
                  }
               }
               if(!this.objMouseOverItem.bSocketed && this.objMouseOverItem.ItemDefinition.aCapacities.length > 0)
               {
                  this.grpPopUp.AddPeek(this.objMouseOverItem);
               }
               else
               {
                  this.grpPopUp.ClearPeek();
               }
               this.grpPopUp.UpdateInfo(_loc3_,this.objMouseOverItem.GetImagePeek());
            }
            this.grpPopUp.Move(this.ptMouse.x + 16,this.ptMouse.y);
         }
         else
         {
            this.grpPopUp.Hide();
            remove(this.grpPopUp);
            if(this.objDragging == null)
            {
               this.grpSlotSource = null;
               this.objParentItem = null;
               this.fRotationOrig = 0;
            }
         }
         var _loc1_:Boolean = false;
         if(this.objDragging == null && FlxG.mouse.justReleased() && this.objMouseOverItem != null && this.objContext == null && !this.objMouseOverItem.Ghosted && this.grpSlotSource != null)
         {
            if((_loc5_ = this.grpSlotSource.RemoveItem(this.objMouseOverItem,this.bMouseWholeStack)) == null)
            {
               _loc5_ = this.grpSlotSource.UnSocketItem(false,this.objMouseOverItem,this.bMouseWholeStack);
            }
            if(_loc5_ != null)
            {
               if(this.grpSlotSource == this.grpEncounterSlot)
               {
                  this.UpdateResponseText();
               }
               if(this.m_nMouseMode != MOUSE_DELETE)
               {
                  this.StartDragging(_loc5_,this.ptMouse);
               }
               else if(this.vForbidDeleteIDs.indexOf(_loc5_.ItemDefinition.nGroupID) >= 0)
               {
                  this.StartDragging(_loc5_,this.ptMouse);
               }
               if(this.m_nMouseMode == MOUSE_TAKE || this.grpSlotSource == this.grpAvailCampSlot)
               {
                  this.ContextTakeDrop();
               }
               else if(this.m_nMouseMode == MOUSE_USE && this.ListUsableItems(_loc5_,this.grpUseSlot).length > 0)
               {
                  this.objLoopFitResult.m_grpSlot = this.grpUseSlot;
                  this.objLoopFitResult.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET;
                  this.ReleaseItem(_loc5_,this.objLoopFitResult);
               }
               if(this.grpSlotSource == this.sprPlayer.grpGroundSlot || this.objMouseOverItem.Slot == this.sprPlayer.grpGroundSlot)
               {
                  this.sprPlayer.m_tilCurrentHex.CalculateValue();
               }
            }
         }
         else if(this.objDragging != null)
         {
            if(FlxG.keys.justReleased("A") || FlxG.keys.justReleased("LEFT") || FlxG.mouse.wheel > 0)
            {
               this.objDragging.Rotate(this.objDragging.m_fAngle - 90);
               this.TintItem(this.objDragging);
            }
            else if(FlxG.keys.justReleased("D") || FlxG.keys.justReleased("RIGHT") || FlxG.mouse.wheel < 0)
            {
               this.objDragging.Rotate(this.objDragging.m_fAngle + 90);
               this.TintItem(this.objDragging);
            }
            if(this.ptMouse.x > this.sprBG.x)
            {
               for each(_loc6_ in this.vCheckSlots)
               {
                  if(_loc6_.alive)
                  {
                     if(_loc6_.btnSlot.bMouseOver)
                     {
                        this.objLoopFitResult = this.TestItemInSocket(this.objDragging,_loc6_);
                        if(this.objLoopFitResult.m_nResult > 0)
                        {
                           break;
                        }
                     }
                     else if((_loc7_ = _loc6_.SocketedItem()) != null && _loc7_.ItemDefinition.aCapacities.length > 0)
                     {
                        _loc8_ = new FlxPoint(this.objDragging.x - _loc6_.sprCap.x,this.objDragging.y - _loc6_.sprCap.y);
                        _loc9_ = Vector.<int>([GUIFitItemResult.RESULT_CANNOT_FIT]);
                        this.objLoopFitResult = this.TestItemInCapBox(this.objDragging,_loc7_,_loc8_,_loc9_);
                        if(this.objLoopFitResult.m_nResult > 0)
                        {
                           break;
                        }
                     }
                  }
               }
            }
            if(this.objLoopFitResult.m_nResult > 0)
            {
               this.sprTint.color = 255;
            }
            else
            {
               this.sprTint.color = 16711680;
            }
            if(FlxG.mouse.clickedDouble() && (this.objLoopFitResult.m_nResult == GUIFitItemResult.RESULT_CAN_FIT || this.objLoopFitResult.m_nResult == GUIFitItemResult.RESULT_CANNOT_FIT || this.objLoopFitResult.m_nResult == GUIFitItemResult.RESULT_CAN_SOCKET))
            {
               this.ContextTakeDrop();
            }
            else if((FlxG.mouse.justReleased() || FlxG.mouse.justReleasedRight()) && this.ptMouse.x > this.sprBG.x)
            {
               _loc1_ = true;
               _loc10_ = null;
               if(FlxG.mouse.justReleasedRight() && this.objDragging.m_vStack.length > 0 && this.objLoopFitResult.m_nResult != GUIFitItemResult.RESULT_CANNOT_FIT && this.objLoopFitResult.m_nResult != GUIFitItemResult.RESULT_CAN_FIT_SWAP && this.objLoopFitResult.m_nResult != GUIFitItemResult.RESULT_CAN_SOCKET_SWAP)
               {
                  (_loc10_ = this.objDragging.m_vStack.pop()).m_vStack = this.objDragging.m_vStack;
                  this.objDragging.m_vStack = new Vector.<ItemInstance>();
                  this.objDragging.UpdateStackImage();
                  _loc10_.UpdateStackImage();
               }
               _loc11_ = int(BarterHex.BARTER_NONE);
               if(this.sprPlayer.m_tilCurrentHex != null)
               {
                  _loc11_ = int(this.sprPlayer.m_tilCurrentHex.m_nBarterTile);
               }
               if(_loc11_ == BarterHex.BARTER_SELL && this.grpSlotSource == this.sprPlayer.grpGroundSlot)
               {
                  this.sprPlayer.m_tilCurrentHex.m_nBarterTile = BarterHex.BARTER_BUYSELL;
               }
               this.ReleaseItem(this.objDragging,this.objLoopFitResult);
               if(this.sprPlayer.m_tilCurrentHex != null)
               {
                  this.sprPlayer.m_tilCurrentHex.m_nBarterTile = _loc11_;
               }
               if(_loc10_ != null)
               {
                  this.StopDragging();
                  this.StartDragging(_loc10_,this.ptMouse);
               }
            }
            else
            {
               this.objDragging.x = this.ptMouse.x - this.ptDragOffset.x;
               this.objDragging.y = this.ptMouse.y - this.ptDragOffset.y;
               this.sprTint.x = this.objDragging.x + this.objDragging.width / 2;
               this.sprTint.y = this.objDragging.y + this.objDragging.height / 2;
            }
         }
         if(_loc1_ == false && FlxG.mouse.justReleasedRight())
         {
            this.objContext = this.objMouseOverItem;
            _loc12_ = new Vector.<ImgButton>();
            if(this.objMouseOverItem != null && !this.objMouseOverItem.Ghosted)
            {
               _loc12_.push(this.btnContextTake);
               if(this.m_nPanel != PANEL_RESPONSE && this.m_nPanel != PANEL_BATTLE && this.m_nPanel != PANEL_CRAFT && this.m_nPanel != PANEL_SKILLS)
               {
                  if(this.objDragging == null && this.ListUsableItems(this.objContext,this.grpUseSlot).length > 0)
                  {
                     _loc12_.push(this.btnContextUse);
                  }
                  if(this.objContext.vItems.length > 0 && this.objContext.Slot != null && this.sprPlayer.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_SELL)
                  {
                     if(this.objContext.Slot.m_bCarried || this.objContext.Slot == this.sprPlayer.grpGroundSlot || this.objContext.Slot == this.sprPlayer.grpCampSlot)
                     {
                        _loc12_.push(this.btnContextEmpty);
                     }
                  }
                  if(this.grpSlotSource != this.grpCraftingIngredientsSlot && this.grpCraftingIngredientsSlot.alive && this.grpCraftingIngredientsSlot.SocketedItem().AcceptsItem(this.objMouseOverItem))
                  {
                     _loc12_.push(this.btnContextCraft);
                  }
                  _loc13_ = 0;
                  while(_loc13_ < this.objContext.ItemDefinition.m_vModeLabels.length)
                  {
                     _loc14_ = this.CreateContext(this.objContext.ItemDefinition.m_vModeLabels[_loc13_]);
                     this.dictContextButtons[_loc14_] = this.objContext.ItemDefinition.m_vModeIDs[_loc13_];
                     _loc12_.push(_loc14_);
                     _loc13_++;
                  }
                  if(this.vForbidDeleteIDs.indexOf(this.objContext.ItemDefinition.nGroupID) < 0)
                  {
                     _loc12_.push(this.btnContextPad);
                     _loc12_.push(this.btnContextDelete);
                  }
               }
            }
            if(_loc12_.length > 0)
            {
               this.ArrangeButtons(_loc12_);
            }
            else
            {
               this.objContext = null;
            }
         }
         else if(FlxG.mouse.justReleased() || this.objContext == null)
         {
            this.objContext = null;
            this.ClearButtons();
         }
         this.objLoopFitResult.destroy();
      }
      
      private function ArrangeButtons(param1:Vector.<ImgButton>) : void
      {
         var _loc4_:ImgButton = null;
         var _loc5_:FlxPoint = null;
         this.ClearButtons(true);
         var _loc2_:int = 0;
         var _loc3_:int = this.ptMouse.y;
         for each(_loc4_ in param1)
         {
            this.AddGUIChild(_loc4_);
            _loc4_.x = this.ptMouse.x;
            _loc4_.y = _loc3_;
            _loc3_ += _loc4_.height;
            this.m_vContextButtons.push(_loc4_);
         }
         _loc3_ += _loc4_.height;
         this.ptTemp1 = new FlxPoint();
         _loc5_ = GUIValues.GetPoint("offset",_loc5_);
         this.ptTemp1.x = _loc5_.x + GUIValues.GetInt("width");
         this.ptTemp1.y = _loc5_.y + GUIValues.GetInt("height") - GUIValues.GetInt("PlayState.camMsg.minheight");
         if(_loc4_.x + _loc4_.width > this.ptTemp1.x)
         {
            _loc2_ = this.ptTemp1.x - _loc4_.x - _loc4_.width;
         }
         if(_loc3_ > this.ptTemp1.y)
         {
            _loc3_ = this.ptTemp1.y - _loc3_;
         }
         else
         {
            _loc3_ = 0;
         }
         if(_loc3_ != 0 || _loc2_ != 0)
         {
            for each(_loc4_ in param1)
            {
               _loc4_.x += _loc2_;
               _loc4_.y += _loc3_;
            }
         }
      }
      
      private function ClearButtons(param1:Boolean = false) : void
      {
         var _loc2_:ImgButton = null;
         var _loc3_:Object = null;
         for each(_loc2_ in this.m_vContextButtons)
         {
            _loc2_.on = false;
            _loc2_.status = FlxButton.NORMAL;
            remove(_loc2_);
         }
         this.m_vContextButtons.length = 0;
         if(param1)
         {
            return;
         }
         for(_loc3_ in this.dictContextButtons)
         {
            delete this.dictContextButtons[_loc3_];
            ImgButton(_loc3_).destroy();
         }
      }
      
      private function ContextEmpty() : void
      {
         var _loc1_:ItemInstance = null;
         var _loc3_:Vector.<int> = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         var _loc2_:ItemInstance = this.objDragging;
         if(_loc2_ == null && this.objContext != null)
         {
            _loc2_ = this.objContext;
            this.grpSlotSource = this.objContext.grpItemPanelSlot;
            this.objParentItem = this.objContext.m_objParentContainer;
            this.fRotationOrig = this.objContext.m_fAngle;
         }
         if(this.grpSlotSource == null)
         {
            return;
         }
         if(_loc2_ != null)
         {
            if(this.sprPlayer.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
            {
               _loc3_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT]);
            }
            _loc4_ = _loc2_.vItems.concat();
            for each(_loc5_ in _loc4_)
            {
               if(_loc5_.Slot != null)
               {
                  _loc6_ = _loc5_.Slot.RemoveItem(_loc5_,true);
               }
               else
               {
                  _loc6_ = _loc2_.RemoveItem(_loc5_,true);
               }
               if(_loc6_ != null)
               {
                  _loc1_ = this.AddItemToCapBox(_loc6_,this.sprPlayer.m_tilCurrentHex.GroundObject,_loc3_);
                  if(_loc1_ == _loc6_)
                  {
                     this.AddItemToCapBox(_loc1_,_loc2_);
                  }
                  else if(_loc1_ != null)
                  {
                     _loc4_.push(_loc1_);
                     this.AddItemToCapBox(_loc1_,_loc2_);
                     if(_loc6_ != null)
                     {
                        FlxG.loadSound(DataHandler.GetSound(_loc6_.ItemDefinition.m_vSounds[1]),GUIEscMenu.m_fSoundVolume,false,true,true);
                     }
                  }
                  else if(_loc6_ != null)
                  {
                     FlxG.loadSound(DataHandler.GetSound(_loc6_.ItemDefinition.m_vSounds[1]),GUIEscMenu.m_fSoundVolume,false,true,true);
                  }
               }
            }
         }
         this.btnContextEmpty.on = false;
         this.btnContextEmpty.status = FlxButton.NORMAL;
      }
      
      private function ContextTakeDrop() : void
      {
         var _loc1_:ItemInstance = null;
         var _loc3_:* = false;
         var _loc4_:Vector.<int> = null;
         var _loc5_:Vector.<GUIInventorySlot> = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:ItemInstance = null;
         var _loc2_:ItemInstance = this.objDragging;
         if(_loc2_ == null && this.objContext != null)
         {
            this.grpSlotSource = this.objContext.grpItemPanelSlot;
            this.objParentItem = this.objContext.m_objParentContainer;
            this.fRotationOrig = this.objContext.m_fAngle;
            if(_loc2_ == null)
            {
               _loc2_ = this.objContext.Slot.RemoveItem(this.objContext,this.bMouseWholeStack);
            }
            if(_loc2_ == null)
            {
               _loc2_ = this.objContext.Slot.UnSocketItem(false,this.objContext,this.bMouseWholeStack);
            }
            if(_loc2_ != null)
            {
               FlxG.loadSound(DataHandler.GetSound(_loc2_.ItemDefinition.m_vSounds[0]),GUIEscMenu.m_fSoundVolume,false,true,true);
            }
         }
         if(this.grpSlotSource == null)
         {
            return;
         }
         if(_loc2_ != null)
         {
            if(this.grpSlotSource.m_bCarried)
            {
               if(this.sprPlayer.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_SELL)
               {
                  _loc4_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_STACK_PARTIAL]);
                  _loc1_ = this.AddItemToCapBox(_loc2_,this.sprPlayer.m_tilCurrentHex.GroundObject,_loc4_);
               }
               else
               {
                  _loc1_ = _loc2_;
               }
            }
            else if(this.grpSlotSource == this.sprPlayer.grpGroundSlot)
            {
               switch(this.m_nPanel)
               {
                  case PANEL_CAMP:
                     _loc5_ = Vector.<GUIInventorySlot>([this.sprPlayer.grpCampSlot]);
                     break;
                  case PANEL_VEHICLE:
                     _loc5_ = Vector.<GUIInventorySlot>([this.grpVehicleSlot]);
                     break;
                  default:
                     _loc5_ = this.sprPlayer.vInvCategories.concat();
               }
               for each(_loc6_ in _loc5_)
               {
                  _loc1_ = this.AddItemToSlot(_loc2_,_loc6_);
                  if(_loc1_ != _loc2_)
                  {
                     break;
                  }
               }
            }
            else if(this.grpSlotSource == this.grpCraftingIngredientsSlot || this.grpSlotSource == this.grpCraftingYieldSlot)
            {
               this.CheckRecipe();
               for each(_loc7_ in this.vAvailCraftingPages)
               {
                  _loc4_ = Vector.<int>([GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_STACK_PARTIAL]);
                  _loc1_ = this.AddItemToCapBox(_loc2_,_loc7_,_loc4_);
                  if(_loc1_ != _loc2_)
                  {
                     break;
                  }
               }
            }
            else if(this.grpSlotSource == this.grpAvailCraftItemsSlot)
            {
               _loc1_ = this.AddItemToSlot(_loc2_,this.grpCraftingIngredientsSlot);
               this.CheckRecipe();
            }
            else if(this.grpSlotSource == this.grpAvailTraitSlot)
            {
               _loc1_ = this.AddItemToCapBox(_loc2_,this.grpTraitSlot.SocketedItem());
               if(_loc1_ != _loc2_)
               {
                  this.SkillPairCheck(_loc2_,this.grpTraitSlot);
               }
            }
            else if(this.grpSlotSource == this.grpAvailSkillSlot)
            {
               _loc1_ = this.AddItemToSlot(_loc2_,this.grpSkillSlot);
               if(_loc1_ != _loc2_)
               {
                  this.SkillPairCheck(_loc2_,this.grpSkillSlot);
               }
            }
            else if(this.grpSlotSource == this.grpSkillSlot || this.grpSlotSource == this.grpTraitSlot)
            {
               _loc4_ = Vector.<int>([GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP]);
               if(_loc2_.ItemDefinition.nGroupID == 91)
               {
                  if(this.grpAvailSkillSlot.alive)
                  {
                     _loc1_ = this.AddItemToCapBox(_loc2_,this.grpAvailSkillSlot.SocketedItem(),_loc4_);
                     if(_loc1_ != _loc2_)
                     {
                        this.SkillPairCheck(_loc2_,this.grpAvailSkillSlot);
                     }
                  }
                  else
                  {
                     _loc1_ = _loc2_;
                  }
               }
               else if(this.grpAvailTraitSlot.alive)
               {
                  _loc1_ = this.AddItemToCapBox(_loc2_,this.grpAvailTraitSlot.SocketedItem(),_loc4_);
                  if(_loc1_ != _loc2_)
                  {
                     this.SkillPairCheck(_loc2_,this.grpAvailTraitSlot);
                  }
               }
               else
               {
                  _loc1_ = _loc2_;
               }
            }
            else if(this.grpSlotSource == this.grpAvailEncounterSlot)
            {
               _loc1_ = this.AddItemToSlot(_loc2_,this.grpEncounterSlot);
               this.UpdateResponseText();
            }
            else if(this.grpSlotSource == this.grpEncounterSlot)
            {
               _loc1_ = this.AddItemToSlot(_loc2_,this.grpAvailEncounterSlot);
               this.UpdateResponseText();
            }
            else if(this.grpSlotSource == this.sprPlayer.grpCampSlot && this.objParentItem != null)
            {
               _loc1_ = this.AddItemToSlot(_loc2_,this.sprPlayer.grpGroundSlot);
            }
            else if(this.grpSlotSource == this.grpAvailCampSlot)
            {
               _loc8_ = this.sprPlayer.grpCampSlot.UnSocketItem(true);
               this.sprPlayer.RemoveCondition(ItemCamp(_loc8_).Condition);
               this.sprPlayer.grpCampSlot.SocketItem(_loc2_);
               this.sprPlayer.AddCondition(ItemCamp(_loc2_).Condition);
               this.sprPlayer.SetCamp(this.sprPlayer.m_tilCurrentHex,ItemCamp(_loc2_));
               this.UpdateCampStats(this.sprPlayer.GetCamp());
               _loc1_ = this.AddItemToSlot(_loc8_,this.grpAvailCampSlot);
               _loc8_.Alpha = 1;
            }
            _loc3_ = _loc1_ == null;
            this.StopDragging();
            if(_loc1_ != null)
            {
               this.StartDragging(_loc1_,this.ptMouse);
               if(_loc1_ == _loc2_)
               {
                  _loc3_ = true;
               }
               if(_loc3_)
               {
                  FlxG.loadSound(DataHandler.GetSound(_loc1_.ItemDefinition.m_vSounds[1]),GUIEscMenu.m_fSoundVolume,false,true,true);
               }
            }
         }
         this.btnContextTake.on = false;
         this.btnContextTake.status = FlxButton.NORMAL;
      }
      
      private function ContextUse() : void
      {
         var _loc1_:ItemInstance = null;
         var _loc2_:GUIFitItemResult = null;
         if(this.objContext != null)
         {
            this.grpSlotSource = this.objContext.grpItemPanelSlot;
            this.objParentItem = this.objContext.m_objParentContainer;
            this.fRotationOrig = this.objContext.m_fAngle;
            _loc1_ = this.objContext.Slot.RemoveItem(this.objContext,this.bMouseWholeStack);
            if(_loc1_ == null)
            {
               _loc1_ = this.objContext.Slot.UnSocketItem(false,this.objContext,this.bMouseWholeStack);
            }
            if(_loc1_ != null)
            {
               _loc2_ = new GUIFitItemResult();
               _loc2_.m_grpSlot = this.grpUseSlot;
               _loc2_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET;
               this.StartDragging(_loc1_,this.ptMouse);
               this.ReleaseItem(_loc1_,_loc2_);
            }
         }
         this.btnContextUse.on = false;
         this.btnContextUse.status = FlxButton.NORMAL;
      }
      
      private function ContextDelete() : void
      {
         var _loc1_:ItemInstance = null;
         if(this.objContext == null)
         {
            this.objContext = this.objDragging;
         }
         if(this.objContext != null)
         {
            if(this.objContext.Slot != null)
            {
               _loc1_ = this.objContext.Slot.RemoveItem(this.objContext,this.bMouseWholeStack);
            }
            if(_loc1_ == null && this.objContext.Slot != null)
            {
               _loc1_ = this.objContext.Slot.UnSocketItem(false,this.objContext,this.bMouseWholeStack);
            }
         }
         this.btnContextDelete.on = false;
         this.btnContextDelete.status = FlxButton.NORMAL;
      }
      
      private function ContextCraft() : void
      {
         var _loc2_:ItemInstance = null;
         this.grpSlotSource = this.objContext.grpItemPanelSlot;
         this.objParentItem = this.objContext.m_objParentContainer;
         this.fRotationOrig = this.objContext.m_fAngle;
         var _loc1_:ItemInstance = this.objContext.Slot.RemoveItem(this.objContext,this.bMouseWholeStack);
         if(_loc1_ == null)
         {
            _loc1_ = this.objContext.Slot.UnSocketItem(false,this.objContext,this.bMouseWholeStack);
         }
         if(_loc1_ != null)
         {
            _loc2_ = this.AddItemToSlot(_loc1_,this.grpCraftingIngredientsSlot);
            this.CheckRecipe();
            if(_loc2_ != _loc1_)
            {
               if(_loc2_ != null)
               {
                  this.StartDragging(_loc2_,this.ptMouse);
                  this.ReleaseItem(_loc2_,new GUIFitItemResult());
               }
            }
         }
         this.btnContextCraft.on = false;
         this.btnContextCraft.status = FlxButton.NORMAL;
      }
      
      private function ContextMode(param1:ImgButton) : void
      {
         if(this.objContext == null)
         {
            this.objContext = this.objDragging;
         }
         if(this.objContext != null)
         {
            this.objContext.ChangeMode(DataHandler.GetItemDef(this.dictContextButtons[param1]));
         }
         param1.on = false;
         param1.status = FlxButton.NORMAL;
      }
      
      private function CreateContext(param1:String) : ImgButton
      {
         var _loc2_:String = "";
         var _loc3_:int = GUIValues.GetInt("GUIEscMenu.UI.Zoom");
         if(_loc3_ == 2)
         {
            _loc2_ = DataHandler.m_strZoomPrefix;
         }
         var _loc4_:ImgButton;
         (_loc4_ = new ImgButton(_loc2_ + "btn_context_blank_on.png",_loc2_ + "btn_context_blank_up.png",_loc2_ + "btn_context_blank_on.png",_loc2_ + "btn_context_blank_on.png",0,0,this.ContextMode,true)).label = new FlxText(0,0,_loc4_.width,param1);
         _loc4_.label.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         _loc4_.labelOffset = new FlxPoint(_loc3_,_loc3_);
         _loc4_.scrollFactor.x = _loc4_.scrollFactor.y = 0;
         return _loc4_;
      }
      
      public function SkillPairCheck(param1:ItemInstance, param2:GUIInventorySlot) : void
      {
         var _loc10_:ItemInstance = null;
         var _loc11_:int = 0;
         var _loc3_:int = 96;
         var _loc4_:int = 91;
         var _loc5_:Vector.<int> = Vector.<int>([0,1,2,3,11]);
         var _loc6_:Vector.<int> = Vector.<int>([4,7,8,9,6]);
         var _loc7_:int = 0;
         var _loc8_:int = int(DataHandler.GetGameVars()["nSkillPoints"]);
         var _loc9_:Vector.<ItemInstance> = this.grpSkillSlot.SocketedItem().GetItems();
         for each(_loc10_ in _loc9_)
         {
            _loc7_ += _loc10_.Weight;
         }
         _loc9_ = this.grpTraitSlot.SocketedItem().GetItems();
         for each(_loc10_ in _loc9_)
         {
            _loc8_ += _loc10_.Weight;
         }
         this.txtSkillTotal.text = "使用: " + _loc7_;
         this.txtSkillTotal.text += "\n剩余: " + (_loc8_ - _loc7_);
         if(_loc7_ > _loc8_)
         {
            this.txtSkillTotal.color = GUIMessageWindow.COLOR_BAD;
         }
         else
         {
            this.txtSkillTotal.color = GUIMessageWindow.COLOR_GOOD;
         }
         if(param1 == null || param2 == null)
         {
            return;
         }
         if(param1.ItemDefinition.nGroupID == _loc3_)
         {
            if((_loc11_ = int(_loc5_.indexOf(param1.ItemDefinition.nSubgroupID))) >= 0)
            {
               _loc10_ = this.grpAvailSkillSlot.SocketedItem().GetItems(_loc4_,_loc6_[_loc11_])[0];
               if(param2 == this.grpAvailTraitSlot)
               {
                  _loc10_.Ghosted = false;
               }
               else
               {
                  _loc10_.Ghosted = true;
               }
            }
         }
         else if(param1.ItemDefinition.nGroupID == _loc4_)
         {
            if((_loc11_ = int(_loc6_.indexOf(param1.ItemDefinition.nSubgroupID))) >= 0)
            {
               _loc10_ = this.grpAvailTraitSlot.SocketedItem().GetItems(_loc3_,_loc5_[_loc11_])[0];
               if(param2 == this.grpAvailSkillSlot)
               {
                  _loc10_.Ghosted = false;
               }
               else
               {
                  _loc10_.Ghosted = true;
               }
            }
         }
      }
      
      public function TestItemInSocket(param1:ItemInstance, param2:GUIInventorySlot) : GUIFitItemResult
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:int = 0;
         var _loc3_:GUIFitItemResult = new GUIFitItemResult();
         _loc3_.m_grpSlot = param2;
         if(param2.AcceptsItem(param1))
         {
            if(param2.m_bAllowStacks)
            {
               if(param2.IsSlotDepthFree(param1.nSlotDepth))
               {
                  _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET;
               }
               else
               {
                  _loc4_ = param2.SocketedItem(param1.nSlotDepth);
                  if((_loc5_ = int(this.CanStackOnItem(param1,_loc4_))) > 0)
                  {
                     _loc3_.m_objItem = _loc4_;
                     if(_loc5_ >= param1.StackCount)
                     {
                        _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_STACK_FULL;
                     }
                     else
                     {
                        _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_STACK_PARTIAL;
                     }
                  }
                  else if(!_loc4_.ItemDefinition.bSocketLocked)
                  {
                     _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET_SWAP;
                  }
               }
            }
            else if(param1.StackCount <= 1)
            {
               if(param2.IsSlotDepthFree(param1.nSlotDepth))
               {
                  _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET;
               }
               else if(!(_loc4_ = param2.SocketedItem(param1.nSlotDepth)).ItemDefinition.bSocketLocked)
               {
                  _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET_SWAP;
               }
            }
            else if(param2.IsSlotDepthFree(param1.nSlotDepth))
            {
               _loc3_.m_nResult = GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL;
            }
         }
         return _loc3_;
      }
      
      public function CanStackOnItem(param1:ItemInstance, param2:ItemInstance) : uint
      {
         if(param1 == param2 || param1 == null || param2 == null)
         {
            return 0;
         }
         if(param2.ItemDefinition.nGroupID == param1.ItemDefinition.nGroupID && param2.ItemDefinition.m_nStackLimit > 1)
         {
            if(param1.ItemDefinition.m_bIgnoreSubGroupWhenStacking || param2.ItemDefinition.nSubgroupID == param1.ItemDefinition.nSubgroupID)
            {
               return param2.ItemDefinition.m_nStackLimit - param2.StackCount;
            }
         }
         return 0;
      }
      
      public function TestItemInCapBox(param1:ItemInstance, param2:ItemInstance, param3:FlxPoint, param4:Vector.<int> = null) : GUIFitItemResult
      {
         var _loc11_:int = 0;
         var _loc12_:ItemInstance = null;
         var _loc13_:Item = null;
         var _loc14_:int = 0;
         var _loc15_:int = 0;
         var _loc16_:Boolean = false;
         var _loc17_:Number = NaN;
         var _loc5_:GUIFitItemResult = new GUIFitItemResult();
         if(param4 == null)
         {
            param4 = Vector.<int>([GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_STACK_PARTIAL]);
         }
         if(param1 == param2)
         {
            return _loc5_;
         }
         if(param2.ItemDefinition.aCapacities.length <= 0)
         {
            return _loc5_;
         }
         var _loc6_:FlxPoint = param2.ItemDefinition.aCapacities[0];
         _loc6_ = new FlxPoint(_loc6_.x * GUIValues.m_fItemZoom,_loc6_.y * GUIValues.m_fItemZoom);
         var _loc7_:FlxPoint = new FlxPoint(Math.round(param3.x / GUIInventorySlot.CapacityPixel) * GUIInventorySlot.CapacityPixel,Math.round(param3.y / GUIInventorySlot.CapacityPixel) * GUIInventorySlot.CapacityPixel);
         var _loc8_:Number = GUIInventorySlot.CapacityPixel / 2.1;
         var _loc9_:Boolean = false;
         if(param3.x < -_loc8_ || param3.y < -_loc8_ || param3.x + param1.width > _loc6_.x + _loc8_ || param3.y + param1.height > _loc6_.y + _loc8_)
         {
            if(param3.x > _loc6_.x + _loc8_ || param3.y > _loc6_.y + _loc8_)
            {
               return _loc5_;
            }
            _loc9_ = true;
         }
         var _loc10_:Vector.<ItemInstance>;
         if((_loc10_ = this.TestItemOverlapsOtherItemInCapBox(param1,param2,_loc7_)).length > 0)
         {
            _loc11_ = 0;
            for each(_loc12_ in _loc10_)
            {
               _loc13_ = _loc12_.ItemDefinition;
               _loc14_ = _loc12_.ptOffset.x + _loc12_.width - param3.x;
               if(_loc11_ < _loc14_)
               {
                  _loc11_ = _loc14_;
               }
               if(_loc12_.Ghosted)
               {
                  (_loc5_ = new GUIFitItemResult()).m_nNextX = _loc11_;
               }
               else
               {
                  if(_loc13_.aCapacities.length > 0 && (_loc12_.bRigidContainer || _loc12_.vItems.length > 0) && _loc12_.AcceptsItem(param1) && param4.indexOf(GUIFitItemResult.RESULT_CAN_FIT_SUB) < 0)
                  {
                     (_loc5_ = this.FindSpaceInCapBox(param1,_loc12_)).m_nNextX = _loc11_;
                     if(param4.indexOf(_loc5_.m_nResult) < 0)
                     {
                        return _loc5_;
                     }
                  }
                  (_loc5_ = new GUIFitItemResult()).m_nNextX = _loc11_;
                  _loc15_ = int(this.CanStackOnItem(param1,_loc12_));
                  _loc16_ = true;
                  if(param4.indexOf(GUIFitItemResult.RESULT_CAN_FIT_SWAP) >= 0)
                  {
                     _loc16_ = false;
                  }
                  else if(param2 is ItemHardware && getQualifiedClassName(_loc12_) != getQualifiedClassName(param1))
                  {
                     _loc16_ = false;
                  }
                  if(param2.Slot != null && param2.Slot.nSlotIndex == 200 && param2.Slot.m_sprOwner == this.sprPlayer && param2.Slot.m_sprOwner.m_tilCurrentHex != null && param2.Slot.m_sprOwner.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
                  {
                     _loc17_ = _loc12_.GetTotalValue();
                     if(_loc12_ is ItemHardware)
                     {
                        _loc17_ = ItemHardware(_loc12_).GetTotalValueHidden();
                     }
                     if(this.sprPlayer.Money < _loc17_)
                     {
                        _loc16_ = false;
                     }
                  }
                  if(param2 is ItemHardware && _loc12_ is ItemSoftware && ItemHardware(param2).bSoftwareAccessible == false)
                  {
                     _loc16_ = false;
                  }
                  if(_loc15_ > 0)
                  {
                     _loc5_.m_objItem = _loc12_;
                     if(_loc15_ >= param1.StackCount)
                     {
                        _loc5_.m_nResult = GUIFitItemResult.RESULT_CAN_STACK_FULL;
                     }
                     else
                     {
                        _loc5_.m_nResult = GUIFitItemResult.RESULT_CAN_STACK_PARTIAL;
                     }
                  }
                  else if(_loc9_ == false && _loc16_ && param2.AcceptsItem(param1) && _loc10_.length == 1)
                  {
                     _loc5_.m_objItem = _loc12_;
                     _loc5_.m_ptPos = new FlxPoint(_loc7_.x,_loc7_.y);
                     _loc5_.m_nResult = GUIFitItemResult.RESULT_CAN_FIT_SWAP;
                  }
                  if(param4.indexOf(_loc5_.m_nResult) < 0)
                  {
                     return _loc5_;
                  }
                  (_loc5_ = new GUIFitItemResult()).m_nNextX = _loc11_;
               }
            }
         }
         else if(_loc9_ == false && param2.AcceptsItem(param1))
         {
            _loc5_.m_nResult = GUIFitItemResult.RESULT_CAN_FIT;
            _loc5_.m_objItem = param2;
            _loc5_.m_ptPos = new FlxPoint(_loc7_.x,_loc7_.y);
         }
         return _loc5_;
      }
      
      public function TestItemOverlapsOtherItemInCapBox(param1:ItemInstance, param2:ItemInstance, param3:FlxPoint) : Vector.<ItemInstance>
      {
         var _loc6_:FlxPoint = null;
         var _loc8_:ItemInstance = null;
         var _loc4_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         if(param2 == null || param1 == null)
         {
            return _loc4_;
         }
         var _loc5_:Number = GUIInventorySlot.CapacityPixel / 2.1;
         var _loc7_:FlxPoint = new FlxPoint();
         for each(_loc8_ in param2.vItems)
         {
            if(_loc8_ != param1)
            {
               _loc6_ = new FlxPoint(_loc8_.x - _loc8_.ptOffset.x,_loc8_.y - _loc8_.ptOffset.y);
               _loc7_.x = -_loc5_;
               _loc7_.y = -_loc5_;
               if(param3.x > _loc8_.ptOffset.x)
               {
                  _loc7_.x = _loc5_;
               }
               if(param3.y > _loc8_.ptOffset.y)
               {
                  _loc7_.y = _loc5_;
               }
               if(param1.overlapsAt(_loc6_.x + param3.x + _loc7_.x,_loc6_.y + param3.y + _loc7_.y,_loc8_))
               {
                  _loc4_.push(_loc8_);
               }
            }
         }
         return _loc4_;
      }
      
      public function FindSpaceInCapBox(param1:ItemInstance, param2:ItemInstance, param3:Vector.<int> = null) : GUIFitItemResult
      {
         var _loc7_:GUIFitItemResult = null;
         var _loc4_:GUIFitItemResult = new GUIFitItemResult();
         if(param2.ItemDefinition.aCapacities.length == 0)
         {
            return _loc4_;
         }
         var _loc5_:FlxPoint = param2.ItemDefinition.aCapacities[0];
         _loc5_ = new FlxPoint(_loc5_.x * GUIValues.m_fItemZoom,_loc5_.y * GUIValues.m_fItemZoom);
         var _loc6_:FlxPoint = new FlxPoint();
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:Boolean = false;
         var _loc11_:int = 0;
         while(_loc11_ <= _loc5_.y)
         {
            if(_loc4_.m_nResult > 0)
            {
               break;
            }
            _loc9_ = 0;
            while(_loc9_ <= _loc5_.x)
            {
               _loc10_ = false;
               _loc8_ = GUIInventorySlot.CapacityPixel;
               _loc6_.x = _loc9_;
               _loc6_.y = _loc11_;
               if((_loc4_ = this.TestItemInCapBox(param1,param2,_loc6_,param3)).m_nNextX > _loc8_)
               {
                  _loc8_ = _loc4_.m_nNextX;
               }
               if(_loc4_.m_nResult == 5 && _loc4_.m_objItem.m_objParentContainer != param2)
               {
                  _loc10_ = true;
               }
               else if(_loc4_.m_nResult >= 6 && _loc4_.m_objItem != param2)
               {
                  _loc10_ = true;
               }
               if(_loc10_)
               {
                  if(_loc7_ == null)
                  {
                     _loc7_ = _loc4_;
                  }
                  _loc4_ = new GUIFitItemResult();
               }
               else if(_loc4_.m_nResult > 0)
               {
                  return _loc4_;
               }
               _loc9_ += _loc8_;
            }
            _loc11_ += GUIInventorySlot.CapacityPixel;
         }
         if(_loc7_ != null)
         {
            return _loc7_;
         }
         return _loc4_;
      }
      
      public function ArrangeItemsInCapBox(param1:ItemInstance, param2:Vector.<int> = null, param3:Boolean = true) : void
      {
         var _loc4_:FlxPoint = null;
         var _loc9_:GUIFitItemResult = null;
         var _loc11_:ItemInstance = null;
         var _loc12_:ItemInstance = null;
         var _loc13_:GUIInventorySlot = null;
         if(param2 == null)
         {
            param2 = new Vector.<int>();
         }
         if(param2.indexOf(GUIFitItemResult.RESULT_CAN_FIT_SUB) < 0)
         {
            param2.push(GUIFitItemResult.RESULT_CAN_FIT_SUB);
         }
         if(param2.indexOf(GUIFitItemResult.RESULT_CAN_FIT_SWAP) < 0)
         {
            param2.push(GUIFitItemResult.RESULT_CAN_FIT_SWAP);
         }
         if(param2.indexOf(GUIFitItemResult.RESULT_CAN_STACK_PARTIAL) < 0)
         {
            param2.push(GUIFitItemResult.RESULT_CAN_STACK_PARTIAL);
         }
         var _loc5_:Vector.<FlxPoint> = new Vector.<FlxPoint>();
         var _loc6_:Vector.<GUIFitItemResult> = new Vector.<GUIFitItemResult>();
         if(param3)
         {
            param1.vItems = param1.vItems.sort(this.CompareItems);
         }
         var _loc7_:int = 0;
         while(_loc7_ < param1.vItems.length)
         {
            _loc11_ = param1.vItems[_loc7_];
            if(_loc4_ == null)
            {
               _loc4_ = new FlxPoint(_loc11_.x - _loc11_.ptOffset.x,_loc11_.y - _loc11_.ptOffset.y);
            }
            _loc5_.push(new FlxPoint(_loc11_.ptOffset.x,_loc11_.ptOffset.y));
            _loc11_.x = _loc4_.x - _loc11_.width;
            _loc11_.ptOffset.x = -_loc11_.width;
            _loc7_++;
         }
         var _loc8_:Vector.<ItemInstance> = param1.vItems.concat();
         var _loc10_:Boolean = false;
         _loc7_ = 0;
         while(_loc7_ < _loc8_.length)
         {
            _loc11_ = _loc8_[_loc7_];
            _loc9_ = this.FindSpaceInCapBox(_loc11_,param1,param2);
            _loc6_.push(_loc9_);
            if(_loc9_.m_nResult == GUIFitItemResult.RESULT_CANNOT_FIT)
            {
               _loc10_ = true;
               break;
            }
            switch(_loc9_.m_nResult)
            {
               case GUIFitItemResult.RESULT_CAN_FIT:
                  _loc11_.ptOffset.x = _loc9_.m_ptPos.x;
                  _loc11_.ptOffset.y = _loc9_.m_ptPos.y;
                  _loc11_.x = _loc4_.x + _loc11_.ptOffset.x;
                  _loc11_.y = _loc4_.y + _loc11_.ptOffset.y;
                  break;
               case GUIFitItemResult.RESULT_CAN_STACK_FULL:
                  if(_loc9_.m_objItem.Slot != null)
                  {
                     _loc12_ = (_loc13_ = _loc9_.m_objItem.Slot).RemoveItem(_loc9_.m_objItem,true);
                  }
                  else
                  {
                     _loc12_ = param1.RemoveItem(_loc9_.m_objItem,true);
                  }
                  if(_loc12_ != null)
                  {
                     _loc9_.m_objItem.StackItem(_loc11_);
                     _loc11_.ptOffset.x = _loc9_.m_objItem.ptOffset.x;
                     _loc11_.ptOffset.y = _loc9_.m_objItem.ptOffset.y;
                     _loc11_.x = _loc4_.x + _loc12_.ptOffset.x;
                     _loc11_.y = _loc4_.y + _loc12_.ptOffset.y;
                  }
                  break;
            }
            _loc7_++;
         }
         if(_loc10_)
         {
            _loc7_ = 0;
            while(_loc7_ < _loc8_.length)
            {
               (_loc11_ = _loc8_[_loc7_]).ptOffset.x = _loc5_[_loc7_].x;
               _loc11_.ptOffset.y = _loc5_[_loc7_].y;
               _loc11_.x = _loc4_.x + _loc11_.ptOffset.x;
               _loc11_.y = _loc4_.y + _loc11_.ptOffset.y;
               _loc7_++;
            }
         }
      }
      
      public function AddItemToCapBox(param1:ItemInstance, param2:ItemInstance, param3:Vector.<int> = null, param4:Boolean = false) : ItemInstance
      {
         var _loc6_:ItemInstance = null;
         var _loc7_:ItemInstance = null;
         if(param2 == null || param1 == null)
         {
            return param1;
         }
         var _loc5_:GUIFitItemResult = _loc5_ = this.FindSpaceInCapBox(param1,param2,param3);
         switch(_loc5_.m_nResult)
         {
            case GUIFitItemResult.RESULT_CAN_FIT:
               if(!param4)
               {
                  this.AddFloatItem(param1,_loc5_);
               }
               if(_loc5_.m_objItem.bSocketed)
               {
                  _loc5_.m_objItem.Slot.AddItem(param1,_loc5_.m_ptPos);
               }
               else
               {
                  _loc5_.m_objItem.AddItem(param1,_loc5_.m_ptPos);
               }
               return null;
            case GUIFitItemResult.RESULT_CAN_STACK_FULL:
               if(!param4)
               {
                  this.AddFloatItem(param1,_loc5_);
               }
               if(_loc5_.m_objItem.Slot != null)
               {
                  _loc5_.m_objItem.Slot.AddStackedItem(param1,_loc5_.m_objItem);
               }
               else
               {
                  _loc7_ = param2.RemoveItem(_loc5_.m_objItem,true);
                  _loc5_.m_objItem.StackItem(param1);
                  param2.AddItem(param1,param1.ptOffset);
               }
               return null;
            case GUIFitItemResult.RESULT_CAN_STACK_PARTIAL:
               if(!param4)
               {
                  this.AddFloatItem(param1,_loc5_);
               }
               if(_loc5_.m_objItem.Slot != null)
               {
                  _loc6_ = _loc5_.m_objItem.Slot.AddStackedItem(param1,_loc5_.m_objItem);
               }
               else
               {
                  _loc7_ = param2.RemoveItem(_loc5_.m_objItem,true);
                  _loc6_ = _loc5_.m_objItem.StackItem(param1);
                  param2.AddItem(param1,param1.ptOffset);
               }
               return this.AddItemToCapBox(_loc6_,param2,param3,param4);
            default:
               return param1;
         }
      }
      
      public function AddItemToSlot(param1:ItemInstance, param2:GUIInventorySlot, param3:Boolean = false) : ItemInstance
      {
         if(param1 == null || param2 == null)
         {
            return param1;
         }
         var _loc4_:GUIFitItemResult = this.TestItemInSocket(param1,param2);
         switch(_loc4_.m_nResult)
         {
            case GUIFitItemResult.RESULT_CAN_SOCKET:
               if(!param3)
               {
                  this.AddFloatItem(param1,_loc4_);
               }
               return param2.SocketItem(param1);
            case GUIFitItemResult.RESULT_CAN_STACK_FULL:
               if(!param3)
               {
                  this.AddFloatItem(param1,_loc4_);
               }
               return param2.SocketItem(param1);
            case GUIFitItemResult.RESULT_CAN_STACK_PARTIAL:
               if(!param3)
               {
                  this.AddFloatItem(param1,_loc4_);
               }
               return param2.SocketItem(param1);
            case GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL:
               if(!param3)
               {
                  this.AddFloatItem(param1,_loc4_);
               }
               return param2.SocketItem(param1);
            default:
               var _loc5_:ItemInstance;
               if((_loc5_ = param2.SocketedItem()) != null)
               {
                  return this.AddItemToCapBox(param1,_loc5_,null,param3);
               }
               return param1;
         }
      }
      
      private function ListUsableItems(param1:ItemInstance, param2:GUIInventorySlot) : Array
      {
         var _loc5_:ItemInstance = null;
         if(param2.AcceptsItem(param1) && param1.IsCharged(1,0,0))
         {
            return [param1];
         }
         var _loc3_:Vector.<ItemInstance> = param1.GetItems();
         _loc3_.push(param1);
         var _loc4_:Array = new Array();
         for each(_loc5_ in _loc3_)
         {
            if(param2.AcceptsItem(_loc5_) && _loc5_.IsCharged(1,0,0))
            {
               _loc4_.push(_loc5_);
            }
         }
         return _loc4_;
      }
      
      public function TestItemsFitGroundCamp(param1:Vector.<ItemInstance>) : Boolean
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         var _loc2_:Boolean = true;
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = true;
         var _loc3_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         for each(_loc4_ in param1)
         {
            (_loc5_ = _loc4_.Clone()).SetFormatIDs();
            _loc3_.push(_loc5_);
            while(_loc5_.StackCount > 1)
            {
               _loc3_.push(_loc5_.m_vStack.pop());
            }
         }
         for each(_loc4_ in _loc3_)
         {
            if((_loc6_ = this.AddItemToCapBox(_loc4_,this.sprPlayer.grpGroundSlot.SocketedItem(),null,true)) != null)
            {
               _loc6_ = this.AddItemToCapBox(_loc6_,this.sprPlayer.grpCampSlot.SocketedItem(),null,true);
            }
            if(_loc6_ != null)
            {
               _loc2_ = false;
               break;
            }
         }
         for each(_loc4_ in _loc3_)
         {
            if(_loc4_.Slot != null)
            {
               _loc4_.Slot.RemoveItem(_loc4_,false);
            }
         }
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = false;
         return _loc2_;
      }
      
      private function ShowAvailIngredients() : void
      {
         var _loc2_:int = 0;
         var _loc1_:int = 0;
         while(_loc1_ < this.m_vKnownRecipes.length)
         {
            this.grpCraftLayer.remove(this.m_vKnownRecipes[_loc1_]);
            _loc2_ = int(this.objPlayState.m_aMouseOverItems.indexOf(this.m_vKnownRecipes[_loc1_]));
            if(_loc2_ >= 0)
            {
               this.objPlayState.m_aMouseOverItems.splice(_loc2_,1);
            }
            _loc1_++;
         }
         this.m_vKnownRecipes.length = 0;
         this.grpAvailCraftItemsSlot.revive();
         this.btnCraftRecipes.on = false;
         this.btnCraftAvail.on = true;
         _loc2_ = int(this.vAvailCraftingPages.indexOf(this.grpAvailCraftItemsSlot.SocketedItem()));
         if(_loc2_ >= this.vAvailCraftingPages.length)
         {
            this.btnCraftNext.kill();
         }
         else
         {
            this.btnCraftNext.revive();
         }
         if(_loc2_ > 0)
         {
            this.btnCraftPrev.revive();
         }
         else
         {
            this.btnCraftPrev.kill();
         }
      }
      
      private function ShowQuickRecipes(param1:int = -1) : void
      {
         var _loc6_:int = 0;
         var _loc2_:int = 0;
         while(_loc2_ < this.m_vKnownRecipes.length)
         {
            this.grpCraftLayer.remove(this.m_vKnownRecipes[_loc2_]);
            if((_loc6_ = int(this.objPlayState.m_aMouseOverItems.indexOf(this.m_vKnownRecipes[_loc2_]))) >= 0)
            {
               this.objPlayState.m_aMouseOverItems.splice(_loc6_,1);
            }
            _loc2_++;
         }
         this.m_vKnownRecipes.length = 0;
         if(param1 < 0)
         {
            param1 = this.m_nRecipeFirstIndex;
         }
         this.m_nRecipeFirstIndex = param1;
         this.grpAvailCraftItemsSlot.kill();
         this.btnCraftAvail.on = false;
         this.btnCraftRecipes.on = true;
         if(param1 > 0)
         {
            this.btnCraftPrev.revive();
         }
         else
         {
            this.btnCraftPrev.kill();
         }
         this.btnCraftNext.kill();
         var _loc3_:FlxPoint = GUIValues.GetPoint("GUIInventory.QuickRecipe.size");
         var _loc4_:FlxPoint = GUIValues.GetPoint("GUIInventory.grpAvailCraftItemsSlot.Cap");
         var _loc5_:int = 0;
         _loc2_ = param1;
         while(_loc2_ < this.sprPlayer.m_vKnownRecipes.length)
         {
            this.AddQuickRecipe(this.sprPlayer.m_vKnownRecipes[_loc2_],_loc4_);
            _loc4_.y += this.m_vKnownRecipes[0].height + 1;
            _loc5_++;
            if(_loc5_ >= _loc3_.y)
            {
               break;
            }
            _loc2_++;
         }
         if(param1 < this.sprPlayer.m_vKnownRecipes.length - _loc3_.y)
         {
            this.btnCraftNext.revive();
         }
      }
      
      public function AddQuickRecipe(param1:Recipe, param2:FlxPoint) : void
      {
         this.ptTemp1 = GUIValues.GetPoint("GUIInventory.QuickRecipe.size",this.ptTemp1);
         var _loc3_:FlxText = new FlxText(0,0,this.ptTemp1.x,param1.m_strName);
         _loc3_.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         var _loc4_:BitmapData = new BitmapData(this.ptTemp1.x,_loc3_.height,true,4281545523);
         var _loc5_:ImgButton = new ImgButton("blank.png","blank.png","blank.png","blank.png",param2.x,param2.y,this.QuickRecipe,true);
         _loc5_.bmpImgDown = _loc5_.bmpImgOn = _loc5_.bmpImgOut = _loc5_.bmpImgOver = _loc4_;
         _loc5_.label = _loc3_;
         _loc5_.labelOffset = new FlxPoint(1,0);
         _loc5_.m_strPopUpText = param1.RecipeText;
         _loc5_.scrollFactor = new FlxPoint();
         _loc5_.UpdateImage();
         _loc5_.cameras = [FlxG.camera];
         this.grpCraftLayer.add(_loc5_);
         this.m_vKnownRecipes.push(_loc5_);
         this.objPlayState.m_aMouseOverItems.push(_loc5_);
      }
      
      public function ClearCrafting() : void
      {
         var _loc3_:ItemInstance = null;
         var _loc1_:ItemInstance = this.grpCraftingIngredientsSlot.SocketedItem();
         var _loc2_:ItemInstance = this.grpCraftingYieldSlot.SocketedItem();
         for each(_loc3_ in this.vAvailCraftingPages)
         {
            this.TransferItemContents(_loc3_);
         }
         this.TransferItemContents(_loc1_);
         for each(_loc3_ in this.vYieldPages)
         {
            this.TransferItemContents(_loc3_);
         }
         this.vCurrentRecipes.length = 0;
         this.objCurrentRecipe = null;
         this.btnYieldPrev.kill();
         this.btnYieldNext.kill();
      }
      
      private function QuickRecipe(param1:ImgButton) : void
      {
         var _loc5_:ItemInstance = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:Vector.<ItemInstance> = null;
         var _loc12_:ItemInstance = null;
         var _loc13_:Vector.<int> = null;
         var _loc14_:String = null;
         var _loc15_:Vector.<ItemInstance> = null;
         var _loc16_:ItemInstance = null;
         var _loc17_:ItemInstance = null;
         if(this.grpCraftingYieldSlot == null || this.grpCraftingIngredientsSlot == null)
         {
            return;
         }
         var _loc2_:int = int(this.m_vKnownRecipes.indexOf(param1));
         if(_loc2_ < 0)
         {
            param1.destroy();
            this.grpCraftLayer.remove(param1);
         }
         var _loc3_:ItemInstance = this.grpCraftingIngredientsSlot.SocketedItem();
         var _loc4_:ItemInstance = this.grpCraftingYieldSlot.SocketedItem();
         this.objCurrentRecipe = null;
         this.btnYieldPrev.kill();
         this.btnYieldNext.kill();
         this.TransferItemContents(_loc4_,null,null,this.vAvailCraftingPages);
         this.grpCraftingYieldSlot.UnSocketItem();
         for each(_loc5_ in this.vYieldPages)
         {
            this.TransferItemContents(_loc5_);
         }
         this.vCurrentRecipes.length = 0;
         this.grpCraftingYieldSlot.SocketItem(this.vYieldPages[0]);
         _loc4_ = this.vYieldPages[0];
         if(_loc2_ < 0)
         {
            return;
         }
         _loc2_ = this.m_nRecipeFirstIndex + _loc2_;
         var _loc6_:Recipe = this.sprPlayer.m_vKnownRecipes[_loc2_];
         var _loc7_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc8_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc9_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         for each(_loc10_ in this.vAvailCraftingPages)
         {
            _loc9_ = _loc9_.concat(_loc10_.vItems);
         }
         _loc9_ = _loc9_.concat(_loc3_.vItems);
         _loc11_ = new Vector.<ItemInstance>();
         for each(_loc12_ in _loc9_)
         {
            if(_loc12_.ItemDefinition.m_vProperties.indexOf(81) < 0)
            {
               _loc11_.push(_loc12_);
            }
         }
         _loc9_ = _loc11_;
         _loc11_ = null;
         _loc9_ = Recipe.PreSortItems(_loc9_);
         _loc13_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_SOCKET,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL]);
         _loc14_ = "";
         _loc15_ = _loc6_.Validate(_loc9_,false,_loc7_,_loc8_);
         for each(_loc2_ in _loc6_.m_vAlsoTry)
         {
            if(_loc15_.length > 0)
            {
               break;
            }
            if(_loc2_ > 0)
            {
               _loc6_ = DataHandler.GetRecipe(_loc2_);
               _loc8_.length = 0;
               _loc7_.length = 0;
               _loc15_ = _loc6_.Validate(_loc9_,false,_loc7_,_loc8_);
            }
         }
         if(_loc15_.length > 0)
         {
            this.objCurrentRecipe = _loc6_;
            _loc16_ = null;
            for each(_loc5_ in _loc7_)
            {
               if(_loc5_.Slot != null)
               {
                  _loc16_ = _loc5_.Slot.RemoveItem(_loc5_);
               }
               else
               {
                  _loc16_ = _loc5_.m_objParentContainer.RemoveItem(_loc5_);
               }
               if(_loc16_ != null)
               {
                  this.AddItemToCapBox(_loc16_,_loc3_,_loc13_);
               }
            }
            for each(_loc5_ in _loc8_)
            {
               if(_loc5_.Slot != null)
               {
                  _loc16_ = _loc5_.Slot.RemoveItem(_loc5_);
               }
               else
               {
                  _loc16_ = _loc5_.m_objParentContainer.RemoveItem(_loc5_);
               }
               if(_loc16_ != null)
               {
                  this.AddItemToCapBox(_loc16_,_loc3_,_loc13_);
               }
            }
            for each(_loc17_ in _loc15_)
            {
               _loc17_.nFormatID = _loc4_.ItemDefinition.aContentIDs[0];
               this.AddItemToCapBox(_loc17_,_loc4_,_loc13_);
               _loc17_.Ghosted = true;
               _loc17_.SetFormatIDs(this.grpAvailCraftItemsSlot.SocketedItem().ItemDefinition.aContentIDs[0],[]);
               _loc17_.CreateAppearance();
            }
            _loc14_ = this.SetYieldItems(0);
         }
         this.UpdateCraftButton(_loc14_);
      }
      
      public function GetSkills(param1:int = -1, param2:int = -1) : Vector.<ItemInstance>
      {
         var _loc3_:ItemInstance = this.grpSkillSlot.SocketedItem();
         if(_loc3_ == null)
         {
            return new Vector.<ItemInstance>();
         }
         return _loc3_.GetItems(param1,param2);
      }
      
      public function TransferItemContents(param1:ItemInstance, param2:Vector.<GUIInventorySlot> = null, param3:Vector.<String> = null, param4:Vector.<ItemInstance> = null) : Vector.<ItemInstance>
      {
         var _loc9_:ItemInstance = null;
         var _loc10_:ItemInstance = null;
         var _loc11_:Boolean = false;
         var _loc12_:String = null;
         var _loc13_:String = null;
         var _loc14_:GUIInventorySlot = null;
         var _loc15_:ItemInstance = null;
         if(param1 == null)
         {
            return new Vector.<ItemInstance>();
         }
         var _loc5_:uint = param1.vItems.length;
         var _loc6_:Vector.<ItemInstance> = param1.vItems.concat();
         var _loc7_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc8_:uint = 0;
         for(; _loc8_ < _loc5_; _loc8_++)
         {
            _loc9_ = _loc6_[_loc8_];
            if(param3 != null)
            {
               _loc11_ = false;
               _loc12_ = _loc9_.ItemDefinition.nGroupID + "." + _loc9_.ItemDefinition.nSubgroupID;
               for each(_loc13_ in param3)
               {
                  if(_loc12_ == _loc13_)
                  {
                     _loc11_ = true;
                     break;
                  }
               }
               if(_loc11_)
               {
                  continue;
               }
            }
            if(param1.Slot != null)
            {
               _loc10_ = param1.Slot.RemoveItem(_loc9_,true);
            }
            else
            {
               _loc10_ = param1.RemoveItem(_loc9_,true);
            }
            if(!_loc9_.Ghosted)
            {
               if(!(param2 == null && param4 == null))
               {
                  if(param2 != null)
                  {
                     for each(_loc14_ in param2)
                     {
                        if((_loc10_ = this.AddItemToCapBox(_loc10_,_loc14_.SocketedItem())) == null)
                        {
                           break;
                        }
                     }
                  }
                  if(_loc10_ != null)
                  {
                     if(param4 != null)
                     {
                        for each(_loc15_ in param4)
                        {
                           if((_loc10_ = this.AddItemToCapBox(_loc10_,_loc15_)) == null)
                           {
                              break;
                           }
                        }
                     }
                  }
                  if(_loc10_ != null)
                  {
                     _loc7_.push(_loc10_);
                  }
               }
            }
         }
         return _loc7_;
      }
      
      private function ReleaseItem(param1:ItemInstance, param2:GUIFitItemResult) : void
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc4_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:* = false;
         switch(param2.m_nResult)
         {
            case GUIFitItemResult.RESULT_CAN_FIT:
               if(param2.m_objItem.bSocketed)
               {
                  param2.m_objItem.grpItemPanelSlot.AddItem(param1,param2.m_ptPos);
               }
               else
               {
                  param2.m_objItem.AddItem(param1,param2.m_ptPos);
               }
               _loc3_ = param2.m_objItem.Slot;
               this.StopDragging();
               break;
            case GUIFitItemResult.RESULT_CAN_STACK_FULL:
               if(param2.m_objItem.Slot != null)
               {
                  param2.m_objItem.Slot.AddStackedItem(param1,param2.m_objItem);
               }
               else
               {
                  _loc6_ = (_loc5_ = param2.m_objItem.m_objParentContainer).RemoveItem(param2.m_objItem,true);
                  param2.m_objItem.StackItem(param1);
                  _loc5_.AddItem(param1,param1.ptOffset);
               }
               _loc3_ = param2.m_objItem.Slot;
               this.StopDragging();
               break;
            case GUIFitItemResult.RESULT_CAN_STACK_PARTIAL:
               if(param2.m_objItem.Slot != null)
               {
                  param1 = param2.m_objItem.Slot.AddStackedItem(param1,param2.m_objItem);
               }
               else
               {
                  _loc6_ = (_loc5_ = param2.m_objItem.m_objParentContainer).RemoveItem(param2.m_objItem,true);
                  param1 = param2.m_objItem.StackItem(param1);
                  _loc5_.AddItem(param1,param1.ptOffset);
               }
               this.StopDragging();
               this.StartDragging(param1,this.ptMouse);
               _loc3_ = param2.m_objItem.Slot;
               break;
            case GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL:
               _loc3_ = param2.m_grpSlot;
               if(_loc3_ == this.grpUseSlot || param1.ItemDefinition.vUseSlots.indexOf(_loc3_.nSlotIndex) >= 0)
               {
                  this.UseItemNew(this.sprPlayer,param1,_loc3_);
                  this.StopDragging();
                  for each(_loc7_ in this.vDegradeCleanup)
                  {
                     _loc8_ = _loc7_.ItemDefinition.m_vProperties.indexOf(82) >= 0;
                     _loc7_.ReplaceDegradedItem(1,_loc8_);
                  }
                  this.vDegradeCleanup.length = 0;
               }
               else
               {
                  _loc4_ = param2.m_grpSlot.SocketItem(param1);
                  this.StopDragging();
                  this.StartDragging(_loc4_,this.ptMouse);
               }
               break;
            case GUIFitItemResult.RESULT_CAN_SOCKET:
               _loc3_ = param2.m_grpSlot;
               if(_loc3_ == this.grpUseSlot || param1.ItemDefinition.vUseSlots.indexOf(_loc3_.nSlotIndex) >= 0)
               {
                  this.UseItemNew(this.sprPlayer,param1,_loc3_);
                  for each(_loc7_ in this.vDegradeCleanup)
                  {
                     _loc8_ = _loc7_.ItemDefinition.m_vProperties.indexOf(82) >= 0;
                     _loc7_.ReplaceDegradedItem(1,_loc8_);
                  }
                  this.vDegradeCleanup.length = 0;
               }
               else
               {
                  param2.m_grpSlot.SocketItem(param1);
               }
               this.StopDragging();
               break;
            case GUIFitItemResult.RESULT_CAN_SOCKET_SWAP:
               _loc3_ = param2.m_grpSlot;
               param2.m_objItem = param2.m_grpSlot.SocketedItem(param1.nSlotDepth);
               param2.m_grpSlot.UnSocketItem(false,param2.m_objItem);
               if(_loc3_ == this.grpUseSlot || param1.ItemDefinition.vUseSlots.indexOf(_loc3_.nSlotIndex) >= 0)
               {
                  this.UseItemNew(this.sprPlayer,param1,_loc3_);
                  for each(_loc7_ in this.vDegradeCleanup)
                  {
                     _loc8_ = _loc7_.ItemDefinition.m_vProperties.indexOf(82) >= 0;
                     _loc7_.ReplaceDegradedItem(1,_loc8_);
                  }
                  this.vDegradeCleanup.length = 0;
               }
               else
               {
                  param2.m_grpSlot.SocketItem(param1);
               }
               this.StopDragging();
               this.StartDragging(param2.m_objItem,this.ptMouse);
               param2.m_objItem.Alpha = 1;
               this.grpSlotSource = null;
               break;
            case GUIFitItemResult.RESULT_CAN_FIT_SWAP:
               _loc3_ = param2.m_objItem.Slot;
               if(_loc3_ != null)
               {
                  _loc3_.RemoveItem(param2.m_objItem,true);
                  _loc3_.AddItem(param1,param2.m_ptPos);
                  this.StopDragging();
                  this.StartDragging(param2.m_objItem,this.ptMouse);
                  this.grpSlotSource = null;
               }
               else if(param2.m_objItem.m_objParentContainer.RemoveItem(param2.m_objItem,true))
               {
                  param2.m_objItem.m_objParentContainer.AddItem(param1,param2.m_ptPos);
                  this.StopDragging();
                  this.StartDragging(param2.m_objItem,this.ptMouse);
                  this.grpSlotSource = null;
               }
               break;
            default:
               if(this.grpSlotSource == null)
               {
                  return;
               }
               if((_loc4_ = this.ReturnObject(param1)) == null)
               {
                  _loc3_ = this.grpSlotSource;
                  this.StopDragging();
               }
               else
               {
                  this.grpSlotSource = null;
                  this.StopDragging();
                  this.StartDragging(param1,this.ptMouse);
               }
               break;
         }
         if(_loc3_ == this.grpCraftingIngredientsSlot || this.grpSlotSource == this.grpCraftingIngredientsSlot)
         {
            this.CheckRecipe();
         }
         else if(_loc3_ == this.grpEncounterSlot)
         {
            this.UpdateResponseText();
         }
         else
         {
            this.SkillPairCheck(param1,_loc3_);
         }
      }
      
      private function UseItemNew(param1:Creature, param2:ItemInstance, param3:GUIInventorySlot) : void
      {
         var _loc5_:ItemInstance = null;
         this.ReturnObject(param2);
         var _loc4_:Array = this.ListUsableItems(param2,param3);
         var _loc6_:int = 0;
         var _loc7_:* = _loc4_;
         for each(_loc5_ in _loc7_)
         {
            param3.UseItem(_loc5_);
            _loc5_.UseDegrade();
            _loc5_.Discharge(1,0,0);
            if(_loc5_.fDurability <= 0)
            {
               this.vDegradeCleanup.push(_loc5_);
            }
         }
      }
      
      private function ReturnObject(param1:ItemInstance) : ItemInstance
      {
         var _loc2_:ItemInstance = null;
         if(param1 == null)
         {
            return null;
         }
         param1.Rotate(this.fRotationOrig);
         if(this.objParentItem != null && this.objParentItem != param1)
         {
            _loc2_ = this.AddItemToSlot(param1,this.grpSlotSource);
         }
         else if(this.grpSlotSource != null)
         {
            _loc2_ = this.grpSlotSource.SocketItem(param1);
         }
         return _loc2_;
      }
      
      public function AddGUIChild(param1:FlxBasic) : void
      {
         add(param1);
         param1.cameras = [FlxG.camera];
      }
      
      public function StartDragging(param1:ItemInstance, param2:FlxPoint) : void
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc4_:Boolean = false;
         if(param1 == null)
         {
            return;
         }
         this.objDragging = param1;
         this.objDragging.revive();
         for each(_loc3_ in this.vItemSlots)
         {
            _loc4_ = false;
            if(_loc3_ == this.grpUseSlot && this.ListUsableItems(param1,_loc3_).length > 0)
            {
               _loc4_ = true;
            }
            if(_loc3_ != this.grpAvailEncounterSlot && _loc3_ != this.sprPlayer.grpGroundSlot)
            {
               _loc3_.ShowOverlay(this.objDragging,_loc4_);
            }
         }
         if(this.sprPlayer.m_tilCurrentHex != null && this.sprPlayer.m_tilCurrentHex.m_nBarterTile == BarterHex.BARTER_SELL && this.grpSlotSource != this.sprPlayer.grpGroundSlot)
         {
            this.sprPlayer.m_tilCurrentHex.GroundObject.SetFormatIDs(-1,[35]);
         }
         this.AddGUIChild(this.objDragging);
         this.ptDragOrigin = new FlxPoint(this.objDragging.x,this.objDragging.y);
         this.ptDragOffset = new FlxPoint(param2.x - this.objDragging.x,param2.y - this.objDragging.y);
         if(this.ptDragOffset.x > this.objDragging.width || this.ptDragOffset.y > this.objDragging.height)
         {
            this.ptDragOffset.x = this.objDragging.width / 2;
            this.ptDragOffset.y = this.objDragging.height / 2;
         }
         this.TintItem(this.objDragging);
         if(this.grpSlotSource == this.grpCraftingIngredientsSlot || this.grpSlotSource == this.grpCraftingYieldSlot)
         {
            this.CheckRecipe();
         }
         FlxG.loadSound(DataHandler.GetSound(this.objDragging.ItemDefinition.m_vSounds[0]),GUIEscMenu.m_fSoundVolume,false,true,true);
      }
      
      private function StopDragging() : void
      {
         var _loc1_:GUIInventorySlot = null;
         for each(_loc1_ in this.vItemSlots)
         {
            _loc1_.HideOverlay();
         }
         if(this.sprPlayer.m_tilCurrentHex != null && this.sprPlayer.m_tilCurrentHex.m_nBarterTile == BarterHex.BARTER_SELL && this.grpSlotSource != this.sprPlayer.grpGroundSlot)
         {
            this.sprPlayer.m_tilCurrentHex.GroundObject.SetFormatIDs();
         }
         remove(this.objDragging);
         if(this.objDragging != null)
         {
            FlxG.loadSound(DataHandler.GetSound(this.objDragging.ItemDefinition.m_vSounds[1]),GUIEscMenu.m_fSoundVolume,false,true,true);
         }
         this.objDragging = null;
         this.TintItem(null);
      }
      
      private function TintItem(param1:ItemInstance) : void
      {
         if(param1 == null)
         {
            remove(this.sprTint);
         }
         else
         {
            this.sprTint.x = param1.x;
            this.sprTint.y = param1.y;
            if(this.sprTint.scale.x != param1.width)
            {
               this.sprTint.scale.x = param1.width;
            }
            if(this.sprTint.scale.y != param1.height)
            {
               this.sprTint.scale.y = param1.height;
            }
            this.sprTint.color = 255;
            this.AddGUIChild(this.sprTint);
         }
      }
      
      private function GetItemUnderPoint(param1:FlxPoint, param2:Vector.<GUIInventorySlot>) : ItemInstance
      {
         var _loc3_:GUIInventorySlot = null;
         var _loc4_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         for each(_loc3_ in param2)
         {
            _loc4_ = _loc3_.SocketedItem();
            if(!(!_loc3_.alive || _loc4_ == null || !_loc4_.alive))
            {
               if(_loc4_.pixelsOverlapPoint(param1))
               {
                  return _loc4_;
               }
               if(_loc3_.sprCap != null && _loc3_.sprCap.visible && _loc3_.sprCap.overlapsPoint(param1,false))
               {
                  for each(_loc5_ in _loc4_.vItems)
                  {
                     if(_loc5_.overlapsPoint(param1))
                     {
                        return _loc5_;
                     }
                  }
               }
            }
         }
         return null;
      }
      
      public function CompareItems(param1:ItemInstance, param2:ItemInstance) : int
      {
         if(param1.m_nSize > param2.m_nSize)
         {
            return -1;
         }
         if(param1.m_nSize < param2.m_nSize)
         {
            return 1;
         }
         return 0;
      }
      
      public function UpdateSkillItems(param1:Vector.<ItemInstance>, param2:Vector.<ItemInstance>) : void
      {
         var _loc5_:ItemInstance = null;
         var _loc6_:ItemInstance = null;
         this.grpSkillSlot.SocketItem(DataHandler.GetItem("93.9"));
         this.grpTraitSlot.SocketItem(DataHandler.GetItem("93.10"));
         var _loc3_:ItemInstance = this.grpAvailTraitSlot.SocketedItem();
         if(_loc3_ == null)
         {
            _loc3_ = DataHandler.GetItem("93.2");
            this.grpAvailTraitSlot.SocketItem(_loc3_);
         }
         else
         {
            this.TransferItemContents(_loc3_);
         }
         var _loc4_:ItemInstance;
         if((_loc4_ = this.grpAvailSkillSlot.SocketedItem()) == null)
         {
            _loc4_ = DataHandler.GetItem("93.3");
            this.grpAvailSkillSlot.SocketItem(_loc4_);
         }
         else
         {
            this.TransferItemContents(_loc4_);
         }
         for each(_loc5_ in param1)
         {
            this.AddItemToCapBox(_loc5_,_loc4_,null,true);
         }
         for each(_loc6_ in param2)
         {
            this.AddItemToCapBox(_loc6_,_loc3_,null,true);
            _loc6_.ItemDefinition.bSocketLocked = false;
         }
         this.ArrangeItemsInCapBox(_loc4_);
         this.ArrangeItemsInCapBox(_loc3_);
      }
      
      public function UpdateCraftingItems(param1:Boolean) : void
      {
         var _loc2_:Vector.<ItemInstance> = null;
         var _loc3_:Vector.<ItemInstance> = null;
         var _loc4_:ItemInstance = null;
         var _loc5_:int = 0;
         var _loc6_:uint = 0;
         var _loc7_:Vector.<int> = null;
         var _loc8_:ItemInstance = null;
         var _loc9_:ItemInstance = null;
         var _loc10_:int = 0;
         var _loc11_:ItemInstance = null;
         var _loc12_:ItemInstance = null;
         var _loc13_:ItemInstance = null;
         if(this.grpCraftingIngredientsSlot.SocketedItem() == null)
         {
            this.grpCraftingIngredientsSlot.SocketItem(DataHandler.GetItem("93.6"));
         }
         if(this.grpCraftingYieldSlot.SocketedItem() == null)
         {
            this.grpCraftingYieldSlot.SocketItem(this.vYieldPages[0]);
         }
         if(this.grpAvailCraftItemsSlot.SocketedItem() == null)
         {
            this.grpAvailCraftItemsSlot.SocketItem(this.vAvailCraftingPages[0]);
         }
         if(this.grpCraftLayer.alive && this.sprPlayer.m_tilCurrentHex != null && this.sprPlayer.m_tilCurrentHex.m_nBarterTile == BarterHex.BARTER_NONE)
         {
            this.objPlayState.ChangeCursor(3);
            if(this.objDragging != null && this.objDragging.m_objProxy != null)
            {
               this.StopDragging();
            }
            this.ClearCrafting();
            _loc2_ = new Vector.<ItemInstance>();
            _loc3_ = this.sprPlayer.GetItems(true,true,true,false);
            _loc3_ = _loc3_.concat(this.sprPlayer.grpGroundSlot.SocketedItem().GetItems());
            _loc3_ = _loc3_.concat(this.sprPlayer.GetCamp().GetItems());
            _loc3_ = _loc3_.concat(this.grpSkillSlot.SocketedItem().GetItems());
            _loc5_ = PlayState.m_objInstance.grpWeatherNode.GetTimeOfDay(PlayState.m_objInstance.objDate);
            if(PlayState.m_objInstance.grpWeatherNode.objWeatherLast.bOvercast == false && _loc5_ > 0 && _loc5_ < 3)
            {
               _loc3_.push(DataHandler.GetItem("25.5"));
            }
            _loc6_ = 0;
            while(_loc6_ < _loc3_.length)
            {
               if(_loc3_[_loc6_].ItemDefinition.m_vProperties.indexOf(76) < 0)
               {
                  if(_loc3_[_loc6_].vItems.length > 0)
                  {
                     _loc3_.concat(_loc3_[_loc6_].vItems);
                  }
                  (_loc9_ = _loc3_[_loc6_].Clone(false)).SetFormatIDs(this.vAvailCraftingPages[0].ItemDefinition.aContentIDs[0],[]);
                  _loc9_.vItems.length = 0;
                  _loc9_.CreateAppearance();
                  _loc9_.m_objProxy = _loc3_[_loc6_];
                  if(_loc9_.m_objProxy.vItems.length > 0 || _loc9_.m_objProxy.m_bEquipped)
                  {
                     _loc9_.SetBracket();
                  }
                  _loc10_ = 0;
                  while(_loc10_ < _loc9_.m_vStack.length)
                  {
                     (_loc11_ = _loc9_.m_vStack[_loc10_]).vItems.length = 0;
                     _loc11_.CreateAppearance();
                     _loc11_.m_objProxy = _loc3_[_loc6_].m_vStack[_loc10_];
                     if(_loc11_.m_objProxy.vItems.length > 0 || _loc11_.m_objProxy.m_bEquipped)
                     {
                        _loc11_.SetBracket();
                     }
                     _loc10_++;
                  }
                  _loc2_.push(_loc9_);
               }
               _loc6_++;
            }
            _loc2_ = _loc2_.sort(this.CompareItems);
            _loc7_ = Vector.<int>([GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CAN_STACK_PARTIAL,GUIFitItemResult.RESULT_CAN_FIT_SUB]);
            for each(_loc8_ in _loc2_)
            {
               _loc12_ = _loc8_;
               for each(_loc13_ in this.vAvailCraftingPages)
               {
                  if((_loc12_ = this.AddItemToCapBox(_loc12_,_loc13_,_loc7_)) == null)
                  {
                     break;
                  }
               }
            }
            this.btnCraftNext.revive();
            this.btnCraftPrev.kill();
            this.ShowAvailIngredients();
            this.objPlayState.ChangeCursor(this.m_nMouseMode + int(this.bMouseWholeStack));
         }
         this.UpdateCraftButton();
      }
      
      public function UpdateGroundItems(param1:Creature, param2:FlxHexTile) : void
      {
         var _loc3_:Boolean = false;
         if(param1 is AICreature)
         {
            if(param2 == this.sprPlayer.m_tilCurrentHex)
            {
               param1.grpGroundSlot = this.sprPlayer.grpGroundSlot;
               _loc3_ = true;
            }
            else
            {
               param1.grpGroundSlot = AICreature(param1).grpGroundSlotOrig;
            }
         }
         var _loc4_:ItemInstance;
         if((_loc4_ = param1.grpGroundSlot.SocketedItem()) != param2.GroundObject || _loc4_.Slot == null)
         {
            if(_loc4_ != null)
            {
               param1.grpGroundSlot.UnSocketItem(true);
            }
            if(param2.GroundObject.m_fZoom != GUIValues.m_fItemZoom)
            {
               param2.GroundObject.SetRes(GUIValues.m_fItemZoom);
            }
            param1.grpGroundSlot.SocketItem(param2.GroundObject);
         }
         else if(_loc3_)
         {
            param1.grpGroundSlot.AddConditionsToCreature(param2.GroundObject,param1);
         }
      }
      
      public function UpdateCampItems(param1:Creature, param2:FlxHexTile) : void
      {
         var _loc7_:ItemCamp = null;
         var _loc3_:ItemInstance = this.grpAvailCampSlot.SocketedItem();
         if(_loc3_ == null)
         {
            _loc3_ = DataHandler.GetItem("93.5");
            this.grpAvailCampSlot.SocketItem(_loc3_);
         }
         else
         {
            this.TransferItemContents(_loc3_);
         }
         var _loc4_:ItemInstance = param1.grpCampSlot.SocketedItem();
         var _loc5_:ItemCamp = param1.GetCamp(param2);
         if(_loc4_ != _loc5_)
         {
            if(_loc4_ != null)
            {
               param1.grpCampSlot.UnSocketItem(true);
               param1.RemoveCondition(ItemCamp(_loc4_).Condition);
            }
            param1.grpCampSlot.SocketItem(_loc5_);
            param1.AddCondition(ItemCamp(_loc5_).Condition,false);
            this.UpdateCampStats(_loc5_);
         }
         var _loc6_:Vector.<ItemCamp> = param1.GetCampList(param2);
         for each(_loc7_ in _loc6_)
         {
            if(_loc7_ != _loc5_)
            {
               this.AddItemToCapBox(_loc7_,_loc3_);
            }
         }
      }
      
      public function UpdateCampStats(param1:ItemCamp) : void
      {
         this.grpCampConcealment.UpdateBars(1 - param1.m_fVisibility,new Array(0,0.25,0.5,0.75,1));
         this.grpCampAlertness.UpdateBars(param1.m_fSleepAwareness,new Array(0,0.25,0.5,0.75,1));
         this.grpCampHealRate.UpdateBars(12 * param1.m_fHealPerHourMod,new Array(0,0.25,0.5,0.75,1));
         this.grpCampShelter.UpdateBars(-param1.WetTempAdjustMod / this.sprPlayer.fWetTempAdjust,new Array(0,0.25,0.5,0.75,1));
         this.grpCampSleepQuality.UpdateBars(1 + param1.fSleepQuality,new Array(0,0.25,0.5,0.75,1));
      }
      
      public function UpdateHealthStats() : void
      {
         this.grpHealthBlood.UpdateBars(this.sprPlayer.m_fBloodLeft / this.sprPlayer.m_fBloodLeftBase,new Array(0,0.25,0.5,0.75,1));
         this.grpHealthInfection.UpdateBars(this.sprPlayer.m_fImmuneLeft / this.sprPlayer.m_fImmuneLeftBase,new Array(0,0.25,0.5,0.75,1));
         this.grpHealthPain.UpdateBars(this.sprPlayer.m_fPainLeft / this.sprPlayer.m_fPainLeftBase,new Array(0,0.25,0.5,0.75,1));
      }
      
      public function UpdateCombatItems() : void
      {
         var _loc3_:ItemInstance = null;
         var _loc1_:ItemInstance = this.grpEncounterSlot.SocketedItem();
         this.TransferItemContents(_loc1_);
         var _loc2_:ItemInstance = this.grpAvailEncounterSlot.SocketedItem();
         this.TransferItemContents(_loc2_);
         if(this.grpBattleLayer.CurrentCombatPair == null)
         {
            return;
         }
         for each(_loc3_ in this.grpBattleLayer.CurrentCombatPair.vAllItems)
         {
            if(_loc3_.Ghosted)
            {
               _loc3_.Ghosted = false;
            }
            if(_loc3_.m_sprIMG == null)
            {
               _loc3_.CreateAppearance();
            }
         }
         _loc2_.vItems = this.grpBattleLayer.CurrentCombatPair.vAllItems.concat();
         this.TransferItemContents(_loc2_,Vector.<GUIInventorySlot>([this.grpAvailEncounterSlot]));
         this.ArrangeItemsInCapBox(_loc2_,null,false);
      }
      
      public function UpdateEncounterItems(param1:Encounter) : void
      {
         var _loc8_:ItemInstance = null;
         var _loc9_:Encounter = null;
         var _loc10_:AICreature = null;
         var _loc11_:BitmapData = null;
         var _loc12_:Point = null;
         var _loc13_:int = 0;
         var _loc14_:int = 0;
         var _loc15_:Creature = null;
         var _loc16_:Object = null;
         var _loc17_:ItemInstance = null;
         var _loc18_:PlayerResponse = null;
         var _loc19_:Boolean = false;
         var _loc20_:* = false;
         var _loc21_:Vector.<ItemInstance> = null;
         var _loc22_:ItemInstance = null;
         var _loc23_:ItemInstance = null;
         var _loc24_:int = 0;
         var _loc25_:uint = 0;
         var _loc26_:int = 0;
         var _loc27_:ItemInstance = null;
         var _loc28_:int = 0;
         var _loc29_:ItemInstance = null;
         var _loc30_:Vector.<int> = null;
         var _loc31_:ItemInstance = null;
         this.m_nState = STATE_NORMAL;
         this.objEncounter = param1;
         var _loc2_:uint = this.sprPlayer.m_tilCurrentHex.m_nBarterTile;
         this.sprPlayer.m_tilCurrentHex.m_nBarterTile = BarterHex.BARTER_NONE;
         this.TransferItemContents(this.grpAvailEncounterSlot.SocketedItem());
         this.grpAvailEncounterSlot.UnSocketItem(true);
         this.TransferItemContents(this.grpEncounterSlot.SocketedItem());
         this.grpEncounterSlot.UnSocketItem(true);
         var _loc3_:ItemInstance = DataHandler.GetItem("93.1");
         this.grpEncounterSlot.SocketItem(_loc3_);
         var _loc4_:ItemInstance = DataHandler.GetItem("93.0");
         this.sprEncounter.Zoom(1);
         if(param1.m_strImg == "EncBlank.png")
         {
            if(param1.m_nType == Encounter.TYPE_COMBAT)
            {
               this.sprEncounter.pixels = DataHandler.GetImage("blank.png");
            }
            else
            {
               this.sprEncounter.pixels = MapUtils.tmapHexes.GetImage(this.sprPlayer.m_tilCurrentHex.index);
            }
         }
         else if(param1.m_strImg == "EncConvo.png")
         {
            if((_loc10_ = DM.m_grpCreature) != null)
            {
               this.sprEncounter.pixels = _loc10_.GetCreatureImage(true);
               if(_loc10_.HasCondition(504) == false)
               {
                  _loc10_.AddCondition(_loc10_.GetCondition(529));
               }
            }
            else
            {
               this.sprEncounter.pixels = this.sprPlayer.GetCreatureImage(false);
            }
         }
         else if(param1.m_strImg == "EncAmbush.png")
         {
            _loc11_ = MapUtils.tmapHexes.GetImage(this.sprPlayer.m_tilCurrentHex.index);
            this.sprEncounter.pixels = new BitmapData(GUIValues.GetPoint("GUIInventory.sprEncounter.size").x,GUIValues.GetPoint("GUIInventory.sprEncounter.size").y,true,0);
            _loc12_ = new Point((this.sprEncounter.pixels.width - _loc11_.width) / 2,(this.sprEncounter.pixels.height - _loc11_.height * 1.5) / 2);
            this.sprEncounter.pixels.copyPixels(_loc11_,_loc11_.rect,_loc12_,null,null,true);
            _loc13_ = 0;
            _loc14_ = int(PlayState.m_objInstance.tilCurrentHex.m_vOccupants.length);
            for each(_loc15_ in PlayState.m_objInstance.tilCurrentHex.m_vOccupants)
            {
               if(!(_loc15_ is Player))
               {
                  _loc11_ = _loc15_.GetCreatureImage(PlayState.m_objInstance.sprPlayer.PlayerCanSee(_loc15_));
                  _loc12_.x = (this.sprEncounter.pixels.width - _loc11_.width) / 2;
                  _loc12_.y = (this.sprEncounter.pixels.height - _loc11_.height) / 2;
                  _loc12_.x += (_loc14_ - _loc13_) * Math.pow(-1,_loc14_ - _loc13_) * 10;
                  _loc12_.y -= (_loc14_ - _loc13_) * 7;
                  _loc13_++;
                  this.sprEncounter.pixels.copyPixels(_loc11_,_loc11_.rect,_loc12_,null,null,true);
               }
            }
         }
         else
         {
            this.sprEncounter.pixels = DataHandler.GetImage(param1.m_strImg);
         }
         _loc4_.ItemDefinition.strName = "";
         _loc4_.ItemDefinition.strDesc = param1.m_strName;
         this.sprEncounter.Zoom(GUIValues.GetInt("GUIInventory.grpEncounterSlot.zoom"));
         GUIValues.SetPosition(this.sprEncounter,"GUIInventory.grpEncounterSlot");
         this.sprEncounter.x += -this.sprEncounter.pixels.width / 2;
         this.sprEncounter.y += -this.sprEncounter.pixels.height / 2;
         this.grpAvailEncounterSlot.SocketItem(_loc4_);
         if(param1.m_nType == Encounter.TYPE_COMBAT && this.sprPlayer.Alive)
         {
            this.grpBattleLayer.SetBattle(this.sprPlayer.m_tilCurrentHex);
         }
         if(PlayState.m_objInstance.m_nGameState != 2)
         {
            for(_loc16_ in param1.m_dictUsedItems)
            {
               _loc17_ = ItemInstance(_loc16_);
               if(param1.m_vRemoveTreasure.indexOf(_loc17_) < 0)
               {
                  _loc18_ = param1.m_dictUsedItems[_loc16_];
                  _loc19_ = _loc17_.m_objProxy.m_bEquipped;
                  _loc17_.m_objProxy.m_bEquipped = true;
                  _loc17_.m_objProxy.Discharge(_loc18_.m_nChargeUses,_loc18_.m_fChargeHours,_loc18_.m_nChargeHexes);
                  if(_loc18_.m_nChargeUses > 0)
                  {
                     _loc17_.m_objProxy.UseDegrade();
                  }
                  _loc17_.m_objProxy.EquipDegrade(_loc18_.m_fChargeHours);
                  _loc17_.m_objProxy.m_bEquipped = _loc19_;
                  _loc20_ = _loc17_.ItemDefinition.m_vProperties.indexOf(82) >= 0;
                  if(_loc17_.m_objProxy.fDurability <= 0)
                  {
                     _loc17_.m_objProxy.ReplaceDegradedItem(1,_loc20_);
                  }
               }
            }
         }
         if(param1.m_vRemoveTreasure.length == 0)
         {
            param1.m_vRemoveTreasure = DataHandler.GetTreasure(param1.m_nRemoveTreasureID).GenerateTreasure();
         }
         if(PlayState.m_objInstance.m_nGameState != 2 && param1.m_vRemoveTreasure.length > 0)
         {
            for each(_loc22_ in param1.m_vRemoveTreasure)
            {
               _loc17_ = null;
               if(_loc22_.m_objProxy != null)
               {
                  _loc17_ = _loc22_.m_objProxy;
               }
               else if(_loc22_ is ItemCamp)
               {
                  if((_loc23_ = this.sprPlayer.grpCampSlot.SocketedItem()) == null || _loc23_.IDString != _loc22_.IDString)
                  {
                     if((_loc21_ = this.grpAvailCampSlot.SocketedItem().GetItems(_loc22_.ItemDefinition.nGroupID,_loc22_.ItemDefinition.nSubgroupID)).length > 0)
                     {
                        _loc23_ = _loc21_[0];
                     }
                  }
                  if(_loc23_ != null && _loc23_.IDString == _loc22_.IDString)
                  {
                     _loc17_ = _loc23_;
                     this.sprPlayer.RememberCamp(this.sprPlayer.m_tilCurrentHex,ItemCamp(_loc23_),true);
                     if((_loc24_ = int(this.sprPlayer.m_tilCurrentHex.m_vCampItems.indexOf(_loc23_))) >= 0)
                     {
                        this.sprPlayer.m_tilCurrentHex.m_vCampItems.splice(_loc24_,1);
                     }
                     if(this.sprPlayer.m_tilCurrentHex.m_vCampItems.length == 0)
                     {
                        this.sprPlayer.m_tilCurrentHex.m_vCampItems = this.sprPlayer.m_tilCurrentHex.m_vCampItems.concat(DataHandler.GetTreasure(this.sprPlayer.m_tilCurrentHex.nDefaultCampID).GenerateTreasure());
                     }
                  }
               }
               else
               {
                  if((_loc21_ = this.sprPlayer.GetItems(true,true,true,true,_loc22_.ItemDefinition.nGroupID,_loc22_.ItemDefinition.nSubgroupID)).length == 0)
                  {
                     _loc21_ = this.sprPlayer.grpGroundSlot.SocketedItem().GetItems(_loc22_.ItemDefinition.nGroupID,_loc22_.ItemDefinition.nSubgroupID);
                  }
                  if(_loc21_.length == 0)
                  {
                     if((_loc21_ = this.sprPlayer.grpCampSlot.SocketedItem().GetItems(_loc22_.ItemDefinition.nGroupID,_loc22_.ItemDefinition.nSubgroupID)).length == 0)
                     {
                        _loc21_ = this.grpAvailCampSlot.SocketedItem().GetItems(_loc22_.ItemDefinition.nGroupID,_loc22_.ItemDefinition.nSubgroupID);
                     }
                  }
                  if(_loc21_.length > 0)
                  {
                     _loc17_ = _loc21_[0];
                  }
               }
               if(_loc17_ != null)
               {
                  if(_loc17_.bSocketed)
                  {
                     _loc17_.Slot.UnSocketItem(true,_loc17_,false);
                     if(_loc17_ is ItemCamp)
                     {
                        this.sprPlayer.RemoveCondition(ItemCamp(_loc17_).Condition);
                        this.UpdateCampItems(this.sprPlayer,this.sprPlayer.m_tilCurrentHex);
                     }
                  }
                  else
                  {
                     _loc17_.Slot.RemoveItem(_loc17_);
                  }
               }
            }
         }
         if(param1.m_nID != DM.m_nNullEnc)
         {
            _loc25_ = 0;
            for each(_loc18_ in param1.m_aResponses)
            {
               _loc26_ = 0;
               while(_loc26_ < _loc18_.m_aItemAmounts.length)
               {
                  if(_loc18_.m_aItemAmounts[_loc26_] > 0)
                  {
                     _loc25_++;
                     break;
                  }
                  _loc26_++;
               }
               if(_loc25_ > 0)
               {
                  break;
               }
               _loc26_ = 0;
               while(_loc26_ < _loc18_.m_aIngredientAmounts.length)
               {
                  if(_loc18_.m_aIngredientAmounts[_loc26_] > 0)
                  {
                     _loc25_++;
                     break;
                  }
                  _loc26_++;
               }
               if(_loc25_ > 0)
               {
                  break;
               }
            }
            if(_loc25_ == 0 && param1.m_nType != Encounter.TYPE_COMBAT)
            {
               this.m_nState = STATE_ENCOUNTER_EXCLUSIVE;
            }
            else if(param1.m_nType == Encounter.TYPE_COMBAT)
            {
               if(this.grpBattleLayer.CurrentCombatPair != null && (this.sprPlayer.HasCondition(137) || this.grpBattleLayer.CurrentCombatPair.nLongestAttackRange < this.grpBattleLayer.CurrentCombatPair.nShortestRange))
               {
                  this.m_nState = STATE_COMBAT_TREASURE;
               }
               else
               {
                  this.m_nState = STATE_COMBAT;
               }
            }
            else
            {
               this.m_nState = STATE_ENCOUNTER;
            }
         }
         var _loc5_:Vector.<ItemInstance> = DataHandler.GetTreasure(param1.m_nItemsID).GenerateTreasure();
         var _loc6_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         var _loc7_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         _loc24_ = 0;
         _loc5_ = (_loc5_ = (_loc5_ = (_loc5_ = _loc5_.concat(this.sprPlayer.GetItems(true,true,true,false))).concat(this.sprPlayer.grpGroundSlot.SocketedItem().GetItems())).concat(this.sprPlayer.GetCamp().GetItems())).concat(this.grpSkillSlot.SocketedItem().GetItems());
         for each(_loc18_ in param1.m_aResponses)
         {
            _loc6_.length = 0;
            if(!(!(_loc9_ = DataHandler.GetEncounter(_loc18_.m_nOutput)).PreconditionsOK(this.sprPlayer) || !_loc18_.Validate(this.sprPlayer,_loc5_,_loc6_)))
            {
               _loc26_ = 0;
               while(_loc26_ < _loc6_.length)
               {
                  if(_loc7_.indexOf(_loc6_[_loc26_]) < 0)
                  {
                     (_loc27_ = _loc6_[_loc26_].Clone()).SetFormatIDs(_loc4_.ItemDefinition.aContentIDs[0],[]);
                     _loc27_.strDesc = DataHandler.GetEncounter(_loc18_.m_nOutput).m_strName;
                     if(_loc6_[_loc26_].ItemDefinition.m_vChargeProfiles.length > 0)
                     {
                        _loc27_.strDesc += "\n\n电量: " + _loc6_[_loc26_].GetRemainingChargeInfo();
                     }
                     _loc27_.m_objProxy = _loc6_[_loc26_];
                     _loc27_.Rotate(0);
                     _loc28_ = 0;
                     while(_loc28_ < _loc27_.m_vStack.length)
                     {
                        (_loc29_ = _loc27_.m_vStack[_loc28_]).strDesc = _loc27_.strDesc;
                        _loc29_.m_objProxy = _loc6_[_loc26_].m_vStack[_loc28_];
                        _loc28_++;
                     }
                     _loc27_.CreateAppearance();
                     this.AddItemToCapBox(_loc27_,_loc4_);
                     _loc7_.push(_loc6_[_loc26_]);
                  }
                  _loc26_++;
               }
            }
         }
         _loc7_.length = 0;
         _loc7_ = null;
         _loc6_.length = 0;
         _loc6_ = null;
         _loc5_ = null;
         if(param1.m_vTreasure.length == 0)
         {
            param1.m_vTreasure = DataHandler.GetTreasure(param1.m_nTreasureID).GenerateTreasure();
         }
         if(PlayState.m_objInstance.m_nGameState != 2 && param1.m_vTreasure.length > 0)
         {
            if(this.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVE)
            {
               this.m_nState = GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE;
            }
            else if(this.m_nState == GUIInventory.STATE_ENCOUNTER)
            {
               this.m_nState = GUIInventory.STATE_ENCOUNTER_TREASURE;
            }
            _loc30_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_FIT_SUB]);
            _loc31_ = this.grpAvailCampSlot.SocketedItem();
            for each(_loc22_ in param1.m_vTreasure)
            {
               (_loc22_ = _loc22_.Clone()).CreateAppearance();
               if(param1.m_nType == Encounter.TYPE_HACKING)
               {
                  var _loc34_:int = 0;
                  var _loc35_:* = param1.m_dictUsedItems;
                  for(_loc16_ in _loc35_)
                  {
                     if((_loc17_ = ItemInstance(_loc16_)).m_objProxy.ItemDefinition.nGroupID == _loc22_.ItemDefinition.nGroupID)
                     {
                        _loc17_.m_objProxy.ChangeMode(_loc22_.ItemDefinition);
                     }
                     else if(_loc17_.m_objProxy.m_objParentContainer != null && _loc17_.m_objProxy.m_objParentContainer.ItemDefinition.nGroupID == _loc22_.ItemDefinition.nGroupID)
                     {
                        _loc17_.m_objProxy.m_objParentContainer.ChangeMode(_loc22_.ItemDefinition);
                     }
                  }
               }
               else if(_loc22_.ItemDefinition.nGroupID == 12 && this.sprPlayer.m_tilCurrentHex.m_vCampItems.length < this.sprPlayer.m_tilCurrentHex.nCampItems)
               {
                  this.AddItemToCapBox(_loc22_,_loc31_,_loc30_);
                  this.sprPlayer.m_tilCurrentHex.m_vCampItems.push(_loc22_);
                  this.sprPlayer.RememberCamp(this.sprPlayer.m_tilCurrentHex,ItemCamp(_loc22_));
               }
               else
               {
                  this.AddItemToCapBox(_loc22_,this.sprPlayer.m_tilCurrentHex.GroundObject,_loc30_);
               }
            }
            this.btnEncViewItems.on = true;
         }
         else
         {
            this.btnEncViewItems.on = false;
         }
         this.txtEncounter.text = param1.m_strDesc;
         this.UpdateResponseText();
         if(param1.m_nType == Encounter.TYPE_COMBAT && this.sprPlayer.Alive)
         {
            this.grpBattleLayer.DisplayCombatPair();
            this.grpBattleLayer.GetBattle().GetOptions();
            this.UpdateCombatItems();
            if(this.sprPlayer.fHoursSlept > 2 * PlayState.HOURS_PER_COMBAT_TURN && this.sprPlayer.m_tilCurrentHex.m_objBattle.IsCombatant(this.sprPlayer))
            {
               this.ConfirmResponse();
            }
         }
         this.sprPlayer.m_tilCurrentHex.m_nBarterTile = _loc2_;
      }
      
      public function CheckAutoCloseEncounter() : void
      {
         var _loc1_:Encounter = null;
         if(this.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE)
         {
            _loc1_ = this.objEncounter.HandleResponse(this.sprPlayer,new Vector.<ItemInstance>(),true);
            if(_loc1_.m_nID == DM.m_nNullEnc)
            {
               this.ConfirmResponse();
            }
         }
      }
      
      public function ConfirmResponse() : void
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         if(this.objDragging != null)
         {
            return;
         }
         ++this.nDebugCounter;
         var _loc1_:Vector.<ItemInstance> = this.grpEncounterSlot.SocketedItem().GetItems();
         var _loc2_:Boolean = false;
         if(this.objEncounter.m_nType == Encounter.TYPE_SCAVENGE)
         {
            _loc2_ = true;
            for each(_loc5_ in _loc1_)
            {
               if(_loc5_.IDString == "90.10")
               {
                  _loc2_ = false;
                  break;
               }
            }
         }
         else if(this.objEncounter.m_nType == Encounter.TYPE_COMBAT)
         {
            _loc2_ = true;
         }
         var _loc3_:Encounter = this.objEncounter.HandleResponse(this.sprPlayer,_loc1_,_loc2_);
         DM.AppendEncounter(_loc3_);
         if(this.objEncounter.m_nID == 42 && _loc1_.length > 0 && _loc1_[0].ItemDefinition.nGroupID == 103)
         {
            this.m_objScavLoc = _loc1_[0];
         }
         _loc1_ = this.grpAvailEncounterSlot.SocketedItem().GetItems();
         for each(_loc4_ in _loc1_)
         {
            _loc4_.Ghosted = false;
         }
         if(this.objEncounter.m_nType == Encounter.TYPE_COMBAT)
         {
            this.objPlayState.EndDMTurn(PlayState.HOURS_PER_COMBAT_TURN,true,true);
         }
         else
         {
            this.objPlayState.EndPlayerTurn(0,false);
         }
         --this.nDebugCounter;
      }
      
      public function ConfirmSkills() : Boolean
      {
         var _loc4_:ItemInstance = null;
         var _loc5_:Boolean = false;
         var _loc1_:int = 0;
         var _loc2_:int = int(DataHandler.GetGameVars()["nSkillPoints"]);
         var _loc3_:Vector.<ItemInstance> = this.grpSkillSlot.SocketedItem().GetItems();
         for each(_loc4_ in _loc3_)
         {
            _loc1_ += _loc4_.Weight;
         }
         _loc3_ = this.grpTraitSlot.SocketedItem().GetItems();
         for each(_loc4_ in _loc3_)
         {
            _loc2_ += _loc4_.Weight;
         }
         _loc5_ = this.objPlayState.grpMsg.m_bIgnoreMessages;
         if(_loc1_ > _loc2_)
         {
            this.objPlayState.grpMsg.m_bIgnoreMessages = false;
            this.objPlayState.grpMsg.MessageFloaty("天赋和缺陷不平衡. 请多选几个缺陷或者少选几个天赋.",false,null,GUIMessageWindow.COLOR_BAD);
            this.objPlayState.grpMsg.m_bIgnoreMessages = _loc5_;
            return false;
         }
         if(this.bUnspentNotify == false && _loc1_ < _loc2_)
         {
            this.bUnspentNotify = true;
            this.objPlayState.grpMsg.m_bIgnoreMessages = false;
            this.objPlayState.grpMsg.MessageFloaty("你有 " + (_loc2_ - _loc1_) + " 点未使用的天赋.如果你想放弃这些额外的点数. 请再点一次\'确认\'",false,null,GUIMessageWindow.COLOR_BAD);
            this.objPlayState.grpMsg.m_bIgnoreMessages = _loc5_;
            return false;
         }
         this.btnSkillsConfirm.kill();
         this.btnSkillsRandom.kill();
         this.grpAvailSkillSlot.kill();
         this.grpAvailTraitSlot.kill();
         this.txtSklAvail.kill();
         this.txtTraitAvail.kill();
         this.txtTraitInstruct.kill();
         this.m_bAvailSkills = false;
         var _loc6_:Vector.<int> = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SWAP]);
         var _loc7_:ItemInstance = DataHandler.GetItem("96.4");
         if(this.FindSpaceInCapBox(_loc7_,this.grpTraitSlot.SocketedItem()).m_nResult == GUIFitItemResult.RESULT_CANNOT_FIT)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(355),false,false);
         }
         _loc7_ = DataHandler.GetItem("91.0");
         if(this.FindSpaceInCapBox(_loc7_,this.grpSkillSlot.SocketedItem()).m_nResult == GUIFitItemResult.RESULT_CANNOT_FIT)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(809),false,false);
         }
         return true;
      }
      
      private function RandomSkills() : void
      {
         var _loc3_:ItemInstance = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:int = 0;
         var _loc7_:int = 0;
         var _loc8_:Number = NaN;
         var _loc1_:ItemInstance = this.grpAvailTraitSlot.SocketedItem();
         var _loc2_:ItemInstance = this.grpAvailSkillSlot.SocketedItem();
         var _loc4_:Vector.<ItemInstance> = this.grpSkillSlot.SocketedItem().GetItems();
         for each(_loc5_ in _loc4_)
         {
            _loc3_ = this.grpSkillSlot.RemoveItem(_loc5_,true);
            if(_loc3_ != null)
            {
               this.AddItemToCapBox(_loc3_,_loc2_);
               this.SkillPairCheck(_loc3_,this.grpAvailSkillSlot);
            }
         }
         _loc4_ = this.grpTraitSlot.SocketedItem().GetItems();
         for each(_loc5_ in _loc4_)
         {
            _loc3_ = this.grpSkillSlot.RemoveItem(_loc5_,true);
            if(_loc3_ != null)
            {
               this.AddItemToCapBox(_loc3_,_loc1_);
               this.SkillPairCheck(_loc3_,this.grpAvailTraitSlot);
            }
         }
         _loc4_ = this.grpAvailTraitSlot.SocketedItem().GetItems();
         _loc6_ = int(DataHandler.GetGameVars()["nSkillPoints"]);
         _loc7_ = 0;
         _loc8_ = 0.5;
         for each(_loc5_ in _loc4_)
         {
            if(DM.Rand(DM.RAND_FLAT) <= _loc8_ && _loc5_.Ghosted == false)
            {
               _loc3_ = this.grpAvailTraitSlot.RemoveItem(_loc5_);
               this.AddItemToSlot(_loc3_,this.grpTraitSlot);
               this.SkillPairCheck(_loc3_,this.grpTraitSlot);
               _loc6_ += _loc5_.Weight;
            }
         }
         _loc4_ = this.grpAvailSkillSlot.SocketedItem().GetItems();
         _loc8_ = 0.5;
         for each(_loc5_ in _loc4_)
         {
            if(_loc7_ + _loc5_.Weight <= _loc6_)
            {
               if(DM.Rand(DM.RAND_FLAT) <= _loc8_ && _loc5_.Ghosted == false)
               {
                  _loc3_ = this.grpAvailSkillSlot.RemoveItem(_loc5_);
                  this.AddItemToSlot(_loc3_,this.grpSkillSlot);
                  this.SkillPairCheck(_loc3_,this.grpSkillSlot);
                  _loc7_ += _loc5_.Weight;
               }
            }
         }
      }
      
      public function ClearCraftYield() : void
      {
         var _loc1_:ItemInstance = null;
         for each(_loc1_ in this.vYieldPages)
         {
            this.TransferItemContents(_loc1_,null,null,this.vAvailCraftingPages);
         }
         this.btnCraftClear.on = false;
         this.btnCraftClear.status = FlxButton.NORMAL;
         this.btnCraftClear.kill();
         this.CheckRecipe();
      }
      
      public function ConfirmCraft() : void
      {
         var _loc1_:ItemInstance = null;
         var _loc2_:Vector.<ItemInstance> = null;
         var _loc3_:Vector.<int> = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:Vector.<ItemInstance> = null;
         var _loc6_:Vector.<ItemInstance> = null;
         var _loc7_:ItemInstance = null;
         var _loc8_:Vector.<ItemInstance> = null;
         var _loc9_:ItemInstance = null;
         var _loc10_:String = null;
         var _loc11_:Vector.<ItemInstance> = null;
         var _loc12_:Vector.<ItemInstance> = null;
         var _loc13_:int = 0;
         var _loc14_:ItemInstance = null;
         var _loc15_:ItemInstance = null;
         var _loc16_:GUIInventorySlot = null;
         var _loc17_:ItemInstance = null;
         var _loc18_:int = 0;
         var _loc19_:ItemInstance = null;
         if(this.sprPlayer.m_fMovesLeft <= 0 || !this.btnCraftConfirm.alive || this.objCurrentRecipe == null)
         {
            this.btnCraftConfirm.on = false;
            return;
         }
         this.btnYieldPrev.kill();
         this.btnYieldNext.kill();
         _loc1_ = this.grpCraftingIngredientsSlot.SocketedItem();
         _loc2_ = new Vector.<ItemInstance>();
         _loc3_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_SOCKET,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL]);
         _loc4_ = _loc1_.GetItems();
         _loc5_ = new Vector.<ItemInstance>();
         _loc6_ = new Vector.<ItemInstance>();
         _loc7_ = this.grpCraftingYieldSlot.SocketedItem();
         _loc4_ = Recipe.PreSortItems(_loc4_);
         if((_loc8_ = this.objCurrentRecipe.Validate(_loc4_,true,_loc5_,_loc6_)).length > 0)
         {
            this.TransferItemContents(_loc7_);
            for each(_loc9_ in _loc8_)
            {
               if(_loc9_.fDurability <= 0)
               {
                  _loc8_ = _loc8_.concat(_loc9_.GetDegradedItems(1));
               }
               else
               {
                  _loc9_.SetFormatIDs(_loc7_.ItemDefinition.aContentIDs[0]);
                  _loc19_ = this.AddItemToCapBox(_loc9_,_loc7_,_loc3_,true);
                  _loc9_.CreateAppearance();
               }
            }
            for each(_loc9_ in _loc8_)
            {
               _loc9_.SetFormatIDs(_loc1_.ItemDefinition.aContentIDs[0]);
               _loc9_.Ghosted = true;
            }
            for each(_loc9_ in _loc5_)
            {
               _loc9_.m_objProxy.UseDegrade();
               _loc9_.m_objProxy.Discharge(1,0,0);
               _loc9_.UseDegrade();
               _loc9_.Discharge(1,0,0);
               if(_loc9_.m_objProxy.fDurability <= 0)
               {
                  this.grpCraftingIngredientsSlot.RemoveItem(_loc9_);
                  _loc2_ = _loc2_.concat(_loc9_.m_objProxy.vItems);
                  if(_loc9_.m_objProxy.bSocketed)
                  {
                     _loc9_.m_objProxy.Slot.UnSocketItem(true,_loc9_.m_objProxy,false);
                  }
                  else
                  {
                     _loc9_.m_objProxy.Slot.RemoveItem(_loc9_.m_objProxy);
                  }
               }
            }
            for each(_loc9_ in _loc6_)
            {
               this.grpCraftingIngredientsSlot.RemoveItem(_loc9_);
               _loc16_ = _loc9_.m_objProxy.Slot;
               _loc2_ = _loc2_.concat(_loc9_.m_objProxy.vItems);
               if(_loc16_ != null)
               {
                  if(_loc9_.m_objProxy.bSocketed)
                  {
                     _loc16_.UnSocketItem(true,_loc9_.m_objProxy,false);
                  }
                  else
                  {
                     _loc16_.RemoveItem(_loc9_.m_objProxy);
                  }
               }
               else if(_loc9_.m_objProxy.m_objParentContainer != null)
               {
                  _loc9_.m_objProxy.m_objParentContainer.RemoveItem(_loc9_.m_objProxy);
               }
               _loc9_.m_objProxy = null;
            }
            _loc10_ = "";
            _loc4_ = this.grpCraftingYieldSlot.SocketedItem().vItems;
            _loc11_ = _loc6_.concat();
            if(this.objCurrentRecipe.m_bTransferComponents)
            {
               _loc11_.length = 0;
               for each(_loc9_ in _loc6_)
               {
                  if(_loc9_.m_vComponents.length > 0)
                  {
                     _loc11_ = _loc11_.concat(_loc9_.m_vComponents);
                  }
                  else
                  {
                     _loc11_.push(_loc9_);
                  }
               }
            }
            _loc12_ = _loc11_.concat();
            for each(_loc13_ in this.objCurrentRecipe.m_vDestroyed)
            {
               for each(_loc9_ in _loc12_)
               {
                  if(DataHandler.GetIngredient(_loc13_).ItemMatches(_loc9_))
                  {
                     _loc11_.splice(_loc11_.indexOf(_loc9_),1);
                  }
               }
            }
            if(this.objCurrentRecipe.m_bReverseTemp == false && this.objCurrentRecipe.m_nReverse != Recipe.REVERSE_FALSE)
            {
               for each(_loc9_ in _loc4_)
               {
                  if(_loc9_.m_vComponents.length > 0)
                  {
                     _loc9_.m_vComponents = _loc11_;
                     break;
                  }
               }
            }
            for each(_loc14_ in _loc4_)
            {
               if(_loc14_.m_objProxy == null)
               {
                  (_loc17_ = _loc14_.Clone()).SetFormatIDs();
                  _loc14_.m_objProxy = _loc17_;
                  _loc18_ = 0;
                  while(_loc18_ < _loc17_.StackCount - 1)
                  {
                     _loc14_.m_vStack[_loc18_].m_objProxy = _loc17_.m_vStack[_loc18_];
                     _loc18_++;
                  }
                  _loc17_.CreateAppearance();
                  if((_loc19_ = this.AddItemToSlot(_loc17_,this.sprPlayer.grpGroundSlot)) != null)
                  {
                     this.AddItemToSlot(_loc17_,this.sprPlayer.grpCampSlot);
                  }
               }
               _loc14_.Ghosted = false;
            }
            for each(_loc14_ in _loc2_)
            {
               (_loc15_ = _loc14_.Clone()).CreateAppearance();
               if((_loc19_ = this.AddItemToSlot(_loc15_,this.sprPlayer.grpGroundSlot)) != null)
               {
                  this.AddItemToSlot(_loc19_,this.sprPlayer.grpCampSlot);
               }
            }
            this.sprPlayer.m_fMovesLeft -= this.objCurrentRecipe.m_fHours;
            this.objPlayState.UpdatePlayerUI();
         }
         this.btnCraftConfirm.status = FlxButton.NORMAL;
         this.btnCraftConfirm.kill();
         this.btnCraftClear.revive();
      }
      
      public function UpdateCraftButton(param1:String = "") : void
      {
         var _loc2_:Boolean = false;
         var _loc3_:Boolean = false;
         var _loc4_:ItemInstance = null;
         var _loc5_:Boolean = false;
         var _loc6_:ItemInstance = null;
         if(this.m_nPanel != PANEL_CRAFT)
         {
            this.btnCraftClear.kill();
            this.btnCraftConfirm.kill();
            return;
         }
         _loc2_ = true;
         _loc3_ = false;
         this.txtCraYield.text = this.m_strCraftOutput;
         if(this.sprPlayer.m_fMovesLeft <= 0)
         {
            param1 = "没有足够的行动点制造.";
            _loc2_ = false;
            _loc3_ = true;
         }
         else if(this.sprPlayer.m_tilCurrentHex != null && this.sprPlayer.m_tilCurrentHex.m_objBattle != null)
         {
            param1 = "对战中不能制造.\n";
            _loc2_ = false;
            _loc3_ = true;
         }
         else if(this.sprPlayer.m_tilCurrentHex != null && this.sprPlayer.m_tilCurrentHex.m_nBarterTile != BarterHex.BARTER_NONE)
         {
            param1 = "商店中不能制造.\n";
            _loc2_ = false;
            _loc3_ = true;
         }
         else if(this.sprPlayer.HasCondition(188))
         {
            param1 = "双手骨折不能制造.\n";
            _loc2_ = false;
            _loc3_ = true;
         }
         else if(this.objCurrentRecipe == null)
         {
            param1 = "替换上方需制作的物品.";
            _loc2_ = false;
            _loc3_ = false;
         }
         else if(param1 != "")
         {
            _loc2_ = false;
            _loc3_ = true;
         }
         this.txtCraftMoves.visible = _loc3_;
         if(this.txtCraftMoves.text != param1)
         {
            this.txtCraftMoves.text = param1;
         }
         if(_loc2_)
         {
            this.btnCraftConfirm.revive();
            this.btnCraftClear.kill();
            this.txtCraYield.text = this.objCurrentRecipe.m_strName;
            if(this.objCurrentRecipe.m_bReverseTemp)
            {
               this.txtCraYield.text += " (reversed)";
            }
         }
         else
         {
            this.btnCraftConfirm.kill();
            this.btnCraftClear.kill();
            this.btnYieldPrev.kill();
            this.btnYieldNext.kill();
            if((_loc4_ = this.grpCraftingYieldSlot.SocketedItem()) != null && _loc4_.vItems.length > 0)
            {
               _loc5_ = true;
               for each(_loc6_ in _loc4_.vItems)
               {
                  if(_loc6_.Ghosted)
                  {
                     _loc5_ = false;
                     break;
                  }
               }
               if(_loc5_)
               {
                  this.btnCraftClear.revive();
               }
            }
         }
         this.btnCraftConfirm.visible = this.btnCraftConfirm.alive;
      }
      
      public function CheckRecipe() : void
      {
         var _loc1_:ItemInstance = null;
         var _loc2_:Vector.<int> = null;
         var _loc3_:Vector.<ItemInstance> = null;
         var _loc4_:String = null;
         var _loc5_:ItemInstance = null;
         var _loc6_:int = 0;
         var _loc7_:Vector.<ItemInstance> = null;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:Recipe = null;
         var _loc11_:Vector.<ItemInstance> = null;
         var _loc12_:Boolean = false;
         var _loc13_:int = 0;
         this.objCurrentRecipe = null;
         for each(_loc1_ in this.vYieldPages)
         {
            this.TransferItemContents(_loc1_,null,null,this.vAvailCraftingPages);
         }
         this.vCurrentRecipes.length = 0;
         _loc2_ = Vector.<int>([GUIFitItemResult.RESULT_CAN_FIT_SUB,GUIFitItemResult.RESULT_CAN_FIT_SWAP,GUIFitItemResult.RESULT_CANNOT_FIT,GUIFitItemResult.RESULT_CAN_SOCKET,GUIFitItemResult.RESULT_CAN_SOCKET_SWAP,GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL]);
         _loc3_ = this.grpCraftingIngredientsSlot.SocketedItem().vItems.concat();
         _loc4_ = "";
         if(_loc3_.length > 0)
         {
            _loc5_ = this.grpCraftingYieldSlot.SocketedItem();
            _loc6_ = 0;
            _loc7_ = new Vector.<ItemInstance>();
            _loc8_ = 0;
            _loc9_ = int(this.grpAvailCraftItemsSlot.SocketedItem().ItemDefinition.aContentIDs[0]);
            this.btnYieldPrev.kill();
            this.btnYieldNext.kill();
            _loc3_ = Recipe.PreSortItems(_loc3_);
            for each(_loc10_ in DataHandler.m_vRecipesSorted)
            {
               if((_loc11_ = _loc10_.Validate(_loc3_,false)).length > 0)
               {
                  if(_loc8_ > 0)
                  {
                     this.btnYieldNext.revive();
                  }
                  _loc12_ = true;
                  _loc13_ = 0;
                  while(_loc13_ < _loc11_.length)
                  {
                     _loc1_ = _loc11_[_loc13_];
                     if(_loc1_.fDurability <= 0)
                     {
                        _loc11_ = _loc11_.concat(_loc1_.GetDegradedItems(1));
                     }
                     else
                     {
                        if(_loc8_ >= this.vYieldPages.length)
                        {
                           break;
                        }
                        _loc1_.SetFormatIDs(this.vYieldPages[_loc8_].ItemDefinition.aContentIDs[0]);
                        if(this.vYieldPages[_loc8_].Slot != null)
                        {
                           this.AddItemToSlot(_loc1_,this.vYieldPages[_loc8_].Slot,true);
                        }
                        else
                        {
                           this.AddItemToCapBox(_loc1_,this.vYieldPages[_loc8_],_loc2_,true);
                        }
                        _loc12_ = false;
                     }
                     _loc13_++;
                  }
                  if(!_loc12_)
                  {
                     for each(_loc1_ in _loc11_)
                     {
                        _loc1_.CreateAppearance();
                        _loc1_.SetFormatIDs(_loc9_);
                        _loc1_.Ghosted = true;
                     }
                     this.vCurrentRecipes.push(_loc10_);
                     _loc8_++;
                     this.sprPlayer.AddRecipe = _loc10_.m_nID;
                  }
               }
            }
            _loc4_ = this.SetYieldItems(0);
         }
         this.UpdateCraftButton(_loc4_);
      }
      
      private function SetYieldItems(param1:int) : String
      {
         var _loc2_:String = null;
         var _loc3_:Vector.<ItemInstance> = null;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:ItemInstance = null;
         _loc2_ = "";
         _loc3_ = this.vYieldPages[param1].vItems;
         if(this.vCurrentRecipes.length == 0)
         {
            this.vCurrentRecipes.push(this.objCurrentRecipe);
         }
         this.objCurrentRecipe = null;
         if(_loc3_.length > 0)
         {
            _loc4_ = new Vector.<ItemInstance>();
            for each(_loc5_ in _loc3_)
            {
               if(_loc5_.m_objProxy == null)
               {
                  _loc4_.push(_loc5_);
               }
            }
            if(this.TestItemsFitGroundCamp(_loc4_) == false)
            {
               _loc2_ = "地面/营地空间不足，无法摆放.";
            }
            this.grpCraftingYieldSlot.UnSocketItem(true);
            this.grpCraftingYieldSlot.SocketItem(this.vYieldPages[param1]);
            this.objCurrentRecipe = this.vCurrentRecipes[param1];
         }
         if(param1 < this.vCurrentRecipes.length - 1 && param1 < this.vYieldPages.length - 1)
         {
            this.btnYieldNext.revive();
         }
         else
         {
            this.btnYieldNext.kill();
         }
         if(param1 > 0)
         {
            this.btnYieldPrev.revive();
         }
         else
         {
            this.btnYieldPrev.kill();
         }
         return _loc2_;
      }
      
      private function UpdateResponseText() : void
      {
         var _loc1_:Vector.<ItemInstance> = null;
         var _loc2_:Encounter = null;
         var _loc3_:* = false;
         var _loc4_:Vector.<ItemInstance> = null;
         var _loc5_:ItemInstance = null;
         if(this.m_nState != STATE_ENCOUNTER && this.m_nState != STATE_ENCOUNTER_TREASURE && this.m_nState != STATE_ENCOUNTER_EXCLUSIVE && this.m_nState != STATE_ENCOUNTER_EXCLUSIVETREASURE && this.m_nState != STATE_COMBAT && this.m_nState != STATE_COMBAT_TREASURE)
         {
            return;
         }
         this.txtEncResponse.text = "目前所选的应对行动:\n";
         _loc1_ = this.grpEncounterSlot.SocketedItem().GetItems();
         _loc2_ = this.objEncounter.HandleResponse(this.sprPlayer,_loc1_);
         if(_loc2_.m_strName != "")
         {
            this.txtEncResponse.text += _loc2_.m_strName;
            switch(this.objEncounter.m_nType)
            {
               case Encounter.TYPE_SCAVENGE:
                  this.grpScavengeAccident.visible = true;
                  this.grpScavengeCreature.visible = true;
                  this.grpScavengeLoot.visible = true;
                  this.grpScavengeAccident.UpdateBars(1 - _loc2_.m_fAccidentChanceTemp,new Array(0,0.25,0.5,0.75,1));
                  this.grpScavengeCreature.UpdateBars(1 - _loc2_.m_fCreatureChanceTemp,new Array(0,0.25,0.5,0.75,1));
                  this.grpScavengeLoot.UpdateBars(_loc2_.m_fLootChanceTemp,new Array(0,0.25,0.5,0.75,1));
                  break;
               case Encounter.TYPE_COMBAT:
                  _loc4_ = this.grpAvailEncounterSlot.SocketedItem().GetItems();
                  _loc3_ = this.grpEncounterSlot.SocketedItem().vItems.length > 0;
                  for each(_loc5_ in _loc4_)
                  {
                     _loc5_.Ghosted = _loc3_;
                  }
                  this.grpScavengeAccident.visible = false;
                  this.grpScavengeCreature.visible = false;
                  this.grpScavengeLoot.visible = false;
                  break;
               default:
                  _loc4_ = this.grpAvailEncounterSlot.SocketedItem().GetItems();
                  _loc3_ = this.objEncounter.m_nType == Encounter.TYPE_NORMAL && (_loc2_.m_nType == Encounter.TYPE_SCAVENGE || _loc2_.m_nID == 101);
                  for each(_loc5_ in _loc4_)
                  {
                     _loc5_.Ghosted = _loc3_;
                  }
                  this.grpScavengeAccident.visible = false;
                  this.grpScavengeCreature.visible = false;
                  this.grpScavengeLoot.visible = false;
            }
         }
         this.btnEncConfirm.y = this.txtEncResponse.y + this.txtEncResponse.height;
         this.btnEncViewItems.y = this.btnEncConfirm.y + this.btnEncConfirm.height;
      }
      
      public function AddFloatItem(param1:ItemInstance, param2:GUIFitItemResult, param3:FlxPoint = null) : void
      {
         var _loc4_:FlxPoint = null;
         var _loc5_:FlxPoint = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc7_:Array = null;
         var _loc9_:FlxSprite = null;
         var _loc10_:VFX = null;
         _loc4_ = new FlxPoint(param1.x,param1.y);
         if(this.grpSlotSource == null)
         {
            _loc4_ = new FlxPoint(FlxG.stage.width,0);
         }
         if(param3)
         {
            _loc5_ = param3;
         }
         else
         {
            if(this.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVE)
            {
               return;
            }
            if((_loc6_ = param2.m_grpSlot) == null && param2.m_objItem != null)
            {
               _loc6_ = param2.m_objItem.Slot;
            }
            if(_loc6_ == null)
            {
               return;
            }
            if(_loc6_.nSlotIndex == 212)
            {
               return;
            }
            if(_loc6_.nSlotIndex == 207)
            {
               if(_loc6_.m_sprOwner != this.sprPlayer)
               {
                  return;
               }
               if(!this.grpVehicleLayer.alive)
               {
                  _loc5_ = new FlxPoint(this.objPlayState.btnVehicle.x,this.objPlayState.btnVehicle.y);
               }
            }
            else if(_loc6_.nSlotIndex == 208 || _loc6_.nSlotIndex == 209)
            {
               if(_loc6_.m_sprOwner != this.sprPlayer)
               {
                  return;
               }
               if(!this.grpCampLayer.alive)
               {
                  _loc5_ = new FlxPoint(this.objPlayState.btnCamp.x,this.objPlayState.btnCamp.y);
               }
            }
            else if(_loc6_.nSlotIndex == 205 || _loc6_.nSlotIndex == 206)
            {
               if(_loc6_.m_sprOwner != this.sprPlayer)
               {
                  return;
               }
               if(!this.grpCraftLayer.alive)
               {
                  _loc5_ = new FlxPoint(this.objPlayState.btnCraft.x,this.objPlayState.btnCraft.y);
               }
            }
            else if(_loc6_.nSlotIndex == 203 || _loc6_.nSlotIndex == 204)
            {
               if(!this.grpSkillsLayer.alive)
               {
                  return;
               }
            }
            else if(_loc6_ == this.grpSkillSlot || _loc6_ == this.grpTraitSlot)
            {
               if(!this.grpSkillsLayer.alive && !this.grpItemsLayer.alive)
               {
                  _loc5_ = new FlxPoint(this.objPlayState.btnSkills.x,this.objPlayState.btnSkills.y);
               }
            }
            else if(_loc6_.nSlotIndex == 201 || _loc6_.nSlotIndex == 202)
            {
               if(!this.grpEncounterLayer.alive)
               {
                  if(!PlayState.m_objInstance.btnEncounter.alive)
                  {
                     return;
                  }
                  _loc5_ = new FlxPoint(this.objPlayState.btnEncounter.x,this.objPlayState.btnEncounter.y);
               }
            }
            else if(_loc6_.nSlotIndex != 211 && _loc6_.nSlotIndex != 212)
            {
               if(_loc6_.m_sprOwner != this.sprPlayer)
               {
                  return;
               }
               if(!_loc6_.alive && this.objPlayState.btnItems.alive)
               {
                  _loc5_ = new FlxPoint(this.objPlayState.btnItems.x,this.objPlayState.btnItems.y);
               }
            }
         }
         if(_loc5_ == null)
         {
            _loc5_ = new FlxPoint();
            switch(param2.m_nResult)
            {
               case GUIFitItemResult.RESULT_CAN_FIT:
                  if(param2.m_objItem.bSocketed)
                  {
                     _loc5_.x = param2.m_objItem.Slot.sprCap.x + param2.m_ptPos.x;
                     _loc5_.y = param2.m_objItem.Slot.sprCap.y + param2.m_ptPos.y;
                  }
                  else
                  {
                     _loc5_.x = param2.m_objItem.x;
                     _loc5_.y = param2.m_objItem.y;
                  }
                  break;
               case GUIFitItemResult.RESULT_CAN_SOCKET:
                  _loc5_.x = param2.m_grpSlot.btnSlot.x + param2.m_grpSlot.btnSlot.width / 2 - param1.width / 2;
                  _loc5_.y = param2.m_grpSlot.btnSlot.y + param2.m_grpSlot.btnSlot.height / 2 - param1.height / 2;
                  break;
               case GUIFitItemResult.RESULT_CAN_SOCKET_PARTIAL:
                  _loc5_.x = param2.m_grpSlot.btnSlot.x + param2.m_grpSlot.btnSlot.width / 2 - param1.width / 2;
                  _loc5_.y = param2.m_grpSlot.btnSlot.y + param2.m_grpSlot.btnSlot.height / 2 - param1.height / 2;
                  break;
               case GUIFitItemResult.RESULT_CAN_FIT_SUB:
                  _loc5_.x = param2.m_objItem.Slot.sprCap.x;
                  _loc5_.y = param2.m_objItem.Slot.sprCap.y;
                  break;
               case GUIFitItemResult.RESULT_CAN_STACK_FULL:
                  _loc5_.x = param2.m_objItem.x;
                  _loc5_.y = param2.m_objItem.y;
                  break;
               case GUIFitItemResult.RESULT_CAN_STACK_PARTIAL:
                  _loc5_.x = param2.m_objItem.x;
                  _loc5_.y = param2.m_objItem.y;
                  break;
               default:
                  return;
            }
         }
         _loc7_ = new Array();
         var _loc8_:Array = new Array();
         param1.visible = false;
         (_loc9_ = new FlxSprite(_loc4_.x,_loc4_.y)).pixels = param1.ItemDefinition.vImageList[param1.m_nImageIndex];
         _loc7_.push(_loc9_);
         _loc10_ = new VFX(_loc7_,[_loc5_,param1],this.FloatItemPerFrame,this.RemoveOldFloatItem);
         add(_loc10_);
         _loc9_.scrollFactor = new FlxPoint();
         _loc9_.cameras = [FlxG.camera];
      }
      
      private function RemoveOldFloatItem(param1:VFX) : void
      {
         remove(param1);
         param1.aParams[1].visible = true;
         param1 = null;
      }
      
      private function FloatItemPerFrame(param1:VFX) : void
      {
         var _loc2_:Number = NaN;
         var _loc3_:int = 0;
         var _loc4_:FlxSprite = null;
         var _loc5_:Number = NaN;
         var _loc6_:Number = NaN;
         _loc2_ = 0.25 * DataHandler.nFPSModifier;
         if(_loc2_ >= 1)
         {
            _loc2_ = 0.9;
         }
         else if(_loc2_ <= 0)
         {
            _loc2_ = 0.1;
         }
         _loc3_ = 0;
         while(_loc3_ < param1.aSprites.length)
         {
            _loc5_ = (_loc4_ = FlxSprite(param1.aSprites[_loc3_])).x - FlxPoint(param1.aParams[0]).x;
            _loc6_ = _loc4_.y - FlxPoint(param1.aParams[0]).y;
            _loc4_.x -= _loc5_ * _loc2_;
            _loc4_.y -= _loc6_ * _loc2_;
            if(Math.abs(_loc5_) <= 5 && Math.abs(_loc6_) <= 5)
            {
               param1.m_bNeedsCleanup = true;
            }
            _loc3_++;
         }
      }
      
      public function ButtonCallback(param1:GUIMenuButton) : void
      {
         this.UpdateScreens(this.aPanels.indexOf(param1));
      }
      
      public function StackCursor(param1:Boolean) : void
      {
         if(param1 != this.bMouseWholeStack)
         {
            this.bMouseWholeStack = param1;
            this.objPlayState.ChangeCursor(this.m_nMouseMode + int(this.bMouseWholeStack));
         }
      }
      
      public function UpdateConditions() : void
      {
         var _loc2_:int = 0;
         var _loc3_:uint = 0;
         var _loc4_:FlxPoint = null;
         var _loc5_:int = 0;
         var _loc6_:PlayerCondition = null;
         var _loc7_:FlxText = null;
         var _loc1_:int = 0;
         _loc2_ = 0;
         _loc3_ = 0;
         while(_loc3_ < this.m_aConditions.length)
         {
            this.grpItemsLayer.remove(this.m_aConditions[_loc3_]);
            this.grpHealthLayer.remove(this.m_aConditions[_loc3_]);
            _loc3_++;
         }
         this.m_aConditions = [];
         _loc4_ = GUIValues.GetPoint("GUIInventory.txtCondTitle");
         _loc4_.y += this.txtCondTitle.height;
         _loc5_ = GUIValues.GetInt("GUIInventory.txtCondStatsWarning.size");
         for each(_loc6_ in this.sprPlayer.aCurrentStates)
         {
            if(!(_loc6_.strName == "" || !_loc6_.m_bDisplay))
            {
               (_loc7_ = new FlxText(_loc4_.x,_loc4_.y + _loc2_,_loc5_,_loc6_.strName)).setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),_loc6_.m_nColor);
               _loc7_.scrollFactor = new FlxPoint();
               _loc7_.cameras = [FlxG.camera];
               this.m_aConditions.push(_loc7_);
               this.grpItemsLayer.add(_loc7_);
               this.grpHealthLayer.add(_loc7_);
               _loc2_ += _loc7_.height;
            }
         }
      }
      
      private function PrevCraft() : void
      {
         var _loc1_:int = 0;
         var _loc2_:String = null;
         var _loc3_:int = 0;
         this.btnCraftPrev.on = false;
         this.btnCraftPrev.status = FlxButton.NORMAL;
         if(this.btnCraftAvail.on)
         {
            _loc1_ = int(this.vAvailCraftingPages.indexOf(this.grpAvailCraftItemsSlot.SocketedItem()));
            if(_loc1_ > 0)
            {
               _loc2_ = this.txtCraftMoves.text;
               this.grpAvailCraftItemsSlot.UnSocketItem(true);
               this.grpAvailCraftItemsSlot.SocketItem(this.vAvailCraftingPages[_loc1_ - 1]);
               this.UpdateCraftButton(_loc2_);
            }
            if(_loc1_ <= 1)
            {
               this.btnCraftPrev.kill();
            }
            if(_loc1_ >= this.vAvailCraftingPages.length - 1)
            {
               this.btnCraftNext.revive();
            }
         }
         else
         {
            _loc3_ = this.m_nRecipeFirstIndex - GUIValues.GetPoint("GUIInventory.QuickRecipe.size").y;
            if(_loc3_ < 0)
            {
               _loc3_ = 0;
            }
            this.ShowQuickRecipes(_loc3_);
         }
      }
      
      private function NextCraft() : void
      {
         var _loc1_:int = 0;
         var _loc2_:String = null;
         var _loc3_:int = 0;
         this.btnCraftNext.on = false;
         this.btnCraftNext.status = FlxButton.NORMAL;
         if(this.btnCraftAvail.on)
         {
            _loc1_ = int(this.vAvailCraftingPages.indexOf(this.grpAvailCraftItemsSlot.SocketedItem()));
            if(_loc1_ < this.vAvailCraftingPages.length - 1)
            {
               _loc2_ = this.txtCraftMoves.text;
               this.grpAvailCraftItemsSlot.UnSocketItem(true);
               this.grpAvailCraftItemsSlot.SocketItem(this.vAvailCraftingPages[_loc1_ + 1]);
               this.UpdateCraftButton(_loc2_);
            }
            if(_loc1_ >= this.vAvailCraftingPages.length - 1)
            {
               this.btnCraftNext.kill();
            }
            if(_loc1_ <= 0)
            {
               this.btnCraftPrev.revive();
            }
         }
         else
         {
            _loc3_ = this.m_nRecipeFirstIndex + GUIValues.GetPoint("GUIInventory.QuickRecipe.size").y;
            if(_loc3_ > this.sprPlayer.m_vKnownRecipes.length - 1)
            {
               _loc3_ = this.sprPlayer.m_vKnownRecipes.length - GUIValues.GetPoint("GUIInventory.QuickRecipe.size").y + 1;
            }
            this.ShowQuickRecipes(_loc3_);
         }
      }
      
      private function PrevYield() : void
      {
         var _loc1_:int = 0;
         this.btnYieldPrev.on = false;
         this.btnYieldPrev.status = FlxButton.NORMAL;
         _loc1_ = int(this.vYieldPages.indexOf(this.grpCraftingYieldSlot.SocketedItem()));
         if(_loc1_ > 0)
         {
            this.UpdateCraftButton(this.SetYieldItems(_loc1_ - 1));
         }
      }
      
      private function NextYield() : void
      {
         var _loc1_:int = 0;
         this.btnYieldNext.on = false;
         this.btnYieldNext.status = FlxButton.NORMAL;
         _loc1_ = int(this.vYieldPages.indexOf(this.grpCraftingYieldSlot.SocketedItem()));
         if(_loc1_ < this.vCurrentRecipes.length - 1 && _loc1_ < this.vYieldPages.length - 1)
         {
            this.UpdateCraftButton(this.SetYieldItems(_loc1_ + 1));
         }
      }
      
      private function CreateAppearance(param1:FlxGroup) : void
      {
         var _loc2_:FlxBasic = null;
         for each(_loc2_ in param1.members)
         {
            if(_loc2_ is GUIInventorySlot)
            {
               GUIInventorySlot(_loc2_).CreateAppearance();
            }
         }
      }
      
      private function DestroyAppearance(param1:FlxGroup) : void
      {
         var _loc2_:FlxBasic = null;
         for each(_loc2_ in param1.members)
         {
            if(_loc2_ is GUIInventorySlot)
            {
               GUIInventorySlot(_loc2_).DestroyAppearance();
            }
         }
      }
      
      public function Show() : void
      {
         this.objPlayState.ChangeCursor(this.m_nMouseMode + int(this.bMouseWholeStack));
         visible = true;
         active = true;
         revive();
         this.UpdateScreens();
         if(this.objDragging != null)
         {
            this.TintItem(this.objDragging);
         }
      }
      
      public function Hide(param1:Boolean = false) : Boolean
      {
         if(this.objDragging != null && param1 == false)
         {
            return false;
         }
         this.DestroyAppearance(this.grpItemsLayer);
         this.DestroyAppearance(this.grpGroundLayer);
         this.DestroyAppearance(this.grpEncounterLayer);
         this.DestroyAppearance(this.grpSkillsLayer);
         this.DestroyAppearance(this.grpVehicleLayer);
         this.DestroyAppearance(this.grpCampLayer);
         this.DestroyAppearance(this.grpBattleLayer);
         this.DestroyAppearance(this.grpHealthLayer);
         this.DestroyAppearance(this.grpCraftLayer);
         visible = false;
         active = false;
         kill();
         PlayState.m_objInstance.bResetCursor = true;
         return true;
      }
      
      public function MouseMode(param1:int = -1) : void
      {
         if(param1 < 0)
         {
            param1 = this.m_nMouseModeLast;
         }
         if(active != true || param1 == this.m_nMouseMode)
         {
            return;
         }
         this.m_nMouseMode = param1;
         switch(param1)
         {
            case MOUSE_TAKE:
               this.m_nMouseModeLast = param1;
               this.btnCursor.bmpImgDown = DataHandler.GetImage("btn_cursors_take.png");
               this.btnCursor.bmpImgOn = DataHandler.GetImage("btn_cursors_take.png");
               this.btnCursor.bmpImgOut = DataHandler.GetImage("btn_cursors_take.png");
               this.btnCursor.bmpImgOver = DataHandler.GetImage("btn_cursors_take.png");
               this.btnCursor.UpdateImage();
               break;
            case MOUSE_DRAG:
               this.m_nMouseModeLast = param1;
               this.btnCursor.bmpImgDown = DataHandler.GetImage("btn_cursors_off.png");
               this.btnCursor.bmpImgOn = DataHandler.GetImage("btn_cursors_off.png");
               this.btnCursor.bmpImgOut = DataHandler.GetImage("btn_cursors_off.png");
               this.btnCursor.bmpImgOver = DataHandler.GetImage("btn_cursors_off.png");
               this.btnCursor.UpdateImage();
               break;
            case MOUSE_USE:
               this.btnCursor.bmpImgDown = DataHandler.GetImage("btn_cursors_use.png");
               this.btnCursor.bmpImgOn = DataHandler.GetImage("btn_cursors_use.png");
               this.btnCursor.bmpImgOut = DataHandler.GetImage("btn_cursors_use.png");
               this.btnCursor.bmpImgOver = DataHandler.GetImage("btn_cursors_use.png");
               this.btnCursor.UpdateImage();
               break;
            case MOUSE_DELETE:
               this.btnCursor.bmpImgDown = DataHandler.GetImage("btn_cursors_destroy.png");
               this.btnCursor.bmpImgOn = DataHandler.GetImage("btn_cursors_destroy.png");
               this.btnCursor.bmpImgOut = DataHandler.GetImage("btn_cursors_destroy.png");
               this.btnCursor.bmpImgOver = DataHandler.GetImage("btn_cursors_destroy.png");
               this.btnCursor.UpdateImage();
         }
         this.objPlayState.ChangeCursor(this.m_nMouseMode + int(this.bMouseWholeStack));
      }
      
      private function MouseModeToggle() : void
      {
         switch(this.m_nMouseMode)
         {
            case MOUSE_TAKE:
               if(this.m_nState == GUIInventory.STATE_NORMAL || this.m_nState == GUIInventory.STATE_ENCOUNTER_TREASURE || this.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE)
               {
                  if(this.m_nPanel == GUIInventory.PANEL_CRAFT)
                  {
                     this.MouseMode(GUIInventory.MOUSE_DELETE);
                  }
                  else if(this.m_nPanel == GUIInventory.PANEL_RESPONSE)
                  {
                     this.MouseMode(GUIInventory.MOUSE_DRAG);
                  }
                  else
                  {
                     this.MouseMode(GUIInventory.MOUSE_USE);
                  }
               }
               else
               {
                  this.MouseMode(GUIInventory.MOUSE_DRAG);
               }
               break;
            case MOUSE_DRAG:
               this.MouseMode(GUIInventory.MOUSE_TAKE);
               break;
            case MOUSE_USE:
               if(this.m_nState == GUIInventory.STATE_NORMAL || this.m_nState == GUIInventory.STATE_ENCOUNTER_TREASURE || this.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE)
               {
                  this.MouseMode(GUIInventory.MOUSE_DELETE);
               }
               else
               {
                  this.MouseMode(GUIInventory.MOUSE_DRAG);
               }
               break;
            case MOUSE_DELETE:
               this.MouseMode(GUIInventory.MOUSE_DRAG);
               break;
            default:
               this.MouseMode();
         }
         PlayState.m_objInstance.bResetCursor = false;
      }
      
      public function SetRes() : void
      {
         var _loc1_:int = 0;
         var _loc3_:FlxPoint = null;
         var _loc4_:FlxPoint = null;
         var _loc5_:String = null;
         var _loc6_:GUIInventorySlot = null;
         var _loc7_:ItemInstance = null;
         _loc1_ = GUIValues.GetInt("Item.zoom");
         var _loc2_:int = GUIValues.GetInt("Item.zoom");
         if(GUIInventorySlot(this.sprPlayer.m_dictSlots[2]).m_fZoom != _loc1_ || GUIValues.m_bOverrideZoom)
         {
            _loc3_ = GUIValues.GetPoint("GUIInventory.sprBG");
            _loc4_ = GUIValues.GetPoint("GUIInventory.sprBG.size");
            this.sprBG.x = _loc3_.x;
            this.sprBG.y = _loc3_.y;
            this.sprBG.pixels = DataHandler.GetImage("GUIBG.png");
            this.sprBG.pixels = GUIValues.ScaleBitmapData(this.sprBG.pixels,_loc4_.x / this.sprBG.width,_loc4_.y / this.sprBG.height);
            GUIValues.SetPosition(this.txtEncounter,"GUIInventory.txtEncounter");
            this.txtEncounter.SetWidth(GUIValues.GetInt("GUIInventory.txtEncounter.size"));
            GUIValues.SetPosition(this.txtEncResponse,"GUIInventory.txtEncResponse");
            this.txtEncResponse.SetWidth(GUIValues.GetInt("GUIInventory.txtEncResponse.size"));
            GUIValues.SetPosition(this.txtEncAvail,"GUIInventory.txtEncAvail");
            this.txtEncAvail.SetWidth(GUIValues.GetInt("GUIInventory.txtEncAvail.size"));
            GUIValues.SetPosition(this.txtEncResponseLabel,"GUIInventory.txtEncResponseLabel");
            this.txtEncResponseLabel.SetWidth(GUIValues.GetInt("GUIInventory.txtEncResponseLabel.size"));
            GUIValues.SetPosition(this.txtCraIngredients,"GUIInventory.txtCraIngredients");
            this.txtCraIngredients.SetWidth(GUIValues.GetInt("GUIInventory.txtCraIngredients.size"));
            GUIValues.SetPosition(this.txtCraYield,"GUIInventory.txtCraYield");
            this.txtCraYield.SetWidth(GUIValues.GetInt("GUIInventory.txtCraYield.size"));
            GUIValues.SetPosition(this.txtCraftMoves,"GUIInventory.txtCraftMoves");
            this.txtCraftMoves.SetWidth(GUIValues.GetInt("GUIInventory.txtCraftMoves.size"));
            GUIValues.SetPosition(this.txtSklAvail,"GUIInventory.txtSklAvail");
            this.txtSklAvail.SetWidth(GUIValues.GetInt("GUIInventory.txtSklAvail.size"));
            GUIValues.SetPosition(this.txtTraitAvail,"GUIInventory.txtTraitAvail");
            this.txtTraitAvail.SetWidth(GUIValues.GetInt("GUIInventory.txtTraitAvail.size"));
            GUIValues.SetPosition(this.txtTraitInstruct,"GUIInventory.txtTraitInstruct");
            this.txtTraitInstruct.SetWidth(GUIValues.GetInt("GUIInventory.txtTraitInstruct.size"));
            GUIValues.SetPosition(this.txtSkillSpace1,"GUIInventory.txtSkillSpace1");
            this.txtSkillSpace1.SetWidth(GUIValues.GetInt("GUIInventory.txtSkillSpace1.size"));
            GUIValues.SetPosition(this.txtTraitSlot1,"GUIInventory.txtTraitSlot1");
            this.txtTraitSlot1.SetWidth(GUIValues.GetInt("GUIInventory.txtTraitSlot1.size"));
            GUIValues.SetPosition(this.txtSkillTotal,"GUIInventory.txtSkillTotal");
            this.txtSkillTotal.SetWidth(GUIValues.GetInt("GUIInventory.txtSkillSpace1.size"));
            GUIValues.SetPosition(this.txtGround,"GUIInventory.txtGroundAvail");
            this.txtGround.SetWidth(GUIValues.GetInt("GUIInventory.txtSklAvail.size"));
            GUIValues.SetPosition(this.txtCamp,"GUIInventory.txtCamp");
            this.txtCamp.SetWidth(GUIValues.GetInt("GUIInventory.txtCamp.size"));
            GUIValues.SetPosition(this.txtAvailCamp,"GUIInventory.txtAvailCamp");
            this.txtAvailCamp.SetWidth(GUIValues.GetInt("GUIInventory.txtAvailCamp.size"));
            GUIValues.SetPosition(this.txtCondTitle,"GUIInventory.txtCondTitle");
            this.txtCondTitle.SetWidth(GUIValues.GetInt("GUIInventory.txtCondStatsWarning.size"));
            GUIValues.SetPosition(this.txtCondStatsWarning,"GUIInventory.txtCondStatsWarning");
            this.txtCondStatsWarning.SetWidth(GUIValues.GetInt("GUIInventory.txtCondStatsWarning.size"));
            GUIValues.SetPosition(this.txtVehicle,"GUIInventory.txtCamp");
            this.txtVehicle.SetWidth(GUIValues.GetInt("GUIInventory.txtCamp.size"));
            this.txtEncounter.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtEncResponse.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),13421568,"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtEncAvail.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtCraIngredients.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtCraYield.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtCraftMoves.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),4294944768,"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtEncResponseLabel.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtSklAvail.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtTraitAvail.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtTraitInstruct.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtSkillSpace1.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtTraitSlot1.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtSkillTotal.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtGround.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtCamp.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtAvailCamp.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtCondTitle.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtCondStatsWarning.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.txtVehicle.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
            this.SkillPairCheck(null,null);
            _loc5_ = "";
            if(_loc1_ == 2)
            {
               _loc5_ = DataHandler.m_strZoomPrefix;
            }
            GUIValues.SetPosition(this.sprCraftArrowDown,"GUIInventory.sprCraftArrowDown");
            this.sprCraftArrowDown.pixels = DataHandler.GetImage("GUIArrowDown.png",_loc5_);
            GUIValues.SetPosition(this.sprCraftArrowRight,"GUIInventory.sprCraftArrowRight");
            this.sprCraftArrowRight.pixels = DataHandler.GetImage("GUIArrowRight.png",_loc5_);
            GUIValues.SetPosition(this.sprSkillArrowRight,"GUIInventory.sprSkillArrowRight");
            this.sprSkillArrowRight.pixels = DataHandler.GetImage("GUIArrowRight.png",_loc5_);
            GUIValues.SetPosition(this.sprTraitArrowRight,"GUIInventory.sprTraitArrowRight");
            this.sprTraitArrowRight.pixels = DataHandler.GetImage("GUIArrowRight.png",_loc5_);
            this.sprEncounter.Zoom(_loc1_);
            GUIValues.SetPosition(this.sprEncounter,"GUIInventory.grpEncounterSlot");
            this.sprEncounter.x += -this.sprEncounter.pixels.width / 2;
            this.sprEncounter.y += -this.sprEncounter.pixels.height / 2;
            GUIValues.SetPosition(this.btnEncConfirm,"GUIInventory.btnEncConfirm");
            this.btnEncConfirm.Zoom(_loc1_);
            this.btnEncConfirm.y = this.txtEncResponse.y + this.txtEncResponse.height;
            GUIValues.SetPosition(this.btnEncViewItems,"GUIInventory.btnEncConfirm");
            this.btnEncViewItems.Zoom(_loc1_);
            this.btnEncViewItems.y += this.btnEncConfirm.height;
            GUIValues.SetPosition(this.btnSkillsConfirm,"GUIInventory.btnSkillsConfirm");
            this.btnSkillsConfirm.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnSkillsRandom,"GUIInventory.btnSkillsRandom");
            this.btnSkillsRandom.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnCraftConfirm,"GUIInventory.btnCraftConfirm");
            this.btnCraftConfirm.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnCraftClear,"GUIInventory.btnCraftConfirm");
            this.btnCraftClear.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnCursor,"PlayState.btnWait");
            this.btnContextCraft.Zoom(_loc1_);
            this.btnContextDelete.Zoom(_loc1_);
            this.btnContextPad.Zoom(_loc1_);
            this.btnContextTake.Zoom(_loc1_);
            this.btnContextUse.Zoom(_loc1_);
            this.btnContextEmpty.Zoom(_loc1_);
            GUIInventorySlot(this.sprPlayer.m_dictSlots[201]).SetRes(_loc1_,"GUIInventory.grpEncounterSlot","GUIInventory.grpEncounterSlot.Cap");
            GUIInventorySlot(this.sprPlayer.m_dictSlots[202]).SetRes(_loc1_,"GUIInventory.grpEncounterSlot","GUIInventory.grpAvailEncounterSlot.Cap");
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpScavengeLoot");
            this.grpScavengeLoot.x = _loc3_.x;
            this.grpScavengeLoot.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpScavengeAccident");
            this.grpScavengeAccident.x = _loc3_.x;
            this.grpScavengeAccident.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpScavengeCreature");
            this.grpScavengeCreature.x = _loc3_.x;
            this.grpScavengeCreature.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpCampConcealment");
            this.grpCampConcealment.x = _loc3_.x;
            this.grpCampConcealment.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpCampShelter");
            this.grpCampShelter.x = _loc3_.x;
            this.grpCampShelter.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpCampAlertness");
            this.grpCampAlertness.x = _loc3_.x;
            this.grpCampAlertness.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpCampHealRate");
            this.grpCampHealRate.x = _loc3_.x;
            this.grpCampHealRate.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpCampSleepQuality");
            this.grpCampSleepQuality.x = _loc3_.x;
            this.grpCampSleepQuality.y = _loc3_.y;
            this.grpAvailTraitSlot.SetRes(_loc1_,"GUIInventory.grpAvailTraitSlot.Cap","GUIInventory.grpAvailTraitSlot.Cap");
            this.grpAvailSkillSlot.SetRes(_loc1_,"GUIInventory.grpAvailSkillSlot.Cap","GUIInventory.grpAvailSkillSlot.Cap");
            this.grpTraitSlot.SetRes(_loc1_,"GUIInventory.Traits","GUIInventory.Traits");
            this.grpSkillSlot.SetRes(_loc1_,"GUIInventory.Skills","GUIInventory.Skills");
            for each(_loc6_ in this.sprPlayer.m_vAllWoundSlots)
            {
               _loc6_.SetRes(_loc1_,"GUIInventory.Body");
            }
            GUIValues.SetPosition(this.sprCraftCapBG,"GUIInventory.sprCraftCapBG");
            this.sprCraftCapBG.Zoom(_loc1_);
            GUIValues.SetPosition(this.sprYieldCapBG,"GUIInventory.sprYieldCapBG");
            this.sprYieldCapBG.Zoom(_loc1_);
            GUIInventorySlot(this.sprPlayer.m_dictSlots[205]).SetRes(_loc1_,"GUIInventory.grpCraftingIngredientsSlot.Cap","GUIInventory.grpCraftingIngredientsSlot.Cap");
            GUIInventorySlot(this.sprPlayer.m_dictSlots[206]).SetRes(_loc1_,"GUIInventory.grpCraftingYieldSlot.Cap","GUIInventory.grpCraftingYieldSlot.Cap");
            GUIInventorySlot(this.sprPlayer.m_dictSlots[210]).SetRes(_loc1_,"GUIInventory.grpAvailCraftItemsSlot.Cap","GUIInventory.grpAvailCraftItemsSlot.Cap");
            GUIValues.SetPosition(this.btnCraftPrev,"GUIInventory.btnPrevCraft");
            this.btnCraftPrev.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnCraftNext,"GUIInventory.btnNextCraft");
            this.btnCraftNext.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnCraftRecipes,"GUIInventory.btnCraftRecipes");
            this.btnCraftRecipes.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnCraftAvail,"GUIInventory.btnCraftAvail");
            this.btnCraftAvail.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnYieldPrev,"GUIInventory.btnPrevYield");
            this.btnYieldPrev.Zoom(_loc1_);
            GUIValues.SetPosition(this.btnYieldNext,"GUIInventory.btnNextYield");
            this.btnYieldNext.Zoom(_loc1_);
            GUIInventorySlot(this.sprPlayer.m_dictSlots[209]).SetRes(_loc1_,"GUIInventory.grpAvailCampSlot.Cap","GUIInventory.grpAvailCampSlot.Cap");
            GUIInventorySlot(this.sprPlayer.m_dictSlots[207]).SetRes(_loc1_,"GUIInventory.grpVehicleSlot","GUIInventory.grpVehicleSlot.Cap");
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpHealthBlood");
            this.grpHealthBlood.x = _loc3_.x;
            this.grpHealthBlood.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpHealthInfection");
            this.grpHealthInfection.x = _loc3_.x;
            this.grpHealthInfection.y = _loc3_.y;
            _loc3_ = GUIValues.GetPoint("GUIInventory.grpHealthPain");
            this.grpHealthPain.x = _loc3_.x;
            this.grpHealthPain.y = _loc3_.y;
            GUIValues.SetPosition(this.sprBody,"GUIInventory.Body");
            this.sprBody.pixels = DataHandler.GetImage("btn_inv_body.png",_loc5_);
            for each(_loc7_ in this.vAvailCraftingPages)
            {
               _loc7_.SetRes(_loc1_);
            }
            if(this.btnCraftRecipes.on)
            {
               this.ShowQuickRecipes();
            }
            if(this.objDragging != null)
            {
               this.objDragging.SetRes(_loc1_);
            }
            for each(_loc7_ in this.vYieldPages)
            {
               _loc7_.SetRes(_loc1_);
            }
            this.grpPopUp.SetRes();
            this.grpBattleLayer.SetRes();
         }
         this.grpDMCLayer.SetRes();
      }
      
      public function UpdateScreens(param1:int = -1) : void
      {
         var _loc2_:Boolean = false;
         var _loc3_:Boolean = false;
         _loc2_ = false;
         if(param1 >= 0 && param1 != this.m_nPanel && this.objDragging != null)
         {
            if(this.m_nPanel == PANEL_CRAFT && this.objDragging.nFormatID == 17)
            {
               _loc2_ = true;
            }
            else if(_loc2_ == false && param1 == PANEL_CRAFT)
            {
               _loc2_ = true;
            }
         }
         if(_loc2_)
         {
            if(this.ReturnObject(this.objDragging) != null)
            {
               return;
            }
            this.StopDragging();
         }
         FlxG.mouse.reset();
         this.objContext = null;
         this.ClearButtons();
         this.objPlayState.grpWeatherNode.grpSky.kill();
         remove(this.objPlayState.grpWeatherNode.grpSky);
         this.btnEncConfirm.status = FlxButton.NORMAL;
         this.btnEncViewItems.status = FlxButton.NORMAL;
         this.btnSkillsConfirm.status = FlxButton.NORMAL;
         this.btnSkillsRandom.status = FlxButton.NORMAL;
         this.objPlayState.grpBtnScreens.revive();
         this.objPlayState.btnItems.on = false;
         this.objPlayState.btnVehicle.on = false;
         this.objPlayState.btnEncounter.on = false;
         this.objPlayState.btnSkills.on = false;
         this.objPlayState.btnCamp.on = false;
         this.objPlayState.btnConditions.on = false;
         this.objPlayState.btnCraft.on = false;
         this.grpUseSlot.revive();
         this.grpEncounterSlot.sprCap.visible = true;
         this.grpAvailEncounterSlot.sprCap.visible = true;
         this.txtEncAvail.visible = true;
         this.txtEncResponseLabel.visible = true;
         this.txtEncResponse.visible = true;
         this.grpItemsLayer.kill();
         this.DestroyAppearance(this.grpItemsLayer);
         this.grpGroundLayer.kill();
         this.DestroyAppearance(this.grpGroundLayer);
         this.grpEncounterLayer.kill();
         this.DestroyAppearance(this.grpEncounterLayer);
         this.grpSkillsLayer.kill();
         this.DestroyAppearance(this.grpSkillsLayer);
         this.grpVehicleLayer.kill();
         this.DestroyAppearance(this.grpVehicleLayer);
         this.grpCampLayer.kill();
         this.DestroyAppearance(this.grpCampLayer);
         this.grpBattleLayer.kill();
         this.DestroyAppearance(this.grpBattleLayer);
         this.grpHealthLayer.kill();
         this.DestroyAppearance(this.grpHealthLayer);
         this.grpDMCLayer.kill();
         this.grpDMCLayer.HideVFX();
         this.grpCraftLayer.kill();
         this.DestroyAppearance(this.grpCraftLayer);
         switch(this.m_nState)
         {
            case STATE_NORMAL:
               this.objPlayState.btnEncounter.kill();
               this.objPlayState.sprEncounterBtn.kill();
               if(this.sprPlayer.m_tilCurrentHex.index == 20)
               {
                  this.objPlayState.btnMainMap.kill();
               }
               break;
            case STATE_ENCOUNTER:
               this.grpUseSlot.kill();
               this.objPlayState.grpBtnScreens.callAll("kill");
               this.objPlayState.btnEncounter.revive();
               this.objPlayState.sprEncounterBtn.revive();
               this.objPlayState.sprEncounterBtn.play("on");
               break;
            case STATE_ENCOUNTER_EXCLUSIVE:
               this.objPlayState.grpBtnScreens.callAll("kill");
               this.objPlayState.btnEncounter.revive();
               this.objPlayState.sprEncounterBtn.revive();
               this.objPlayState.sprEncounterBtn.play("on");
               this.grpUseSlot.kill();
               this.grpEncounterSlot.sprCap.visible = false;
               this.grpAvailEncounterSlot.sprCap.visible = false;
               this.txtEncAvail.visible = false;
               this.txtEncResponseLabel.visible = false;
               this.txtEncResponse.visible = false;
               break;
            case STATE_ENCOUNTER_EXCLUSIVETREASURE:
               this.objPlayState.btnEncounter.revive();
               this.objPlayState.sprEncounterBtn.revive();
               this.objPlayState.sprEncounterBtn.play("on");
               this.objPlayState.btnSkills.kill();
               this.objPlayState.btnMainMap.kill();
               this.objPlayState.btnMap.kill();
               this.objPlayState.btnConditions.kill();
               if(this.objEncounter.m_aConditions.indexOf(DM.ENCOUNTER_CRAFT_ID) < 0)
               {
                  this.objPlayState.btnCraft.kill();
               }
               this.grpUseSlot.kill();
               this.grpEncounterSlot.sprCap.visible = false;
               this.grpAvailEncounterSlot.sprCap.visible = false;
               this.txtEncAvail.visible = false;
               this.txtEncResponseLabel.visible = false;
               this.txtEncResponse.visible = false;
               break;
            case STATE_ENCOUNTER_TREASURE:
               this.objPlayState.btnEncounter.revive();
               this.objPlayState.sprEncounterBtn.revive();
               this.objPlayState.sprEncounterBtn.play("on");
               this.objPlayState.btnMainMap.kill();
               if(this.objEncounter.m_aConditions.indexOf(DM.ENCOUNTER_CRAFT_ID) < 0)
               {
                  this.objPlayState.btnCraft.kill();
               }
               this.objPlayState.btnMap.kill();
               this.objPlayState.btnConditions.kill();
               this.grpUseSlot.kill();
               break;
            case STATE_SKILL_EXCLUSIVE:
               this.objPlayState.grpBtnScreens.callAll("kill");
               this.grpUseSlot.kill();
               break;
            case STATE_COMBAT:
               this.grpUseSlot.kill();
               this.objPlayState.grpBtnScreens.callAll("kill");
               this.objPlayState.btnEncounter.revive();
               this.objPlayState.sprEncounterBtn.revive();
               this.objPlayState.sprEncounterBtn.play("on");
               break;
            case STATE_COMBAT_TREASURE:
               this.objPlayState.btnEncounter.revive();
               this.objPlayState.sprEncounterBtn.revive();
               this.objPlayState.sprEncounterBtn.play("on");
               this.objPlayState.btnMainMap.kill();
               this.objPlayState.btnMap.kill();
               this.objPlayState.btnCraft.kill();
               this.grpUseSlot.kill();
         }
         if(param1 < 0)
         {
            param1 = int(this.m_nPanel);
         }
         if(param1 < this.aPanels.length)
         {
            this.aPanels[param1].on = true;
            this.aPanels[param1].revive();
         }
         if(this.m_nMouseModeRestore > 0)
         {
            this.MouseMode(this.m_nMouseModeRestore);
            this.m_nMouseModeRestore = -1;
         }
         switch(param1)
         {
            case PANEL_ITEMS:
               this.grpItemsLayer.revive();
               this.CreateAppearance(this.grpItemsLayer);
               _loc3_ = this.sprPlayer.HasCondition(53);
               this.grpHealthBlood.visible = _loc3_;
               this.grpHealthInfection.visible = _loc3_;
               this.grpHealthPain.visible = _loc3_;
               this.txtCondStatsWarning.visible = !_loc3_;
               this.btnEncViewItems.on = false;
               this.UpdateConditions();
               break;
            case PANEL_VEHICLE:
               this.grpVehicleLayer.revive();
               this.CreateAppearance(this.grpVehicleLayer);
               this.btnEncViewItems.on = false;
               break;
            case PANEL_CRAFT:
               this.grpCraftLayer.revive();
               this.btnYieldPrev.kill();
               this.btnYieldNext.kill();
               this.CreateAppearance(this.grpCraftLayer);
               this.btnEncViewItems.on = false;
               if(this.btnCraftAvail.on)
               {
                  this.ShowAvailIngredients();
               }
               else
               {
                  this.ShowQuickRecipes();
               }
               if(this.m_nMouseMode == GUIInventory.MOUSE_USE)
               {
                  PlayState.m_objInstance.bResetCursor = true;
               }
               break;
            case PANEL_CAMP:
               this.grpCampLayer.revive();
               this.CreateAppearance(this.grpCampLayer);
               this.btnEncViewItems.on = false;
               if(!this.objPlayState.bRest)
               {
                  this.objPlayState.btnRest.kill();
               }
               if(!this.objPlayState.bSleep)
               {
                  this.objPlayState.btnSleep.kill();
               }
               this.objPlayState.btnWake.kill();
               this.grpCampConcealment.visible = this.sprPlayer.HasCondition(122);
               this.grpCampAlertness.visible = this.sprPlayer.HasCondition(207);
               this.grpCampHealRate.visible = this.sprPlayer.HasCondition(53);
               break;
            case PANEL_RESPONSE:
               this.grpEncounterLayer.revive();
               this.CreateAppearance(this.grpEncounterLayer);
               if(this.btnEncViewItems.on == false)
               {
                  this.btnEncViewItems.kill();
               }
               if(this.m_nMouseMode != GUIInventory.MOUSE_TAKE)
               {
                  this.m_nMouseModeRestore = this.m_nMouseMode;
                  this.MouseMode(GUIInventory.MOUSE_TAKE);
               }
               PlayState.m_objInstance.bResetCursor = true;
               break;
            case PANEL_SKILLS:
               this.grpSkillsLayer.revive();
               this.CreateAppearance(this.grpSkillsLayer);
               this.btnEncViewItems.on = false;
               if(this.m_nMouseMode != GUIInventory.MOUSE_TAKE)
               {
                  this.m_nMouseModeRestore = this.m_nMouseMode;
                  this.MouseMode(GUIInventory.MOUSE_TAKE);
               }
               if(!this.m_bAvailSkills)
               {
                  this.grpAvailSkillSlot.kill();
                  this.grpAvailTraitSlot.kill();
                  this.btnSkillsConfirm.kill();
                  this.btnSkillsRandom.kill();
                  this.sprSkillArrowRight.kill();
                  this.sprTraitArrowRight.kill();
                  this.txtSklAvail.kill();
                  this.txtTraitAvail.kill();
                  this.txtTraitInstruct.kill();
                  this.txtSkillTotal.kill();
               }
               else
               {
                  this.SkillPairCheck(null,null);
               }
               PlayState.m_objInstance.bResetCursor = true;
               break;
            case PANEL_BATTLE:
               this.grpEncounterLayer.revive();
               this.btnEncViewItems.kill();
               this.txtEncounter.kill();
               this.grpBattleLayer.revive();
               this.CreateAppearance(this.grpEncounterLayer);
               this.CreateAppearance(this.grpBattleLayer);
               add(this.objPlayState.grpWeatherNode.grpSky);
               this.objPlayState.grpWeatherNode.MoveIcon(GUIValues.GetPoint("GUIBattleScreen.WeatherNode"));
               this.objPlayState.grpWeatherNode.revive();
               if(this.m_nMouseMode != GUIInventory.MOUSE_TAKE)
               {
                  this.m_nMouseModeRestore = this.m_nMouseMode;
                  this.MouseMode(GUIInventory.MOUSE_TAKE);
               }
               PlayState.m_objInstance.bResetCursor = true;
               break;
            case PANEL_DMC:
               this.grpDMCLayer.UpdateButtons();
               this.grpDMCLayer.revive();
               this.grpDMCLayer.ShowVFX();
               PlayState.m_objInstance.bResetCursor = true;
               break;
            case PANEL_HEALTH:
               this.grpHealthLayer.revive();
               this.CreateAppearance(this.grpHealthLayer);
               this.btnEncViewItems.on = false;
               _loc3_ = this.sprPlayer.HasCondition(53);
               this.grpHealthBlood.visible = _loc3_;
               this.grpHealthInfection.visible = _loc3_;
               this.grpHealthPain.visible = _loc3_;
               this.txtCondStatsWarning.visible = !_loc3_;
               this.UpdateConditions();
         }
         this.m_nPanel = param1;
      }
   }
}

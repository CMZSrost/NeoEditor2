package
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.events.*;
   import flash.geom.Rectangle;
   import flash.net.FileReference;
   import flash.ui.Mouse;
   import flash.utils.Dictionary;
   import flash.utils.getTimer;
   import org.flixel.*;
   
   public class PlayState extends FlxState
   {
      
      public static var m_objInstance:PlayState;
      
      public static const HOURS_PER_TURN:int = 1;
      
      public static const HOURS_PER_COMBAT_TURN:Number = 0.01;
      
      public static const GAMESTATE_LOADING:uint = 0;
      
      public static const GAMESTATE_LOADINGCOMPLETE:uint = 1;
      
      public static const GAMESTATE_READINGMAPCOMPLETE:uint = 2;
      
      public static const GAMESTATE_GAMEREADY:uint = 3;
      
      public static const GAMESTATE_DMRUNNING:uint = 4;
      
      public static const GAMESTATE_SLEEPING:uint = 5;
      
      public static const GAMESTATE_INVENTORY:uint = 6;
      
      public static const GAMESTATE_MINIMAP:uint = 7;
      
      public static const GAMESTATE_MAPEDITOR:uint = 8;
      
      public static const GAMESTATE_CONDITIONS:uint = 9;
       
      
      private var nScrollSpeed:int = 8;
      
      private var nScrollSpeedMod:int = 32;
      
      private var nScrollArea:int = 40;
      
      public var camMsg:FlxCamera;
      
      private var rectCam:Rectangle;
      
      private var ptCam:FlxPoint;
      
      public var objDate:Date;
      
      public var objOldDate:Date;
      
      public var nTimeOfDay:int = 0;
      
      private var vMapNames:Vector.<String>;
      
      private var nMapIndex:uint = 1;
      
      public var nMapStyle:uint = 0;
      
      public var nStartState:uint = 3;
      
      public var m_nEndingID:int = 1;
      
      private var nRows:int = 200;
      
      private var nCols:int = 60;
      
      public var aObjectsUnderMouse:Array;
      
      public var ptMouse:FlxPoint;
      
      private var ptMouseScreen:FlxPoint;
      
      public var ptRightClickOrigin:FlxPoint;
      
      public var tilCurrentHex:FlxHexTile;
      
      public var grpWeatherNode:WeatherNode;
      
      public var m_aCreatures:Array;
      
      private var m_aTilesToUpdate:Array;
      
      public var m_aMouseOverItems:Array;
      
      private var grpPopUp:TextPopUp;
      
      private var grpHUD:FlxGroup;
      
      public var grpInventoryUI:GUIInventory;
      
      private var grpMap:FlxGroup;
      
      private var grpCreatures:FlxGroup;
      
      public var grpVFX:FlxGroup;
      
      private var grpLabels:FlxGroup;
      
      public var grpMinimap:Minimap;
      
      private var grpAttackMode:FlxGroup;
      
      public var grpMsg:GUIMessageWindow;
      
      private var grpHelp:GUIEscMenu;
      
      public var grpBtnActions:FlxGroup;
      
      public var grpBtnScreens:FlxGroup;
      
      private var m_sndSkillSelect:FlxSound;
      
      private var m_sndMusic:FlxSound;
      
      private var m_fMusicFade:Number = 10;
      
      private var m_fMusicNext:Number;
      
      private var m_fMusicFadeNext:Number;
      
      private var m_bMusicFading:Boolean;
      
      private var m_fMusicGap:Number = 300;
      
      private var m_fMusicLoopLength:Number = 120;
      
      private var m_fMusicLoopProb:Number = 0.65;
      
      private var txtMovesLeft:FlxText;
      
      private var txtMovesLeftReserve:FlxText;
      
      private var txtPlayerMoney:FlxText;
      
      private var txtAttackMode:FlxText;
      
      private var txtAttackModeCharges:FlxText;
      
      private var txtVersion:FlxText;
      
      private var txtHexInfo:FlxText;
      
      private var sprCamera:FlxSprite;
      
      public var sprPlayer:Player;
      
      public var sprHilight:FlxSprite;
      
      private var sprAttackMode:FlxSprite;
      
      private var sprAttackModeBG:FlxSprite;
      
      private var sprCorner:FlxSprite;
      
      private var sprAttackModeCharge:FlxSprite;
      
      public var sprEncounterBtn:FlxSprite;
      
      private var btnAmodeUp:ImgButton;
      
      private var btnAmodeDn:ImgButton;
      
      public var btnMenu:ImgButton;
      
      public var btnMainMap:ImgButton;
      
      public var btnMap:ImgButton;
      
      public var btnConditions:ImgButton;
      
      public var btnSleep:ImgButton;
      
      public var btnWake:ImgButton;
      
      private var btnWait:ImgButton;
      
      private var btnScavenge:ImgButton;
      
      private var btnHide:ImgButton;
      
      private var btnHideTracks:ImgButton;
      
      private var btnSpy:ImgButton;
      
      private var btnRun:ImgButton;
      
      public var btnRest:ImgButton;
      
      private var btnMovesBG:GUIMenuButton;
      
      public var btnItems:ImgButton;
      
      public var btnVehicle:ImgButton;
      
      public var btnEncounter:ImgButton;
      
      public var btnSkills:ImgButton;
      
      public var btnCamp:ImgButton;
      
      public var btnCraft:ImgButton;
      
      private var m_dictScreenFunctions:Dictionary;
      
      private var grpHungerBar:GUITintBar;
      
      private var grpThirstBar:GUITintBar;
      
      private var grpSleepBar:GUITintBar;
      
      private var grpBodyTempBar:GUITintBar;
      
      private var grpAmbientBar:GUITintBar;
      
      private var grpLoadBar:GUITintBar;
      
      private var grpWoundBar:GUITintBar;
      
      public var m_nGameState:uint;
      
      private var bFading:Boolean = false;
      
      private var m_bCreatureSorted:Boolean = false;
      
      public var bResetCursor:Boolean = true;
      
      public var bPlayerReady:Boolean = false;
      
      public var bDMGame:Boolean = true;
      
      private var bScavenge:Boolean = false;
      
      public var bRest:Boolean = false;
      
      public var bSleep:Boolean = false;
      
      public var bScavTut:Boolean = false;
      
      private var bIgnoreNextMouseUp:Boolean = false;
      
      private var tmapHexes:FlxHexmap;
      
      private var nHexType:int = 0;
      
      private var vCursors:Vector.<Bitmap>;
      
      private var nCursor:int = 0;
      
      private var vFloatyQueue:Vector.<TextFloaty>;
      
      private var nFloatyDelay:int;
      
      private var m_nLastTick:uint = 0;
      
      public var m_objSG:SaveGameData;
      
      private var m_fHoursPassed:Number = 0;
      
      private var m_objGameStats:GameStats;
      
      public var m_txtLoadMessage:FlxText;
      
      private var m_nLoadingStage:int;
      
      private var m_objLoadData:Object;
      
      private var m_vContextButtons:Vector.<ImgButton>;
      
      public var nUpdateTime:int;
      
      public var nStartTime:int;
      
      public var nEndTime:int;
      
      public var bDebug:Boolean;
      
      public var nDebugCounter:int = 0;
      
      private var objFile:FileReference;
      
      public var m_grpLetterbox:GUILetterbox;
      
      public var objStartDate:Date;
      
      public function PlayState()
      {
         this.vMapNames = Vector.<String>(["Excel50x100","MapMiniMichigan.png","GBSCrashSite.png"]);
         this.objStartDate = new Date(2031,8,14,6);
         super();
      }
      
      override public function create() : void
      {
         FlxG.log("PlayState.create starting...");
         if(!Mouse.supportsNativeCursor)
         {
            FlxG.mouse.show();
         }
         FlxG.bgColor = 4278190080;
         FlxG.sounds.callAll("stop");
         this.bDebug = false;
         this.ptMouse = new FlxPoint();
         this.ptMouseScreen = new FlxPoint();
         this.rectCam = new Rectangle();
         this.ptCam = new FlxPoint();
         m_objInstance = this;
         this.m_nGameState = GAMESTATE_LOADING;
         this.m_aMouseOverItems = new Array();
         this.aObjectsUnderMouse = new Array();
         this.vFloatyQueue = new Vector.<TextFloaty>();
         this.nFloatyDelay = 0;
         this.m_nLoadingStage = 0;
         this.m_fMusicNext = this.m_fMusicGap;
         this.m_fMusicFadeNext = this.m_fMusicGap;
         this.m_bMusicFading = false;
         this.m_vContextButtons = new Vector.<ImgButton>();
         this.vCursors = Vector.<Bitmap>([new Bitmap(DataHandler.GetImage("CurDefault.png")),new Bitmap(DataHandler.GetImage("CurTargetValid.png")),new Bitmap(DataHandler.GetImage("CurTargetInvalid.png")),new Bitmap(DataHandler.GetImage("CurHourglass.png")),new Bitmap(DataHandler.GetImage("CurTake.png")),new Bitmap(DataHandler.GetImage("CurTakeStack.png")),new Bitmap(DataHandler.GetImage("CurDrag.png")),new Bitmap(DataHandler.GetImage("CurDragStack.png")),new Bitmap(DataHandler.GetImage("CurUse.png")),new Bitmap(DataHandler.GetImage("CurUseStack.png")),new Bitmap(DataHandler.GetImage("CurDelete.png")),new Bitmap(DataHandler.GetImage("CurDeleteStack.png")),new Bitmap(DataHandler.GetImage("CurSpy.png"))]);
         this.grpMap = new FlxGroup();
         this.grpCreatures = new FlxGroup();
         this.grpHUD = new FlxGroup();
         this.grpVFX = new FlxGroup();
         this.grpLabels = new FlxGroup();
         this.sprCamera = new FlxSprite(300,150,null);
         this.sprCamera.makeGraphic(1,1,0);
         this.grpMinimap = new Minimap();
         this.m_grpLetterbox = new GUILetterbox();
         this.grpHelp = new GUIEscMenu();
         add(this.grpMinimap);
         add(this.grpMap);
         add(this.grpCreatures);
         add(this.sprCamera);
         add(this.grpVFX);
         add(this.grpLabels);
         add(this.grpHUD);
         FlxG.stage.root.addEventListener(Event.FULLSCREEN,this.grpHelp.HandlerFullscreen);
         FlxG.stage.root.addEventListener(Event.RESIZE,this.grpHelp.HandlerResize);
         MapUtils.Initialize();
         if(DataHandler.GetPref("bScavTut") != null)
         {
            this.bScavTut = DataHandler.GetPref("bScavTut");
         }
         FlxG.log("PlayState.create finished.");
         FlxG.watch(this,"nUpdateTime");
         FlxG.watch(this,"nStartTime");
         FlxG.watch(this,"nEndTime");
      }
      
      override public function update() : void
      {
         var _loc1_:uint = 0;
         this.nStartTime = getTimer();
         super.update();
         if(this.grpHelp.alive)
         {
            this.KeyHandler();
            return;
         }
         switch(this.m_nGameState)
         {
            case GAMESTATE_CONDITIONS:
               this.MouseHandler();
               this.KeyHandler();
               this.UpdateMusic();
               break;
            case GAMESTATE_MAPEDITOR:
               this.MouseHandler();
               this.KeyHandler();
               break;
            case GAMESTATE_MINIMAP:
               this.MouseHandler();
               this.KeyHandler();
               this.UpdateMusic();
               break;
            case GAMESTATE_INVENTORY:
               this.MouseHandler();
               this.KeyHandler();
               this.UpdateMusic();
               break;
            case GAMESTATE_DMRUNNING:
               this.MouseHandler();
               this.KeyHandler();
               this.FloatyUpdate();
               this.UpdateMusic();
               if(this.m_bCreatureSorted == false)
               {
                  this.m_aCreatures.sortOn("m_fLeader");
                  this.m_bCreatureSorted = true;
               }
               switch(DM.MoveCreatures(this.m_aCreatures))
               {
                  case DM.MOVERESULT_MOVED:
                     DM.NextEncounter();
                     break;
                  case DM.MOVERESULT_WAIT:
                     break;
                  case DM.MOVERESULT_DONE:
                     _loc1_ = FlxU.getTicks();
                     if(_loc1_ - this.m_nLastTick > 1000)
                     {
                        this.m_nLastTick = FlxU.getTicks();
                        if(this.sprPlayer.Asleep)
                        {
                           this.sprPlayer.Asleep = false;
                        }
                        if(this.sprPlayer.Asleep || this.sprPlayer.Resting)
                        {
                           this.EndPlayerTurn(PlayState.HOURS_PER_TURN);
                           this.EndDMTurn(this.m_fHoursPassed,true,true);
                        }
                        else if(this.m_fHoursPassed > 0)
                        {
                           this.EndDMTurn(this.m_fHoursPassed,true,true);
                           if(GUIEscMenu.bAutosave && !DataHandler.SaveGame(this.FinishAutoSave))
                           {
                              this.grpMsg.MessageFloaty("Error: Unable to access save data location.",false,null,GUIMessageWindow.COLOR_BAD);
                           }
                        }
                        else
                        {
                           DM.EncounterCheck(this.tilCurrentHex,true);
                           if(DM.m_aEventQueue.length > 0)
                           {
                              DM.NextEncounter();
                           }
                           else
                           {
                              this.Mode(GAMESTATE_GAMEREADY);
                           }
                        }
                     }
               }
               break;
            case GAMESTATE_SLEEPING:
               this.MouseHandler();
               this.KeyHandler();
               this.FloatyUpdate();
               this.UpdateMusic();
               break;
            case GAMESTATE_GAMEREADY:
               this.m_bCreatureSorted = false;
               this.MouseHandler();
               this.KeyHandler();
               this.FloatyUpdate();
               this.UpdateMusic();
               break;
            case GAMESTATE_READINGMAPCOMPLETE:
               this.MouseHandler();
               this.KeyHandler();
               if(this.bPlayerReady || this.m_objSG != null)
               {
                  this.StartGame();
               }
               break;
            case GAMESTATE_LOADINGCOMPLETE:
               this.KeyHandler();
               MapUtils.ReadTerrainBitmapRow();
               break;
            case GAMESTATE_LOADING:
               if(DataHandler.bLoadingComplete)
               {
                  this.FadeIn();
               }
         }
         this.nEndTime = getTimer();
         this.nUpdateTime = this.nEndTime - this.nStartTime;
      }
      
      override public function destroy() : void
      {
         var _loc1_:String = null;
         var _loc2_:Creature = null;
         var _loc3_:Object = null;
         var _loc4_:ImgButton = null;
         var _loc5_:int = 0;
         FlxG.stage.root.removeEventListener(Event.FULLSCREEN,this.grpHelp.HandlerFullscreen);
         FlxG.stage.root.removeEventListener(Event.RESIZE,this.grpHelp.HandlerResize);
         this.camMsg = DataHandler.DestroyObject(this.camMsg);
         this.objDate = null;
         this.objOldDate = null;
         this.objStartDate = null;
         for each(_loc1_ in this.vMapNames)
         {
            _loc1_ = null;
         }
         this.vMapNames = null;
         this.aObjectsUnderMouse = null;
         this.ptMouse = null;
         this.ptMouseScreen = null;
         this.ptRightClickOrigin = null;
         this.tilCurrentHex = null;
         this.grpWeatherNode = DataHandler.DestroyObject(this.grpWeatherNode);
         for each(_loc2_ in this.m_aCreatures)
         {
            _loc2_.destroy();
         }
         this.m_aCreatures = null;
         this.m_aTilesToUpdate = null;
         this.grpPopUp = DataHandler.DestroyObject(this.grpPopUp);
         this.grpHUD = DataHandler.DestroyObject(this.grpHUD);
         this.grpInventoryUI = DataHandler.DestroyObject(this.grpInventoryUI);
         this.grpMap = DataHandler.DestroyObject(this.grpMap);
         this.grpCreatures = DataHandler.DestroyObject(this.grpCreatures);
         this.grpVFX = DataHandler.DestroyObject(this.grpVFX);
         this.grpLabels = DataHandler.DestroyObject(this.grpLabels);
         this.grpMinimap = DataHandler.DestroyObject(this.grpMinimap);
         this.grpAttackMode = DataHandler.DestroyObject(this.grpAttackMode);
         this.grpMsg = DataHandler.DestroyObject(this.grpMsg);
         this.grpHelp = DataHandler.DestroyObject(this.grpHelp);
         this.grpBtnActions = DataHandler.DestroyObject(this.grpBtnActions);
         this.grpBtnScreens = DataHandler.DestroyObject(this.grpBtnScreens);
         this.m_sndSkillSelect = DataHandler.DestroyObject(this.m_sndSkillSelect);
         this.m_sndMusic = DataHandler.DestroyObject(this.m_sndMusic);
         this.txtMovesLeft = DataHandler.DestroyObject(this.txtMovesLeft);
         this.txtMovesLeftReserve = DataHandler.DestroyObject(this.txtMovesLeftReserve);
         this.txtPlayerMoney = DataHandler.DestroyObject(this.txtPlayerMoney);
         this.txtAttackMode = DataHandler.DestroyObject(this.txtAttackMode);
         this.txtAttackModeCharges = DataHandler.DestroyObject(this.txtAttackModeCharges);
         this.txtVersion = DataHandler.DestroyObject(this.txtVersion);
         this.txtHexInfo = DataHandler.DestroyObject(this.txtHexInfo);
         this.sprCamera = DataHandler.DestroyObject(this.sprCamera);
         this.sprPlayer = DataHandler.DestroyObject(this.sprPlayer);
         this.sprHilight = DataHandler.DestroyObject(this.sprHilight);
         this.sprAttackMode = DataHandler.DestroyObject(this.sprAttackMode);
         this.sprAttackModeBG = DataHandler.DestroyObject(this.sprAttackModeBG);
         this.sprCorner = DataHandler.DestroyObject(this.sprCorner);
         this.sprAttackModeCharge = DataHandler.DestroyObject(this.sprAttackModeCharge);
         this.sprEncounterBtn = DataHandler.DestroyObject(this.sprEncounterBtn);
         this.btnAmodeUp = DataHandler.DestroyObject(this.btnAmodeUp);
         this.btnAmodeDn = DataHandler.DestroyObject(this.btnAmodeDn);
         this.btnMenu = DataHandler.DestroyObject(this.btnMenu);
         this.btnMainMap = DataHandler.DestroyObject(this.btnMainMap);
         this.btnMap = DataHandler.DestroyObject(this.btnMap);
         this.btnConditions = DataHandler.DestroyObject(this.btnConditions);
         this.btnSleep = DataHandler.DestroyObject(this.btnSleep);
         this.btnWake = DataHandler.DestroyObject(this.btnWake);
         this.btnWait = DataHandler.DestroyObject(this.btnWait);
         this.btnScavenge = DataHandler.DestroyObject(this.btnScavenge);
         this.btnHide = DataHandler.DestroyObject(this.btnHide);
         this.btnHideTracks = DataHandler.DestroyObject(this.btnHideTracks);
         this.btnSpy = DataHandler.DestroyObject(this.btnSpy);
         this.btnRun = DataHandler.DestroyObject(this.btnRun);
         this.btnRest = DataHandler.DestroyObject(this.btnRest);
         this.btnMovesBG = DataHandler.DestroyObject(this.btnMovesBG);
         this.btnItems = DataHandler.DestroyObject(this.btnItems);
         this.btnVehicle = DataHandler.DestroyObject(this.btnVehicle);
         this.btnEncounter = DataHandler.DestroyObject(this.btnEncounter);
         this.btnSkills = DataHandler.DestroyObject(this.btnSkills);
         this.btnCamp = DataHandler.DestroyObject(this.btnCamp);
         this.btnCraft = DataHandler.DestroyObject(this.btnCraft);
         for(_loc3_ in this.m_dictScreenFunctions)
         {
            delete this.m_dictScreenFunctions[_loc3_];
         }
         this.m_dictScreenFunctions = null;
         this.grpHungerBar = DataHandler.DestroyObject(this.grpHungerBar);
         this.grpThirstBar = DataHandler.DestroyObject(this.grpThirstBar);
         this.grpSleepBar = DataHandler.DestroyObject(this.grpSleepBar);
         this.grpBodyTempBar = DataHandler.DestroyObject(this.grpBodyTempBar);
         this.grpAmbientBar = DataHandler.DestroyObject(this.grpAmbientBar);
         this.grpLoadBar = DataHandler.DestroyObject(this.grpLoadBar);
         this.grpWoundBar = DataHandler.DestroyObject(this.grpWoundBar);
         this.tmapHexes = DataHandler.DestroyObject(this.tmapHexes);
         this.vCursors = null;
         if(this.vFloatyQueue != null)
         {
            _loc5_ = 0;
            while(_loc5_ < this.vFloatyQueue.length)
            {
               this.vFloatyQueue[_loc5_].destroy();
               this.vFloatyQueue[_loc5_] = null;
               _loc5_++;
            }
            this.vFloatyQueue = null;
         }
         this.m_objSG = DataHandler.DestroyObject(this.m_objSG);
         this.m_txtLoadMessage = DataHandler.DestroyObject(this.m_txtLoadMessage);
         this.m_grpLetterbox = DataHandler.DestroyObject(this.m_grpLetterbox);
         MapUtils.removeEventListener(MapUtils.strFinishedBitmap,this.ReadMapComplete);
         MapUtils.destroy();
         DM.destroy();
         this.grpPopUp = DataHandler.DestroyObject(this.grpPopUp);
         m_objInstance = null;
         this.m_aMouseOverItems = null;
         for each(_loc4_ in this.m_vContextButtons)
         {
            _loc4_.destroy();
         }
         this.m_vContextButtons = null;
         super.destroy();
      }
      
      private function FinishAutoSave(param1:Boolean) : void
      {
         if(!param1)
         {
            this.grpMsg.MessageFloaty("Error: Unable to save.",false,null,GUIMessageWindow.COLOR_BAD);
         }
      }
      
      public function Mode(param1:uint) : void
      {
         var _loc2_:FlxHexTile = null;
         this.btnMainMap.on = false;
         this.btnConditions.on = false;
         this.btnEncounter.on = false;
         this.btnItems.on = false;
         this.btnSkills.on = false;
         this.btnMap.on = false;
         this.btnVehicle.on = false;
         this.btnCamp.on = false;
         this.btnCraft.on = false;
         switch(param1)
         {
            case GAMESTATE_CONDITIONS:
               if(!this.grpInventoryUI.Hide())
               {
                  return;
               }
               this.grpHUD.revive();
               this.grpLabels.kill();
               this.grpVFX.kill();
               this.grpHUD.active = true;
               this.grpMap.kill();
               this.grpCreatures.kill();
               this.grpBtnActions.callAll("kill");
               this.grpInventoryUI.UpdateScreens();
               if(this.m_nGameState == GAMESTATE_GAMEREADY)
               {
                  this.btnEncounter.kill();
                  this.sprEncounterBtn.kill();
               }
               this.grpMinimap.Hide();
               this.ChangeCursor();
               this.grpWeatherNode.UpdateSounds(0.5);
               break;
            case GAMESTATE_MAPEDITOR:
               for each(_loc2_ in this.tmapHexes.getTiles())
               {
                  _loc2_.nExploredState = 0;
               }
               this.tmapHexes.setDirty();
               this.sprHilight.pixels = this.tmapHexes.GetImage(this.nHexType);
               this.grpHUD.revive();
               this.grpVFX.kill();
               this.grpMap.revive();
               this.grpLabels.revive();
               this.grpCreatures.kill();
               this.grpMsg.kill();
               this.grpMsg.active = false;
               this.camMsg.visible = false;
               this.grpInventoryUI.Hide();
               this.grpMinimap.Hide();
               this.grpBtnActions.callAll("kill");
               this.grpBtnScreens.callAll("kill");
               this.grpAmbientBar.kill();
               this.grpBodyTempBar.kill();
               this.grpHungerBar.kill();
               this.grpLoadBar.kill();
               this.grpWoundBar.kill();
               this.grpSleepBar.kill();
               this.grpThirstBar.kill();
               this.txtPlayerMoney.kill();
               this.grpAttackMode.kill();
               this.txtMovesLeft.kill();
               this.ChangeCursor();
               this.grpWeatherNode.StopSounds();
               break;
            case GAMESTATE_MINIMAP:
               if(!this.grpInventoryUI.Hide())
               {
                  return;
               }
               this.grpHUD.revive();
               this.grpMap.kill();
               this.grpLabels.kill();
               this.grpVFX.kill();
               this.grpCreatures.kill();
               this.grpBtnActions.callAll("kill");
               this.btnEncounter.kill();
               this.sprEncounterBtn.kill();
               if(this.tilCurrentHex.index == 20)
               {
                  this.btnMainMap.kill();
               }
               this.ChangeCursor();
               this.grpMinimap.Show();
               this.grpWeatherNode.UpdateSounds(0.5);
               break;
            case GAMESTATE_INVENTORY:
               this.grpHUD.revive();
               this.grpLabels.kill();
               this.grpVFX.kill();
               this.grpMap.kill();
               this.grpCreatures.kill();
               this.grpBtnActions.callAll("kill");
               this.btnEncounter.kill();
               this.sprEncounterBtn.kill();
               this.ChangeCursor();
               this.grpInventoryUI.Show();
               this.grpMinimap.Hide();
               this.grpWeatherNode.UpdateSounds(0.5);
               break;
            case GAMESTATE_DMRUNNING:
               this.grpHUD.revive();
               this.grpWeatherNode.MoveIcon(GUIValues.GetPoint("WeatherNode"));
               if(!this.sprPlayer.Asleep)
               {
                  this.grpMap.revive();
                  this.grpLabels.revive();
                  this.grpVFX.revive();
                  this.grpCreatures.revive();
               }
               this.grpMsg.revive();
               this.grpHUD.active = true;
               this.grpMsg.active = true;
               this.camMsg.visible = true;
               this.grpInventoryUI.Hide();
               this.grpWeatherNode.grpSky.revive();
               this.grpMinimap.Hide();
               this.grpBtnActions.callAll("kill");
               this.grpBtnScreens.callAll("kill");
               if(this.sprPlayer.Resting)
               {
                  this.btnRest.revive();
               }
               this.sprHilight.visible = false;
               this.ChangeCursor(3);
               break;
            case GAMESTATE_SLEEPING:
               this.grpHUD.revive();
               this.grpMsg.revive();
               this.grpVFX.revive();
               this.grpHUD.active = true;
               this.grpMsg.active = true;
               this.camMsg.visible = true;
               this.grpInventoryUI.Hide();
               this.grpWeatherNode.grpSky.revive();
               this.grpMinimap.Hide();
               this.grpMap.kill();
               this.grpLabels.kill();
               this.grpCreatures.kill();
               this.grpBtnActions.callAll("kill");
               this.grpBtnScreens.callAll("kill");
               this.sprHilight.visible = false;
               this.btnWake.revive();
               this.ChangeCursor();
               break;
            case GAMESTATE_GAMEREADY:
               if(!this.grpInventoryUI.Hide())
               {
                  return;
               }
               this.grpHUD.active = true;
               this.grpMsg.active = true;
               this.camMsg.visible = true;
               this.grpWeatherNode.MoveIcon(GUIValues.GetPoint("WeatherNode"));
               this.grpHUD.revive();
               this.grpMap.revive();
               this.grpLabels.revive();
               this.grpMsg.revive();
               this.grpVFX.revive();
               this.grpCreatures.revive();
               this.grpWeatherNode.grpSky.revive();
               this.grpMinimap.Hide();
               this.grpBtnActions.callAll("revive");
               this.grpBtnScreens.callAll("revive");
               this.btnWake.kill();
               this.sprHilight.visible = true;
               this.btnMainMap.on = true;
               this.btnEncounter.kill();
               this.sprEncounterBtn.kill();
               this.grpAttackMode.revive();
               this.grpWeatherNode.UpdateSounds(1);
               this.UpdatePlayerUI();
               if(!this.bRest)
               {
                  this.btnRest.kill();
               }
               if(!this.bSleep)
               {
                  this.btnSleep.kill();
               }
               if(!this.bScavenge)
               {
                  this.btnScavenge.kill();
               }
               this.ChangeCursor();
               break;
            case GAMESTATE_READINGMAPCOMPLETE:
               this.grpMap.kill();
               this.grpLabels.kill();
               this.grpVFX.kill();
               this.grpCreatures.kill();
               if(this.m_objSG != null)
               {
                  this.grpInventoryUI.Hide();
               }
               else
               {
                  this.grpInventoryUI.Show();
               }
               this.grpHUD.revive();
               this.grpWeatherNode.grpSky.kill();
               this.grpBtnActions.callAll("kill");
               this.grpBtnScreens.callAll("kill");
               break;
            case GAMESTATE_LOADINGCOMPLETE:
               this.grpMap.kill();
               this.grpLabels.kill();
               this.grpVFX.kill();
               this.grpCreatures.kill();
               if(this.m_objSG != null)
               {
                  this.grpInventoryUI.Hide();
               }
               else
               {
                  this.grpInventoryUI.Show();
                  this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_SKILLS);
                  this.m_sndSkillSelect = FlxG.loadSound(cueSkillSelect,GUIEscMenu.m_fMusicVolume);
                  this.m_sndSkillSelect.play();
               }
         }
         this.m_nGameState = param1;
      }
      
      public function UpdateMusic() : void
      {
         var _loc1_:Number = NaN;
         var _loc2_:int = 0;
         var _loc3_:* = false;
         var _loc4_:Vector.<Class> = null;
         var _loc5_:Number = NaN;
         this.m_fMusicNext -= FlxG.elapsed;
         this.m_fMusicFadeNext -= FlxG.elapsed;
         if(this.m_sndMusic != null && this.m_sndMusic.volume != GUIEscMenu.m_fMusicVolume)
         {
            this.m_sndMusic.volume = GUIEscMenu.m_fMusicVolume;
         }
         if(this.m_sndSkillSelect != null && this.m_sndSkillSelect.volume != GUIEscMenu.m_fMusicVolume)
         {
            this.m_sndSkillSelect.volume = GUIEscMenu.m_fMusicVolume;
         }
         if(this.m_sndMusic != null && this.m_sndMusic.active && (this.m_bMusicFading == false && (this.m_fMusicFadeNext <= 0 && this.m_fMusicFadeNext < this.m_fMusicNext || this.m_fMusicNext <= this.m_fMusicFade)))
         {
            _loc1_ = Math.min(this.m_fMusicFade,this.m_fMusicNext);
            this.m_sndMusic.fadeOut(_loc1_ + 1);
            this.m_bMusicFading = true;
         }
         else if(this.m_fMusicNext <= 0)
         {
            _loc3_ = Math.random() < this.m_fMusicLoopProb;
            _loc4_ = DataHandler.m_vWildMusicCues;
            if(this.tilCurrentHex.index == 20)
            {
               _loc3_ = false;
               _loc4_ = DataHandler.m_vDMCMusicCues;
            }
            if(_loc3_)
            {
               _loc2_ = Math.random() * Math.floor(DataHandler.m_vWildMusicLoops.length);
               this.m_sndMusic = FlxG.loadSound(DataHandler.m_vWildMusicLoops[_loc2_],GUIEscMenu.m_fMusicVolume,true);
               _loc5_ = this.m_sndMusic._sound.length * Math.random();
               this.m_sndMusic.fadeIn(this.m_fMusicFade,_loc5_);
               this.m_fMusicFadeNext = (0.5 + Math.random()) * this.m_fMusicLoopLength;
               this.m_fMusicNext = this.m_fMusicFadeNext + this.m_fMusicGap + Math.random() * this.m_fMusicGap;
            }
            else
            {
               _loc2_ = Math.random() * Math.floor(_loc4_.length);
               this.m_sndMusic = FlxG.loadSound(_loc4_[_loc2_]);
               this.m_sndMusic.play(true);
               this.m_sndMusic.volume = GUIEscMenu.m_fMusicVolume;
               this.m_fMusicFadeNext = this.m_sndMusic._sound.length / 1000;
               this.m_fMusicNext = this.m_sndMusic._sound.length / 1000 + this.m_fMusicGap + Math.random() * this.m_fMusicGap;
            }
            this.m_bMusicFading = false;
         }
      }
      
      private function GetMusicGap() : Number
      {
         return this.m_fMusicGap + Math.random() * this.m_fMusicGap;
      }
      
      private function FadeIn() : void
      {
         FlxG.flash(4278190080,0.5);
         this.LoadingComplete();
      }
      
      private function LoadingComplete() : void
      {
         if(this.nMapStyle == 2)
         {
            MapUtils.addEventListener(MapUtils.strFinishedBitmap,this.ReadMapComplete);
            MapUtils.LoadTerrainBitmap(this.vMapNames[this.nMapIndex]);
         }
         this.SetupHUD();
         this.objStartDate = new Date(DataHandler.GetGameVars()["nStartDateYear"],DataHandler.GetGameVars()["nStartDateMonth"] - 1,DataHandler.GetGameVars()["nStartDateDay"],DataHandler.GetGameVars()["nStartDateHour"]);
         if(isNaN(this.objStartDate.getTime()))
         {
            this.objStartDate = new Date(2031,8,14,6);
         }
         this.objDate = new Date(this.objStartDate.toString());
         this.objOldDate = new Date(this.objStartDate.toString());
         this.objOldDate.setHours(this.objOldDate.getHours() - PlayState.HOURS_PER_TURN);
         this.sprPlayer = new Player();
         this.sprPlayer.Initialize();
         this.UpdatePlayerUI();
         this.m_aMouseOverItems.push(this.sprPlayer);
         this.grpInventoryUI = new GUIInventory(this.sprPlayer,this.PlayerReady);
         add(this.grpInventoryUI);
         this.grpInventoryUI.setAll("cameras",[FlxG.camera]);
         add(this.grpHelp);
         add(this.m_grpLetterbox);
         DM.Initialize();
         if(this.m_objSG != null)
         {
            this.grpInventoryUI.m_bAvailSkills = false;
         }
         else
         {
            this.sprPlayer.m_bCondQueue = true;
         }
         this.Mode(GAMESTATE_LOADINGCOMPLETE);
         this.SetRes();
         GUIValues.m_bOverrideZoom = false;
         MapUtils.addEventListener(MapUtils.strFinishedBitmap,this.ReadMapComplete);
         switch(this.nMapStyle)
         {
            case 1:
               MapUtils.LoadMapDef(this.vMapNames[this.nMapIndex]);
               break;
            case 3:
               MapUtils.GenerateTerrain(this.nCols,this.nRows);
               break;
            case 2:
               break;
            case 0:
            default:
               MapUtils.RandomizeTerrain(this.vMapNames[this.nMapIndex]);
         }
      }
      
      private function ReadMapComplete(param1:Event) : void
      {
         this.grpMsg.MessageFloaty("Reading map complete. Starting game.",false);
         MapUtils.removeEventListener(MapUtils.strFinishedBitmap,this.ReadMapComplete);
         this.tmapHexes = MapUtils.tmapHexes;
         this.nCols = this.tmapHexes.widthInTiles;
         this.nRows = this.tmapHexes.heightInTiles;
         this.grpMap.add(this.tmapHexes);
         var _loc2_:FlxPoint = GUIValues.GetPoint("offset");
         FlxG.camera.setBounds(-_loc2_.x,-_loc2_.y,this.tmapHexes.width + _loc2_.x * 2,this.tmapHexes.height + _loc2_.y * 2 + GUIValues.GetInt("PlayState.camMsg.minheight"),true);
         FlxG.camera.follow(this.sprCamera);
         this.Mode(GAMESTATE_READINGMAPCOMPLETE);
      }
      
      private function PlayerReady() : void
      {
         var _loc1_:FlxCamera = null;
         if(this.grpInventoryUI.ConfirmSkills() == false)
         {
            return;
         }
         this.sprPlayer.ProcessConditionQueue();
         this.grpInventoryUI.m_nState = GUIInventory.STATE_NORMAL;
         this.grpInventoryUI.Hide();
         this.m_sndSkillSelect.fadeOut(1);
         this.grpHUD.active = false;
         this.grpMsg.active = false;
         this.grpHUD.visible = false;
         this.grpMsg.visible = false;
         this.camMsg.visible = false;
         for each(_loc1_ in FlxG.cameras)
         {
            _loc1_.stopFX();
         }
         FlxG.flash(4278190080,0.5);
         this.bPlayerReady = true;
         this.m_fMusicNext = this.GetMusicGap();
      }
      
      private function SetupHUD() : void
      {
         this.grpWeatherNode = new WeatherNode(this.grpVFX);
         this.m_aMouseOverItems.push(this.grpWeatherNode);
         this.grpPopUp = new TextPopUp();
         this.m_dictScreenFunctions = new Dictionary();
         var _loc1_:int = 63 * 1;
         var _loc2_:int = (FlxG.height - GUIValues.GetInt("PlayState.camMsg.minheight")) * 1;
         this.camMsg = new FlxCamera(_loc1_,_loc2_,FlxG.width - (_loc1_ + 200) / 1,GUIValues.GetInt("PlayState.camMsg.maxheight"),1);
         this.camMsg.setBounds(0,0,this.camMsg.width,this.camMsg.height);
         FlxG.addCamera(this.camMsg);
         this.grpMsg = new GUIMessageWindow(GUIValues.GetInt("PlayState.camMsg.minheight"),this.camMsg.width);
         var _loc3_:uint = 2;
         var _loc4_:uint = 0;
         var _loc5_:uint = uint(FlxG.width - 63);
         var _loc6_:uint = 2;
         var _loc7_:uint = 59;
         var _loc8_:BitmapData = new BitmapData(_loc7_,22,false,4281545523);
         this.grpBtnActions = new FlxGroup();
         this.grpBtnScreens = new FlxGroup();
         this.btnMenu = new ImgButton("btn_main_help_dn.png","btn_main_help.png","btn_main_help_on.png","btn_main_help_on.png",_loc3_ - 2,_loc4_,this.ToggleEscMenu);
         this.btnMenu.m_strPopUpText = "打开/关闭游戏菜单 (F1)";
         this.m_aMouseOverItems.push(this.btnMenu);
         _loc4_ += 0 + this.btnMenu.height;
         this.txtMovesLeft = new FlxText(_loc3_,_loc4_,_loc7_,"Moves Left: 0/0");
         this.txtVersion = new FlxText(_loc3_,FlxG.height - 12,_loc7_,DataHandler.m_strVersion);
         this.txtHexInfo = new FlxText(0,0,200,"Hex Movement and Tracking Info");
         this.btnMovesBG = new GUIMenuButton(_loc8_,_loc8_,_loc8_,_loc8_,_loc3_,_loc4_ + 1);
         this.m_aMouseOverItems.push(this.btnMovesBG);
         _loc4_ += 24;
         this.grpHungerBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"饥饿:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.m_aMouseOverItems.push(this.grpHungerBar);
         _loc4_ += this.grpHungerBar.height;
         this.grpThirstBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"口渴:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.m_aMouseOverItems.push(this.grpThirstBar);
         _loc4_ += this.grpThirstBar.height;
         this.grpSleepBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"精力:",new Array(4294533376,4294365184,4287167745));
         this.m_aMouseOverItems.push(this.grpSleepBar);
         _loc4_ += this.grpSleepBar.height;
         this.grpLoadBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"负重:",new Array(4294533376,4294365184,4287167745));
         this.m_aMouseOverItems.push(this.grpLoadBar);
         _loc4_ += this.grpLoadBar.height;
         this.grpBodyTempBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"温度:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.m_aMouseOverItems.push(this.grpBodyTempBar);
         _loc4_ += this.grpBodyTempBar.height;
         this.grpAmbientBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"温度:",new Array(4283675117,4291545534,4293733646));
         this.m_aMouseOverItems.push(this.grpAmbientBar);
         _loc4_ += this.grpAmbientBar.height;
         this.grpWoundBar = new GUITintBar(_loc3_,_loc4_,_loc7_,"伤势:",new Array(4292345857,4294533376,4294365184,4287167745));
         this.m_aMouseOverItems.push(this.grpWoundBar);
         _loc4_ += this.grpWoundBar.height;
         this.txtPlayerMoney = new FlxText(_loc3_,_loc4_,FlxG.width / 2,"");
         _loc4_ += 15;
         this.btnWait = new ImgButton("btn_main_wait_dn.png","btn_main_wait.png","btn_main_wait_on.png","btn_main_wait_on.png",_loc5_,_loc6_,this.BtnEndTurn);
         this.btnWait.m_strPopUpText = "结束回合，回复行动点数 (空格键)";
         this.m_aMouseOverItems.push(this.btnWait);
         _loc6_ += 0 + this.btnWait.height;
         this.btnSleep = new ImgButton("btn_main_sleep_dn.png","btn_main_sleep.png","btn_main_sleep_on.png","btn_main_sleep_on.png",_loc5_,_loc6_,this.PlayerSleep);
         this.btnSleep.m_strPopUpText = "躺下休息，回复疲劳度\n\n(注意！睡眠期间是无防备的，所以要在安全点休息)";
         this.m_aMouseOverItems.push(this.btnSleep);
         this.btnWake = new ImgButton("btn_main_wake_dn.png","btn_main_wake.png","btn_main_wake_on.png","btn_main_wake_on.png",_loc5_,_loc6_,this.PlayerSleep);
         this.btnWake.m_strPopUpText = "试着清醒起来.";
         this.m_aMouseOverItems.push(this.btnWake);
         _loc6_ += 0 + this.btnSleep.height;
         this.btnRest = new ImgButton("btn_main_rest_dn.png","btn_main_rest.png","btn_main_rest_on.png","btn_main_rest_on.png",_loc5_,_loc6_,this.PlayerRest);
         this.btnRest.m_strPopUpText = "休息直至重伤治愈 (环境有变化或者有生物出现会马上停止休息)";
         this.m_aMouseOverItems.push(this.btnRest);
         _loc6_ += 0 + this.btnRest.height;
         this.btnRun = new ImgButton("btn_main_run_dn.png","btn_main_run.png","btn_main_run_on.png","btn_main_run_on.png",_loc5_,_loc6_,this.PlayerRun);
         this.btnRun.m_strPopUpText = "开始/停止奔跑.";
         this.m_aMouseOverItems.push(this.btnRun);
         this.txtMovesLeftReserve = new FlxText(_loc5_ - 25,_loc6_ + 4,30,"0/0");
         _loc6_ += 0 + this.btnSleep.height;
         this.btnHide = new ImgButton("btn_main_hide_dn.png","btn_main_hide.png","btn_main_hide_on.png","btn_main_hide_on.png",_loc5_,_loc6_,this.PlayerHide);
         this.btnHide.m_strPopUpText = "开始/停止潜行 (会消耗更多行动点数)";
         this.m_aMouseOverItems.push(this.btnHide);
         _loc6_ += 0 + this.btnSleep.height;
         this.btnHideTracks = new ImgButton("btn_main_hidetracks_dn.png","btn_main_hidetracks.png","btn_main_hidetracks_on.png","btn_main_hidetracks_on.png",_loc5_,_loc6_,this.PlayerHideTracks);
         this.btnHideTracks.m_strPopUpText = "掩盖此处的痕迹 (1行动点)";
         this.m_aMouseOverItems.push(this.btnHideTracks);
         _loc6_ += 0 + this.btnSleep.height;
         this.btnSpy = new ImgButton("btn_main_spy_dn.png","btn_main_spy.png","btn_main_spy_on.png","btn_main_spy_on.png",_loc5_,_loc6_,this.PlayerSpy);
         this.btnSpy.m_strPopUpText = "侦查该处或该处生物 (1行动点)";
         this.m_aMouseOverItems.push(this.btnSpy);
         _loc6_ += 0 + this.btnSleep.height;
         this.btnScavenge = new ImgButton("btn_main_scavenge_dn.png","btn_main_scavenge.png","btn_main_scavenge_on.png","btn_main_scavenge_on.png",_loc5_,_loc6_,this.PlayerScavenge);
         this.btnScavenge.m_strPopUpText = "搜索周围，寻找有用的东西 (E)";
         this.m_dictScreenFunctions[this.btnScavenge] = this.PlayerScavenge;
         this.m_aMouseOverItems.push(this.btnScavenge);
         _loc6_ += 0 + this.btnSleep.height;
         this.btnMainMap = new ImgButton("btn_main_hexmap_on.png","btn_main_hexmap.png","btn_main_hexmap_on.png","btn_main_hexmap_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnMainMap] = this.ShowMain;
         this.btnMainMap.m_strPopUpText = "显示地图 (TAB)";
         this.m_aMouseOverItems.push(this.btnMainMap);
         _loc4_ += 0 + this.btnMainMap.height;
         this.btnConditions = new ImgButton("btn_main_cond_on.png","btn_main_cond.png","btn_main_cond_on.png","btn_main_cond_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnConditions] = this.ShowConditions;
         this.btnConditions.m_strPopUpText = "打开/关闭 角色菜单 (C)";
         this.m_aMouseOverItems.push(this.btnConditions);
         _loc4_ += 0 + this.btnConditions.height;
         this.btnMap = new ImgButton("btn_main_map_on.png","btn_main_map.png","btn_main_map_on.png","btn_main_map_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnMap] = this.ShowMinimap;
         this.m_aMouseOverItems.push(this.btnMap);
         this.btnMap.m_strPopUpText = "显示大地图 (M)";
         _loc4_ += 0 + this.btnMap.height;
         this.btnItems = new ImgButton("btn_inv_menu_items_on.png","btn_inv_menu_items.png","btn_inv_menu_items_on.png","btn_inv_menu_items_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnItems] = this.ShowItems;
         this.btnItems.m_strPopUpText = "打开/关闭物品菜单 (Q)";
         this.m_aMouseOverItems.push(this.btnItems);
         _loc4_ += 0 + this.btnItems.height;
         this.btnVehicle = new ImgButton("btn_inv_menu_vehicle_on.png","btn_inv_menu_vehicle.png","btn_inv_menu_vehicle_on.png","btn_inv_menu_vehicle_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnVehicle] = this.ShowVehicle;
         this.btnVehicle.m_strPopUpText = "打开/关闭载具菜单 (V)";
         this.m_aMouseOverItems.push(this.btnVehicle);
         _loc4_ += 0 + this.btnVehicle.height;
         this.btnCamp = new ImgButton("btn_inv_menu_camp_on.png","btn_inv_menu_camp.png","btn_inv_menu_camp_on.png","btn_inv_menu_camp_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnCamp] = this.ShowCamp;
         this.btnCamp.m_strPopUpText = "打开/关闭营地菜单 (R)";
         this.m_aMouseOverItems.push(this.btnCamp);
         _loc4_ += 0 + this.btnCamp.height;
         this.btnCraft = new ImgButton("btn_inv_menu_craft_on.png","btn_inv_menu_craft.png","btn_inv_menu_craft_on.png","btn_inv_menu_craft_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnCraft] = this.ShowCraft;
         this.btnCraft.m_strPopUpText = "打开/关闭制作界面 (X)";
         this.m_aMouseOverItems.push(this.btnCraft);
         _loc4_ += 0 + this.btnCraft.height;
         this.btnSkills = new ImgButton("btn_inv_menu_skills_on.png","btn_inv_menu_skills.png","btn_inv_menu_skills_on.png","btn_inv_menu_skills_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnSkills] = this.ShowSkills;
         this.btnSkills.m_strPopUpText = "打开/关闭技能天赋界面";
         this.m_aMouseOverItems.push(this.btnSkills);
         _loc4_ += 0 + this.btnSkills.height;
         this.btnEncounter = new ImgButton("btn_inv_menu_respond_on.png","btn_inv_menu_respond.png","btn_inv_menu_respond_on.png","btn_inv_menu_respond_on.png",_loc3_ - 2,_loc4_,this.ToggleScreen,true);
         this.m_dictScreenFunctions[this.btnEncounter] = this.ShowEncounters;
         this.btnEncounter.m_strPopUpText = "打开/关闭对战界面 (E)";
         this.m_aMouseOverItems.push(this.btnEncounter);
         _loc4_ += 0 + this.btnEncounter.height;
         this.sprEncounterBtn = new FlxSprite(this.btnEncounter.x,this.btnEncounter.y);
         this.sprEncounterBtn.loadBitmap(DataHandler.GetImage("btn_inv_menu_respond_anim.png"),true,false,63,40);
         this.sprEncounterBtn.addAnimation("on",[0,1],2);
         this.grpAttackMode = new FlxGroup();
         this.txtAttackMode = new FlxText(FlxG.width - 200 + 24,FlxG.height - 60 + 1,200,"Attack Mode: ");
         this.txtAttackModeCharges = new FlxText(FlxG.width - 24,FlxG.height - 16,24,"");
         this.sprAttackModeBG = new FlxSprite(FlxG.width - 200,FlxG.height - 60);
         this.sprAttackModeBG.pixels = DataHandler.GetImage("AModeFrame.png");
         this.sprCorner = new FlxSprite(FlxG.width - 200,FlxG.height - 60);
         this.sprCorner.pixels = DataHandler.GetImage("GUIBlankBlock.png");
         this.sprAttackMode = new FlxSprite(FlxG.width - 200,FlxG.height - 60);
         this.sprAttackModeCharge = new FlxSprite();
         this.btnAmodeDn = new ImgButton("btn_amode_dn_dn.png","btn_amode_dn.png","btn_amode_dn_over.png","btn_amode_dn.png",this.sprAttackModeBG.x,this.sprAttackModeBG.y + 42,this.AModeDn);
         this.btnAmodeDn.m_strPopUpText = "更改战斗方式 (O)";
         this.m_aMouseOverItems.push(this.btnAmodeDn);
         this.btnAmodeUp = new ImgButton("btn_amode_up_dn.png","btn_amode_up.png","btn_amode_up_over.png","btn_amode_up.png",this.sprAttackModeBG.x,this.sprAttackModeBG.y + 19,this.AModeUp);
         this.btnAmodeUp.m_strPopUpText = "更改战斗方式 (L)";
         this.m_aMouseOverItems.push(this.btnAmodeUp);
         this.grpAttackMode.add(this.sprCorner);
         this.grpAttackMode.add(this.sprAttackModeBG);
         this.grpAttackMode.add(this.btnAmodeDn);
         this.grpAttackMode.add(this.btnAmodeUp);
         this.grpAttackMode.add(this.sprAttackMode);
         this.grpAttackMode.add(this.sprAttackModeCharge);
         this.grpAttackMode.add(this.txtAttackMode);
         this.grpAttackMode.add(this.txtAttackModeCharges);
         this.m_txtLoadMessage = new FlxText(FlxG.width / 2,FlxG.height / 2,FlxG.width / 2,"载入: 打开文件...");
         this.m_txtLoadMessage.scrollFactor = new FlxPoint();
         this.txtPlayerMoney.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtAttackMode.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtAttackModeCharges.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtMovesLeft.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtMovesLeftReserve.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtVersion.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.txtHexInfo.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.m_txtLoadMessage.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.btnMovesBG.onOut = this.ClearPopUpText;
         this.grpHUD.add(this.txtPlayerMoney);
         this.grpHUD.add(this.btnMovesBG);
         this.grpHUD.add(this.txtMovesLeft);
         this.grpHUD.add(this.txtVersion);
         this.grpHUD.add(this.txtHexInfo);
         this.grpHUD.add(this.grpBtnActions);
         this.grpHUD.add(this.grpBtnScreens);
         this.grpBtnActions.add(this.btnSleep);
         this.grpBtnActions.add(this.btnWake);
         this.grpBtnActions.add(this.btnWait);
         this.grpBtnActions.add(this.btnRun);
         this.grpBtnActions.add(this.txtMovesLeftReserve);
         this.grpBtnActions.add(this.btnHide);
         this.grpBtnActions.add(this.btnHideTracks);
         this.grpBtnActions.add(this.btnSpy);
         this.grpBtnActions.add(this.btnScavenge);
         this.grpBtnActions.add(this.btnRest);
         this.grpHUD.add(this.btnMenu);
         this.grpBtnScreens.add(this.btnMainMap);
         this.grpBtnScreens.add(this.btnConditions);
         this.grpBtnScreens.add(this.btnMap);
         this.grpBtnScreens.add(this.btnItems);
         this.grpBtnScreens.add(this.btnVehicle);
         this.grpBtnScreens.add(this.btnEncounter);
         this.grpBtnScreens.add(this.sprEncounterBtn);
         this.grpBtnScreens.add(this.btnSkills);
         this.grpBtnScreens.add(this.btnCamp);
         this.grpBtnScreens.add(this.btnCraft);
         this.grpHUD.add(this.grpWeatherNode);
         this.grpHUD.add(this.grpHungerBar);
         this.grpHUD.add(this.grpThirstBar);
         this.grpHUD.add(this.grpLoadBar);
         this.grpHUD.add(this.grpWoundBar);
         this.grpHUD.add(this.grpSleepBar);
         this.grpHUD.add(this.grpBodyTempBar);
         this.grpHUD.add(this.grpAmbientBar);
         this.grpHUD.add(this.grpMsg);
         this.grpHUD.add(this.grpAttackMode);
         this.grpHUD.setAll("scrollFactor",new FlxPoint(0,0));
         this.grpHUD.setAll("cameras",[FlxG.camera]);
         this.grpMsg.setAll("cameras",[this.camMsg]);
         this.grpLabels.setAll("cameras",[FlxG.camera]);
      }
      
      public function StartGame() : void
      {
         var _loc2_:Array = null;
         var _loc3_:uint = 0;
         var _loc4_:FlxHexTile = null;
         var _loc5_:int = 0;
         var _loc6_:uint = 0;
         if(this.m_nLoadingStage == 0)
         {
            this.grpHUD.active = true;
            this.grpMsg.active = true;
            this.grpHUD.visible = true;
            this.grpMsg.visible = true;
            this.camMsg.visible = true;
            this.grpCreatures.add(this.sprPlayer);
            this.grpCreatures.setAll("cameras",[FlxG.camera]);
            this.m_aCreatures = new Array();
            this.m_aTilesToUpdate = new Array();
            this.grpWeatherNode.Reset();
            this.UpdateDate();
            this.grpMinimap.Initialize();
            DataHandler.ReInitialize();
            DM.StartGame();
            _loc2_ = this.tmapHexes.getTiles();
            _loc3_ = DataHandler.GetGameVars()["nStartHexY"] * this.tmapHexes.widthInTiles + DataHandler.GetGameVars()["nStartHexX"];
            if(_loc3_ >= _loc2_.length)
            {
               _loc3_ = Math.random() * _loc2_.length;
            }
            _loc4_ = _loc2_[_loc3_];
            _loc5_ = 0;
            while(_loc5_ < 100)
            {
               if(_loc4_.nTerrainCost < 11)
               {
                  break;
               }
               _loc3_ = this.tmapHexes.totalTiles * Math.random();
               _loc4_ = _loc2_[_loc3_];
               _loc5_++;
            }
            this.tilCurrentHex = _loc4_;
            _loc6_ = 0;
            while(_loc6_ < this.tmapHexes.totalTiles)
            {
               (_loc4_ = _loc2_[_loc6_]).nExploredState = 2;
               _loc6_++;
            }
            this.sprHilight = new FlxSprite();
            this.sprHilight.pixels = DataHandler.GetImage("HexHilight.png");
            this.sprHilight.visible = false;
            this.grpMap.add(this.sprHilight);
            this.grpMap.setAll("cameras",[FlxG.camera]);
         }
         var _loc1_:Boolean = true;
         if(this.m_objSG != null)
         {
            this.LoadGame();
            if(this.m_nLoadingStage < 0)
            {
               this.UpdatePlayerUI();
               if(this.sprPlayer.Asleep)
               {
                  PlayState.m_objInstance.Mode(PlayState.GAMESTATE_SLEEPING);
               }
               _loc1_ = false;
            }
         }
         else
         {
            this.sprPlayer.Spawn(this.tilCurrentHex.GetHexCoords());
            this.UpdatePlayer();
            this.m_nLoadingStage = -1;
         }
         if(this.m_nLoadingStage < 0)
         {
            if(this.nMapStyle != 1)
            {
               DataHandler.RandomizeEncounters();
            }
            this.CenterCamera(this.sprPlayer.m_sprImage);
            this.m_objSG = DataHandler.DestroyObject(this.m_objSG);
            if(_loc1_)
            {
               DM.EncounterCheck(this.tilCurrentHex,false,false);
            }
            this.grpMsg.m_bIgnoreMessages = false;
            this.grpMsg.MessageFloaty("游戏开始.");
            if(this.bDMGame)
            {
               DM.NextEncounter();
            }
            else
            {
               this.Mode(GAMESTATE_GAMEREADY);
            }
            this.SetRes();
         }
      }
      
      public function EndGame() : void
      {
         var _loc2_:uint = 0;
         if(this.m_objGameStats != null)
         {
            return;
         }
         var _loc1_:* = "";
         this.m_objGameStats = new GameStats();
         this.LogPlayerStats(_loc1_);
         FlxG.scores.length = 0;
         FlxG.scores.push(this.m_objGameStats);
         if(this.sprPlayer.Alive == false)
         {
            _loc2_ = 0;
            while(_loc2_ < this.sprPlayer.aCurrentStates.length)
            {
               if(PlayerCondition(this.sprPlayer.aCurrentStates[_loc2_]).bFatal)
               {
                  if(_loc1_.length > 0)
                  {
                     _loc1_ += "\n";
                  }
                  _loc1_ += PlayerCondition(this.sprPlayer.aCurrentStates[_loc2_]).strDesc.replace(/<us>/gi,this.sprPlayer.Name);
               }
               _loc2_++;
            }
            DataHandler.DeleteSave();
            this.grpMsg.MessageFloaty(_loc1_);
            FlxG.switchState(new GameOverState());
         }
         else
         {
            this.m_objGameStats.m_nEncounterID = this.m_nEndingID;
            FlxG.switchState(new EndState());
         }
      }
      
      public function LogPlayerStats(param1:String) : void
      {
         var _loc4_:PlayerCondition = null;
         var _loc2_:Number = this.objDate.getTime() - this.objStartDate.getTime();
         _loc2_ = _loc2_ / 1000 / 60 / 60;
         if(this.m_objGameStats == null)
         {
            return;
         }
         this.m_objGameStats.m_strCauseOfDeath = param1;
         this.m_objGameStats.m_fHoursSurvived = _loc2_;
         this.m_objGameStats.m_nMorality = this.sprPlayer.m_nMorality;
         this.m_objGameStats.m_bmpPlayer = this.sprPlayer.GetCreatureImage(true);
         var _loc3_:String = "";
         for each(_loc4_ in this.sprPlayer.aCurrentStates)
         {
            if(_loc4_.m_bDisplayGameOver)
            {
               _loc3_ += _loc4_.strDesc.replace(/<us>/gi,"玩家") + "\n";
            }
         }
         this.m_objGameStats.m_strConditions = _loc3_;
         this.m_objGameStats.m_strCauseOfDeath += "\n\n最后行动:";
         this.m_objGameStats.m_strLog = this.grpMsg.Tail();
      }
      
      public function UpdatePlayer() : void
      {
         this.sprPlayer.EndTurn(0,this.grpWeatherNode.objWeatherLast);
         this.UpdatePlayerUI();
         this.CheckPlayerDeath();
      }
      
      public function CheckPlayerDeath() : void
      {
         if(!this.sprPlayer.Alive && !this.bFading)
         {
            this.bFading = true;
            FlxG.fade(4278190080,1,this.EndGame);
         }
      }
      
      public function ChangeMap() : void
      {
         this.grpMap.remove(this.tmapHexes);
         MapUtils.LoadMapDef("GBSCrashSite.png");
         this.tmapHexes = MapUtils.tmapHexes;
         this.nCols = this.tmapHexes.widthInTiles;
         this.nRows = this.tmapHexes.heightInTiles;
         this.grpMap.add(this.tmapHexes);
         var _loc1_:FlxPoint = GUIValues.GetPoint("offset");
         FlxG.camera.setBounds(-_loc1_.x,-_loc1_.y,this.tmapHexes.width + _loc1_.x * 2,this.tmapHexes.height + _loc1_.y * 2 + GUIValues.GetInt("PlayState.camMsg.minheight"),true);
         FlxG.camera.follow(this.sprCamera);
         this.sprPlayer.Spawn(new FlxPoint(10,10));
         this.UpdatePlayer();
         this.CenterCamera(this.sprPlayer.m_sprImage);
      }
      
      public function EndPlayerTurn(param1:Number, param2:Boolean = true) : void
      {
         this.m_fHoursPassed = param1;
         this.UpdatePlayer();
         if(param2)
         {
            DM.RefreshCreatures(this.m_aCreatures);
         }
         DM.NextEncounter();
      }
      
      public function EndDMTurn(param1:Number, param2:Boolean, param3:Boolean) : void
      {
         var _loc7_:ItemInstance = null;
         var _loc8_:AICreature = null;
         var _loc9_:FlxHexTile = null;
         var _loc10_:int = 0;
         var _loc11_:ItemCamp = null;
         this.objOldDate.setTime(this.objDate.getTime());
         this.objDate.setTime(this.objDate.getTime() + param1 * 60 * 60 * 1000);
         this.UpdateDate();
         this.CenterCamera(this.sprPlayer.m_sprImage);
         this.grpMsg.EndTurn();
         if(param2)
         {
            this.sprPlayer.EndTurn(param1,this.grpWeatherNode.objWeatherLast);
         }
         else
         {
            this.sprPlayer.EndTurn(param1,null);
         }
         this.CheckPlayerDeath();
         var _loc4_:Array = new Array();
         var _loc5_:uint = 0;
         while(_loc5_ < this.m_aTilesToUpdate.length)
         {
            _loc9_ = this.m_aTilesToUpdate[_loc5_];
            _loc9_.Scent -= param1 / PlayState.HOURS_PER_TURN;
            if(this.grpWeatherNode.objWeatherLast.bPrecip)
            {
               _loc9_.Scent -= param1 / PlayState.HOURS_PER_TURN;
            }
            if(_loc9_.Scent <= 0)
            {
               _loc4_.push(_loc9_);
            }
            _loc5_++;
         }
         _loc5_ = 0;
         while(_loc5_ < _loc4_.length)
         {
            if((_loc10_ = int(this.m_aTilesToUpdate.indexOf(_loc4_[_loc5_]))) >= 0)
            {
               this.m_aTilesToUpdate.splice(_loc10_,1);
            }
            _loc5_++;
         }
         var _loc6_:Vector.<ItemInstance> = new Vector.<ItemInstance>();
         for each(_loc9_ in this.tmapHexes.vVisibleHexes)
         {
            if(_loc9_.HasGroundItems)
            {
               _loc6_ = _loc9_.GroundObject.vItems.concat(_loc6_);
            }
            for each(_loc11_ in _loc9_.m_vCampItems)
            {
               _loc6_ = _loc6_.concat(_loc11_.vItems);
            }
         }
         for each(_loc7_ in _loc6_)
         {
            _loc7_.TimeDegrade(this.objDate);
            if(_loc7_.fDurability <= 0)
            {
               _loc7_.ReplaceDegradedItem();
            }
         }
         this.grpInventoryUI.CheckRecipe();
         if(param3 == false)
         {
            return;
         }
         for each(_loc8_ in this.m_aCreatures)
         {
            if(_loc8_.Alive == false)
            {
               this.RemoveCreature(_loc8_);
            }
         }
         if(param2)
         {
            DM.UpdateCreatures(this.m_aCreatures,param1,this.grpWeatherNode.objWeatherLast);
         }
         else
         {
            DM.UpdateCreatures(this.m_aCreatures,param1,null);
         }
         DM.EncounterCheck(this.tilCurrentHex);
         this.UpdatePlayerUI();
         DM.NextEncounter();
         this.m_fHoursPassed = 0;
      }
      
      public function BtnEndTurn() : void
      {
         this.EndPlayerTurn(PlayState.HOURS_PER_TURN);
         this.btnWait.on = false;
         this.btnWait.status = FlxButton.NORMAL;
      }
      
      public function AddCreature(param1:AICreature, param2:FlxPoint) : void
      {
         if(param1 == null && param2 == null)
         {
            return;
         }
         this.grpCreatures.add(param1);
         this.grpCreatures.setAll("cameras",[FlxG.camera]);
         this.m_aCreatures.push(param1);
         param1.Spawn(param2);
         param1.EquipBestWeapon();
         this.m_aMouseOverItems.push(param1);
         DM.EncounterCheck(this.tilCurrentHex,true);
      }
      
      public function RemoveCreature(param1:AICreature) : void
      {
         this.grpCreatures.remove(param1,true);
         var _loc2_:int = int(this.m_aCreatures.indexOf(param1));
         this.m_aCreatures.splice(_loc2_,1);
         _loc2_ = int(this.m_aMouseOverItems.indexOf(param1));
         this.m_aMouseOverItems.splice(_loc2_,1);
      }
      
      public function UpdateAttackModeUI() : void
      {
         var _loc1_:String = null;
         var _loc2_:String = null;
         var _loc3_:FlxPoint = null;
         var _loc4_:FlxPoint = null;
         var _loc5_:int = 0;
         if(this.sprPlayer.CurrentAttackMode != null)
         {
            _loc1_ = "= " + this.sprPlayer.CurrentAttackMode.m_strName;
            if(this.txtAttackMode.text != _loc1_)
            {
               this.txtAttackMode.text = _loc1_;
               _loc2_ = "";
               if(GUIValues.GetInt("Item.zoom") == 2)
               {
                  _loc2_ = DataHandler.m_strZoomPrefix;
               }
               this.sprAttackMode.pixels = DataHandler.GetImage(this.sprPlayer.CurrentAttackMode.m_strIMG,_loc2_);
               if(this.sprPlayer.CurrentAttackMode.m_vChargeProfiles.length > 0)
               {
                  this.txtAttackModeCharges.visible = true;
                  this.sprAttackModeCharge.pixels = this.sprPlayer.CurrentAttackMode.m_bmpChargeIMG;
                  this.sprAttackModeCharge.visible = true;
               }
               else
               {
                  this.txtAttackModeCharges.visible = false;
                  this.sprAttackModeCharge.visible = false;
               }
            }
            if(this.sprPlayer.CurrentAttackMode.m_vChargeProfiles.length > 0)
            {
               this.txtAttackModeCharges.text = "x" + this.sprPlayer.CurrentAttackMode.ChargesLeft();
               _loc3_ = GUIValues.GetPoint("offset");
               _loc4_ = new FlxPoint(GUIValues.GetInt("width"),GUIValues.GetInt("PlayState.grpAttackMode.ChargeHeight"));
               _loc5_ = GUIValues.GetInt("PlayState.sprAttackModeChargeOffset");
               this.txtAttackModeCharges.x = _loc3_.x + _loc4_.x - _loc5_ - this.txtAttackModeCharges.width;
               this.txtAttackModeCharges.y = _loc3_.y + _loc4_.y - _loc5_ - this.txtAttackModeCharges.height;
               this.sprAttackModeCharge.x = this.txtAttackModeCharges.x - this.sprAttackModeCharge.width;
               this.sprAttackModeCharge.y = _loc3_.y + _loc4_.y - _loc5_ - this.sprAttackModeCharge.height;
            }
         }
      }
      
      public function UpdatePlayerUI() : void
      {
         var _loc4_:PlayerCondition = null;
         var _loc9_:int = 0;
         var _loc10_:Number = NaN;
         var _loc11_:Number = NaN;
         var _loc12_:Number = NaN;
         var _loc1_:Number = Math.max(1,this.sprPlayer.m_nMovesPerTurn + this.sprPlayer.fMovesPerTurnModifier);
         var _loc2_:Number = Math.max(0,this.sprPlayer.m_fMovesLeft);
         var _loc3_:String = "行动点: " + _loc2_.toFixed(2) + "/" + _loc1_.toString();
         if(this.txtMovesLeft.text != _loc3_)
         {
            this.txtMovesLeft.text = _loc3_;
         }
         if(this.grpInventoryUI != null)
         {
            this.grpInventoryUI.UpdateCraftButton();
            this.grpInventoryUI.UpdateHealthStats();
         }
         _loc3_ = Math.max(0,Math.ceil(this.sprPlayer.m_fMoveReserveRemaining)) + "/" + Math.max(0,this.sprPlayer.m_fMoveReserve).toFixed(0);
         if(this.txtMovesLeftReserve.text != _loc3_)
         {
            this.txtMovesLeftReserve.text = _loc3_;
         }
         if(this.sprPlayer.m_fMovesLeft <= 0)
         {
            if(!this.sprPlayer.HasCondition(120) && !this.sprPlayer.HasCondition(121))
            {
               this.btnHide.kill();
            }
            this.btnHideTracks.kill();
            this.btnSpy.kill();
            this.btnRun.kill();
            this.txtMovesLeftReserve.kill();
            this.btnWait.on = true;
         }
         else
         {
            this.btnWait.on = false;
            this.btnWait.status = FlxButton.NORMAL;
         }
         if(this.sprPlayer.m_fMoveReserveRemaining <= 0 || this.sprPlayer.m_fMoveReserve <= 0)
         {
            this.btnRun.kill();
            this.txtMovesLeftReserve.kill();
         }
         if(Boolean(this.sprPlayer.m_dictCrippled[this.sprPlayer.CRIPPLED_LEFTARM]) && Boolean(this.sprPlayer.m_dictCrippled[this.sprPlayer.CRIPPLED_RIGHTARM]))
         {
            this.btnHideTracks.kill();
         }
         _loc3_ = "$" + this.sprPlayer.Money.toFixed(2);
         if(this.txtPlayerMoney.text != _loc3_)
         {
            this.txtPlayerMoney.text = _loc3_;
         }
         this.btnMovesBG.strPopUpText = "行动点/回合: " + this.sprPlayer.m_nMovesPerTurn;
         var _loc5_:Number = 0;
         var _loc6_:uint = 0;
         while(_loc6_ < this.sprPlayer.aCurrentStates.length)
         {
            if((_loc9_ = int((_loc4_ = this.sprPlayer.aCurrentStates[_loc6_]).m_aFieldNames.indexOf("fMovesPerTurnModifier"))) >= 0)
            {
               _loc5_ = Number(_loc4_.m_aModifiers[_loc9_]);
               if(_loc4_.m_bStackable)
               {
                  _loc5_ *= _loc4_.m_nStacked;
               }
               this.btnMovesBG.strPopUpText += "\n" + _loc4_.strName + ": " + _loc5_.toString();
            }
            _loc6_++;
         }
         _loc4_ = null;
         this.UpdateAttackModeUI();
         var _loc7_:Number = -this.sprPlayer.fSleepDebt;
         this.grpSleepBar.UpdateBars(_loc7_,new Array(-this.sprPlayer.aRestedStates[0][0],-this.sprPlayer.aRestedStates[1][0],-this.sprPlayer.aRestedStates[2][0],-this.sprPlayer.aRestedStates[3][0]));
         this.grpSleepBar.m_btnBG.strPopUpText = this.sprPlayer.m_objCurrentRestCond.strDesc.replace(/<us>/gi,this.sprPlayer.Name);
         this.grpSleepBar.m_txtLabel.text = this.sprPlayer.m_objCurrentRestCond.strName;
         _loc7_ = -this.sprPlayer.fFoodDebt;
         this.grpHungerBar.m_btnBG.strPopUpText = this.sprPlayer.m_objCurrentHungerCond.strDesc.replace(/<us>/gi,this.sprPlayer.Name);
         this.grpHungerBar.m_txtLabel.text = this.sprPlayer.m_objCurrentHungerCond.strName;
         this.grpHungerBar.UpdateBars(_loc7_,new Array(-this.sprPlayer.aHungerStates[0][0],-this.sprPlayer.aHungerStates[1][0],-this.sprPlayer.aHungerStates[2][0],-this.sprPlayer.aHungerStates[3][0],-this.sprPlayer.aHungerStates[4][0]));
         _loc7_ = -this.sprPlayer.fWaterDebt;
         this.grpThirstBar.m_btnBG.strPopUpText = this.sprPlayer.m_objCurrentThirstCond.strDesc.replace(/<us>/gi,this.sprPlayer.Name);
         this.grpThirstBar.m_txtLabel.text = this.sprPlayer.m_objCurrentThirstCond.strName;
         this.grpThirstBar.UpdateBars(_loc7_,new Array(-this.sprPlayer.aThirstStates[0][0],-this.sprPlayer.aThirstStates[1][0],-this.sprPlayer.aThirstStates[2][0],-this.sprPlayer.aThirstStates[3][0],-this.sprPlayer.aThirstStates[4][0]));
         var _loc8_:Number;
         if((_loc8_ = this.sprPlayer.m_fEncumberanceLimit) < 1)
         {
            _loc8_ = this.sprPlayer.Encumberance;
         }
         _loc7_ = -this.sprPlayer.Encumberance;
         this.grpLoadBar.m_btnBG.strPopUpText = this.sprPlayer.m_objCurrentLoadCond.strDesc.replace(/<us>/gi,this.sprPlayer.Name);
         this.grpLoadBar.m_txtLabel.text = this.sprPlayer.m_objCurrentLoadCond.strName;
         this.grpLoadBar.UpdateBars(_loc7_,new Array(-_loc8_ / this.sprPlayer.aLoadStates[0][0],-_loc8_ / this.sprPlayer.aLoadStates[1][0],-_loc8_ / this.sprPlayer.aLoadStates[2][0],-_loc8_ / this.sprPlayer.aLoadStates[3][0]));
         _loc3_ = "健康";
         _loc7_ = 1;
         if(this.sprPlayer.m_fBloodLeft / this.sprPlayer.m_fBloodLeftBase < _loc7_)
         {
            _loc7_ = this.sprPlayer.m_fBloodLeft / this.sprPlayer.m_fBloodLeftBase;
            _loc3_ = "失血";
         }
         if(this.sprPlayer.m_fImmuneLeft / this.sprPlayer.m_fImmuneLeftBase < _loc7_)
         {
            _loc7_ = this.sprPlayer.m_fImmuneLeft / this.sprPlayer.m_fImmuneLeftBase;
            _loc3_ = "生病";
         }
         if(this.sprPlayer.m_fPainLeft / this.sprPlayer.m_fPainLeftBase < _loc7_)
         {
            _loc7_ = this.sprPlayer.m_fPainLeft / this.sprPlayer.m_fPainLeftBase;
            _loc3_ = "疼痛";
         }
         this.grpWoundBar.m_btnBG.strPopUpText = _loc3_;
         this.grpWoundBar.m_txtLabel.text = _loc3_;
         this.grpWoundBar.UpdateBars(_loc7_,new Array(0,0.25,0.5,0.75,1));
         this.grpBodyTempBar.m_btnBG.strPopUpText = this.sprPlayer.m_objCurrentTempCond.strDesc.replace(/<us>/gi,this.sprPlayer.Name);
         this.grpBodyTempBar.m_txtLabel.text = this.sprPlayer.m_objCurrentTempCond.strName;
         if(this.sprPlayer.fCoreTemp <= this.sprPlayer.fNormalBodyTemp)
         {
            _loc7_ = this.sprPlayer.fCoreTemp;
            if(this.grpBodyTempBar.m_aColors.length != 4)
            {
               this.grpBodyTempBar.m_aColors = new Array(4292345857,4294533376,4294365184,4287167745);
            }
            this.grpBodyTempBar.UpdateBars(_loc7_,new Array(this.sprPlayer.aCoreTempStates[7][0] - 1,this.sprPlayer.aCoreTempStates[7][0],this.sprPlayer.aCoreTempStates[6][0],this.sprPlayer.aCoreTempStates[5][0],this.sprPlayer.aCoreTempStates[4][0]));
         }
         else
         {
            _loc7_ = -this.sprPlayer.fCoreTemp;
            if(this.grpBodyTempBar.m_aColors.length != 4)
            {
               this.grpBodyTempBar.m_aColors = new Array(4292345857,4294533376,4294365184,4287167745);
            }
            this.grpBodyTempBar.UpdateBars(_loc7_,[-this.sprPlayer.aCoreTempStates[0][0],-this.sprPlayer.aCoreTempStates[1][0],-this.sprPlayer.aCoreTempStates[2][0],-this.sprPlayer.aCoreTempStates[3][0],-this.sprPlayer.aCoreTempStates[4][0]]);
         }
         if(this.grpWeatherNode.objWeatherLast != null)
         {
            _loc10_ = this.sprPlayer.fAdjMaxSafeTemp - this.sprPlayer.fAdjMinSafeTemp;
            _loc11_ = this.sprPlayer.fAdjMinSafeTemp - _loc10_ * 3;
            _loc12_ = this.sprPlayer.fAdjMaxSafeTemp + _loc10_ * 3;
            _loc7_ = this.grpWeatherNode.objWeatherLast.fTemp;
            this.grpAmbientBar.UpdateBars(_loc7_,new Array(_loc11_,this.sprPlayer.fAdjMinSafeTemp,this.sprPlayer.fAdjMaxSafeTemp,_loc12_));
            if(this.grpWeatherNode.objWeatherLast.fTemp < this.sprPlayer.fAdjMinSafeTemp)
            {
               this.grpAmbientBar.m_btnBG.strPopUpText = "你被冻的浑身发抖.";
            }
            else if(this.grpWeatherNode.objWeatherLast.fTemp > this.sprPlayer.fAdjMaxSafeTemp)
            {
               this.grpAmbientBar.m_btnBG.strPopUpText = "你热的大汗淋漓.";
            }
            else
            {
               this.grpAmbientBar.m_btnBG.strPopUpText = "你感觉很舒服.";
            }
            if(this.sprPlayer.HasCondition(836))
            {
               this.grpAmbientBar.m_btnBG.strPopUpText += "\n(室外温度: " + this.grpWeatherNode.GetTemperatureString(this.objDate,true) + ")";
            }
         }
         this.bScavenge = false;
         this.bRest = false;
         this.bSleep = false;
         if(this.sprPlayer.m_tilCurrentHex != null && this.grpInventoryUI.m_nState == GUIInventory.STATE_NORMAL)
         {
            if(this.sprPlayer.m_fMovesLeft > 0 && this.sprPlayer.m_tilCurrentHex.m_vScavengeItems.length > 0 && (!this.sprPlayer.m_dictCrippled[this.sprPlayer.CRIPPLED_LEFTARM] && !this.sprPlayer.m_dictCrippled[this.sprPlayer.CRIPPLED_RIGHTARM]))
            {
               this.bScavenge = true;
            }
            if(this.sprPlayer.m_fPainLeft + this.sprPlayer.m_fBloodLeft + this.sprPlayer.m_fImmuneLeft < this.sprPlayer.m_fPainLeftBase + this.sprPlayer.m_fBloodLeftBase + this.sprPlayer.m_fImmuneLeftBase)
            {
               this.bRest = true;
            }
            if(this.sprPlayer.fSleepDebt > 16)
            {
               this.bSleep = true;
            }
         }
         if(this.sprPlayer.HasCondition(120) || this.sprPlayer.HasCondition(121))
         {
            this.btnHide.bmpImgDown = DataHandler.GetImage("btn_main_unhide_dn.png");
            this.btnHide.bmpImgOn = DataHandler.GetImage("btn_main_unhide_on.png");
            this.btnHide.bmpImgOut = DataHandler.GetImage("btn_main_unhide.png");
            this.btnHide.bmpImgOver = DataHandler.GetImage("btn_main_unhide_on.png");
         }
         else
         {
            this.btnHide.bmpImgDown = DataHandler.GetImage("btn_main_hide_dn.png");
            this.btnHide.bmpImgOn = DataHandler.GetImage("btn_main_hide_on.png");
            this.btnHide.bmpImgOut = DataHandler.GetImage("btn_main_hide.png");
            this.btnHide.bmpImgOver = DataHandler.GetImage("btn_main_hide_on.png");
         }
         this.btnRun.on = this.sprPlayer.HasCondition(123);
         this.btnHide.on = this.sprPlayer.HasCondition(120) || this.sprPlayer.HasCondition(121);
         this.btnSpy.on = this.nCursor == 12;
         if(this.grpInventoryUI != null && (this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_HEALTH || this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_ITEMS))
         {
            this.grpInventoryUI.UpdateConditions();
         }
      }
      
      private function UpdateDate() : void
      {
         this.grpWeatherNode.UpdateWeather(this.objDate);
         this.grpWeatherNode.UpdateSounds();
         var _loc1_:int = this.grpWeatherNode.GetTimeOfDay(this.objDate);
         if(this.nTimeOfDay != _loc1_)
         {
            this.nTimeOfDay = _loc1_;
            this.sprPlayer.MessageFloaty("现在是 " + this.grpWeatherNode.GetTimeOfDayString(this.objDate),false);
         }
         var _loc2_:Boolean = this.tmapHexes.SwapImage(this.nTimeOfDay);
         if(_loc2_)
         {
            this.UpdateVisibility(this.tilCurrentHex);
         }
      }
      
      public function UpdateVisibility(param1:FlxHexTile) : void
      {
         var _loc7_:FlxHexTile = null;
         var _loc8_:Creature = null;
         if(param1 == null)
         {
            return;
         }
         var _loc2_:int = 0;
         while(_loc2_ < this.tmapHexes.vVisibleHexes.length)
         {
            this.tmapHexes.vVisibleHexes[_loc2_].nExploredState = 1;
            for each(_loc8_ in this.tmapHexes.vVisibleHexes[_loc2_].m_vOccupants)
            {
               _loc8_.m_bVisibleBefore = _loc8_.visible;
               _loc8_.visible = false;
            }
            this.grpMinimap.MinimapHex(this.tmapHexes.vVisibleHexes[_loc2_]);
            _loc2_++;
         }
         this.tmapHexes.vVisibleHexes.length = 0;
         var _loc3_:Number = param1.m_vLightLevels[this.nTimeOfDay];
         var _loc4_:Number = (_loc4_ = Math.max(_loc3_ * this.sprPlayer.VisionRange,0)) + param1.nVizIncrease;
         var _loc5_:Vector.<FlxHexTile> = MapUtils.GetVisibleHexes(param1.GetHexCoords(),_loc4_,this.sprPlayer.MinLightLevel,true);
         var _loc6_:Boolean = this.sprPlayer.HasCondition(109);
         if(_loc3_ + this.sprPlayer.LightLevel < this.sprPlayer.MinLightLevel)
         {
            if(!_loc6_)
            {
               this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(109));
            }
            if(this.sprPlayer.m_fMovesLeft - param1.nTerrainCost > 1)
            {
               this.sprPlayer.m_fMovesLeft = 1 + param1.nTerrainCost;
            }
         }
         else if(_loc6_)
         {
            this.sprPlayer.RemoveCondition(this.sprPlayer.GetCondition(109));
            this.grpMsg.MessageFloaty(this.sprPlayer.Name + " 你又能看到了.",true,null,GUIMessageWindow.COLOR_GOOD);
         }
         for each(_loc7_ in _loc5_)
         {
            if(_loc7_ != null)
            {
               _loc7_.nExploredState = 0;
               this.tmapHexes.vVisibleHexes.push(_loc7_);
               this.grpMinimap.MinimapHex(_loc7_);
               for each(_loc8_ in _loc7_.m_vOccupants)
               {
                  if(Boolean(_loc8_.Alive) && this.sprPlayer.CanSeeCreature(_loc8_))
                  {
                     _loc8_.visible = true;
                     if(_loc8_ != this.sprPlayer && this.sprPlayer.Resting)
                     {
                        this.sprPlayer.Resting = false;
                        this.grpMsg.MessageFloaty("停止休息: 附近有生物.");
                     }
                  }
               }
               this.ArrangeCreaturesInHex(param1);
            }
         }
         this.grpMinimap.MovePlayer(this.sprPlayer.x,this.sprPlayer.y);
         MapUtils.tmapHexes.setDirty();
      }
      
      public function RevealHex(param1:uint, param2:uint, param3:String = null, param4:Boolean = true) : void
      {
         var _loc6_:FlxText = null;
         var _loc9_:FlxText = null;
         var _loc5_:FlxHexTile;
         if((_loc5_ = this.tmapHexes.getTiles()[this.tmapHexes.widthInTiles * param2 + param1]) == null)
         {
            return;
         }
         if(_loc5_.nExploredState == 2)
         {
            _loc5_.nExploredState = 1;
         }
         this.grpMinimap.MinimapHex(_loc5_,param3,param4);
         if(param3 == null)
         {
            return;
         }
         var _loc7_:int = _loc5_.x;
         var _loc8_:int = _loc5_.y + 35;
         for each(_loc9_ in this.grpLabels.members)
         {
            if(_loc9_ != null && _loc9_.x == _loc7_ && _loc9_.y == _loc8_)
            {
               _loc6_ = _loc9_;
               break;
            }
         }
         _loc9_ = null;
         if(param3 != "")
         {
            if(_loc6_ == null)
            {
               (_loc6_ = new FlxText(_loc7_,_loc8_,180,param3)).setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
               this.grpLabels.add(_loc6_);
            }
            else
            {
               _loc6_.text = param3;
            }
         }
         else if(_loc6_ != null)
         {
            this.grpLabels.remove(_loc6_);
            _loc6_.destroy();
         }
      }
      
      private function PlayerRest() : void
      {
         if(!this.sprPlayer.Resting && this.m_nGameState == GAMESTATE_GAMEREADY)
         {
            this.btnCamp.on = !this.btnCamp.on;
            this.ToggleScreen(this.btnCamp);
            this.btnRest.on = false;
            this.btnRest.status = FlxButton.NORMAL;
            return;
         }
         this.sprPlayer.Resting = !this.sprPlayer.Resting;
         if(this.sprPlayer.Resting)
         {
            this.EndPlayerTurn(PlayState.HOURS_PER_TURN);
         }
         this.bIgnoreNextMouseUp = true;
      }
      
      private function PlayerSleep() : void
      {
         this.btnSleep.on = false;
         this.btnSleep.status = FlxButton.NORMAL;
         if(!this.sprPlayer.Asleep && this.m_nGameState == GAMESTATE_GAMEREADY)
         {
            this.btnCamp.on = !this.btnCamp.on;
            this.ToggleScreen(this.btnCamp);
            return;
         }
         this.grpMsg.m_bIgnoreMessages = true;
         this.sprPlayer.Asleep = !this.sprPlayer.Asleep;
         this.grpMsg.m_bIgnoreMessages = false;
         if(this.sprPlayer.Asleep)
         {
            this.EndPlayerTurn(PlayState.HOURS_PER_TURN);
         }
         else
         {
            this.UpdatePlayer();
         }
      }
      
      private function PlayerScavenge() : void
      {
         var _loc2_:Encounter = null;
         this.btnScavenge.on = false;
         this.UpdatePlayerUI();
         this.sprPlayer.UpdateStatus();
         var _loc1_:Encounter = DataHandler.GetEncounter(42);
         if(!this.bScavTut)
         {
            _loc2_ = DataHandler.GetEncounter(69);
            _loc1_ = _loc1_.Clone();
            _loc1_.m_strImg = _loc2_.m_strImg;
            _loc1_.m_strDesc = _loc2_.m_strDesc;
            if(this.bScavTut == false)
            {
               this.bScavTut = true;
               DataHandler.SavePrefs();
            }
         }
         DM.AppendEncounter(_loc1_);
         DM.NextEncounter();
      }
      
      private function PlayerRun() : void
      {
         if(this.sprPlayer.HasCondition(123))
         {
            this.sprPlayer.RemoveCondition(this.sprPlayer.GetCondition(123));
         }
         else if(this.sprPlayer.m_fMoveReserveRemaining > 0)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(123));
         }
         this.btnRun.on = this.sprPlayer.HasCondition(123);
      }
      
      public function PlayerHide() : void
      {
         if(this.sprPlayer.HasCondition(120) || this.sprPlayer.HasCondition(121))
         {
            this.sprPlayer.RemoveCondition(this.sprPlayer.GetCondition(120));
            this.sprPlayer.RemoveCondition(this.sprPlayer.GetCondition(121));
            this.sprPlayer.MessageFloaty("玩家停止隐匿.");
         }
         else
         {
            --this.sprPlayer.m_fMovesLeft;
            if(this.sprPlayer.HasCondition(122))
            {
               this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(121));
            }
            else
            {
               this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(120));
            }
            this.sprPlayer.MessageFloaty("玩家开始隐匿.");
         }
         this.UpdatePlayerUI();
         this.bIgnoreNextMouseUp = true;
         this.Mode(this.m_nGameState);
      }
      
      private function PlayerHideTracks() : void
      {
         if(this.sprPlayer.HasCondition(51))
         {
            this.sprPlayer.m_tilCurrentHex.Scent *= 0.1;
         }
         else
         {
            this.sprPlayer.m_tilCurrentHex.Scent *= 0.5;
         }
         --this.sprPlayer.m_fMovesLeft;
         this.btnHideTracks.on = false;
         this.UpdatePlayerUI();
         this.bIgnoreNextMouseUp = true;
         this.sprPlayer.MessageFloaty("玩家隐藏痕迹.");
         this.Mode(this.m_nGameState);
      }
      
      private function PlayerSpy() : void
      {
         if(this.nCursor == 12)
         {
            this.ChangeCursor();
         }
         else
         {
            this.ChangeCursor(12);
         }
      }
      
      public function AlignCreatureToHex(param1:Creature, param2:FlxHexTile) : void
      {
         var _loc6_:int = 0;
         var _loc7_:Boolean = false;
         var _loc8_:Creature = null;
         var _loc9_:ItemCamp = null;
         var _loc10_:FlxSound = null;
         var _loc3_:Number = param1.m_nMovesPerTurn + param1.fMovesPerTurnModifier;
         if(_loc3_ < 1)
         {
            _loc3_ = 1;
         }
         var _loc4_:Number = param1.m_fScent - param1.m_fMovesLeft / _loc3_;
         param1.x = param2.x + (param2.width - param1.m_sprImage.width) / 2;
         param1.y = param2.y + (param2.height - param1.m_sprImage.height) / 2;
         var _loc5_:FlxHexTile = param1.m_tilCurrentHex;
         for each(_loc6_ in _loc5_.m_vCondIDs)
         {
            param1.RemoveCondition(param1.GetCondition(_loc6_));
         }
         param1.m_tilCurrentHex.RemoveCreature(param1);
         if(param1 is AICreature)
         {
            _loc9_ = param1.GetCamp(param1.m_tilCurrentHex);
            param1.RemoveCondition(param1.GetCondition(394));
            _loc9_ = param1.GetCamp(param2);
            param1.AddCondition(_loc9_.Condition.Clone(),false,false);
         }
         param2.AddCreature(param1);
         param1.m_tilCurrentHex = param2;
         for each(_loc6_ in param2.m_vCondIDs)
         {
            param1.AddCondition(param1.GetCondition(_loc6_));
         }
         _loc7_ = false;
         if(param1.HasCondition(120) == false && param1.HasCondition(121) == false)
         {
            param1.JustMoved = 1;
         }
         else
         {
            _loc7_ = true;
         }
         this.grpInventoryUI.UpdateGroundItems(param1,param2);
         for each(_loc8_ in _loc5_.m_vOccupants)
         {
            this.grpInventoryUI.UpdateGroundItems(_loc8_,_loc5_);
         }
         if((param1 == this.sprPlayer || param2.nExploredState == 0) && this.sprPlayer.CanSeeCreature(param1))
         {
            if(_loc7_)
            {
               this.grpMsg.MessageFloaty(param1.Name + " 偷偷靠近 " + param2.strName + ".");
            }
            _loc10_ = FlxG.loadSound(DataHandler.GetSound("cueFootstepsDirt"),GUIEscMenu.m_fSoundVolume,false,true,true);
            param1.visible = true;
            if(param1 != this.sprPlayer && this.sprPlayer.Resting)
            {
               this.sprPlayer.Resting = false;
               this.grpMsg.MessageFloaty("停止休息: 附近有生物.");
            }
            if(param1 != this.sprPlayer && this.sprPlayer.m_tilCurrentHex != null)
            {
               if(MapUtils.GetHexDistance(param1.m_tilCurrentHex.GetHexCoords(),this.sprPlayer.m_tilCurrentHex.GetHexCoords()) == 1)
               {
                  this.bRest = false;
               }
            }
         }
         else
         {
            param1.visible = false;
         }
         this.ArrangeCreaturesInHex(param2);
         if(_loc4_ > param2.Scent)
         {
            param2.Scent = _loc4_;
            param2.m_objScentOwner = param1;
            if(this.m_aTilesToUpdate.indexOf(param2) < 0)
            {
               this.m_aTilesToUpdate.push(param2);
            }
         }
      }
      
      public function AlignPlayerToHex(param1:FlxHexTile) : void
      {
         var _loc2_:Creature = null;
         var _loc3_:Vector.<ItemInstance> = null;
         var _loc4_:ItemCamp = null;
         var _loc5_:ItemInstance = null;
         this.AlignCreatureToHex(this.sprPlayer,param1);
         this.grpInventoryUI.UpdateCampItems(this.sprPlayer,param1);
         param1.bVisited = true;
         for each(_loc2_ in this.tilCurrentHex.m_vOccupants)
         {
            this.grpInventoryUI.UpdateGroundItems(_loc2_,this.tilCurrentHex);
         }
         this.tilCurrentHex = param1;
         this.tmapHexes.setDirty();
         this.UpdateVisibility(param1);
         _loc3_ = this.grpInventoryUI.grpCraftingIngredientsSlot.SocketedItem().vItems.concat();
         _loc3_ = param1.GroundObject.vItems.concat(_loc3_);
         for each(_loc4_ in param1.m_vCampItems)
         {
            _loc3_ = _loc3_.concat(_loc4_.vItems);
         }
         for each(_loc5_ in _loc3_)
         {
            _loc5_.TimeDegrade(this.objDate);
            if(_loc5_.fDurability <= 0)
            {
               _loc5_.ReplaceDegradedItem();
            }
         }
         this.grpInventoryUI.ClearCrafting();
         if(this.tilCurrentHex.index == 20 && !this.sprPlayer.HasCondition(366))
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(366));
            this.m_fMusicNext = 1;
         }
      }
      
      private function ArrangeCreaturesInHex(param1:FlxHexTile) : void
      {
         var _loc3_:Creature = null;
         var _loc2_:uint = 0;
         if(param1 == this.sprPlayer.m_tilCurrentHex)
         {
            this.sprPlayer.x = param1.x + (param1.width - this.sprPlayer.m_sprImage.width) / 2;
            this.sprPlayer.y = param1.y + (param1.height - this.sprPlayer.m_sprImage.height) / 2;
            _loc2_++;
         }
         for each(_loc3_ in param1.m_vOccupants)
         {
            if(_loc3_.visible && _loc3_ != this.sprPlayer)
            {
               _loc3_.x = param1.x + (param1.width - _loc3_.m_sprImage.width) / 2;
               _loc3_.y = param1.y + (param1.height - _loc3_.m_sprImage.height) / 2;
               _loc3_.x += _loc2_ * Math.pow(-1,_loc2_) * 10;
               _loc3_.y -= _loc2_ * 7;
               _loc2_++;
            }
         }
         this.grpCreatures.sort();
      }
      
      private function MovePlayer(param1:FlxHexTile) : void
      {
         var _loc5_:int = 0;
         var _loc2_:int = int(MapUtils.GetHexDistance(this.tilCurrentHex.GetHexCoords(),param1.GetHexCoords()));
         var _loc3_:* = _loc2_ == 1;
         var _loc4_:Boolean = false;
         if(param1 == this.tilCurrentHex)
         {
            this.ShowItems();
         }
         else if(_loc3_)
         {
            if(!param1.bPassable)
            {
               this.grpMsg.MessageFloaty("这块区域不能通行.");
            }
            else if(this.sprPlayer.HasCondition(56))
            {
               this.grpMsg.MessageFloaty("负重过高不能移动.");
            }
            else if(this.sprPlayer.m_fMovesLeft <= 0)
            {
               this.grpMsg.MessageFloaty("本回合没有行动点数了.");
            }
            else if(_loc4_)
            {
               this.OutlandClick(param1);
            }
            else
            {
               _loc5_ = param1.nTerrainCost;
               this.AlignPlayerToHex(param1);
               this.sprPlayer.m_fMovesLeft -= _loc5_ * this.sprPlayer.m_fMoveCost;
               this.sprPlayer.fSleepDebt += _loc5_ * this.sprPlayer.m_fFatigueModifier;
               if(this.sprPlayer.m_fMovesLeft < 0)
               {
                  this.sprPlayer.m_fMovesLeft = 0;
               }
               if(this.sprPlayer.HasCondition(123))
               {
                  --this.sprPlayer.m_fMoveReserveRemaining;
                  if(this.sprPlayer.m_fMoveReserveRemaining <= 0)
                  {
                     this.sprPlayer.m_fMoveReserveRemaining = 0;
                     this.sprPlayer.RemoveCondition(this.sprPlayer.GetCondition(123));
                  }
               }
               this.UpdatePlayerUI();
               this.sprPlayer.UpdateStatus();
               DM.EncounterCheck(param1);
               DM.NextEncounter();
            }
         }
         else if(_loc4_)
         {
            this.OutlandClick(param1);
         }
      }
      
      private function OutlandClick(param1:FlxHexTile) : void
      {
         DM.AppendEncounter(DataHandler.GetEncounter(94));
         if(param1.GetHexCoords().x > 40)
         {
            DM.AppendEncounter(DataHandler.GetEncounter(70));
         }
         DM.NextEncounter();
      }
      
      private function ArrangeButtons(param1:Vector.<ImgButton>) : void
      {
         var _loc3_:ImgButton = null;
         this.ClearButtons();
         var _loc2_:int = this.ptMouseScreen.y;
         for each(_loc3_ in param1)
         {
            add(_loc3_);
            _loc3_.cameras = [FlxG.camera];
            _loc3_.x = this.ptMouseScreen.x;
            _loc3_.y = _loc2_;
            _loc2_ += _loc3_.height;
            this.m_vContextButtons.push(_loc3_);
         }
      }
      
      private function ClearButtons() : void
      {
         var _loc1_:ImgButton = null;
         for each(_loc1_ in this.m_vContextButtons)
         {
            remove(_loc1_);
            _loc1_.destroy();
         }
         this.m_vContextButtons.length = 0;
      }
      
      private function CreateContext(param1:String, param2:Function) : ImgButton
      {
         var _loc3_:String = "";
         var _loc4_:int;
         if((_loc4_ = GUIValues.GetInt("Minimap.zoom")) == 2)
         {
            _loc3_ = DataHandler.m_strZoomPrefix;
         }
         var _loc5_:ImgButton;
         (_loc5_ = new ImgButton(_loc3_ + "btn_context_blank_on.png",_loc3_ + "btn_context_blank_up.png",_loc3_ + "btn_context_blank_on.png",_loc3_ + "btn_context_blank_on.png",0,0,param2,true)).label = new FlxText(0,0,_loc5_.width,param1);
         _loc5_.label.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         _loc5_.labelOffset = new FlxPoint(_loc4_,_loc4_);
         _loc5_.scrollFactor.x = _loc5_.scrollFactor.y = 0;
         return _loc5_;
      }
      
      public function ClearPopUpText() : void
      {
         this.grpPopUp.Hide();
         remove(this.grpPopUp);
      }
      
      public function AddPopUpText() : void
      {
         add(this.grpPopUp);
         this.grpPopUp.Show();
      }
      
      public function ChangeCursor(param1:int = 0) : void
      {
         var _loc2_:int = 0;
         var _loc3_:int = 0;
         if(param1 == 0)
         {
            switch(this.m_nGameState)
            {
               case GAMESTATE_DMRUNNING:
                  param1 = 3;
                  break;
               case GAMESTATE_SLEEPING:
                  param1 = 3;
                  break;
               default:
                  param1 = 0;
            }
         }
         if(param1 == 1 || param1 == 2 || param1 == 12)
         {
            _loc2_ = _loc3_ = this.vCursors[param1].width / 2;
         }
         if(param1 != this.nCursor)
         {
            FlxG.mouse.loadBitmap(this.vCursors[param1],1,_loc2_,_loc3_);
            this.nCursor = param1;
         }
      }
      
      private function MouseHandler() : void
      {
         var _loc2_:FlxHexTile = null;
         var _loc4_:uint = 0;
         var _loc5_:AICreature = null;
         var _loc6_:GUIInventoryWound = null;
         var _loc7_:GUITintBar = null;
         var _loc8_:GUIMenuButton = null;
         var _loc9_:ImgButton = null;
         var _loc10_:WeatherNode = null;
         var _loc11_:* = false;
         var _loc12_:* = null;
         var _loc13_:Vector.<ImgButton> = null;
         var _loc14_:Boolean = false;
         var _loc15_:FlxPoint = null;
         var _loc16_:Boolean = false;
         var _loc17_:FlxHexTile = null;
         var _loc18_:Creature = null;
         this.ptMouse = FlxG.mouse.getWorldPosition(null,this.ptMouse);
         this.aObjectsUnderMouse.length = 0;
         this.ptMouseScreen = FlxG.mouse.getScreenPosition(null,this.ptMouseScreen);
         var _loc1_:String = "";
         var _loc3_:int = 0;
         this.grpPopUp.Move(this.ptMouseScreen.x + 16,this.ptMouseScreen.y);
         this.ClearPopUpText();
         this.ptCam = GUIValues.GetPoint("PlayState.camMsg",this.ptCam);
         this.rectCam.y = this.ptCam.y;
         this.rectCam.x = this.ptCam.x;
         this.rectCam.width = GUIValues.GetInt("PlayState.camMsg.width");
         this.rectCam.height = GUIValues.GetInt("PlayState.camMsg.minheight");
         if(!this.grpMsg.m_bCollapsed)
         {
            this.rectCam.y -= GUIValues.GetInt("PlayState.camMsg.maxheight") - this.rectCam.height;
         }
         if(FlxG.mouse.wheel > 0 && this.grpInventoryUI.objDragging == null)
         {
            this.grpMsg.ScrollUp();
         }
         else if(FlxG.mouse.wheel < 0 && this.grpInventoryUI.objDragging == null)
         {
            this.grpMsg.ScrollDown();
         }
         _loc4_ = 0;
         while(_loc4_ < this.m_aMouseOverItems.length)
         {
            switch(this.m_aMouseOverItems[_loc4_].constructor)
            {
               case AICreature:
                  if((_loc5_ = this.m_aMouseOverItems[_loc4_]).visible && _loc5_.alive && this.sprPlayer.CurrentAttackMode != null && _loc5_.m_sprImage.pixelsOverlapPoint(this.ptMouse))
                  {
                     if(!(_loc11_ = MapUtils.GetHexDistance(_loc5_.m_tilCurrentHex.GetHexCoords(),this.sprPlayer.m_tilCurrentHex.GetHexCoords()) <= 1))
                     {
                        _loc11_ = this.sprPlayer.HasCondition(34) || this.sprPlayer.HasCondition(35);
                     }
                     this.grpPopUp.UpdateInfo(_loc5_.GetPopUpText(_loc11_));
                     this.AddPopUpText();
                     this.aObjectsUnderMouse.push(_loc5_);
                     _loc5_ = null;
                  }
                  _loc5_ = null;
                  break;
               case Player:
                  if(this.sprPlayer.alive && this.sprPlayer.m_sprImage.pixelsOverlapPoint(this.ptMouse))
                  {
                     this.grpPopUp.UpdateInfo(this.sprPlayer.GetPopUpText(true));
                     this.AddPopUpText();
                     return;
                  }
                  break;
               case GUIInventoryWound:
                  if((_loc6_ = GUIInventoryWound(this.m_aMouseOverItems[_loc4_])).alive && !_loc6_.m_bHealed && _loc6_.btnSlot.pixelsOverlapPoint(this.ptMouse,17))
                  {
                     this.grpPopUp.UpdateInfo(_loc6_.btnSlot.m_strPopUpText);
                     this.AddPopUpText();
                     _loc6_ = null;
                     return;
                  }
                  _loc6_ = null;
                  break;
               case GUITintBar:
                  if((_loc7_ = GUITintBar(this.m_aMouseOverItems[_loc4_])).visible && _loc7_.m_btnBG.overlapsPoint(this.ptMouseScreen))
                  {
                     this.grpPopUp.UpdateInfo(_loc7_.m_btnBG.strPopUpText);
                     this.AddPopUpText();
                     this.aObjectsUnderMouse.push(_loc7_);
                     _loc7_ = null;
                     return;
                  }
                  _loc7_ = null;
                  break;
               case GUIMenuButton:
                  if((_loc8_ = GUIMenuButton(this.m_aMouseOverItems[_loc4_])).visible && _loc8_.overlapsPoint(this.ptMouseScreen))
                  {
                     this.grpPopUp.UpdateInfo(_loc8_.strPopUpText);
                     this.AddPopUpText();
                     this.aObjectsUnderMouse.push(_loc8_);
                     _loc8_ = null;
                     return;
                  }
                  _loc8_ = null;
                  break;
               case ImgButton:
                  if((_loc9_ = ImgButton(this.m_aMouseOverItems[_loc4_])).alive && _loc9_.bMouseOver)
                  {
                     this.grpPopUp.UpdateInfo(_loc9_.m_strPopUpText);
                     this.AddPopUpText();
                     this.aObjectsUnderMouse.push(_loc9_);
                     _loc9_ = null;
                     return;
                  }
                  _loc9_ = null;
                  break;
               case WeatherNode:
                  if((_loc10_ = WeatherNode(this.m_aMouseOverItems[_loc4_])).grpSky.alive && this.ptMouseScreen.x > _loc10_.x && this.ptMouseScreen.y > _loc10_.y && this.ptMouseScreen.x < _loc10_.x + _loc10_.width && this.ptMouseScreen.y < _loc10_.y + _loc10_.width)
                  {
                     _loc12_ = (_loc12_ = "" + _loc10_.m_strPopUpText) + ("\n" + _loc10_.GetTimeOfDayString(this.objDate,this.sprPlayer.HasCondition(572)));
                     if(this.sprPlayer.HasCondition(836))
                     {
                        _loc12_ += "\n室外温度: " + _loc10_.GetTemperatureString(this.objDate,true);
                     }
                     this.grpPopUp.UpdateInfo(_loc12_);
                     this.AddPopUpText();
                     this.aObjectsUnderMouse.push(_loc10_);
                     _loc10_ = null;
                     return;
                  }
                  _loc10_ = null;
                  break;
            }
            if(this.aObjectsUnderMouse.length > 0)
            {
               break;
            }
            _loc4_++;
         }
         if(this.m_nGameState == GAMESTATE_GAMEREADY)
         {
            if(FlxG.mouse.justReleasedRight())
            {
               this.ptRightClickOrigin = null;
               _loc13_ = new Vector.<ImgButton>();
               _loc14_ = true;
               _loc4_ = 0;
               while(_loc4_ < this.aObjectsUnderMouse.length)
               {
                  if(this.aObjectsUnderMouse[_loc4_] != null)
                  {
                     if(this.aObjectsUnderMouse[_loc4_] is AICreature && this.nCursor != 12 && AICreature(this.aObjectsUnderMouse[_loc4_]).m_tilCurrentHex == this.sprPlayer.m_tilCurrentHex)
                     {
                        _loc13_.push(this.CreateContext("Engage",this.StartBattle));
                        this.bIgnoreNextMouseUp = true;
                        _loc14_ = false;
                     }
                  }
                  _loc4_++;
               }
               if(_loc14_)
               {
                  this.ptRightClickOrigin = new FlxPoint(this.ptMouse.x,this.ptMouse.y);
                  _loc13_.push(this.CreateContext("营地标志",this.ToggleCampIcon));
                  this.bIgnoreNextMouseUp = true;
               }
               if(_loc13_.length > 0)
               {
                  this.ArrangeButtons(_loc13_);
               }
               _loc13_ = null;
            }
            else if(FlxG.mouse.justPressedRight())
            {
               this.ptRightClickOrigin = new FlxPoint(this.ptMouse.x,this.ptMouse.y);
            }
            else if(FlxG.mouse.pressedRight() && this.ptRightClickOrigin != null)
            {
               _loc15_ = new FlxPoint(-(this.ptMouse.x - this.ptRightClickOrigin.x) / 50,-(this.ptMouse.y - this.ptRightClickOrigin.y) / 50);
               this.MoveCamera(_loc15_);
               this.ptRightClickOrigin.x -= _loc15_.x;
               this.ptRightClickOrigin.y -= _loc15_.y;
            }
            else if(FlxG.mouse.justReleased())
            {
               this.ClearButtons();
            }
            if(this.aObjectsUnderMouse.length == 0)
            {
               _loc2_ = MapUtils.GetHexUnderPoint(this.ptMouse);
               this.aObjectsUnderMouse.push(_loc2_);
               _loc16_ = _loc2_ != null && _loc2_.nExploredState < 2;
               if(_loc2_ != this.tilCurrentHex && MapUtils.CanSeeHex(_loc2_,this.sprPlayer.MinLightLevel,this.nTimeOfDay) == false)
               {
                  _loc16_ = false;
               }
               if(_loc16_)
               {
                  _loc12_ = _loc2_.strDesc;
                  if(_loc2_.bPassable)
                  {
                     _loc12_ += "\n行动花费: " + _loc2_.nTerrainCost;
                  }
                  else
                  {
                     _loc12_ += "\n不能通过";
                  }
                  if(MapUtils.GetHexDistance(_loc2_.m_ptHexCoords,this.sprPlayer.m_tilCurrentHex.m_ptHexCoords) <= 1 && _loc2_.Scent > this.sprPlayer.m_fTrackingThreshold && _loc2_.m_objScentOwner != null)
                  {
                     _loc12_ += "\n痕迹: " + _loc2_.m_objScentOwner.Name;
                  }
                  if(this.txtHexInfo.text != _loc12_)
                  {
                     this.txtHexInfo.text = _loc12_;
                     this.txtHexInfo.y = GUIValues.GetPoint("PlayState.txtHexInfo").y - this.txtHexInfo.height;
                  }
               }
            }
            if(this.nCursor != 12)
            {
               this.ChangeCursor(_loc3_);
            }
         }
         else if(this.m_nGameState == GAMESTATE_MAPEDITOR)
         {
            this.aObjectsUnderMouse.push(MapUtils.GetHexUnderPoint(this.ptMouse));
         }
         if(!this.bIgnoreNextMouseUp && FlxG.mouse.justReleased())
         {
            if(this.ptMouseScreen.x >= this.rectCam.x && this.ptMouseScreen.x <= this.rectCam.x + this.rectCam.width && this.ptMouseScreen.y >= this.rectCam.y)
            {
               if(this.grpMsg.m_bCollapsed)
               {
                  this.grpMsg.Expand();
               }
               else
               {
                  this.grpMsg.Collapse();
               }
            }
            else
            {
               if(this.m_nGameState != GAMESTATE_GAMEREADY && this.m_nGameState != GAMESTATE_MAPEDITOR)
               {
                  return;
               }
               if(this.aObjectsUnderMouse.length > 0)
               {
                  _loc4_ = 0;
                  while(_loc4_ < this.aObjectsUnderMouse.length)
                  {
                     if(this.aObjectsUnderMouse[_loc4_] != null)
                     {
                        if(this.aObjectsUnderMouse[_loc4_] is FlxHexTile)
                        {
                           switch(this.m_nGameState)
                           {
                              case GAMESTATE_MAPEDITOR:
                                 MapUtils.SetHex(this.aObjectsUnderMouse[_loc4_],this.nHexType);
                                 break;
                              default:
                                 _loc17_ = this.aObjectsUnderMouse[_loc4_];
                                 if(this.nCursor == 1)
                                 {
                                    this.ChangeCursor();
                                    this.UpdatePlayerUI();
                                    _loc17_ = null;
                                    break;
                                 }
                                 if(this.nCursor == 12 && _loc17_.nExploredState == 0)
                                 {
                                    --this.sprPlayer.m_fMovesLeft;
                                    for each(_loc18_ in _loc17_.m_vOccupants)
                                    {
                                       if(!(_loc18_ is Player || _loc18_.Asleep))
                                       {
                                          _loc18_.m_fVisibility += 0.5;
                                          if(this.sprPlayer.PlayerCanSee(_loc18_))
                                          {
                                             _loc18_.visible = true;
                                             _loc18_.m_bVisibleBefore = true;
                                          }
                                          _loc18_.m_fVisibility -= 0.5;
                                       }
                                    }
                                    this.ChangeCursor();
                                    this.UpdatePlayerUI();
                                    this.sprPlayer.MessageFloaty("玩家侦测 " + _loc17_.strName + ".");
                                    this.Mode(this.m_nGameState);
                                    _loc17_ = null;
                                    break;
                                 }
                                 this.MovePlayer(this.aObjectsUnderMouse[_loc4_] as FlxHexTile);
                                 _loc17_ = null;
                                 break;
                           }
                        }
                        else if(this.aObjectsUnderMouse[_loc4_] is AICreature)
                        {
                           if(this.nCursor == 12)
                           {
                              _loc5_ = this.aObjectsUnderMouse[_loc4_];
                              --this.sprPlayer.m_fMovesLeft;
                              _loc5_.m_bSpied = true;
                              this.ChangeCursor();
                              this.UpdatePlayerUI();
                              this.sprPlayer.MessageFloaty("玩家侦测 " + _loc5_.Name + ".");
                              this.Mode(this.m_nGameState);
                              _loc5_ = null;
                              break;
                           }
                        }
                     }
                     _loc4_++;
                  }
               }
            }
         }
         this.UpdateHilight();
         if(this.m_vContextButtons.length == 0)
         {
            this.bIgnoreNextMouseUp = false;
         }
      }
      
      public function Resize(param1:Event) : void
      {
      }
      
      public function UpdateHilight() : void
      {
         var _loc1_:int = 0;
         var _loc2_:FlxHexTile = null;
         var _loc3_:FlxPoint = null;
         if(this.aObjectsUnderMouse.length > 0)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aObjectsUnderMouse.length)
            {
               if(this.aObjectsUnderMouse[_loc1_] != null)
               {
                  if(this.aObjectsUnderMouse[_loc1_].constructor == FlxHexTile)
                  {
                     _loc2_ = this.aObjectsUnderMouse[_loc1_] as FlxHexTile;
                     if(this.m_nGameState == GAMESTATE_MAPEDITOR)
                     {
                        _loc3_ = _loc2_.GetHexCoords();
                        this.grpPopUp.UpdateInfo(_loc3_.x + "," + _loc3_.y);
                        this.AddPopUpText();
                     }
                     else if(MapUtils.GetHexDistance(_loc2_.m_ptHexCoords,this.sprPlayer.m_tilCurrentHex.m_ptHexCoords) == 1 && _loc2_.bPassable && this.sprPlayer.m_fMovesLeft > 0)
                     {
                        this.sprHilight.pixels = DataHandler.GetImage("HexHilight.png");
                     }
                     else
                     {
                        this.sprHilight.pixels = DataHandler.GetImage("HexHilightInvalid.png");
                     }
                     this.sprHilight.visible = true;
                     this.sprHilight.x = _loc2_.x;
                     this.sprHilight.y = _loc2_.y;
                     break;
                  }
               }
               _loc1_++;
            }
         }
      }
      
      public function CenterCamera(param1:FlxSprite) : void
      {
         var _loc2_:FlxPoint = new FlxPoint(this.sprCamera.x - (param1.x + param1.width / 2),this.sprCamera.y - (param1.y + param1.height / 2 + GUIValues.GetInt("PlayState.camMsg.minheight") / 2));
         this.MoveCamera(_loc2_);
      }
      
      private function MoveCamera(param1:FlxPoint) : void
      {
         this.sprCamera.x -= param1.x;
         this.sprCamera.y -= param1.y;
         var _loc2_:int = GUIValues.GetInt("width");
         var _loc3_:int = GUIValues.GetInt("height");
         var _loc4_:int = GUIValues.GetInt("PlayState.camMsg.minheight");
         if(this.sprCamera.x > this.tmapHexes.width - _loc2_ / 2)
         {
            this.sprCamera.x = this.tmapHexes.width - _loc2_ / 2;
         }
         if(this.sprCamera.x < _loc2_ / 2)
         {
            this.sprCamera.x = _loc2_ / 2;
         }
         if(this.sprCamera.y > this.tmapHexes.height - _loc3_ / 2 + _loc4_)
         {
            this.sprCamera.y = this.tmapHexes.height - _loc3_ / 2 + _loc4_;
         }
         if(this.sprCamera.y < _loc3_ / 2)
         {
            this.sprCamera.y = _loc3_ / 2;
         }
         this.grpMinimap.MoveCamera(this.sprCamera);
      }
      
      private function FloatyUpdate() : void
      {
         var _loc1_:TextFloaty = null;
         --this.nFloatyDelay;
         if(this.nFloatyDelay < 0)
         {
            this.nFloatyDelay = 0;
            if(this.vFloatyQueue.length > 0)
            {
               _loc1_ = this.vFloatyQueue[0];
               this.vFloatyQueue.splice(0,1);
               this.AddTextFloaty(_loc1_.m_strText,_loc1_.m_ptPos,_loc1_.m_nColor);
            }
         }
      }
      
      public function QueueTextFloaty(param1:String, param2:FlxPoint, param3:int = -1) : void
      {
         this.vFloatyQueue.push(new TextFloaty(param1,param2,param3));
      }
      
      public function AddTextFloaty(param1:String, param2:FlxPoint, param3:int = -1) : void
      {
         if(param3 < 0)
         {
            param3 = int(GUIMessageWindow.COLOR_DEFAULT);
         }
         var _loc4_:Array = new Array();
         var _loc5_:FlxText;
         (_loc5_ = new FlxText(param2.x,param2.y,180,param1)).setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),param3,"center",GUIValues.GetInt("nBodyFontShadowColor"));
         _loc4_.push(_loc5_);
         this.nFloatyDelay = _loc5_.height * 2;
         _loc5_.x -= _loc5_.width / 2;
         var _loc6_:VFX = new VFX(_loc4_,null,this.TextFloatyPerFrame,this.RemoveOldTextFloaty);
         this.grpVFX.add(_loc6_);
         _loc5_.cameras = [FlxG.camera];
      }
      
      private function RemoveOldTextFloaty(param1:VFX) : void
      {
         this.grpVFX.remove(param1);
         param1 = null;
      }
      
      private function TextFloatyPerFrame(param1:VFX) : void
      {
         var _loc3_:FlxSprite = null;
         var _loc2_:int = 0;
         while(_loc2_ < param1.aSprites.length)
         {
            _loc3_ = FlxSprite(param1.aSprites[_loc2_]);
            _loc3_.y -= 0.5 * DataHandler.nFPSModifier;
            _loc3_.alpha -= 0.01;
            if(_loc3_.alpha <= 0)
            {
               param1.m_bNeedsCleanup = true;
            }
            _loc2_++;
         }
      }
      
      private function StartBattle(param1:ImgButton) : void
      {
         var _loc2_:Creature = null;
         for each(_loc2_ in this.tilCurrentHex.m_vOccupants)
         {
            _loc2_.RemoveCondition(_loc2_.GetCondition(500));
            _loc2_.RemoveCondition(_loc2_.GetCondition(123));
         }
         DM.EncounterCheck(this.tilCurrentHex,true,false);
         DM.NextEncounter();
         this.ClearButtons();
      }
      
      private function ToggleCampIcon(param1:ImgButton) : void
      {
         var _loc2_:FlxHexTile = MapUtils.GetHexUnderPoint(this.ptRightClickOrigin);
         if(_loc2_ == null)
         {
            return;
         }
         _loc2_.IsCampTile = !_loc2_.IsCampTile;
         _loc2_ = null;
         this.ClearButtons();
      }
      
      public function ToggleEscMenu() : void
      {
         if(this.grpHelp.alive)
         {
            if(this.grpHelp.BackOut())
            {
               this.Mode(this.m_nGameState);
            }
         }
         else
         {
            this.grpHelp.Show();
            this.ClearPopUpText();
            this.grpHUD.kill();
            this.grpMsg.kill();
            this.grpVFX.kill();
            this.grpInventoryUI.Hide(true);
            this.grpMinimap.Hide();
            this.grpInventoryUI.grpDMCLayer.HideVFX();
            this.grpInventoryUI.grpDMCLayer.kill();
            this.grpMap.kill();
            this.grpLabels.kill();
            this.grpCreatures.kill();
            this.grpBtnActions.callAll("kill");
            this.grpBtnScreens.callAll("kill");
            if(this.sprHilight != null)
            {
               this.sprHilight.visible = false;
            }
         }
         this.camMsg.visible = !this.grpHelp.alive;
         this.btnMenu.status = FlxButton.NORMAL;
      }
      
      public function ShowItems() : void
      {
         this.grpInventoryUI.CheckAutoCloseEncounter();
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_ITEMS);
         if(this.grpInventoryUI.objEncounter.m_nType == Encounter.TYPE_COMBAT)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(157),false,false);
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(158),false,false);
            this.sprPlayer.m_tilCurrentHex.m_objBattle.TrimMoves(this.sprPlayer);
            this.grpInventoryUI.UpdateCombatItems();
         }
      }
      
      private function ShowEncounters() : void
      {
         if(this.grpInventoryUI.m_nState == GUIInventory.STATE_COMBAT || this.grpInventoryUI.m_nState == GUIInventory.STATE_COMBAT_TREASURE)
         {
            this.Mode(GAMESTATE_INVENTORY);
            this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_BATTLE);
         }
         else if(this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_TREASURE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER)
         {
            this.Mode(GAMESTATE_INVENTORY);
            this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_RESPONSE);
         }
         else if(this.tilCurrentHex.index == 20)
         {
            this.Mode(GAMESTATE_INVENTORY);
            this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_DMC);
         }
      }
      
      private function ShowVehicle() : void
      {
         this.grpInventoryUI.CheckAutoCloseEncounter();
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_VEHICLE);
         if(this.sprPlayer.m_tilCurrentHex.m_objBattle != null)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(157),false,false);
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(158),false,false);
            this.sprPlayer.m_tilCurrentHex.m_objBattle.TrimMoves(this.sprPlayer);
            this.grpInventoryUI.UpdateCombatItems();
         }
      }
      
      private function ShowCamp() : void
      {
         this.grpInventoryUI.CheckAutoCloseEncounter();
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_CAMP);
         if(this.sprPlayer.m_tilCurrentHex.m_objBattle != null)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(157),false,false);
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(158),false,false);
            this.sprPlayer.m_tilCurrentHex.m_objBattle.TrimMoves(this.sprPlayer);
            this.grpInventoryUI.UpdateCombatItems();
         }
      }
      
      private function ShowCraft() : void
      {
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_CRAFT);
         this.grpInventoryUI.UpdateCraftingItems(true);
         if(this.sprPlayer.m_tilCurrentHex.m_objBattle != null)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(157),false,false);
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(158),false,false);
            this.sprPlayer.m_tilCurrentHex.m_objBattle.TrimMoves(this.sprPlayer);
            this.grpInventoryUI.UpdateCombatItems();
         }
      }
      
      private function ShowMain() : void
      {
         this.Mode(GAMESTATE_GAMEREADY);
      }
      
      private function ShowConditions() : void
      {
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_HEALTH);
         if(this.sprPlayer.m_tilCurrentHex.m_objBattle != null)
         {
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(157),false,false);
            this.sprPlayer.AddCondition(this.sprPlayer.GetCondition(158),false,false);
            this.sprPlayer.m_tilCurrentHex.m_objBattle.TrimMoves(this.sprPlayer);
            this.grpInventoryUI.UpdateCombatItems();
         }
      }
      
      private function ShowMinimap() : void
      {
         this.Mode(GAMESTATE_MINIMAP);
      }
      
      private function ShowSkills() : void
      {
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_SKILLS);
      }
      
      private function ShowDMC() : void
      {
         this.Mode(GAMESTATE_INVENTORY);
         this.grpInventoryUI.UpdateScreens(GUIInventory.PANEL_DMC);
      }
      
      private function ToggleScreen(param1:ImgButton) : void
      {
         if(param1.alive)
         {
            if(!param1.on)
            {
               if(this.btnMainMap.alive)
               {
                  this.ShowMain();
               }
               else
               {
                  this.ShowEncounters();
               }
            }
            else
            {
               this.m_dictScreenFunctions[param1]();
            }
         }
      }
      
      private function AModeUp() : void
      {
         this.sprPlayer.ChangeAttackMode(this.sprPlayer.m_nAttackMode + 1);
         this.UpdatePlayerUI();
      }
      
      private function AModeDn() : void
      {
         this.sprPlayer.ChangeAttackMode(this.sprPlayer.m_nAttackMode - 1);
         this.UpdatePlayerUI();
      }
      
      public function ZoomCam() : void
      {
         if(this.grpHelp == null || this.camMsg == null)
         {
            return;
         }
         var _loc1_:FlxPoint = new FlxPoint(1360,768);
         var _loc2_:FlxPoint = GUIValues.GetPoint("PlayState.camMsg");
         _loc2_.x = _loc1_.x / 2 - (_loc1_.x / 2 - _loc2_.x) * GUIValues.m_fCamZoom;
         _loc2_.y = _loc1_.y / 2 - (_loc1_.y / 2 - _loc2_.y) * GUIValues.m_fCamZoom;
         this.camMsg.x = _loc2_.x;
         this.camMsg.y = _loc2_.y;
         this.camMsg.zoom = GUIValues.m_fCamZoom;
         this.camMsg.SetSize(GUIValues.GetInt("PlayState.camMsg.width"),GUIValues.GetInt("PlayState.camMsg.minheight"));
         this.camMsg.setBounds(0,0,this.camMsg.width,this.camMsg.height);
         this.grpMinimap.ZoomCam();
         if(this.grpInventoryUI.grpDMCLayer != null)
         {
            this.grpInventoryUI.grpDMCLayer.ZoomCam();
         }
      }
      
      public function SetRes() : void
      {
         var _loc3_:FlxPoint = null;
         var _loc5_:Creature = null;
         var _loc6_:Dictionary = null;
         var _loc7_:Object = null;
         var _loc8_:BattleMove = null;
         var _loc1_:int = GUIValues.GetInt("Item.zoom");
         if(GUIValues.m_fItemZoom != _loc1_)
         {
            _loc6_ = DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictItems;
            for(_loc7_ in _loc6_)
            {
               if(_loc6_[_loc7_] is Item)
               {
                  Item(_loc6_[_loc7_]).SetRes(_loc1_);
               }
            }
            _loc6_ = DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictMoves;
            for each(_loc8_ in _loc6_)
            {
               _loc8_.ItemRef.SetRes(_loc1_);
            }
            GUIValues.m_fItemZoom = _loc1_;
         }
         this.m_grpLetterbox.SetRes();
         if(this.m_nGameState < GAMESTATE_LOADINGCOMPLETE)
         {
            return;
         }
         var _loc2_:FlxPoint = new FlxPoint(1360,768);
         this.ZoomCam();
         this.grpMsg.SetRes();
         this.grpPopUp.SetRes();
         _loc3_ = GUIValues.GetPoint("offset");
         this.btnMenu.x = _loc3_.x;
         this.btnMenu.y = _loc3_.y;
         this.txtVersion.x = _loc3_.x;
         this.txtVersion.y = _loc3_.y + _loc2_.y - this.txtVersion.height - 14;
         _loc3_.y += this.btnMenu.height;
         this.txtMovesLeft.x = _loc3_.x;
         this.txtMovesLeft.y = _loc3_.y;
         _loc3_.y += 1;
         this.btnMovesBG.x = _loc3_.x;
         this.btnMovesBG.y = _loc3_.y;
         _loc3_.y += 23;
         this.grpHungerBar.x = _loc3_.x;
         this.grpHungerBar.y = _loc3_.y;
         _loc3_.y += this.grpHungerBar.height;
         this.grpThirstBar.x = _loc3_.x;
         this.grpThirstBar.y = _loc3_.y;
         _loc3_.y += this.grpThirstBar.height;
         this.grpSleepBar.x = _loc3_.x;
         this.grpSleepBar.y = _loc3_.y;
         _loc3_.y += this.grpSleepBar.height;
         this.grpLoadBar.x = _loc3_.x;
         this.grpLoadBar.y = _loc3_.y;
         _loc3_.y += this.grpLoadBar.height;
         this.grpBodyTempBar.x = _loc3_.x;
         this.grpBodyTempBar.y = _loc3_.y;
         _loc3_.y += this.grpBodyTempBar.height;
         this.grpAmbientBar.x = _loc3_.x;
         this.grpAmbientBar.y = _loc3_.y;
         _loc3_.y += this.grpAmbientBar.height;
         this.grpWoundBar.x = _loc3_.x;
         this.grpWoundBar.y = _loc3_.y;
         _loc3_.y += this.grpWoundBar.height;
         this.txtPlayerMoney.x = _loc3_.x;
         this.txtPlayerMoney.y = _loc3_.y;
         _loc3_ = GUIValues.GetPoint("PlayState.btnWait");
         this.btnWait.x = _loc3_.x;
         this.btnWait.y = _loc3_.y;
         _loc3_.y += this.btnWait.height;
         this.btnSleep.x = _loc3_.x;
         this.btnSleep.y = _loc3_.y;
         _loc3_.y += this.btnSleep.height;
         this.btnRest.x = _loc3_.x;
         this.btnRest.y = _loc3_.y;
         _loc3_.y += this.btnRest.height;
         this.btnRun.x = _loc3_.x;
         this.btnRun.y = _loc3_.y;
         this.txtMovesLeftReserve.x = _loc3_.x + GUIValues.GetPoint("PlayState.txtMovesLeftReserve").x;
         this.txtMovesLeftReserve.y = _loc3_.y + GUIValues.GetPoint("PlayState.txtMovesLeftReserve").y;
         _loc3_.y += this.btnRun.height;
         this.btnHide.x = _loc3_.x;
         this.btnHide.y = _loc3_.y;
         _loc3_.y += this.btnHide.height;
         this.btnHideTracks.x = _loc3_.x;
         this.btnHideTracks.y = _loc3_.y;
         _loc3_.y += this.btnHideTracks.height;
         this.btnSpy.x = _loc3_.x;
         this.btnSpy.y = _loc3_.y;
         _loc3_.y += this.btnSpy.height;
         this.btnScavenge.x = _loc3_.x;
         this.btnScavenge.y = _loc3_.y;
         _loc3_.y += this.btnScavenge.height;
         _loc3_ = GUIValues.GetPoint("PlayState.btnMainMap");
         this.btnMainMap.x = _loc3_.x;
         this.btnMainMap.y = _loc3_.y;
         _loc3_.y += this.btnMainMap.height;
         this.btnMap.x = _loc3_.x;
         this.btnMap.y = _loc3_.y;
         _loc3_.y += this.btnMap.height;
         this.btnEncounter.x = _loc3_.x;
         this.btnEncounter.y = _loc3_.y;
         this.sprEncounterBtn.x = _loc3_.x;
         this.sprEncounterBtn.y = _loc3_.y;
         _loc3_.y += this.btnEncounter.height;
         this.btnSkills.x = _loc3_.x;
         this.btnSkills.y = _loc3_.y;
         _loc3_.y += this.btnSkills.height;
         this.btnCraft.x = _loc3_.x;
         this.btnCraft.y = _loc3_.y;
         _loc3_ = GUIValues.GetPoint("PlayState.btnItems");
         this.btnItems.x = _loc3_.x;
         this.btnItems.y = _loc3_.y;
         _loc3_.y += this.btnItems.height;
         this.btnConditions.x = _loc3_.x;
         this.btnConditions.y = _loc3_.y;
         _loc3_.y += this.btnConditions.height;
         this.btnCamp.x = _loc3_.x;
         this.btnCamp.y = _loc3_.y;
         _loc3_.y += this.btnCraft.height;
         this.btnVehicle.x = _loc3_.x;
         this.btnVehicle.y = _loc3_.y;
         _loc3_ = GUIValues.GetPoint("PlayState.m_txtLoadMessage");
         this.m_txtLoadMessage.x = _loc3_.x;
         this.m_txtLoadMessage.y = _loc3_.y;
         this.m_txtLoadMessage.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"));
         if(false == true && false)
         {
            this.m_txtLoadMessage.SetWidth(800);
         }
         else
         {
            this.m_txtLoadMessage.SetWidth(300);
         }
         _loc3_ = GUIValues.GetPoint("PlayState.txtHexInfo");
         this.txtHexInfo.x = _loc3_.x;
         this.txtHexInfo.y = _loc3_.y;
         this.txtHexInfo.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"));
         this.txtHexInfo.SetWidth(GUIValues.GetInt("PlayState.txtHexInfo.width"));
         var _loc4_:String = "";
         if(GUIValues.GetInt("Item.zoom") == 2)
         {
            _loc4_ = DataHandler.m_strZoomPrefix;
         }
         _loc3_ = GUIValues.GetPoint("PlayState.sprCorner");
         this.sprCorner.x = _loc3_.x;
         this.sprCorner.y = _loc3_.y;
         _loc3_ = GUIValues.GetPoint("PlayState.sprAttackModeBG");
         this.sprAttackModeBG.pixels = DataHandler.GetImage(GUIValues.GetString("PlayState.sprAttackModeBG.image"));
         this.sprAttackModeBG.x = _loc3_.x;
         this.sprAttackModeBG.y = _loc3_.y;
         this.sprAttackMode.pixels = DataHandler.GetImage(this.sprPlayer.CurrentAttackMode.m_strIMG,_loc4_);
         this.sprAttackMode.x = _loc3_.x;
         this.sprAttackMode.y = _loc3_.y + (this.sprAttackModeBG.height - this.sprAttackMode.height) / 2;
         _loc3_ = GUIValues.GetPoint("PlayState.txtAttackMode");
         this.txtAttackMode.x = _loc3_.x;
         this.txtAttackMode.y = _loc3_.y;
         this.txtAttackMode.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"));
         this.txtAttackMode.SetWidth(GUIValues.GetInt("PlayState.txtAttackMode.width"));
         _loc4_ = "";
         if(GUIValues.GetInt("PlayState.grpAttackMode.zoom") == 2)
         {
            _loc4_ = DataHandler.m_strZoomPrefix;
         }
         this.txtAttackModeCharges.SetWidth(GUIValues.GetInt("PlayState.txtAttackModeCharges.width"));
         this.txtAttackModeCharges.setFormat(GUIValues.GetString("strLabelFontName"),GUIValues.GetInt("nLabelFontSize"),GUIValues.GetInt("nBodyFontColor"));
         if(this.sprPlayer.CurrentAttackMode.m_bmpChargeIMG != null)
         {
            this.sprAttackModeCharge.pixels = DataHandler.GetImage(this.sprPlayer.CurrentAttackMode.m_strChargeIMG,_loc4_);
         }
         _loc3_ = GUIValues.GetPoint("PlayState.btnAmodeDn");
         this.btnAmodeDn.x = _loc3_.x;
         this.btnAmodeDn.y = _loc3_.y;
         this.btnAmodeDn.Zoom(GUIValues.GetInt("PlayState.grpAttackMode.zoom"));
         _loc3_ = GUIValues.GetPoint("PlayState.btnAmodeUp");
         this.btnAmodeUp.x = _loc3_.x;
         this.btnAmodeUp.y = _loc3_.y;
         this.btnAmodeUp.Zoom(GUIValues.GetInt("PlayState.grpAttackMode.zoom"));
         this.UpdateAttackModeUI();
         this.grpInventoryUI.SetRes();
         this.sprPlayer.SetRes();
         this.grpWeatherNode.SetRes();
         if(this.m_nGameState == GAMESTATE_INVENTORY)
         {
            this.grpWeatherNode.MoveIcon(GUIValues.GetPoint("GUIBattleScreen.WeatherNode"));
         }
         for each(_loc5_ in this.m_aCreatures)
         {
            _loc5_.SetRes();
         }
         if(this.m_nGameState < GAMESTATE_READINGMAPCOMPLETE)
         {
            return;
         }
         this.grpMinimap.SetRes();
      }
      
      private function KeyHandler() : void
      {
         if(FlxG.keys.justReleased("F1"))
         {
            this.ToggleEscMenu();
         }
         if(FlxG.keys.justReleased("TAB"))
         {
            if(this.btnMainMap.alive)
            {
               this.ShowMain();
            }
         }
         var _loc1_:int = this.nScrollSpeed;
         if(FlxG.keys.SHIFT)
         {
            _loc1_ = this.nScrollSpeedMod;
         }
         this.grpInventoryUI.StackCursor(!FlxG.keys.SHIFT);
         if(FlxG.keys.justReleased("SPACE"))
         {
            if(this.sprPlayer.Resting && this.m_nGameState == GAMESTATE_DMRUNNING)
            {
               this.sprPlayer.Resting = false;
            }
            else if(this.m_nGameState == GAMESTATE_GAMEREADY)
            {
               this.BtnEndTurn();
            }
            else if(this.m_nGameState == GAMESTATE_INVENTORY)
            {
               if(this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_TREASURE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE || this.grpInventoryUI.m_nState == GUIInventory.STATE_COMBAT || this.grpInventoryUI.m_nState == GUIInventory.STATE_COMBAT_TREASURE)
               {
                  this.grpInventoryUI.ConfirmResponse();
               }
               else if(this.grpInventoryUI.m_nState == GUIInventory.STATE_SKILL_EXCLUSIVE)
               {
                  this.grpInventoryUI.btnSkillsConfirm.OnUp();
               }
               else if(this.grpInventoryUI.m_nState == GUIInventory.STATE_NORMAL && this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_CRAFT && this.grpInventoryUI.btnCraftConfirm.alive)
               {
                  this.grpInventoryUI.ConfirmCraft();
               }
               else if(this.grpInventoryUI.m_nState == GUIInventory.STATE_NORMAL && this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_CRAFT && this.grpInventoryUI.btnCraftClear.alive)
               {
                  this.grpInventoryUI.ClearCraftYield();
               }
            }
            else if(this.m_nGameState == GAMESTATE_READINGMAPCOMPLETE)
            {
               if(this.grpInventoryUI.m_nState == GUIInventory.STATE_SKILL_EXCLUSIVE)
               {
                  this.grpInventoryUI.btnSkillsConfirm.OnUp();
               }
            }
         }
         if(FlxG.keys.W || FlxG.keys.UP)
         {
            if(this.m_nGameState == GAMESTATE_MINIMAP)
            {
               this.MoveCamera(new FlxPoint(0,_loc1_ * this.grpMinimap.nScrollMod * DataHandler.nFPSModifier));
            }
            else if(this.m_nGameState == GAMESTATE_INVENTORY && this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_DMC)
            {
               this.grpInventoryUI.grpDMCLayer.MoveCamera(0,this.grpInventoryUI.grpDMCLayer.nScrollSpeed * DataHandler.nFPSModifier);
            }
            else if(this.m_nGameState != GAMESTATE_INVENTORY)
            {
               this.MoveCamera(new FlxPoint(0,_loc1_ * DataHandler.nFPSModifier));
            }
         }
         if(FlxG.keys.A || FlxG.keys.LEFT)
         {
            if(this.m_nGameState == GAMESTATE_MINIMAP)
            {
               this.MoveCamera(new FlxPoint(_loc1_ * this.grpMinimap.nScrollMod * DataHandler.nFPSModifier,0));
            }
            else if(this.m_nGameState == GAMESTATE_INVENTORY && this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_DMC)
            {
               this.grpInventoryUI.grpDMCLayer.MoveCamera(this.grpInventoryUI.grpDMCLayer.nScrollSpeed * DataHandler.nFPSModifier,0);
            }
            else if(this.m_nGameState != GAMESTATE_INVENTORY)
            {
               this.MoveCamera(new FlxPoint(_loc1_ * DataHandler.nFPSModifier,0));
            }
         }
         if(FlxG.keys.S || FlxG.keys.DOWN)
         {
            if(this.m_nGameState == GAMESTATE_MINIMAP)
            {
               this.MoveCamera(new FlxPoint(0,-_loc1_ * this.grpMinimap.nScrollMod * DataHandler.nFPSModifier));
            }
            else if(this.m_nGameState == GAMESTATE_INVENTORY && this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_DMC)
            {
               this.grpInventoryUI.grpDMCLayer.MoveCamera(0,-this.grpInventoryUI.grpDMCLayer.nScrollSpeed * DataHandler.nFPSModifier);
            }
            else if(this.m_nGameState != GAMESTATE_INVENTORY)
            {
               this.MoveCamera(new FlxPoint(0,-_loc1_ * DataHandler.nFPSModifier));
            }
         }
         if(FlxG.keys.D || FlxG.keys.RIGHT)
         {
            if(this.m_nGameState == GAMESTATE_MINIMAP)
            {
               this.MoveCamera(new FlxPoint(-_loc1_ * this.grpMinimap.nScrollMod * DataHandler.nFPSModifier,0));
            }
            else if(this.m_nGameState == GAMESTATE_INVENTORY && this.grpInventoryUI.m_nPanel == GUIInventory.PANEL_DMC)
            {
               this.grpInventoryUI.grpDMCLayer.MoveCamera(-this.grpInventoryUI.grpDMCLayer.nScrollSpeed * DataHandler.nFPSModifier,0);
            }
            else if(this.m_nGameState != GAMESTATE_INVENTORY)
            {
               this.MoveCamera(new FlxPoint(-_loc1_ * DataHandler.nFPSModifier,0));
            }
         }
         if(FlxG.keys.justReleased("I") || FlxG.keys.justReleased("Q"))
         {
            this.btnItems.on = !this.btnItems.on;
            this.ToggleScreen(this.btnItems);
         }
         if(FlxG.keys.justReleased("C"))
         {
            this.btnConditions.on = !this.btnConditions.on;
            this.ToggleScreen(this.btnConditions);
         }
         if(FlxG.keys.justReleased("M"))
         {
            this.btnMap.on = !this.btnMap.on;
            this.ToggleScreen(this.btnMap);
         }
         if(FlxG.keys.justReleased("R"))
         {
            this.btnCamp.on = !this.btnCamp.on;
            this.ToggleScreen(this.btnCamp);
         }
         if(FlxG.keys.justReleased("X"))
         {
            this.btnCraft.on = !this.btnCraft.on;
            this.ToggleScreen(this.btnCraft);
         }
         if(FlxG.keys.justReleased("V"))
         {
            this.btnVehicle.on = !this.btnVehicle.on;
            this.ToggleScreen(this.btnVehicle);
         }
         if(FlxG.keys.justReleased("ONE"))
         {
            if(this.grpInventoryUI.m_nMouseMode != GUIInventory.MOUSE_TAKE)
            {
               this.grpInventoryUI.MouseMode(GUIInventory.MOUSE_TAKE);
            }
            else
            {
               this.grpInventoryUI.MouseMode(GUIInventory.MOUSE_DRAG);
            }
            this.bResetCursor = true;
         }
         else if(FlxG.keys.pressed("TWO"))
         {
            if((this.grpInventoryUI.m_nState == GUIInventory.STATE_NORMAL || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_TREASURE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE) && this.grpInventoryUI.m_nPanel != GUIInventory.PANEL_CRAFT && this.grpInventoryUI.m_nMouseMode != GUIInventory.MOUSE_USE)
            {
               this.grpInventoryUI.MouseMode(GUIInventory.MOUSE_USE);
            }
            this.bResetCursor = true;
         }
         else if(FlxG.keys.pressed("THREE"))
         {
            if((this.grpInventoryUI.m_nState == GUIInventory.STATE_NORMAL || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_TREASURE || this.grpInventoryUI.m_nState == GUIInventory.STATE_ENCOUNTER_EXCLUSIVETREASURE) && this.grpInventoryUI.m_nMouseMode != GUIInventory.MOUSE_DELETE)
            {
               this.grpInventoryUI.MouseMode(GUIInventory.MOUSE_DELETE);
            }
            this.bResetCursor = true;
         }
         else if(this.bResetCursor)
         {
            this.grpInventoryUI.MouseMode();
         }
         if(FlxG.keys.justReleased("E"))
         {
            if(this.btnScavenge.alive)
            {
               this.btnScavenge.on = true;
               this.ToggleScreen(this.btnScavenge);
            }
            else
            {
               this.btnEncounter.on = !this.btnEncounter.on;
               this.ToggleScreen(this.btnEncounter);
            }
         }
         if(FlxG.keys.justReleased("PAGEDOWN"))
         {
            if(this.m_nGameState == GAMESTATE_MAPEDITOR)
            {
               --this.nHexType;
               if(this.nHexType < 0)
               {
                  this.nHexType = DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_aHexTypes.length - 1;
               }
               this.sprHilight.pixels = this.tmapHexes.GetImage(this.nHexType);
            }
            else
            {
               this.grpMsg.ScrollDown();
            }
         }
         if(FlxG.keys.justReleased("PAGEUP"))
         {
            if(this.m_nGameState == GAMESTATE_MAPEDITOR)
            {
               ++this.nHexType;
               if(this.nHexType >= DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_aHexTypes.length)
               {
                  this.nHexType = 0;
               }
               this.sprHilight.pixels = this.tmapHexes.GetImage(this.nHexType);
            }
            else
            {
               this.grpMsg.ScrollUp();
            }
         }
         if(FlxG.keys.justReleased("O"))
         {
            this.AModeUp();
         }
         else if(FlxG.keys.justReleased("L"))
         {
            this.AModeDn();
         }
      }
      
      public function LoadGame() : void
      {
         var _loc3_:Vector.<AICreature> = null;
         var _loc4_:Array = null;
         var _loc5_:SaveGameHex = null;
         var _loc6_:int = 0;
         var _loc7_:int = 0;
         var _loc8_:EncounterTrigger = null;
         var _loc9_:AICreature = null;
         var _loc10_:SaveGameCreature = null;
         var _loc11_:FlxHexTile = null;
         var _loc12_:int = 0;
         var _loc13_:Number = NaN;
         var _loc14_:String = null;
         var _loc15_:FlxPoint = null;
         var _loc16_:GUIInventorySlot = null;
         var _loc17_:Vector.<ItemInstance> = null;
         var _loc18_:ItemInstance = null;
         var _loc19_:SaveGameItem = null;
         var _loc1_:int = getTimer();
         var _loc2_:int = _loc1_;
         switch(this.m_nLoadingStage)
         {
            case 0:
               this.grpMsg.m_bIgnoreMessages = true;
               this.grpHUD.add(this.m_txtLoadMessage);
               this.m_objLoadData = new Object();
               this.objDate.setTime(this.objStartDate.getTime() + this.m_objSG.m_fHours * 60 * 60 * 1000);
               this.objOldDate.setTime(this.objStartDate.getTime() + (this.m_objSG.m_fHours - PlayState.HOURS_PER_TURN) * 60 * 60 * 1000);
               this.bScavTut = this.m_objSG.m_bScavTut;
               this.UpdateDate();
               this.m_txtLoadMessage.text = "载入: 事件触发器...";
               ++this.m_nLoadingStage;
               break;
            case 1:
               DataHandler.ResetEncounterTriggers();
               for each(_loc7_ in this.m_objSG.m_vEncounterTriggersRemaining)
               {
                  _loc8_ = DataHandler.GetEncounterTrigger(_loc7_);
                  DataHandler.AddEncounterTrigger(_loc8_);
               }
               this.m_txtLoadMessage.text = "载入: 读取 事件队列...";
               ++this.m_nLoadingStage;
               break;
            case 2:
               DM.m_aEventQueue = new Array();
               for each(_loc7_ in this.m_objSG.m_vEventQueue)
               {
                  DM.AppendEncounter(DataHandler.GetEncounter(_loc7_ + 1));
               }
               this.m_txtLoadMessage.text = "载入: 生物列表...";
               ++this.m_nLoadingStage;
               break;
            case 3:
               for each(_loc9_ in this.m_aCreatures)
               {
                  this.RemoveCreature(_loc9_);
               }
               _loc3_ = new Vector.<AICreature>();
               for each(_loc10_ in this.m_objSG.m_vCreatures)
               {
                  if((_loc9_ = DataHandler.GetCreature(_loc10_.m_nID)) != null)
                  {
                     _loc9_.SaveData = _loc10_;
                     _loc3_.push(_loc9_);
                  }
               }
               this.m_objLoadData.vCreatures = _loc3_;
               for each(_loc11_ in this.tmapHexes.vVisibleHexes)
               {
                  _loc11_.nExploredState = 2;
               }
               MapUtils.tmapHexes.vVisibleHexes = new Vector.<FlxHexTile>();
               this.m_objLoadData.nStart = 0;
               this.m_objLoadData.aTiles = MapUtils.tmapHexes.getTiles();
               this.m_txtLoadMessage.text = "载入: 已探索地块...";
               ++this.m_nLoadingStage;
               break;
            case 4:
               _loc4_ = this.m_objLoadData.aTiles as Array;
               _loc3_ = Vector.<AICreature>(this.m_objLoadData.vCreatures);
               _loc7_ = _loc6_ = int(this.m_objLoadData.nStart);
               while(_loc7_ < this.m_objSG.m_vVisibleHexes.length)
               {
                  _loc5_ = this.m_objSG.m_vVisibleHexes[_loc7_];
                  if((_loc11_ = _loc4_[_loc5_.m_nMapIndex]).GroundObject.Slot != null)
                  {
                     _loc11_.GroundObject.Slot.UnSocketItem(true);
                  }
                  _loc11_.SaveData = _loc5_;
                  _loc11_.CalculateValue();
                  if(_loc5_.m_nExploredState < 2)
                  {
                     this.grpMinimap.MinimapHex(_loc11_);
                  }
                  for each(_loc12_ in _loc5_.m_vOccupantIndices)
                  {
                     _loc13_ = _loc11_.Scent;
                     if(_loc12_ >= 0 && _loc12_ < _loc3_.length)
                     {
                        this.AddCreature(_loc3_[_loc12_],_loc11_.GetHexCoords());
                     }
                     _loc11_.Scent = _loc13_;
                  }
                  if(_loc5_.m_nScentOwnerIndex >= 0 && _loc5_.m_nScentOwnerIndex < _loc3_.length)
                  {
                     _loc11_.m_objScentOwner = _loc3_[_loc5_.m_nScentOwnerIndex];
                  }
                  else if(_loc5_.m_nScentOwnerIndex == -2)
                  {
                     _loc11_.m_objScentOwner = this.sprPlayer;
                  }
                  _loc2_ = getTimer();
                  if(_loc2_ - _loc1_ > 100)
                  {
                     this.m_txtLoadMessage.text = "载入: 已探索地块 " + _loc7_ + " 共 " + this.m_objSG.m_vVisibleHexes.length + "...";
                     this.m_objLoadData.nStart = _loc7_ + 1;
                     return;
                  }
                  _loc7_++;
               }
               this.m_objLoadData.aTiles = _loc4_;
               this.m_txtLoadMessage.text = "载入: 大地图...";
               ++this.m_nLoadingStage;
               break;
            case 5:
               _loc4_ = this.m_objLoadData.aTiles as Array;
               for(_loc14_ in this.m_objSG.m_dictMapLabels)
               {
                  _loc15_ = FlxHexTile(_loc4_[int(_loc14_)]).GetHexCoords();
                  this.RevealHex(_loc15_.x,_loc15_.y,this.m_objSG.m_dictMapLabels[_loc14_],false);
               }
               for(_loc14_ in this.m_objSG.m_dictMinimapLabels)
               {
                  _loc15_ = FlxHexTile(_loc4_[int(_loc14_)]).GetHexCoords();
                  this.RevealHex(_loc15_.x,_loc15_.y,this.m_objSG.m_dictMinimapLabels[_loc14_],true);
               }
               this.m_txtLoadMessage.text = "载入: 玩家数据...";
               ++this.m_nLoadingStage;
               break;
            case 6:
               this.sprPlayer.Money = this.m_objSG.m_fMoney;
               this.sprPlayer.m_strVersion = this.m_objSG.m_strVersion;
               _loc13_ = MapUtils.GetTileByCoords(this.m_objSG.m_ptHex).Scent;
               this.sprPlayer.Spawn(this.m_objSG.m_ptHex);
               this.sprPlayer.m_tilCurrentHex.Scent = _loc13_;
               for each(_loc16_ in this.grpInventoryUI.vSaveSlots)
               {
                  _loc17_ = _loc16_.GetAllSocketedItems();
                  for each(_loc18_ in _loc17_)
                  {
                     _loc16_.UnSocketItem(true,_loc18_);
                  }
               }
               for each(_loc19_ in this.m_objSG.m_vNonInventoryItems)
               {
                  (_loc18_ = DataHandler.GetItem(_loc19_.strID)).SaveData = _loc19_;
                  for each(_loc16_ in this.grpInventoryUI.vSaveSlots)
                  {
                     if(_loc16_.nSlotIndex == _loc19_.m_nSlotIndex)
                     {
                        _loc16_.SocketItem(_loc18_);
                        break;
                     }
                  }
               }
               this.sprPlayer.grpCampSlot.UnSocketItem(true);
               this.sprPlayer.SaveData = this.m_objSG.m_objPlayer;
               this.m_txtLoadMessage.text = "载入: 已学会配方...";
               ++this.m_nLoadingStage;
               break;
            case 7:
               for each(_loc7_ in this.m_objSG.m_vKnownRecipes)
               {
                  this.sprPlayer.AddRecipe = _loc7_;
               }
               this.m_txtLoadMessage.text = "载入: 后期处理...";
               ++this.m_nLoadingStage;
               break;
            case 8:
               _loc13_ = this.sprPlayer.m_tilCurrentHex.Scent;
               this.AlignPlayerToHex(this.sprPlayer.m_tilCurrentHex);
               this.sprPlayer.m_tilCurrentHex.Scent = _loc13_;
               this.grpInventoryUI.UpdateCraftingItems(false);
               if(false == true && false)
               {
                  this.grpInventoryUI.add(this.m_txtLoadMessage);
               }
               else
               {
                  this.grpHUD.remove(this.m_txtLoadMessage);
               }
               this.grpMsg.m_bIgnoreMessages = false;
               this.m_nLoadingStage = -1;
               this.m_objLoadData = null;
         }
      }
   }
}

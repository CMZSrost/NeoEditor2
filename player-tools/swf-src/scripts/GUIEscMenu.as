package
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.StageDisplayState;
   import flash.display.StageScaleMode;
   import flash.events.*;
   import flash.geom.Point;
   import flash.net.URLRequest;
   import flash.net.navigateToURL;
   import flash.system.Capabilities;
   import flash.system.fscommand;
   import flash.utils.getQualifiedClassName;
   import flash.utils.getTimer;
   import org.flixel.*;
   
   public class GUIEscMenu extends FlxGroup
   {
      
      public static var m_fMusicVolume:Number;
      
      public static var m_fSoundVolume:Number;
      
      private static var m_bPrefsLoaded:Boolean;
      
      public static var bAutosave:Boolean;
      
      private static const MODE_NORMAL:uint = 0;
      
      private static const MODE_CREDITS:uint = 1;
      
      private static const MODE_INSTRUCTIONS:uint = 2;
      
      private static const MODE_OPTIONS:uint = 3;
       
      
      private var m_btnPlay:ImgButton;
      
      private var m_btnExit:ImgButton;
      
      private var m_btnCredits:ImgButton;
      
      private var m_btnOptions:ImgButton;
      
      private var m_btnMute:ImgButton;
      
      private var m_btnInstr:ImgButton;
      
      private var m_btnBBG:ImgButton;
      
      private var m_btnGreenlight:ImgButton;
      
      private var m_sprBugsBG:FlxSprite;
      
      private var m_btnZomboid:ImgButton;
      
      private var m_btnAUTO:ImgButton;
      
      private var m_btn800:ImgButton;
      
      private var m_btn1024:ImgButton;
      
      private var m_btn1360:ImgButton;
      
      private var m_btn1600:ImgButton;
      
      private var m_btn2400:ImgButton;
      
      private var m_btnReset:ImgButton;
      
      private var m_btnFullscreen:ImgButton;
      
      private var m_btnFilter:ImgButton;
      
      private var m_btnStretch:ImgButton;
      
      private var m_btnTerEdit:FlxButton;
      
      private var m_btnResume:ImgButton;
      
      private var m_btnSave:ImgButton;
      
      private var m_nSaveAlphaMin:int = 0;
      
      private var m_btnLoad:ImgButton;
      
      private var m_btnQuit:ImgButton;
      
      private var m_btnMoreInfo:ImgButton;
      
      private var m_btnRetry:ImgButton;
      
      private var m_btnOK:ImgButton;
      
      private var m_btnCancel:ImgButton;
      
      private var m_btnMusicUp:ImgButton;
      
      private var m_btnMusicDn:ImgButton;
      
      private var m_btnSoundUp:ImgButton;
      
      private var m_btnSoundDn:ImgButton;
      
      private var m_btnMasterUp:ImgButton;
      
      private var m_btnMasterDn:ImgButton;
      
      private var m_btnFPSUp:ImgButton;
      
      private var m_btnFPSDn:ImgButton;
      
      private var m_btnManual:ImgButton;
      
      private var m_btnWiki:ImgButton;
      
      private var m_btnMain:ImgButton;
      
      private var m_btnAutosave:ImgButton;
      
      private var m_sprTitle:FlxSprite;
      
      private var m_sprCredits:FlxSprite;
      
      private var m_sprSparkle:FlxSprite;
      
      private var m_sprSaveGameWarning:FlxSprite;
      
      private var m_sprHelpPlayer:FlxSprite;
      
      private var m_sprHelpCur:FlxSprite;
      
      private var m_sprHelpRClick:FlxSprite;
      
      private var m_sprHelpWASD:FlxSprite;
      
      private var m_sprHelpLClick:FlxSprite;
      
      private var m_sprHelpL2Click:FlxSprite;
      
      private var m_sprHelpSpace:FlxSprite;
      
      private var m_sprHelpSLClick:FlxSprite;
      
      private var m_sprTitleBG1:FlxSprite;
      
      private var m_sprTitleMtns1:FlxSprite;
      
      private var m_sprTitleIslands1:FlxSprite;
      
      private var m_sprTitleMid1:FlxSprite;
      
      private var m_sprTitleTrees1:FlxSprite;
      
      private var m_sprTitleGrass1:FlxSprite;
      
      private var m_sprTitleMtns2:FlxSprite;
      
      private var m_sprTitleIslands2:FlxSprite;
      
      private var m_sprTitleMid2:FlxSprite;
      
      private var m_sprTitleTrees2:FlxSprite;
      
      private var m_sprTitleGrass2:FlxSprite;
      
      private var m_vBGs:Vector.<FlxSprite>;
      
      private var m_nWrapX:int;
      
      private var m_txtVersion:FlxText;
      
      private var m_txtCopyright:FlxText;
      
      private var txtCreditNames:FlxText;
      
      private var txtCreditRoles:FlxText;
      
      private var m_txtLoadingGame:FlxText;
      
      private var m_txtMusicVolName:FlxText;
      
      private var m_txtMusicVolValue:FlxText;
      
      private var m_txtSoundVolName:FlxText;
      
      private var m_txtSoundVolValue:FlxText;
      
      private var m_txtMasterVolName:FlxText;
      
      private var m_txtMasterVolValue:FlxText;
      
      private var m_txtFPSName:FlxText;
      
      private var m_txtFPSValue:FlxText;
      
      private var vCursors:Vector.<Bitmap>;
      
      private var vScreenSizes:Vector.<Vector.<int>>;
      
      private var nCursor:int;
      
      private var nResOverride:int;
      
      private var strFullscreenOverride:String;
      
      private var strStretchOverride:String;
      
      private var m_nMode:int;
      
      private var m_grpBG:FlxGroup;
      
      private var m_grpButtons:FlxGroup;
      
      private var m_grpCredits:FlxGroup;
      
      private var m_grpInstructions:FlxGroup;
      
      private var m_grpOptions:FlxGroup;
      
      private var m_sndTitle:FlxSound;
      
      public function GUIEscMenu()
      {
         super();
         if(FlxG.state is MenuState)
         {
            this.m_sndTitle = FlxG.loadSound(cueTitleMusic);
            this.m_sndTitle.play();
         }
         this.m_grpBG = new FlxGroup();
         this.m_grpButtons = new FlxGroup();
         this.m_grpCredits = new FlxGroup();
         this.m_grpOptions = new FlxGroup();
         this.m_grpInstructions = new FlxGroup();
         m_fMusicVolume = 1;
         m_fSoundVolume = 1;
         bAutosave = true;
         this.m_nMode = MODE_NORMAL;
         this.nCursor = -1;
         this.nResOverride = -1;
         this.strStretchOverride = "";
         this.strFullscreenOverride = "";
         this.vScreenSizes = new Vector.<Vector.<int>>();
         this.vScreenSizes.push(Vector.<int>([800,450,GUIValues.GUITYPE_800X450]));
         this.vScreenSizes.push(Vector.<int>([800,600,GUIValues.GUITYPE_800X450]));
         this.vScreenSizes.push(Vector.<int>([1024,768,GUIValues.GUITYPE_1024X768]));
         this.vScreenSizes.push(Vector.<int>([1280,720,GUIValues.GUITYPE_800X450]));
         this.vScreenSizes.push(Vector.<int>([1280,768,GUIValues.GUITYPE_800X450]));
         this.vScreenSizes.push(Vector.<int>([1280,800,GUIValues.GUITYPE_800X450]));
         this.vScreenSizes.push(Vector.<int>([1280,960,GUIValues.GUITYPE_1024X768]));
         this.vScreenSizes.push(Vector.<int>([1280,1024,GUIValues.GUITYPE_1024X768]));
         this.vScreenSizes.push(Vector.<int>([1360,768,GUIValues.GUITYPE_1360X768]));
         this.vScreenSizes.push(Vector.<int>([1366,768,GUIValues.GUITYPE_1360X768]));
         this.vScreenSizes.push(Vector.<int>([1440,900,GUIValues.GUITYPE_1360X768]));
         this.vScreenSizes.push(Vector.<int>([1600,900,GUIValues.GUITYPE_1600X900]));
         this.vScreenSizes.push(Vector.<int>([1600,1200,GUIValues.GUITYPE_1024X768]));
         this.vScreenSizes.push(Vector.<int>([1680,1050,GUIValues.GUITYPE_1600X900]));
         this.vScreenSizes.push(Vector.<int>([1920,1080,GUIValues.GUITYPE_1600X900]));
         this.vScreenSizes.push(Vector.<int>([1920,1200,GUIValues.GUITYPE_1600X900]));
         this.vScreenSizes.push(Vector.<int>([2400,1350,GUIValues.GUITYPE_2400X1350]));
         this.vScreenSizes.push(Vector.<int>([2560,1600,GUIValues.GUITYPE_2400X1350]));
         var _loc1_:FlxPoint = new FlxPoint();
         this.m_sprTitleBG1 = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitleBG1.pixels = DataHandler.GetImage("titleScrollBg.png");
         this.m_nWrapX = _loc1_.x + this.m_sprTitleBG1.width;
         this.m_sprTitleMtns1 = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitleMtns1.pixels = DataHandler.GetImage("titleScrollMtns.png");
         this.m_sprTitleMtns2 = new FlxSprite(_loc1_.x - this.m_sprTitleBG1.width,_loc1_.y);
         this.m_sprTitleMtns2.pixels = DataHandler.GetImage("titleScrollMtns.png");
         this.m_sprTitleIslands1 = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitleIslands1.pixels = DataHandler.GetImage("titleScrollIslands.png");
         this.m_sprTitleIslands2 = new FlxSprite(_loc1_.x - this.m_sprTitleBG1.width,_loc1_.y);
         this.m_sprTitleIslands2.pixels = DataHandler.GetImage("titleScrollIslands.png");
         this.m_sprTitleMid1 = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitleMid1.pixels = DataHandler.GetImage("titleScrollMid.png");
         this.m_sprTitleMid2 = new FlxSprite(_loc1_.x - this.m_sprTitleBG1.width,_loc1_.y);
         this.m_sprTitleMid2.pixels = DataHandler.GetImage("titleScrollMid.png");
         this.m_sprTitleTrees1 = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitleTrees1.pixels = DataHandler.GetImage("titleScrollTrees.png");
         this.m_sprTitleTrees2 = new FlxSprite(_loc1_.x - this.m_sprTitleBG1.width,_loc1_.y);
         this.m_sprTitleTrees2.pixels = DataHandler.GetImage("titleScrollTrees.png");
         this.m_sprTitleGrass1 = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitleGrass1.pixels = DataHandler.GetImage("titleScrollGrass.png");
         this.m_sprTitleGrass2 = new FlxSprite(_loc1_.x - this.m_sprTitleBG1.width,_loc1_.y);
         this.m_sprTitleGrass2.pixels = DataHandler.GetImage("titleScrollGrass.png");
         this.m_vBGs = Vector.<FlxSprite>([this.m_sprTitleGrass1,this.m_sprTitleGrass2,this.m_sprTitleTrees1,this.m_sprTitleTrees2,this.m_sprTitleMid1,this.m_sprTitleMid2,this.m_sprTitleMtns1,this.m_sprTitleMtns2,this.m_sprTitleIslands1,this.m_sprTitleIslands2]);
         this.m_sprTitle = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprTitle.pixels = DataHandler.GetImage("nsLogo.png");
         this.m_sprCredits = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprCredits.pixels = new BitmapData(1,1,false,4278190080);
         this.m_sprSaveGameWarning = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprSaveGameWarning.pixels = DataHandler.GetImage("GUISaveGameWarning.png");
         this.m_sprHelpCur = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpCur.pixels = DataHandler.GetImage("sprHelpCur.png");
         this.m_sprHelpL2Click = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpL2Click.pixels = DataHandler.GetImage("sprHelpL2Click.png");
         this.m_sprHelpLClick = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpLClick.pixels = DataHandler.GetImage("sprHelpLClick.png");
         this.m_sprHelpPlayer = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpPlayer.pixels = DataHandler.GetImage("sprHelpPlayer.png");
         this.m_sprHelpRClick = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpRClick.pixels = DataHandler.GetImage("sprHelpRClick.png");
         this.m_sprHelpSLClick = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpSLClick.pixels = DataHandler.GetImage("sprHelpSLClick.png");
         this.m_sprHelpSpace = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpSpace.pixels = DataHandler.GetImage("sprHelpSpace.png");
         this.m_sprHelpWASD = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprHelpWASD.pixels = DataHandler.GetImage("sprHelpWASD.png");
         this.m_txtVersion = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.m_txtVersion.width"),DataHandler.m_strVersion);
         this.m_txtCopyright = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.m_txtCopyright.width"),DataHandler.m_strCopyright);
         this.txtCreditNames = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditNames.width"),DataHandler.m_strCredits);
         this.txtCreditRoles = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),DataHandler.m_strCreditRoles);
         this.m_txtMasterVolName = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"游戏音量");
         this.m_txtMusicVolName = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"音乐音量");
         this.m_txtSoundVolName = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"音效音量");
         this.m_txtFPSName = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"锁定 FPS");
         this.m_txtMasterVolValue = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"100%");
         this.m_txtMusicVolValue = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"100%");
         this.m_txtSoundVolValue = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"100%");
         this.m_txtFPSValue = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"),"60");
         this.m_btnPlay = new ImgButton("btn_play_dn.png","btn_play_off.png","btn_play_on.png","btn_play_on.png",_loc1_.x,_loc1_.y,this.Play);
         this.m_btnPlay.visible = false;
         this.m_btnResume = new ImgButton("btn_esc_resume_dn.png","btn_esc_resume.png","btn_esc_resume_on.png","btn_esc_resume_on.png",_loc1_.x,_loc1_.y,this.ToggleEscMenu);
         this.m_btnLoad = new ImgButton("btn_load_dn.png","btn_load_off.png","btn_load_on.png","btn_load_on.png",_loc1_.x,_loc1_.y,this.LoadGame);
         this.m_btnLoad.visible = false;
         this.m_btnSave = new ImgButton("btn_esc_save_dn.png","btn_esc_save.png","btn_esc_save_on.png","btn_esc_save_on.png",_loc1_.x,_loc1_.y,this.SaveGame);
         this.m_btnQuit = new ImgButton("btn_esc_quit_dn.png","btn_esc_quit.png","btn_esc_quit_on.png","btn_esc_quit_on.png",_loc1_.x,_loc1_.y,this.Quit);
         this.m_txtLoadingGame = new FlxText(_loc1_.x,_loc1_.y,GUIValues.GetInt("GUIEscMenu.m_txtLoadingGame.width"),"");
         this.m_txtLoadingGame.alpha = 0;
         this.m_btnCredits = new ImgButton("btn_credits_dn.png","btn_credits_off.png","btn_credits_on.png","btn_credits_on.png",_loc1_.x,_loc1_.y,this.ToggleCredits);
         this.m_btnInstr = new ImgButton("btn_instr_dn.png","btn_instr_off.png","btn_instr_on.png","btn_instr_on.png",_loc1_.x,_loc1_.y,this.ToggleInstr);
         this.m_btnMoreInfo = new ImgButton("btn_moreinfo_dn.png","btn_moreinfo_off.png","btn_moreinfo_on.png","btn_moreinfo_on.png",_loc1_.x,_loc1_.y,this.MoreInfoSave);
         this.m_btnRetry = new ImgButton("btn_retry_dn.png","btn_retry_off.png","btn_retry_on.png","btn_retry_on.png",_loc1_.x,_loc1_.y,this.SaveTest);
         this.m_btnExit = new ImgButton("btn_esc_exit_dn.png","btn_esc_exit.png","btn_esc_exit_on.png","btn_esc_exit_on.png",_loc1_.x,_loc1_.y,this.Exit);
         this.m_btnOptions = new ImgButton("btn_options_dn.png","btn_options_off.png","btn_options_on.png","btn_options_on.png",_loc1_.x,_loc1_.y,this.ToggleOptions);
         this.m_btnReset = new ImgButton("btn_reset_dn.png","btn_reset_off.png","btn_reset_on.png","btn_reset_on.png",_loc1_.x,_loc1_.y,this.ResetOptions);
         this.m_btnMute = new ImgButton("btn_mute_dn.png","btn_mute_off.png","btn_mute_on.png","btn_mute_on.png",_loc1_.x,_loc1_.y,this.Mute);
         this.m_btnFullscreen = new ImgButton("btn_fullscreen_dn.png","btn_fullscreen_off.png","btn_fullscreen_on.png","btn_fullscreen_on.png",_loc1_.x,_loc1_.y,this.OnFullscreen);
         this.m_btnAUTO = new ImgButton("btn_auto_dn.png","btn_auto_off.png","btn_auto_on.png","btn_auto_on.png",_loc1_.x,_loc1_.y,this.SetResAUTO);
         this.m_btn800 = new ImgButton("btn_800_dn.png","btn_800_off.png","btn_800_on.png","btn_800_on.png",_loc1_.x,_loc1_.y,this.SetRes800);
         this.m_btn1024 = new ImgButton("btn_1024_dn.png","btn_1024_off.png","btn_1024_on.png","btn_1024_on.png",_loc1_.x,_loc1_.y,this.SetRes1024);
         this.m_btn1360 = new ImgButton("btn_1360_dn.png","btn_1360_off.png","btn_1360_on.png","btn_1360_on.png",_loc1_.x,_loc1_.y,this.SetRes1360);
         this.m_btn1600 = new ImgButton("btn_1600_dn.png","btn_1600_off.png","btn_1600_on.png","btn_1600_on.png",_loc1_.x,_loc1_.y,this.SetRes1600);
         this.m_btn2400 = new ImgButton("btn_2400_dn.png","btn_2400_off.png","btn_2400_on.png","btn_2400_on.png",_loc1_.x,_loc1_.y,this.SetRes2400);
         this.m_btnFilter = new ImgButton("btn_filter_dn.png","btn_filter_off.png","btn_filter_on.png","btn_filter_on.png",_loc1_.x,_loc1_.y,this.OnFilter);
         this.m_btnStretch = new ImgButton("btn_stretch_dn.png","btn_stretch_off.png","btn_stretch_on.png","btn_stretch_on.png",_loc1_.x,_loc1_.y,this.OnStretch);
         this.m_btnMusicUp = new ImgButton("btn_craft_next_dn.png","btn_craft_next.png","btn_craft_next_on.png","btn_craft_next_on.png",_loc1_.x,_loc1_.y,this.Volume,true);
         this.m_btnMusicDn = new ImgButton("btn_craft_prev_dn.png","btn_craft_prev.png","btn_craft_prev_on.png","btn_craft_prev_on.png",_loc1_.x,_loc1_.y,this.Volume,true);
         this.m_btnSoundUp = new ImgButton("btn_craft_next_dn.png","btn_craft_next.png","btn_craft_next_on.png","btn_craft_next_on.png",_loc1_.x,_loc1_.y,this.Volume,true);
         this.m_btnSoundDn = new ImgButton("btn_craft_prev_dn.png","btn_craft_prev.png","btn_craft_prev_on.png","btn_craft_prev_on.png",_loc1_.x,_loc1_.y,this.Volume,true);
         this.m_btnMasterUp = new ImgButton("btn_craft_next_dn.png","btn_craft_next.png","btn_craft_next_on.png","btn_craft_next_on.png",_loc1_.x,_loc1_.y,this.Volume,true);
         this.m_btnMasterDn = new ImgButton("btn_craft_prev_dn.png","btn_craft_prev.png","btn_craft_prev_on.png","btn_craft_prev_on.png",_loc1_.x,_loc1_.y,this.Volume,true);
         this.m_btnFPSUp = new ImgButton("btn_craft_next_dn.png","btn_craft_next.png","btn_craft_next_on.png","btn_craft_next_on.png",_loc1_.x,_loc1_.y,this.Framerate,true);
         this.m_btnFPSDn = new ImgButton("btn_craft_prev_dn.png","btn_craft_prev.png","btn_craft_prev_on.png","btn_craft_prev_on.png",_loc1_.x,_loc1_.y,this.Framerate,true);
         this.m_btnManual = new ImgButton("btn_manual_dn.png","btn_manual_off.png","btn_manual_on.png","btn_manual_on.png",_loc1_.x,_loc1_.y,this.Manual);
         this.m_btnWiki = new ImgButton("btn_wiki_dn.png","btn_wiki_off.png","btn_wiki_on.png","btn_wiki_on.png",_loc1_.x,_loc1_.y,this.Wiki);
         this.m_btnMain = new ImgButton("btn_main_help_dn.png","btn_main_help.png","btn_main_help_on.png","btn_main_help_on.png",_loc1_.x,_loc1_.y,this.BackOut);
         this.m_btnAutosave = new ImgButton("btn_autosave_dn.png","btn_autosave_off.png","btn_autosave_on.png","btn_autosave_on.png",_loc1_.x,_loc1_.y,this.OnAutosave);
         this.m_btnGreenlight = new ImgButton("btn_bugs_dn.png","btn_bugs_off.png","btn_bugs_on.png","btn_bugs_on.png",_loc1_.x,_loc1_.y,this.Greenlight);
         this.m_sprBugsBG = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprBugsBG.pixels = DataHandler.GetImage("btn_esc_bugs_bg.png");
         this.m_btnZomboid = new ImgButton("btn_esc_zomboid_dn.png","btn_esc_zomboid_off.png","btn_esc_zomboid_on.png","btn_esc_zomboid_on.png",_loc1_.x,_loc1_.y,this.Zomboid);
         this.m_btnBBG = new ImgButton("bbgLink.png","bbgLink.png","bbgLink.png","bbgLink.png",_loc1_.x,_loc1_.y,this.BBGLink);
         this.m_sprSparkle = new FlxSprite(_loc1_.x,_loc1_.y);
         this.m_sprSparkle.pixels = DataHandler.GetImage("Sparkle.png");
         this.m_sprSparkle.moves = true;
         this.m_btnTerEdit = new FlxButton(_loc1_.x,_loc1_.y,"TERRAIN",this.Terrain);
         this.m_btnTerEdit.color = 4281349427;
         this.m_btnTerEdit.label.color = 4294967295;
         this.m_btnTerEdit.label.x = this.m_btnTerEdit.x;
         this.m_btnTerEdit.label.y = this.m_btnTerEdit.y;
         this.m_btnTerEdit.visible = false;
         this.vCursors = Vector.<Bitmap>([new Bitmap(DataHandler.GetImage("CurDefault.png")),new Bitmap(DataHandler.GetImage("CurUse.png"))]);
         add(this.m_grpBG);
         this.m_grpBG.add(this.m_sprTitleBG1);
         this.m_grpBG.add(this.m_sprTitleMtns1);
         this.m_grpBG.add(this.m_sprTitleMtns2);
         this.m_grpBG.add(this.m_sprTitleIslands1);
         this.m_grpBG.add(this.m_sprTitleIslands2);
         this.m_grpBG.add(this.m_sprTitleMid1);
         this.m_grpBG.add(this.m_sprTitleMid2);
         this.m_grpBG.add(this.m_sprTitleTrees1);
         this.m_grpBG.add(this.m_sprTitleTrees2);
         this.m_grpBG.add(this.m_sprTitleGrass1);
         this.m_grpBG.add(this.m_sprTitleGrass2);
         add(this.m_grpButtons);
         if(FlxG.state is MenuState)
         {
            this.m_grpButtons.add(this.m_btnPlay);
            this.m_grpButtons.add(this.m_btnLoad);
            this.m_grpButtons.add(this.m_btnExit);
         }
         else
         {
            this.m_grpButtons.add(this.m_btnResume);
            this.m_grpButtons.add(this.m_btnSave);
            this.m_grpButtons.add(this.m_btnQuit);
         }
         add(this.m_grpCredits);
         this.m_grpCredits.add(this.m_sprCredits);
         this.m_grpCredits.add(this.txtCreditNames);
         this.m_grpCredits.add(this.txtCreditRoles);
         add(this.m_grpInstructions);
         this.m_grpInstructions.add(this.m_sprCredits);
         this.m_grpInstructions.add(this.m_sprHelpCur);
         this.m_grpInstructions.add(this.m_sprHelpL2Click);
         this.m_grpInstructions.add(this.m_sprHelpLClick);
         this.m_grpInstructions.add(this.m_sprHelpPlayer);
         this.m_grpInstructions.add(this.m_sprHelpRClick);
         this.m_grpInstructions.add(this.m_sprHelpSLClick);
         this.m_grpInstructions.add(this.m_sprHelpSpace);
         this.m_grpInstructions.add(this.m_sprHelpWASD);
         this.m_grpInstructions.add(this.m_btnManual);
         this.m_grpInstructions.add(this.m_btnMain);
         this.m_grpInstructions.add(this.m_btnWiki);
         add(this.m_grpOptions);
         this.m_grpOptions.add(this.m_btnMain);
         this.m_grpOptions.add(this.m_btnReset);
         this.m_grpOptions.add(this.m_btnAUTO);
         this.m_grpOptions.add(this.m_btn800);
         this.m_grpOptions.add(this.m_btn1024);
         this.m_grpOptions.add(this.m_btn1360);
         this.m_grpOptions.add(this.m_btnFullscreen);
         this.m_grpOptions.add(this.m_btnFilter);
         this.m_grpOptions.add(this.m_btnStretch);
         this.m_grpOptions.add(this.m_btnMute);
         this.m_grpOptions.add(this.m_btnMusicUp);
         this.m_grpOptions.add(this.m_btnMusicDn);
         this.m_grpOptions.add(this.m_btnSoundUp);
         this.m_grpOptions.add(this.m_btnSoundDn);
         this.m_grpOptions.add(this.m_btnMasterUp);
         this.m_grpOptions.add(this.m_btnMasterDn);
         this.m_grpOptions.add(this.m_btnFPSUp);
         this.m_grpOptions.add(this.m_btnFPSDn);
         this.m_grpOptions.add(this.m_btnAutosave);
         this.m_grpOptions.add(this.m_txtMasterVolName);
         this.m_grpOptions.add(this.m_txtMusicVolName);
         this.m_grpOptions.add(this.m_txtSoundVolName);
         this.m_grpOptions.add(this.m_txtFPSName);
         this.m_grpOptions.add(this.m_txtMasterVolValue);
         this.m_grpOptions.add(this.m_txtMusicVolValue);
         this.m_grpOptions.add(this.m_txtSoundVolValue);
         this.m_grpOptions.add(this.m_txtFPSValue);
         this.m_grpButtons.add(this.m_sprTitle);
         this.m_grpButtons.add(this.m_txtVersion);
         this.m_grpButtons.add(this.m_txtCopyright);
         this.m_grpButtons.add(this.m_txtLoadingGame);
         this.m_grpButtons.add(this.m_sprBugsBG);
         this.m_grpButtons.add(this.m_btnCredits);
         this.m_grpButtons.add(this.m_btnOptions);
         this.m_grpButtons.add(this.m_btnInstr);
         this.m_grpButtons.add(this.m_btnMute);
         this.m_grpButtons.add(this.m_btnGreenlight);
         this.m_grpButtons.add(this.m_btnZomboid);
         this.m_grpButtons.add(this.m_btnBBG);
         this.m_grpButtons.add(this.m_sprSparkle);
         this.LoadPrefs();
         this.SetRes(GUIValues.GetRes());
         if(this.nResOverride < 0)
         {
            this.HandlerResize();
         }
         setAll("scrollFactor",new FlxPoint(0,0));
         setAll("cameras",[FlxG.camera]);
         kill();
      }
      
      override public function destroy() : void
      {
         this.m_btnPlay = DataHandler.DestroyObject(this.m_btnPlay);
         this.m_btnCredits = DataHandler.DestroyObject(this.m_btnCredits);
         this.m_btnOptions = DataHandler.DestroyObject(this.m_btnOptions);
         this.m_btnReset = DataHandler.DestroyObject(this.m_btnReset);
         this.m_btnMute = DataHandler.DestroyObject(this.m_btnMute);
         this.m_btnInstr = DataHandler.DestroyObject(this.m_btnInstr);
         this.m_btnMoreInfo = DataHandler.DestroyObject(this.m_btnMoreInfo);
         this.m_btnRetry = DataHandler.DestroyObject(this.m_btnRetry);
         this.m_btnBBG = DataHandler.DestroyObject(this.m_btnBBG);
         this.m_btnGreenlight = DataHandler.DestroyObject(this.m_btnGreenlight);
         this.m_sprBugsBG = DataHandler.DestroyObject(this.m_sprBugsBG);
         this.m_btnZomboid = DataHandler.DestroyObject(this.m_btnZomboid);
         this.m_btnAUTO = DataHandler.DestroyObject(this.m_btnAUTO);
         this.m_btn800 = DataHandler.DestroyObject(this.m_btn800);
         this.m_btn1024 = DataHandler.DestroyObject(this.m_btn1024);
         this.m_btn1360 = DataHandler.DestroyObject(this.m_btn1360);
         this.m_btn1600 = DataHandler.DestroyObject(this.m_btn1600);
         this.m_btn2400 = DataHandler.DestroyObject(this.m_btn2400);
         this.m_btnFullscreen = DataHandler.DestroyObject(this.m_btnFullscreen);
         this.m_btnFilter = DataHandler.DestroyObject(this.m_btnFilter);
         this.m_btnStretch = DataHandler.DestroyObject(this.m_btnStretch);
         this.m_btnTerEdit = DataHandler.DestroyObject(this.m_btnTerEdit);
         this.m_btnResume = DataHandler.DestroyObject(this.m_btnResume);
         this.m_btnSave = DataHandler.DestroyObject(this.m_btnSave);
         this.m_btnLoad = DataHandler.DestroyObject(this.m_btnLoad);
         this.m_btnQuit = DataHandler.DestroyObject(this.m_btnQuit);
         this.m_btnOK = DataHandler.DestroyObject(this.m_btnOK);
         this.m_btnCancel = DataHandler.DestroyObject(this.m_btnCancel);
         this.m_btnMusicUp = DataHandler.DestroyObject(this.m_btnMusicUp);
         this.m_btnMusicDn = DataHandler.DestroyObject(this.m_btnMusicDn);
         this.m_btnSoundUp = DataHandler.DestroyObject(this.m_btnSoundUp);
         this.m_btnSoundDn = DataHandler.DestroyObject(this.m_btnSoundDn);
         this.m_btnMasterUp = DataHandler.DestroyObject(this.m_btnMasterUp);
         this.m_btnMasterDn = DataHandler.DestroyObject(this.m_btnMasterDn);
         this.m_btnFPSUp = DataHandler.DestroyObject(this.m_btnFPSUp);
         this.m_btnFPSDn = DataHandler.DestroyObject(this.m_btnFPSDn);
         this.m_btnExit = DataHandler.DestroyObject(this.m_btnExit);
         this.m_btnManual = DataHandler.DestroyObject(this.m_btnManual);
         this.m_btnWiki = DataHandler.DestroyObject(this.m_btnWiki);
         this.m_btnMain = DataHandler.DestroyObject(this.m_btnMain);
         this.m_btnAutosave = DataHandler.DestroyObject(this.m_btnAutosave);
         this.m_sprTitle = DataHandler.DestroyObject(this.m_sprTitle);
         this.m_sprCredits = DataHandler.DestroyObject(this.m_sprCredits);
         this.m_sprSparkle = DataHandler.DestroyObject(this.m_sprSparkle);
         this.m_sprSaveGameWarning = DataHandler.DestroyObject(this.m_sprSaveGameWarning);
         this.m_sprHelpCur = DataHandler.DestroyObject(this.m_sprHelpCur);
         this.m_sprHelpL2Click = DataHandler.DestroyObject(this.m_sprHelpL2Click);
         this.m_sprHelpLClick = DataHandler.DestroyObject(this.m_sprHelpLClick);
         this.m_sprHelpPlayer = DataHandler.DestroyObject(this.m_sprHelpPlayer);
         this.m_sprHelpRClick = DataHandler.DestroyObject(this.m_sprHelpRClick);
         this.m_sprHelpSLClick = DataHandler.DestroyObject(this.m_sprHelpSLClick);
         this.m_sprHelpSpace = DataHandler.DestroyObject(this.m_sprHelpSpace);
         this.m_sprHelpWASD = DataHandler.DestroyObject(this.m_sprHelpWASD);
         this.m_sprTitleBG1 = DataHandler.DestroyObject(this.m_sprTitleBG1);
         this.m_sprTitleMtns1 = DataHandler.DestroyObject(this.m_sprTitleMtns1);
         this.m_sprTitleIslands1 = DataHandler.DestroyObject(this.m_sprTitleIslands1);
         this.m_sprTitleMid1 = DataHandler.DestroyObject(this.m_sprTitleMid1);
         this.m_sprTitleTrees1 = DataHandler.DestroyObject(this.m_sprTitleTrees1);
         this.m_sprTitleGrass1 = DataHandler.DestroyObject(this.m_sprTitleGrass1);
         this.m_sprTitleMtns2 = DataHandler.DestroyObject(this.m_sprTitleMtns2);
         this.m_sprTitleIslands2 = DataHandler.DestroyObject(this.m_sprTitleIslands2);
         this.m_sprTitleMid2 = DataHandler.DestroyObject(this.m_sprTitleMid2);
         this.m_sprTitleTrees2 = DataHandler.DestroyObject(this.m_sprTitleTrees2);
         this.m_sprTitleGrass2 = DataHandler.DestroyObject(this.m_sprTitleGrass2);
         this.m_vBGs = null;
         this.m_txtVersion = DataHandler.DestroyObject(this.m_txtVersion);
         this.m_txtCopyright = DataHandler.DestroyObject(this.m_txtCopyright);
         this.txtCreditNames = DataHandler.DestroyObject(this.txtCreditNames);
         this.txtCreditRoles = DataHandler.DestroyObject(this.txtCreditRoles);
         this.m_txtLoadingGame = DataHandler.DestroyObject(this.m_txtLoadingGame);
         this.m_txtMasterVolName = DataHandler.DestroyObject(this.m_txtMasterVolName);
         this.m_txtMusicVolName = DataHandler.DestroyObject(this.m_txtMusicVolName);
         this.m_txtSoundVolName = DataHandler.DestroyObject(this.m_txtSoundVolName);
         this.m_txtFPSName = DataHandler.DestroyObject(this.m_txtFPSName);
         this.m_txtMasterVolValue = DataHandler.DestroyObject(this.m_txtMasterVolName);
         this.m_txtMusicVolValue = DataHandler.DestroyObject(this.m_txtMusicVolName);
         this.m_txtSoundVolValue = DataHandler.DestroyObject(this.m_txtSoundVolName);
         this.m_txtFPSValue = DataHandler.DestroyObject(this.m_txtFPSName);
         this.vCursors = null;
         this.strFullscreenOverride = null;
         this.m_grpBG = DataHandler.DestroyObject(this.m_grpBG);
         this.m_grpButtons = DataHandler.DestroyObject(this.m_grpButtons);
         this.m_grpCredits = DataHandler.DestroyObject(this.m_grpCredits);
         this.m_grpOptions = DataHandler.DestroyObject(this.m_grpOptions);
         this.m_grpInstructions = DataHandler.DestroyObject(this.m_grpInstructions);
         this.m_sndTitle = DataHandler.DestroyObject(this.m_sndTitle);
      }
      
      override public function update() : void
      {
         var _loc4_:FlxSprite = null;
         super.update();
         var _loc1_:FlxPoint = FlxG.mouse.getScreenPosition();
         if(FlxG.mouse.pressed())
         {
            if(this.m_nMode == MODE_CREDITS)
            {
               this.ToggleCredits();
            }
         }
         if(false && false)
         {
            DataHandler.update();
         }
         this.UpdateButtons();
         if(DataHandler.bLoadingComplete)
         {
            this.m_btnPlay.visible = true;
            this.m_btnLoad.visible = true;
            this.m_btnTerEdit.visible = true;
         }
         var _loc2_:Number = 30 * (Math.cos(getTimer() / 60 / 10) - 0.9);
         if(_loc2_ < 0)
         {
            _loc2_ = 0;
         }
         this.m_sprSparkle.scale.x = this.m_sprSparkle.scale.y = _loc2_;
         this.m_sprSparkle.angularVelocity = 200;
         this.m_txtLoadingGame.alpha -= 0.01;
         var _loc3_:Number = 0.5;
         this.m_sprTitleGrass1.x += _loc3_ * 1;
         this.m_sprTitleGrass2.x += _loc3_ * 1;
         this.m_sprTitleTrees1.x += _loc3_ * 0.7;
         this.m_sprTitleTrees2.x += _loc3_ * 0.7;
         this.m_sprTitleMid1.x += _loc3_ * 0.5;
         this.m_sprTitleMid2.x += _loc3_ * 0.5;
         this.m_sprTitleIslands1.x += _loc3_ * 0.35;
         this.m_sprTitleIslands2.x += _loc3_ * 0.35;
         this.m_sprTitleMtns1.x += _loc3_ * 0.2;
         this.m_sprTitleMtns2.x += _loc3_ * 0.2;
         for each(_loc4_ in this.m_vBGs)
         {
            if(_loc4_.x >= this.m_nWrapX)
            {
               _loc4_.x -= this.m_sprTitleBG1.width * 2;
            }
         }
         if(FlxG.state is MenuState)
         {
            FlxG.sounds.update();
            if(this.m_sndTitle.volume != m_fMusicVolume)
            {
               this.m_sndTitle.volume = m_fMusicVolume;
            }
         }
      }
      
      private function UpdateButtons() : void
      {
         if(this.m_btnMute.on != FlxG.mute)
         {
            this.m_btnMute.on = FlxG.mute;
         }
         var _loc1_:* = FlxG.stage.displayState == StageDisplayState.FULL_SCREEN_INTERACTIVE;
         var _loc2_:* = this.nResOverride >= 0;
         var _loc3_:int = int(GUIValues.GetRes());
         if(this.m_btnFullscreen.on != _loc1_)
         {
            this.m_btnFullscreen.on = _loc1_;
         }
         if(this.m_btnFilter.on != FlxG.camera.antialiasing)
         {
            this.m_btnFilter.on = FlxG.camera.antialiasing;
         }
         if(this.m_btnStretch.on != (this.strStretchOverride == StageScaleMode.SHOW_ALL))
         {
            this.m_btnStretch.on = this.strStretchOverride == StageScaleMode.SHOW_ALL;
         }
         if(this.m_btnAUTO.on == _loc2_)
         {
            this.m_btnAUTO.on = !_loc2_;
         }
         if(this.m_btn800.on != (_loc2_ && _loc3_ == GUIValues.GUITYPE_800X450))
         {
            this.m_btn800.on = _loc2_ && _loc3_ == GUIValues.GUITYPE_800X450;
         }
         if(this.m_btn1024.on != (_loc2_ && _loc3_ == GUIValues.GUITYPE_1024X768))
         {
            this.m_btn1024.on = _loc2_ && _loc3_ == GUIValues.GUITYPE_1024X768;
         }
         if(this.m_btn1360.on != (_loc2_ && _loc3_ == GUIValues.GUITYPE_1360X768))
         {
            this.m_btn1360.on = _loc2_ && _loc3_ == GUIValues.GUITYPE_1360X768;
         }
         if(this.m_btn1600.on != (_loc2_ && _loc3_ == GUIValues.GUITYPE_1600X900))
         {
            this.m_btn1600.on = _loc2_ && _loc3_ == GUIValues.GUITYPE_1600X900;
         }
         if(this.m_btn2400.on != (_loc2_ && _loc3_ == GUIValues.GUITYPE_2400X1350))
         {
            this.m_btn2400.on = _loc2_ && _loc3_ == GUIValues.GUITYPE_2400X1350;
         }
         if(this.m_btnAutosave.on != bAutosave)
         {
            this.m_btnAutosave.on = bAutosave;
         }
      }
      
      public function ChangeCursor(param1:int = 0) : void
      {
         if(param1 != this.nCursor)
         {
            FlxG.mouse.loadBitmap(this.vCursors[param1]);
            this.nCursor = param1;
         }
      }
      
      public function BackOut() : Boolean
      {
         switch(this.m_nMode)
         {
            case MODE_CREDITS:
               this.ToggleCredits();
               return false;
            case MODE_INSTRUCTIONS:
               this.ToggleInstr();
               return false;
            case MODE_OPTIONS:
               this.ToggleOptions();
               return false;
            default:
               this.m_btnResume.status = FlxButton.NORMAL;
               FlxG.mouse.update(FlxG.mouse.x,FlxG.mouse.y);
               PlayState.m_objInstance.btnMenu.on = false;
               kill();
               return true;
         }
      }
      
      public function Show() : void
      {
         alive = true;
         exists = true;
         this.m_grpBG.revive();
         if(this.m_nMode == MODE_CREDITS)
         {
            this.m_grpCredits.revive();
         }
         if(this.m_nMode == MODE_OPTIONS)
         {
            this.m_grpOptions.revive();
         }
         if(this.m_nMode == MODE_INSTRUCTIONS)
         {
            this.m_grpInstructions.revive();
         }
         if(this.m_nMode == MODE_NORMAL)
         {
            this.m_grpButtons.revive();
         }
         if(FlxG.state is PlayState)
         {
            PlayState.m_objInstance.ChangeCursor();
            PlayState.m_objInstance.btnMenu.on = true;
            this.SaveQuitBtns();
         }
         else
         {
            this.SaveTest();
         }
      }
      
      private function SaveQuitBtns() : void
      {
         if(FlxG.state is PlayState && PlayState(FlxG.state).m_nGameState >= 3)
         {
            this.m_btnSave.revive();
            this.m_btnQuit.kill();
         }
         else
         {
            this.m_btnSave.kill();
            this.m_btnQuit.revive();
         }
      }
      
      public function ShowMsg(param1:String) : void
      {
         this.m_txtLoadingGame.text = param1;
         this.m_txtLoadingGame.alpha = 1;
      }
      
      private function Play() : void
      {
         if(DataHandler.bLoadingComplete)
         {
            this.m_sndTitle.fadeOut(0.5);
            FlxG.fade(4278190080,0.5,this.StartGame);
         }
      }
      
      private function Terrain() : void
      {
         if(DataHandler.bLoadingComplete)
         {
            this.m_sndTitle.fadeOut(0.5);
            FlxG.fade(4278190080,0.5,this.StartTerrain);
         }
      }
      
      private function Quit() : void
      {
         PlayState.m_objInstance.LogPlayerStats("Quit");
         FlxG.switchState(new MenuState());
      }
      
      private function Exit() : void
      {
         fscommand("quit");
      }
      
      private function Framerate(param1:ImgButton, param2:Boolean = true) : void
      {
         var _loc3_:int = 10;
         switch(param1)
         {
            case this.m_btnFPSDn:
               if(FlxG.flashFramerate > _loc3_)
               {
                  FlxG.flashFramerate -= _loc3_;
               }
               this.m_btnFPSDn.on = false;
               break;
            case this.m_btnFPSUp:
               if(FlxG.flashFramerate <= DataHandler.nFPS - _loc3_)
               {
                  FlxG.flashFramerate += _loc3_;
               }
               this.m_btnFPSUp.on = false;
         }
         FlxG.framerate = FlxG.flashFramerate;
         DataHandler.nFPSModifier = DataHandler.nFPS / FlxG.flashFramerate;
         this.m_txtFPSValue.text = FlxG.flashFramerate.toString();
         if(param2)
         {
            this.SavePrefs();
         }
      }
      
      private function Mute() : void
      {
         FlxG.mute = this.m_btnMute.on;
         this.SavePrefs();
      }
      
      private function Volume(param1:ImgButton) : void
      {
         switch(param1)
         {
            case this.m_btnMasterDn:
               FlxG.volume -= 0.1;
               this.m_btnMasterDn.on = false;
               break;
            case this.m_btnMasterUp:
               FlxG.volume += 0.1;
               this.m_btnMasterUp.on = false;
               break;
            case this.m_btnMusicDn:
               m_fMusicVolume -= 0.1;
               this.m_btnMusicDn.on = false;
               break;
            case this.m_btnMusicUp:
               m_fMusicVolume += 0.1;
               this.m_btnMusicUp.on = false;
               break;
            case this.m_btnSoundDn:
               m_fSoundVolume -= 0.1;
               this.m_btnSoundDn.on = false;
               break;
            case this.m_btnSoundUp:
               m_fSoundVolume += 0.1;
               this.m_btnSoundUp.on = false;
         }
         if(m_fMusicVolume > 1)
         {
            m_fMusicVolume = 1;
         }
         else if(m_fMusicVolume < 0)
         {
            m_fMusicVolume = 0;
         }
         if(m_fSoundVolume > 1)
         {
            m_fSoundVolume = 1;
         }
         else if(m_fSoundVolume < 0)
         {
            m_fSoundVolume = 0;
         }
         this.m_txtMasterVolValue.text = int(FlxG.volume * 100).toString() + "%";
         this.m_txtMusicVolValue.text = int(m_fMusicVolume * 100).toString() + "%";
         this.m_txtSoundVolValue.text = int(m_fSoundVolume * 100).toString() + "%";
         if(PlayState.m_objInstance != null)
         {
            PlayState.m_objInstance.UpdateMusic();
         }
         this.SavePrefs();
      }
      
      private function ResetOptions() : void
      {
         this.nResOverride = -1;
         this.strFullscreenOverride = "";
         this.strStretchOverride = "";
         FlxG.flashFramerate = DataHandler.nFPS;
         FlxG.framerate = FlxG.flashFramerate;
         this.Framerate(null,false);
         this.Fullscreen(false);
         this.Stretch(false);
         this.Filter(false);
         this.SetResAUTO();
         this.ZoomCam();
         this.SavePrefs();
      }
      
      private function ToggleOptions() : void
      {
         if(this.m_nMode == MODE_OPTIONS)
         {
            this.m_grpOptions.kill();
            this.m_grpButtons.revive();
            this.SaveQuitBtns();
            this.m_btnOptions.on = false;
            this.m_nMode = MODE_NORMAL;
            this.m_btnMain.on = false;
            this.m_btnMain.status = FlxButton.NORMAL;
         }
         else
         {
            this.m_grpButtons.kill();
            this.m_grpOptions.revive();
            this.m_btnOptions.on = false;
            this.m_btnOptions.status = FlxButton.NORMAL;
            this.m_nMode = MODE_OPTIONS;
            this.m_btnMain.x = GUIValues.GetPoint("GUIEscMenu.m_btnOptions").x;
            this.m_btnMain.y = GUIValues.GetPoint("GUIEscMenu.m_btnOptions").y;
         }
      }
      
      private function ToggleCredits() : void
      {
         if(this.m_nMode == MODE_CREDITS)
         {
            this.m_grpCredits.kill();
            this.m_btnCredits.on = false;
            this.m_btnCredits.status = FlxButton.NORMAL;
            this.m_nMode = MODE_NORMAL;
         }
         else
         {
            this.m_grpCredits.revive();
            this.m_nMode = MODE_CREDITS;
            this.m_sprCredits.x = GUIValues.GetPoint("GUIEscMenu.m_sprCredits").x;
            this.m_sprCredits.y = GUIValues.GetPoint("GUIEscMenu.m_sprCredits").y;
         }
      }
      
      private function ToggleInstr() : void
      {
         if(this.m_nMode == MODE_INSTRUCTIONS)
         {
            this.m_grpInstructions.kill();
            this.m_grpButtons.revive();
            this.m_nMode = MODE_NORMAL;
            this.m_btnMain.on = false;
            this.m_btnMain.status = FlxButton.NORMAL;
         }
         else
         {
            this.m_grpButtons.kill();
            this.m_grpInstructions.revive();
            this.m_btnInstr.on = false;
            this.m_btnInstr.status = FlxButton.NORMAL;
            this.m_nMode = MODE_INSTRUCTIONS;
            this.m_sprCredits.x = GUIValues.GetPoint("GUIEscMenu.m_sprCredits.Instr").x;
            this.m_sprCredits.y = GUIValues.GetPoint("GUIEscMenu.m_sprCredits.Instr").y;
            this.m_btnMain.x = GUIValues.GetPoint("GUIEscMenu.m_btnMain").x;
            this.m_btnMain.y = GUIValues.GetPoint("GUIEscMenu.m_btnMain").y;
         }
      }
      
      private function Wiki() : void
      {
         var _loc1_:URLRequest = new URLRequest("http://neoscavenger.wikia.com/");
         navigateToURL(_loc1_);
         this.m_btnWiki.on = false;
         this.m_btnWiki.status = FlxButton.NORMAL;
      }
      
      private function Manual() : void
      {
         var _loc1_:URLRequest = new URLRequest(§_a_-_---§.§_a_--_--§(-1820302790));
         navigateToURL(_loc1_);
         this.m_btnManual.on = false;
         this.m_btnManual.status = FlxButton.NORMAL;
      }
      
      private function MoreInfoSave() : void
      {
         var _loc1_:URLRequest = new URLRequest("http://bluebottlegames.com/main/node/762");
         navigateToURL(_loc1_);
      }
      
      private function BBGLink() : void
      {
         var _loc1_:URLRequest = new URLRequest(DataHandler.strProductURL);
         navigateToURL(_loc1_);
      }
      
      private function Greenlight() : void
      {
         var _loc1_:URLRequest = new URLRequest("http://bluebottlegames.com/main/forum");
         navigateToURL(_loc1_);
      }
      
      private function Zomboid() : void
      {
         var _loc1_:URLRequest = new URLRequest("http://projectzomboid.com/");
         navigateToURL(_loc1_);
      }
      
      private function StartTerrain() : void
      {
         var _loc1_:PlayState = new PlayState();
         _loc1_.bPlayerReady = true;
         _loc1_.bDMGame = false;
         _loc1_.nMapStyle = 1;
         FlxG.switchState(_loc1_);
      }
      
      private function StartGame() : void
      {
         FlxG.switchState(new PlayState());
      }
      
      private function LoadGame() : void
      {
         var _loc1_:Boolean = DataHandler.LoadGame();
         if(!_loc1_)
         {
            this.m_txtLoadingGame.text = "没有发现存档数据.";
            this.m_txtLoadingGame.alpha = 1;
         }
      }
      
      private function SaveGame() : void
      {
         this.m_txtLoadingGame.text = "保存游戏...";
         this.m_txtLoadingGame.alpha = 1;
         this.m_nSaveAlphaMin = 0.5;
         if(!DataHandler.SaveGame(this.FinishSave))
         {
            this.m_txtLoadingGame.text = "错误：无法访问保存数据位置。";
         }
      }
      
      public function FinishSave(param1:Boolean) : void
      {
         if(param1)
         {
            this.m_txtLoadingGame.text = "保存成功!";
            this.Quit();
         }
         else
         {
            this.m_txtLoadingGame.text = "错误:无法保存.";
         }
         this.m_txtLoadingGame.alpha = 1;
         this.m_nSaveAlphaMin = 0;
      }
      
      private function OnAutosave() : void
      {
         bAutosave = this.m_btnAutosave.on;
         this.SavePrefs();
      }
      
      private function OnFilter() : void
      {
         this.Filter(this.m_btnFilter.on);
         this.SavePrefs();
      }
      
      private function OnFullscreen() : void
      {
         this.Fullscreen(this.m_btnFullscreen.on);
         this.strFullscreenOverride = FlxG.stage.displayState;
         this.SavePrefs();
      }
      
      private function OnStretch() : void
      {
         this.Stretch(this.m_btnStretch.on);
         this.SavePrefs();
      }
      
      private function Filter(param1:Boolean) : void
      {
         var _loc2_:FlxCamera = FlxG.camera;
         _loc2_.antialiasing = param1;
      }
      
      private function Fullscreen(param1:Boolean) : void
      {
         if(param1)
         {
            FlxG.stage.displayState = StageDisplayState.FULL_SCREEN_INTERACTIVE;
            if(FlxG.stage.displayState != StageDisplayState.FULL_SCREEN_INTERACTIVE)
            {
               fscommand("fullscreen","true");
            }
         }
         else
         {
            FlxG.stage.displayState = StageDisplayState.NORMAL;
         }
      }
      
      private function Stretch(param1:Boolean) : void
      {
         if(param1)
         {
            this.strStretchOverride = StageScaleMode.SHOW_ALL;
         }
         else
         {
            this.strStretchOverride = StageScaleMode.NO_SCALE;
         }
         this.ZoomCam();
      }
      
      private function SetResAUTO() : void
      {
         GUIValues.m_bOverrideZoom = true;
         this.nResOverride = -1;
         this.HandlerResize();
         this.SavePrefs();
         GUIValues.m_bOverrideZoom = false;
      }
      
      private function SetRes800() : void
      {
         GUIValues.m_bOverrideZoom = true;
         this.SetRes(GUIValues.GUITYPE_800X450);
         this.nResOverride = GUIValues.GUITYPE_800X450;
         this.SavePrefs();
         GUIValues.m_bOverrideZoom = false;
      }
      
      private function SetRes1024() : void
      {
         GUIValues.m_bOverrideZoom = true;
         this.SetRes(GUIValues.GUITYPE_1024X768);
         this.nResOverride = GUIValues.GUITYPE_1024X768;
         this.SavePrefs();
         GUIValues.m_bOverrideZoom = false;
      }
      
      private function SetRes1360() : void
      {
         GUIValues.m_bOverrideZoom = true;
         this.SetRes(GUIValues.GUITYPE_1360X768);
         this.nResOverride = GUIValues.GUITYPE_1360X768;
         this.SavePrefs();
         GUIValues.m_bOverrideZoom = false;
      }
      
      private function SetRes1600() : void
      {
         GUIValues.m_bOverrideZoom = true;
         this.SetRes(GUIValues.GUITYPE_1600X900);
         GUIValues.m_bOverrideZoom = false;
      }
      
      private function SetRes2400() : void
      {
         GUIValues.m_bOverrideZoom = true;
         this.SetRes(GUIValues.GUITYPE_2400X1350);
         GUIValues.m_bOverrideZoom = false;
      }
      
      private function ToggleEscMenu() : void
      {
         if(PlayState.m_objInstance != null)
         {
            PlayState.m_objInstance.ToggleEscMenu();
         }
      }
      
      private function LoadPrefs() : void
      {
         var _loc2_:* = false;
         var _loc1_:FlxSave = new FlxSave();
         if(_loc1_.bind("flixel") && _loc1_.data.res != null)
         {
            if(_loc1_.data.res != null)
            {
               if(_loc1_.data.res.strFullscreenOverride != null)
               {
                  this.strFullscreenOverride = _loc1_.data.res.strFullscreenOverride;
                  _loc2_ = this.strFullscreenOverride == StageDisplayState.FULL_SCREEN_INTERACTIVE;
                  if(Capabilities.os.toLowerCase().search("mac") >= 0)
                  {
                     _loc2_ = this.strFullscreenOverride != StageDisplayState.NORMAL;
                  }
                  this.Fullscreen(_loc2_);
               }
               if(_loc1_.data.res.strStretchOverride != null)
               {
                  this.strStretchOverride = _loc1_.data.res.strStretchOverride;
                  this.Stretch(this.strStretchOverride == StageScaleMode.SHOW_ALL);
               }
               if(_loc1_.data.res.bFilter != null)
               {
                  this.Filter(_loc1_.data.res.bFilter);
               }
               if(_loc1_.data.res.nResOverride != null)
               {
                  this.nResOverride = _loc1_.data.res.nResOverride;
                  GUIValues.SetRes(_loc1_.data.res.nResOverride);
               }
               if(_loc1_.data.res.nFPS != null)
               {
                  FlxG.flashFramerate = _loc1_.data.res.nFPS;
                  this.Framerate(null,false);
               }
            }
            if(_loc1_.data.sound != null)
            {
               if(_loc1_.data.sound.mute != null)
               {
                  FlxG.mute = _loc1_.data.sound.mute;
               }
               if(_loc1_.data.sound.volmusic != null)
               {
                  m_fMusicVolume = _loc1_.data.sound.volmusic;
               }
               if(_loc1_.data.sound.volsound != null)
               {
                  m_fSoundVolume = _loc1_.data.sound.volsound;
               }
               if(_loc1_.data.sound.volmaster != null)
               {
                  FlxG.volume = _loc1_.data.sound.volmaster;
               }
               this.m_txtMasterVolValue.text = int(FlxG.volume * 100).toString() + "%";
               this.m_txtMusicVolValue.text = int(m_fMusicVolume * 100).toString() + "%";
               this.m_txtSoundVolValue.text = int(m_fSoundVolume * 100).toString() + "%";
            }
            if(_loc1_.data.autosave != null)
            {
               bAutosave = _loc1_.data.autosave;
            }
            _loc1_.destroy();
         }
         GUIEscMenu.m_bPrefsLoaded = true;
      }
      
      private function SavePrefs() : void
      {
         var _loc1_:FlxSave = new FlxSave();
         if(_loc1_.bind("flixel"))
         {
            if(_loc1_.data.res == null)
            {
               _loc1_.data.res = new Object();
            }
            if(this.nResOverride >= 0)
            {
               _loc1_.data.res.nResOverride = this.nResOverride;
            }
            else
            {
               delete _loc1_.data.res["nResOverride"];
            }
            if(this.strFullscreenOverride != "")
            {
               _loc1_.data.res.strFullscreenOverride = this.strFullscreenOverride;
            }
            else
            {
               delete _loc1_.data.res["strFullscreenOverride"];
            }
            if(this.strStretchOverride != "")
            {
               _loc1_.data.res.strStretchOverride = this.strStretchOverride;
            }
            else
            {
               delete _loc1_.data.res["strStretchOverride"];
            }
            _loc1_.data.res.bFilter = FlxG.camera.antialiasing;
            _loc1_.data.res.nFPS = FlxG.flashFramerate;
            if(_loc1_.data.sound == null)
            {
               _loc1_.data.sound = new Object();
            }
            _loc1_.data.sound.mute = FlxG.mute;
            _loc1_.data.sound.volmusic = m_fMusicVolume;
            _loc1_.data.sound.volsound = m_fSoundVolume;
            _loc1_.data.sound.volmaster = FlxG.volume;
            _loc1_.data.autosave = bAutosave;
            _loc1_.close();
         }
      }
      
      private function SaveTest() : void
      {
         var _loc1_:FlxSave = new FlxSave();
         if(_loc1_.bind("nsTest"))
         {
            if(_loc1_.data.test == null)
            {
               _loc1_.data.test = new Object();
            }
            _loc1_.data.test.bWorks = true;
            if(_loc1_.close() == false)
            {
               this.m_grpButtons.add(this.m_sprSaveGameWarning);
               this.m_grpButtons.add(this.m_btnMoreInfo);
               this.m_grpButtons.add(this.m_btnRetry);
               this.m_txtLoadingGame.text = "错误:无法保存.";
               this.m_txtLoadingGame.alpha = 1;
               this.m_nSaveAlphaMin = 0;
            }
            else
            {
               this.m_grpButtons.remove(this.m_sprSaveGameWarning);
               this.m_grpButtons.remove(this.m_btnMoreInfo);
               this.m_grpButtons.remove(this.m_btnRetry);
               this.m_txtLoadingGame.text = "测试保存游戏:成功!";
               this.m_txtLoadingGame.alpha = 1;
               this.m_nSaveAlphaMin = 0;
            }
         }
      }
      
      public function HandlerResize(param1:Event = null) : void
      {
         if(this.strStretchOverride == StageScaleMode.SHOW_ALL)
         {
            this.ZoomCam();
         }
         if(this.nResOverride >= 0)
         {
            return;
         }
         var _loc2_:int = FlxG.stage.stageWidth;
         var _loc3_:int = FlxG.stage.stageHeight;
         var _loc4_:int = int(this.vScreenSizes.length - 1);
         while(_loc4_ >= 0)
         {
            if(_loc2_ >= this.vScreenSizes[_loc4_][0])
            {
               if(_loc3_ >= this.vScreenSizes[_loc4_][1])
               {
                  this.SetRes(this.vScreenSizes[_loc4_][2]);
                  break;
               }
            }
            _loc4_--;
         }
      }
      
      public function HandlerFullscreen(param1:Event = null) : void
      {
         this.strFullscreenOverride = FlxG.stage.displayState;
         if(this.m_btnExit.on == false)
         {
            this.SavePrefs();
         }
      }
      
      private function ZoomCam() : void
      {
         var _loc4_:Number = NaN;
         var _loc1_:FlxCamera = FlxG.camera;
         var _loc2_:FlxPoint = new FlxPoint(1360,768);
         GUIValues.m_fCamZoom = GUIValues.GetInt("zoom");
         if(this.strStretchOverride == StageScaleMode.SHOW_ALL)
         {
            GUIValues.m_fCamZoom = FlxG.stage.stageWidth / GUIValues.GetInt("width");
            _loc4_ = FlxG.stage.stageHeight / GUIValues.GetInt("height");
            if(GUIValues.m_fCamZoom > _loc4_)
            {
               GUIValues.m_fCamZoom = _loc4_;
            }
         }
         var _loc3_:FlxPoint = new FlxPoint();
         _loc3_.x = _loc2_.x / 2 - (_loc2_.x / 2 - _loc3_.x) * GUIValues.m_fCamZoom;
         _loc3_.y = _loc2_.y / 2 - (_loc2_.y / 2 - _loc3_.y) * GUIValues.m_fCamZoom;
         _loc1_.x = _loc3_.x;
         _loc1_.y = _loc3_.y;
         _loc1_.zoom = GUIValues.m_fCamZoom;
         _loc1_.SetSize(_loc2_.x,_loc2_.y);
         if(MapUtils.tmapHexes != null)
         {
            _loc3_ = GUIValues.GetPoint("offset");
            _loc1_.setBounds(-_loc3_.x,-_loc3_.y,MapUtils.tmapHexes.width + _loc3_.x * 2,MapUtils.tmapHexes.height + _loc3_.y * 2 + GUIValues.GetInt("PlayState.camMsg.minheight"),true);
         }
         if(FlxG.state is PlayState)
         {
            PlayState.m_objInstance.ZoomCam();
         }
      }
      
      private function SetRes(param1:int = 0) : void
      {
         var _loc3_:FlxPoint = null;
         var _loc4_:FlxPoint = null;
         var _loc2_:Boolean = GUIValues.m_bOverrideZoom;
         if(GUIValues.GetRes() != param1)
         {
            GUIValues.m_bOverrideZoom = true;
         }
         GUIValues.SetRes(param1);
         if(FlxG.state is PlayState)
         {
            PlayState.m_objInstance.SetRes();
         }
         else if(FlxG.state is MenuState)
         {
            MenuState(FlxG.state).m_grpLetterbox.SetRes();
         }
         this.ZoomCam();
         var _loc5_:String = "";
         if(GUIValues.GetInt("GUIEscMenu.UI.Zoom") == 2)
         {
            _loc5_ = DataHandler.m_strZoomPrefix;
         }
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprTitle");
         this.m_sprTitle.x = _loc4_.x;
         this.m_sprTitle.y = _loc4_.y;
         this.m_sprTitle.pixels = DataHandler.GetImage("nsLogo.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprCredits");
         this.m_sprCredits.x = _loc4_.x;
         this.m_sprCredits.y = _loc4_.y;
         this.m_sprCredits.scale.x = GUIValues.GetInt("width");
         this.m_sprCredits.scale.y = GUIValues.GetInt("height");
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprSaveGameWarning");
         this.m_sprSaveGameWarning.x = _loc4_.x;
         this.m_sprSaveGameWarning.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtVersion");
         this.m_txtVersion.x = _loc4_.x;
         this.m_txtVersion.y = _loc4_.y;
         this.m_txtVersion.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),11846087,"right");
         this.m_txtVersion.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtVersion.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtCopyright");
         this.m_txtCopyright.x = _loc4_.x;
         this.m_txtCopyright.y = _loc4_.y;
         this.m_txtCopyright.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),11846087,"left");
         this.m_txtCopyright.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtCopyright.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.txtCreditNames");
         this.txtCreditNames.x = _loc4_.x;
         this.txtCreditNames.y = _loc4_.y;
         this.txtCreditNames.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.txtCreditNames.SetWidth(GUIValues.GetInt("GUIEscMenu.txtCreditNames.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.txtCreditRoles");
         this.txtCreditRoles.x = _loc4_.x;
         this.txtCreditRoles.y = _loc4_.y;
         this.txtCreditRoles.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"),"right");
         this.txtCreditRoles.SetWidth(GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtMasterVolName");
         this.m_txtMasterVolName.x = _loc4_.x;
         this.m_txtMasterVolName.y = _loc4_.y;
         this.m_txtMasterVolName.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtMasterVolName.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtMusicVolName");
         this.m_txtMusicVolName.x = _loc4_.x;
         this.m_txtMusicVolName.y = _loc4_.y;
         this.m_txtMusicVolName.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtMusicVolName.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtSoundVolName");
         this.m_txtSoundVolName.x = _loc4_.x;
         this.m_txtSoundVolName.y = _loc4_.y;
         this.m_txtSoundVolName.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtSoundVolName.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtFPSName");
         this.m_txtFPSName.x = _loc4_.x;
         this.m_txtFPSName.y = _loc4_.y;
         this.m_txtFPSName.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtFPSName.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtMasterVolValue");
         this.m_txtMasterVolValue.x = _loc4_.x;
         this.m_txtMasterVolValue.y = _loc4_.y;
         this.m_txtMasterVolValue.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtMasterVolValue.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtMusicVolValue");
         this.m_txtMusicVolValue.x = _loc4_.x;
         this.m_txtMusicVolValue.y = _loc4_.y;
         this.m_txtMusicVolValue.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtMusicVolValue.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtSoundVolValue");
         this.m_txtSoundVolValue.x = _loc4_.x;
         this.m_txtSoundVolValue.y = _loc4_.y;
         this.m_txtSoundVolValue.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtSoundVolValue.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtFPSValue");
         this.m_txtFPSValue.x = _loc4_.x;
         this.m_txtFPSValue.y = _loc4_.y;
         this.m_txtFPSValue.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left");
         this.m_txtFPSValue.SetWidth(GUIValues.GetInt("GUIEscMenu.m_txtMasterVolName.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprTitleGrass1");
         this.m_sprTitleGrass1.y = _loc4_.y;
         this.m_sprTitleGrass2.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprTitleIslands1");
         this.m_sprTitleIslands1.y = _loc4_.y;
         this.m_sprTitleIslands2.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprTitleMid1");
         this.m_sprTitleMid1.y = _loc4_.y;
         this.m_sprTitleMid2.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprTitleMtns1");
         this.m_sprTitleMtns1.y = _loc4_.y;
         this.m_sprTitleMtns2.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprTitleTrees1");
         this.m_sprTitleTrees1.y = _loc4_.y;
         this.m_sprTitleTrees2.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnPlay");
         this.m_btnPlay.x = _loc4_.x;
         this.m_btnPlay.y = _loc4_.y;
         this.m_btnPlay.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         this.m_btnSave.x = _loc4_.x;
         this.m_btnSave.y = _loc4_.y;
         this.m_btnSave.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         this.m_btnQuit.x = _loc4_.x;
         this.m_btnQuit.y = _loc4_.y;
         this.m_btnQuit.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnExit");
         this.m_btnExit.x = _loc4_.x;
         this.m_btnExit.y = _loc4_.y;
         this.m_btnExit.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnLoad");
         this.m_btnLoad.x = _loc4_.x;
         this.m_btnLoad.y = _loc4_.y;
         this.m_btnLoad.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         this.m_btnResume.x = _loc4_.x;
         this.m_btnResume.y = _loc4_.y;
         this.m_btnResume.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_txtLoadingGame");
         this.m_txtLoadingGame.x = _loc4_.x;
         this.m_txtLoadingGame.y = _loc4_.y;
         this.m_txtLoadingGame.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),11846087,"left");
         this.m_txtLoadingGame.SetWidth(GUIValues.GetInt("GUIEscMenu.txtCreditRoles.width"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnCredits");
         this.m_btnCredits.x = _loc4_.x;
         this.m_btnCredits.y = _loc4_.y;
         this.m_btnCredits.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnOptions");
         this.m_btnOptions.x = _loc4_.x;
         this.m_btnOptions.y = _loc4_.y;
         this.m_btnOptions.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnInstr");
         this.m_btnInstr.x = _loc4_.x;
         this.m_btnInstr.y = _loc4_.y;
         this.m_btnInstr.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprSaveGameWarning.MoreInfo");
         this.m_btnMoreInfo.x = _loc4_.x;
         this.m_btnMoreInfo.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprSaveGameWarning.Retry");
         this.m_btnRetry.x = _loc4_.x;
         this.m_btnRetry.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnReset");
         this.m_btnReset.x = _loc4_.x;
         this.m_btnReset.y = _loc4_.y;
         this.m_btnReset.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnMute");
         this.m_btnMute.x = _loc4_.x;
         this.m_btnMute.y = _loc4_.y;
         this.m_btnMute.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnFullscreen");
         this.m_btnFullscreen.x = _loc4_.x;
         this.m_btnFullscreen.y = _loc4_.y;
         this.m_btnFullscreen.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnAUTO");
         this.m_btnAUTO.x = _loc4_.x;
         this.m_btnAUTO.y = _loc4_.y;
         this.m_btnAUTO.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btn800");
         this.m_btn800.x = _loc4_.x;
         this.m_btn800.y = _loc4_.y;
         this.m_btn800.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btn1024");
         this.m_btn1024.x = _loc4_.x;
         this.m_btn1024.y = _loc4_.y;
         this.m_btn1024.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btn1360");
         this.m_btn1360.x = _loc4_.x;
         this.m_btn1360.y = _loc4_.y;
         this.m_btn1360.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btn1600");
         this.m_btn1600.x = _loc4_.x;
         this.m_btn1600.y = _loc4_.y;
         this.m_btn1600.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btn2400");
         this.m_btn2400.x = _loc4_.x;
         this.m_btn2400.y = _loc4_.y;
         this.m_btn2400.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnFilter");
         this.m_btnFilter.x = _loc4_.x;
         this.m_btnFilter.y = _loc4_.y;
         this.m_btnFilter.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnStretch");
         this.m_btnStretch.x = _loc4_.x;
         this.m_btnStretch.y = _loc4_.y;
         this.m_btnStretch.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnMusicUp");
         this.m_btnMusicUp.x = _loc4_.x;
         this.m_btnMusicUp.y = _loc4_.y;
         this.m_btnMusicUp.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnMusicDn");
         this.m_btnMusicDn.x = _loc4_.x;
         this.m_btnMusicDn.y = _loc4_.y;
         this.m_btnMusicDn.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnSoundUp");
         this.m_btnSoundUp.x = _loc4_.x;
         this.m_btnSoundUp.y = _loc4_.y;
         this.m_btnSoundUp.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnSoundDn");
         this.m_btnSoundDn.x = _loc4_.x;
         this.m_btnSoundDn.y = _loc4_.y;
         this.m_btnSoundDn.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnMasterUp");
         this.m_btnMasterUp.x = _loc4_.x;
         this.m_btnMasterUp.y = _loc4_.y;
         this.m_btnMasterUp.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnMasterDn");
         this.m_btnMasterDn.x = _loc4_.x;
         this.m_btnMasterDn.y = _loc4_.y;
         this.m_btnMasterDn.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnFPSUp");
         this.m_btnFPSUp.x = _loc4_.x;
         this.m_btnFPSUp.y = _loc4_.y;
         this.m_btnFPSUp.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnFPSDn");
         this.m_btnFPSDn.x = _loc4_.x;
         this.m_btnFPSDn.y = _loc4_.y;
         this.m_btnFPSDn.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnAutosave");
         this.m_btnAutosave.x = _loc4_.x;
         this.m_btnAutosave.y = _loc4_.y;
         this.m_btnAutosave.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnGreenlight");
         this.m_btnGreenlight.x = _loc4_.x;
         this.m_btnGreenlight.y = _loc4_.y;
         this.m_btnGreenlight.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprBugsBG");
         this.m_sprBugsBG.x = _loc4_.x;
         this.m_sprBugsBG.y = _loc4_.y;
         this.m_sprBugsBG.pixels = DataHandler.GetImage("btn_esc_bugs_bg.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnZomboid");
         this.m_btnZomboid.x = _loc4_.x;
         this.m_btnZomboid.y = _loc4_.y;
         this.m_btnZomboid.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnBBG");
         this.m_btnBBG.x = _loc4_.x;
         this.m_btnBBG.y = _loc4_.y;
         this.m_btnBBG.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprSparkle");
         this.m_sprSparkle.x = _loc4_.x;
         this.m_sprSparkle.y = _loc4_.y;
         this.m_sprSparkle.pixels = DataHandler.GetImage("Sparkle.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnTerEdit");
         this.m_btnTerEdit.x = _loc4_.x;
         this.m_btnTerEdit.y = _loc4_.y;
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpCur");
         this.m_sprHelpCur.x = _loc4_.x;
         this.m_sprHelpCur.y = _loc4_.y;
         this.m_sprHelpCur.pixels = DataHandler.GetImage("sprHelpCur.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpL2Click");
         this.m_sprHelpL2Click.x = _loc4_.x;
         this.m_sprHelpL2Click.y = _loc4_.y;
         this.m_sprHelpL2Click.pixels = DataHandler.GetImage("sprHelpL2Click.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpLClick");
         this.m_sprHelpLClick.x = _loc4_.x;
         this.m_sprHelpLClick.y = _loc4_.y;
         this.m_sprHelpLClick.pixels = DataHandler.GetImage("sprHelpLClick.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpRClick");
         this.m_sprHelpRClick.x = _loc4_.x;
         this.m_sprHelpRClick.y = _loc4_.y;
         this.m_sprHelpRClick.pixels = DataHandler.GetImage("sprHelpRClick.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpSLClick");
         this.m_sprHelpSLClick.x = _loc4_.x;
         this.m_sprHelpSLClick.y = _loc4_.y;
         this.m_sprHelpSLClick.pixels = DataHandler.GetImage("sprHelpSLClick.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpSpace");
         this.m_sprHelpSpace.x = _loc4_.x;
         this.m_sprHelpSpace.y = _loc4_.y;
         this.m_sprHelpSpace.pixels = DataHandler.GetImage("sprHelpSpace.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpWASD");
         this.m_sprHelpWASD.x = _loc4_.x;
         this.m_sprHelpWASD.y = _loc4_.y;
         this.m_sprHelpWASD.pixels = DataHandler.GetImage("sprHelpWASD.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_sprHelpPlayer");
         this.m_sprHelpPlayer.x = _loc4_.x;
         this.m_sprHelpPlayer.y = _loc4_.y;
         this.m_sprHelpPlayer.pixels = DataHandler.GetImage("sprHelpPlayer.png",_loc5_);
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnManual");
         this.m_btnManual.x = _loc4_.x;
         this.m_btnManual.y = _loc4_.y;
         this.m_btnManual.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnWiki");
         this.m_btnWiki.x = _loc4_.x;
         this.m_btnWiki.y = _loc4_.y;
         this.m_btnWiki.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnMain");
         if(this.m_nMode == MODE_OPTIONS)
         {
            _loc4_ = GUIValues.GetPoint("GUIEscMenu.m_btnOptions");
         }
         this.m_btnMain.x = _loc4_.x;
         this.m_btnMain.y = _loc4_.y;
         this.m_btnMain.Zoom(GUIValues.GetInt("GUIEscMenu.UI.Zoom"));
         GUIValues.m_bOverrideZoom = _loc2_;
      }
   }
}

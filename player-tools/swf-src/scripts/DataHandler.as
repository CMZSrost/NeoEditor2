package
{
   import flash.display.Bitmap;
   import flash.display.BitmapData;
   import flash.display.Stage;
   import flash.events.*;
   import flash.geom.Matrix;
   import flash.net.URLLoader;
   import flash.net.URLRequest;
   import flash.net.URLRequestMethod;
   import flash.net.URLVariables;
   import flash.net.navigateToURL;
   import flash.system.System;
   import flash.text.TextField;
   import flash.text.TextFormat;
   import flash.utils.ByteArray;
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class DataHandler extends EventDispatcher
   {
      
      private static var objEvtDispatcher:EventDispatcher;
      
      private static var strServerURL:String;
      
      private static var m_strCurrentModPath:String;
      
      private static var m_strCurrentModFile:String;
      
      private static var m_strCurrentModName:String;
      
      private static var strIMGFolder:String;
      
      private static var strFontFolder:String;
      
      private static var strSoundFolder:String;
      
      private static var strQuery:String;
      
      public static var m_vRecipesSorted:Vector.<Recipe>;
      
      public static var m_dictForbiddenHexes:Dictionary;
      
      private static var m_aEncounterTriggersRemaining:Array;
      
      public static var m_vWildMusicLoops:Vector.<Class>;
      
      public static var m_vWildMusicCues:Vector.<Class>;
      
      public static var m_vDMCMusicCues:Vector.<Class>;
      
      private static var dictData:Dictionary;
      
      private static var dictXMLTables:Dictionary;
      
      private static var m_objData:DataSet;
      
      private static var nImgBatchSize:int;
      
      private static var vNoRemapGroups:Vector.<int>;
      
      public static var nRecipesLoaded:int;
      
      public static var strProductURL:String = §_a_-_---§.§_a_--_--§(-1820302792);
      
      private static var aLoadList:Array;
      
      private static var m_bNextStep:Boolean;
      
      private static var bSeparateXMLTables:Boolean = false;
      
      private static var bLoadingBaseData:Boolean = true;
      
      private static var nStepsLoaded:int;
      
      private static var strMissing:String;
      
      private static var bmpMissing:Bitmap;
      
      public static var bLoadingComplete:Boolean;
      
      public static var m_objSG:FlxSave;
      
      public static var strCurrentTable:String;
      
      public static var m_strZoomPrefix:String = "x2_";
      
      public static var m_strBasePrefix:String = "0";
      
      public static var m_strSaveGameBind:String = "nsSGv1";
      
      public static var m_objPrefs:FlxSave;
      
      public static var m_strPrefsBind:String = "nsPrefsv1";
      
      public static var m_strCredits:String = "Daniel Fedor\n\n" + "Cameron Harris\n" + "Harald Hagen\n" + "Josh Culler\n" + "Paul Csapo (PCDMS - UK)\n" + "Jordan Grimmer\n" + "Klaus Pillon\n" + "Max Antonov\n\n" + "Rochelle Fedor, Chris Fedor, Foster Stainthorpe, Kim Stainthorpe, Cole Dorchester, Brook Bakay, Henry Smith, Jack Nilssen, Gareth Fouche, Andrew Gardner, Jason Levine, Alf Pardo, Chris Blackbourne, Lars Doucet, David Fedor, Jan Salmanowicz (Kaaven), Markofbear, RJ Costanzo (Nickboom), Scavenger, Minister Max, Bernd Wahl, Dragoonseal, J.R. Rosello, Lina Lamprou, 还有来自所有玩家的建议和耐心，还有爸爸妈妈\n\n" + "Flixel, FlashDevelop, TortoiseSVN, MySQL, Audacity, Chevy Ray\'s AssetBatcher\n";
      
      public static var m_strCreditRoles:String = "Created By:\n\n" + "Additional Writing & Design:\n\n" + "Music:\n" + "Additional Audio:\n" + "Additional Art:\n\n\n\n" + "Special Thanks:\n\n\n\n\n\n\n\n" + "Free Tools that made\nN.E.O. Scavenger possible:\n";
      
      public static var m_fPercentLoaded:Number;
      
      public static var stage:Stage;
      
      public static var m_strCopyright:String = "Copyright 2014  Blue Bottle Games. All Rights Reserved.";
      
      public static var nFPS:int = 60;
      
      public static var nFPSModifier:int = 1;
      
      private static var MissingImg:Class = DataHandler_MissingImg;
      
      private static var baFile:ByteArray;
      
      private static var strFile:String;
      
      private static var xmlData:XML;
      
      private static var m_bEmbed:Boolean;
      
      private static var m_bEmbedIMG:Boolean;
      
      public static var m_strDebug:String;
      
      public static var m_strDebugTail:String;
      
      public static var m_nDebugCounter:int;
      
      public static var myURL:String = "localhost";
      
      public static var m_strVersion:String = "v1.15 " + "1/6/2017";
       
      
      public function DataHandler()
      {
         super();
      }
      
      public static function Initialize() : void
      {
         var _loc1_:String = null;
         var _loc2_:int = 0;
         strServerURL = "http://localhost/main/sites/all/themes/danland/includes/neoscavenger/";
         strIMGFolder = §_a_-_---§.§_a_--_--§(-1820302827);
         strFontFolder = "fonts/";
         strQuery = "";
         nStepsLoaded = 0;
         strMissing = "missing.png";
         bmpMissing = new MissingImg();
         bLoadingComplete = false;
         m_strCurrentModName = m_strBasePrefix;
         m_fPercentLoaded = 0;
         nImgBatchSize = 500;
         m_strDebug = "";
         m_strDebugTail = "";
         m_nDebugCounter = 0;
         nRecipesLoaded = 0;
         vNoRemapGroups = Vector.<int>([7,8,9,12,20,25,26,35,36,90,91,96,103]);
         objEvtDispatcher = new EventDispatcher();
         dictData = new Dictionary();
         m_aEncounterTriggersRemaining = new Array();
         m_vRecipesSorted = new Vector.<Recipe>();
         m_dictForbiddenHexes = new Dictionary();
         dictXMLTables = new Dictionary();
         m_vWildMusicCues = new Vector.<Class>();
         m_vWildMusicLoops = new Vector.<Class>();
         m_vDMCMusicCues = new Vector.<Class>();
         m_vWildMusicCues.push(cueExploreTrack1);
         m_vWildMusicCues.push(cueExploreTrack2);
         m_vWildMusicCues.push(cueExploreTrack3);
         m_vWildMusicCues.push(cueExploreTrack4);
         m_vWildMusicCues.push(cueExploreTrack5);
         m_vWildMusicCues.push(cueExploreTrack7);
         m_vWildMusicCues.push(cueExploreTrack8);
         m_vDMCMusicCues.push(cueDMCTrack1);
         m_vDMCMusicCues.push(cueDMCTrack3);
         m_vDMCMusicCues.push(cueDMCTrack4);
         m_vDMCMusicCues.push(cueDMCTrack5);
         m_vWildMusicLoops.push(loopMapWild01);
         m_bEmbed = false;
         m_bEmbedIMG = false;
         aLoadList = new Array();
         m_bNextStep = false;
         if(false == false && false == false)
         {
            _loc1_ = stage.root.loaderInfo.url;
            _loc1_ = unescape(_loc1_);
            _loc1_ = _loc1_.replace("file|/","");
            _loc2_ = int(_loc1_.lastIndexOf("/"));
            _loc1_ = _loc1_.substr(0,_loc2_ + 1);
            aLoadList = new Array();
            strServerURL = _loc1_;
            m_strCurrentModPath = _loc1_;
            aLoadList.push(new Array(LoadData,new URLRequest(_loc1_ + §_a_-_---§.§_a_--_--§(-1820302807)),ParseModList));
            dictData[m_strBasePrefix] = new DataSet(m_strBasePrefix);
            aLoadList.push(new Array(LoadMod,strServerURL + "neogame.xml",m_strBasePrefix));
            SetupXMLLoadList(true,false,m_strBasePrefix,strServerURL);
            m_bNextStep = true;
            return;
         }
         dictData[m_strBasePrefix] = new DataSet(m_strBasePrefix);
         m_objData = dictData[m_strBasePrefix];
         SetupXMLLoadList(m_bEmbed,m_bEmbedIMG,m_strBasePrefix,strServerURL);
         m_bNextStep = true;
      }
      
      public static function update() : void
      {
         if(m_bNextStep)
         {
            NextStep();
         }
      }
      
      public static function SetupXMLLoadList(param1:Boolean, param2:Boolean, param3:String, param4:String) : void
      {
         var _loc5_:DataSet = dictData[param3];
         if(param2 == false)
         {
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302791)),ParseImageList));
            aLoadList.push(new Array(_loc5_.LoadImages,param4 + strIMGFolder,StoreImages));
         }
         else
         {
            NEOScavengerImages.Init();
            NEOScavengerImagesX2.Init();
         }
         NEOScavengerSounds.Init(dictData[m_strBasePrefix]);
         if(param1)
         {
            aLoadList.push(new Array(LoadDataHardcoded,"gamevars",ParseGameVars));
            aLoadList.push(new Array(LoadDataHardcoded,"chargeprofiles",ParseChargeProfiles));
            aLoadList.push(new Array(LoadDataHardcoded,"containertypes",ParseContainerTypes));
            aLoadList.push(new Array(LoadDataHardcoded,"itemtypes",ParseItemTypes));
            aLoadList.push(new Array(LoadDataHardcoded,"treasuretable",ParseTreasureTypes));
            aLoadList.push(new Array(LoadDataHardcoded,"attackmodes",ParseAttackModes));
            aLoadList.push(new Array(LoadDataHardcoded,"hextypes",ParseHexTypes));
            aLoadList.push(new Array(LoadDataHardcoded,"conditions",ParseConditions));
            aLoadList.push(new Array(LoadDataHardcoded,"factions",ParseFactions));
            aLoadList.push(new Array(LoadDataHardcoded,"creatures",ParseCreatures));
            aLoadList.push(new Array(LoadDataHardcoded,"creaturesources",ParseCreatureSources));
            aLoadList.push(new Array(LoadDataHardcoded,"encounters",ParseEncounters));
            aLoadList.push(new Array(LoadDataHardcoded,"headlines",ParseHeadlines));
            aLoadList.push(new Array(LoadDataHardcoded,"datafiles",ParseDatafiles));
            aLoadList.push(new Array(LoadDataHardcoded,"camptypes",ParseCampTypes));
            aLoadList.push(new Array(LoadDataHardcoded,"battlemoves",ParseBattleMoves));
            aLoadList.push(new Array(LoadDataHardcoded,"maps",ParseMaps));
            aLoadList.push(new Array(LoadDataHardcoded,"encountertriggers",ParseEncounterTriggers));
            aLoadList.push(new Array(LoadDataHardcoded,"itemprops",ParseItemProps));
            aLoadList.push(new Array(LoadDataHardcoded,"ingredients",ParseIngredients));
            aLoadList.push(new Array(LoadDataHardcoded,"recipes",ParseRecipes));
            aLoadList.push(new Array(LoadDataHardcoded,"barterhexes",ParseBarterHexes));
            aLoadList.push(new Array(LoadDataHardcoded,"forbiddenhexes",ParseForbiddenHexes));
            aLoadList.push(new Array(LoadDataHardcoded,"dmcplaces",ParseDMCPlaces));
            aLoadList.push(new Array(UpdateTemplateItems));
            if(param3 != m_strBasePrefix)
            {
               aLoadList.push(new Array(RemapErrorReport,null,null));
            }
         }
         else
         {
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302795)),ParseGameVars));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302811)),ParseChargeProfiles));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302805)),ParseContainerTypes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302785)),ParseItemTypes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302809)),ParseTreasureTypes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302806)),ParseAttackModes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302816)),ParseHexTypes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302786)),ParseConditions));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302803)),ParseFactions));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302825)),ParseCreatures));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302830)),ParseCreatureSources));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302826)),ParseEncounters));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302789)),ParseHeadlines));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302799)),ParseDatafiles));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302802)),ParseCampTypes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302796)),ParseBattleMoves));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302808)),ParseMaps));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302828)),ParseEncounterTriggers));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302801)),ParseItemProps));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302829)),ParseIngredients));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302797)),ParseRecipes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302794)),ParseBarterHexes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302787)),ParseForbiddenHexes));
            aLoadList.push(new Array(LoadData,new URLRequest(param4 + §_a_-_---§.§_a_--_--§(-1820302814)),ParseDMCPlaces));
         }
      }
      
      private static function SetupSeparateXMLLoadList(param1:String) : void
      {
         bSeparateXMLTables = true;
         aLoadList.splice(0,0,new Array(LoadMod,m_strCurrentModPath + "data/gamevars.xml",param1));
         aLoadList.splice(1,0,new Array(LoadMod,m_strCurrentModPath + "data/chargeprofiles.xml",param1));
         aLoadList.splice(2,0,new Array(LoadMod,m_strCurrentModPath + "data/containertypes.xml",param1));
         aLoadList.splice(3,0,new Array(LoadMod,m_strCurrentModPath + "data/itemtypes.xml",param1));
         aLoadList.splice(4,0,new Array(LoadMod,m_strCurrentModPath + "data/treasuretable.xml",param1));
         aLoadList.splice(5,0,new Array(LoadMod,m_strCurrentModPath + "data/attackmodes.xml",param1));
         aLoadList.splice(6,0,new Array(LoadMod,m_strCurrentModPath + "data/hextypes.xml",param1));
         aLoadList.splice(7,0,new Array(LoadMod,m_strCurrentModPath + "data/conditions.xml",param1));
         aLoadList.splice(8,0,new Array(LoadMod,m_strCurrentModPath + "data/factions.xml",param1));
         aLoadList.splice(9,0,new Array(LoadMod,m_strCurrentModPath + "data/creatures.xml",param1));
         aLoadList.splice(10,0,new Array(LoadMod,m_strCurrentModPath + "data/creaturesources.xml",param1));
         aLoadList.splice(11,0,new Array(LoadMod,m_strCurrentModPath + "data/encounters.xml",param1));
         aLoadList.splice(12,0,new Array(LoadMod,m_strCurrentModPath + "data/headlines.xml",param1));
         aLoadList.splice(13,0,new Array(LoadMod,m_strCurrentModPath + "data/datafiles.xml",param1));
         aLoadList.splice(14,0,new Array(LoadMod,m_strCurrentModPath + "data/camptypes.xml",param1));
         aLoadList.splice(15,0,new Array(LoadMod,m_strCurrentModPath + "data/battlemoves.xml",param1));
         aLoadList.splice(16,0,new Array(LoadMod,m_strCurrentModPath + "data/maps.xml",param1));
         aLoadList.splice(17,0,new Array(LoadMod,m_strCurrentModPath + "data/encountertriggers.xml",param1));
         aLoadList.splice(18,0,new Array(LoadMod,m_strCurrentModPath + "data/itemprops.xml",param1));
         aLoadList.splice(19,0,new Array(LoadMod,m_strCurrentModPath + "data/ingredients.xml",param1));
         aLoadList.splice(20,0,new Array(LoadMod,m_strCurrentModPath + "data/recipes.xml",param1));
         aLoadList.splice(21,0,new Array(LoadMod,m_strCurrentModPath + "data/barterhexes.xml",param1));
         aLoadList.splice(22,0,new Array(LoadMod,m_strCurrentModPath + "data/forbiddenhexes.xml",param1));
         aLoadList.splice(23,0,new Array(LoadMod,m_strCurrentModPath + "data/dmcplaces.xml",param1));
      }
      
      public static function ReInitialize() : void
      {
         ResetEncounterTriggers();
         if(dictData[m_strBasePrefix] != null)
         {
            dictData[m_strBasePrefix].ReInitialize();
         }
      }
      
      private static function MenuMsg(param1:String) : void
      {
         if(FlxG.state is MenuState)
         {
            MenuState(FlxG.state).ShowMsg(param1);
         }
         m_strDebug += "\n" + param1;
         m_strDebugTail = param1;
      }
      
      private static function NextStep() : void
      {
         m_bNextStep = false;
         if(aLoadList.length <= 0)
         {
            bLoadingComplete = true;
            m_fPercentLoaded = 1;
            MenuMsg("数据加载完毕。");
            System.disposeXML(xmlData);
            xmlData = null;
            return;
         }
         if(aLoadList[0][1] == null && aLoadList[0][2] == null)
         {
            aLoadList[0][0]();
         }
         else if(aLoadList[0][1] == null)
         {
            aLoadList[0][0](aLoadList[0][2]);
         }
         else
         {
            aLoadList[0][0](aLoadList[0][1],aLoadList[0][2]);
         }
         m_fPercentLoaded = nStepsLoaded / (nStepsLoaded + aLoadList.length);
         if(m_objData != null && m_objData.nDictIMGLength != 0)
         {
            m_fPercentLoaded += 1 / (nStepsLoaded + aLoadList.length) * m_objData.nDictIMGLoaded / m_objData.nDictIMGLength;
         }
         aLoadList.splice(0,1);
         ++nStepsLoaded;
      }
      
      public static function LoadMod(param1:String, param2:String) : void
      {
         if(m_strCurrentModName != param2)
         {
            bLoadingBaseData = false;
         }
         m_objData = dictData[param2];
         m_strCurrentModName = param2;
         m_strCurrentModPath = param1.substring(0,param1.lastIndexOf("/") + 1);
         m_strCurrentModFile = param1.substring(param1.lastIndexOf("/") + 1,param1.length - 4);
         if(m_strCurrentModFile == "neogame")
         {
            bSeparateXMLTables = false;
         }
         var _loc3_:URLRequest = new URLRequest(param1);
         var _loc4_:URLLoader;
         (_loc4_ = new URLLoader(_loc3_)).addEventListener(Event.COMPLETE,ParseMod);
         _loc4_.addEventListener(IOErrorEvent.IO_ERROR,ErrorMod);
         _loc4_.addEventListener(SecurityErrorEvent.SECURITY_ERROR,HTTPErrorMod);
         MenuMsg("加载mod数据文件..." + _loc3_.url);
      }
      
      public static function ParseMod(param1:Event) : void
      {
         var e:Event = param1;
         if(e is ErrorEvent)
         {
            ErrorLoadingMessage(ErrorEvent(e));
            return;
         }
         MenuMsg("加载mod数据文件...完成");
         e.currentTarget.removeEventListener(Event.COMPLETE,ParseMod);
         e.currentTarget.removeEventListener(IOErrorEvent.IO_ERROR,ErrorMod);
         e.currentTarget.removeEventListener(SecurityErrorEvent.SECURITY_ERROR,HTTPErrorMod);
         MenuMsg("解析mod数据文件...");
         try
         {
            if(bSeparateXMLTables)
            {
               dictXMLTables[m_strCurrentModFile] = new XML(e.target.data);
            }
            else
            {
               xmlData = new XML(e.target.data);
            }
         }
         catch(err:TypeError)
         {
            MenuMsg(err.message);
            return;
         }
         m_bNextStep = true;
      }
      
      public static function ErrorLoadingMessage(param1:ErrorEvent) : void
      {
         MenuMsg(param1.text + "\n");
         if(param1.errorID == 2032)
         {
            MenuMsg("找不到文件。");
         }
         if(m_strCurrentModFile == "neogame")
         {
            MenuMsg("\n尝试寻找分散的数据文件...");
            SetupSeparateXMLLoadList(m_strCurrentModName);
            m_bNextStep = true;
         }
         else if(bSeparateXMLTables)
         {
            if(bLoadingBaseData == false)
            {
               m_bNextStep = true;
            }
         }
      }
      
      public static function ErrorMod(param1:IOErrorEvent) : void
      {
         MenuMsg(param1.text + "\n");
         if(param1.errorID == 2032)
         {
            MenuMsg("找不到文件。");
         }
         if(m_strCurrentModFile == "neogame")
         {
            MenuMsg("\n尝试分离数据表...");
            SetupSeparateXMLLoadList(m_strCurrentModName);
            m_bNextStep = true;
         }
         else if(bSeparateXMLTables)
         {
            if(bLoadingBaseData == false)
            {
               m_bNextStep = true;
            }
         }
      }
      
      public static function HTTPErrorMod(param1:SecurityErrorEvent) : void
      {
         MenuMsg(param1.text);
      }
      
      public static function CleanMasterString(param1:String) : String
      {
         return ("_" + param1).substr(1);
      }
      
      public static function LoadData(param1:URLRequest, param2:Function) : void
      {
         param1.method = URLRequestMethod.POST;
         var _loc3_:DataLoader = new DataLoader(param2);
         _loc3_.load(param1);
      }
      
      public static function LoadDataHardcoded(param1:String, param2:Function) : void
      {
         var _loc5_:XML = null;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:XML = null;
         var _loc3_:URLVariables = new URLVariables();
         _loc3_["nRows"] = 0;
         var _loc4_:uint = 0;
         if(bSeparateXMLTables)
         {
            strCurrentTable = param1;
            xmlData = dictXMLTables[param1];
            if(xmlData == null)
            {
               MenuMsg(param1 + " 数据未找到，继续....");
               m_bNextStep = true;
               return;
            }
         }
         for each(_loc5_ in xmlData.database.table)
         {
            if((_loc6_ = CleanMasterString(_loc5_.@name)) == param1)
            {
               for each(_loc8_ in _loc5_.column)
               {
                  _loc7_ = CleanMasterString(_loc8_.@name + _loc4_);
                  _loc3_[_loc7_] = CleanMasterString(_loc8_);
               }
               _loc4_++;
               _loc3_["nRows"] = _loc4_;
            }
         }
         if(bSeparateXMLTables)
         {
            if(dictXMLTables[strCurrentTable] != null)
            {
               System.disposeXML(dictXMLTables[strCurrentTable]);
               dictXMLTables[strCurrentTable] = 1;
               delete dictXMLTables[strCurrentTable];
            }
         }
         param2(_loc3_);
         _loc3_ = null;
      }
      
      private static function ParseModList(param1:*) : void
      {
         var _loc4_:String = null;
         var _loc5_:String = null;
         if(param1 is ErrorEvent)
         {
            ErrorLoadingMessage(param1);
            return;
         }
         MenuMsg("解析mod列表.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = param1["strModURL" + _loc3_];
            _loc5_ = param1["strModName" + _loc3_];
            if(_loc4_ == null)
            {
               MenuMsg("MOD URL在getmods.php: strModUrl中不存在" + _loc3_ + "\n");
               return;
            }
            _loc4_ = (_loc4_ = _loc4_.replace(/\n/gi,"")).replace(/\r/gi,"");
            if(_loc5_ == null)
            {
               MenuMsg("MOD NAME在getmods.php: strModName中不存在" + _loc3_ + "\n");
               return;
            }
            if((_loc5_ = (_loc5_ = _loc5_.replace(/\n/gi,"")).replace(/\r/gi,"")) != m_strBasePrefix && dictData[_loc5_] == null)
            {
               dictData[_loc5_] = new DataSet(_loc5_);
            }
            aLoadList.push(new Array(LoadMod,strServerURL + _loc4_ + "/" + "neogame.xml",_loc5_));
            SetupXMLLoadList(true,false,_loc5_,strServerURL + _loc4_ + "/");
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function StoreImages(param1:Bitmap, param2:String) : void
      {
         var _loc3_:Array = null;
         if(param2 != "")
         {
            m_objData.StoreImages(param1,param2);
         }
         m_nDebugCounter = m_objData.nDictIMGLoaded;
         if(m_objData.nBatchIMGLoaded >= m_objData.nImgBatchCurrentMax && m_objData.nDictIMGLoaded < m_objData.nDictIMGLength)
         {
            m_objData.nBatchIMGLoaded = 0;
            _loc3_ = [new Array(m_objData.LoadImages,m_strCurrentModPath + strIMGFolder,StoreImages)];
            if(m_objData.m_strID == m_strBasePrefix && m_objData.m_vTreasures.length == 0)
            {
               _loc3_ = [new Array(m_objData.LoadImages,strServerURL + strIMGFolder,StoreImages)];
            }
            aLoadList = _loc3_.concat(aLoadList);
            m_bNextStep = true;
         }
         else if(m_objData.nDictIMGLoaded >= m_objData.nDictIMGLength)
         {
            m_bNextStep = true;
         }
      }
      
      private static function ParseImageList(param1:*) : void
      {
         var _loc5_:String = null;
         if(param1 is ErrorEvent)
         {
            ErrorLoadingMessage(param1);
            return;
         }
         MenuMsg("解析图片列表...");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         m_objData.ResetIMGCounters();
         var _loc3_:Vector.<String> = new Vector.<String>();
         var _loc4_:int = 0;
         while(_loc4_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc5_ = (_loc5_ = (_loc5_ = (_loc5_ = param1["strImageURL" + _loc4_]).replace(/\n/gi,"")).replace(/\r/gi,"")).replace(/ /gi,"");
            m_objData.m_dictIMG[_loc5_] = _loc5_;
            ++m_objData.nDictIMGLength;
            _loc3_.push(_loc5_);
            if(_loc3_.length > nImgBatchSize || _loc4_ == _loc2_ - 1)
            {
               m_objData.vImageBatches.push(_loc3_.concat());
               _loc3_.length = 0;
            }
            _loc4_++;
         }
         MenuMsg("加载图片...");
         m_bNextStep = true;
      }
      
      public static function DestroyObject(param1:Object) : *
      {
         if(param1 == null)
         {
            return null;
         }
         if("destroy" in param1)
         {
            param1.destroy();
         }
         return null;
      }
      
      public static function GetRemapIDString(param1:DataRef, param2:String) : String
      {
         var _loc3_:DataSet = null;
         var _loc4_:String = null;
         var _loc5_:String = null;
         if(m_objData.m_strID == m_strBasePrefix)
         {
            if(param1.Mod == "" || param1.Mod == m_strBasePrefix)
            {
               return param1.StrID;
            }
            _loc3_ = GetDataSet(param1.Mod);
            if(_loc3_ == null)
            {
               MenuMsg("未找到Mod: \"" + param1.Mod + "\"\n");
            }
            return _loc3_.m_objRemap[param2][param1.StrID];
         }
         if(param1.Mod == m_strBasePrefix)
         {
            return param1.StrID;
         }
         if((_loc4_ = param1.Mod) == "")
         {
            _loc4_ = m_objData.m_strID;
         }
         if((_loc5_ = GetDataSet(_loc4_).m_objRemap[param2][param1.StrID]) == null)
         {
            if(param2 == "m_dictIMG")
            {
               _loc5_ = strMissing;
            }
            else if(param2 == "m_dictSND")
            {
               _loc5_ = "cueMissing";
            }
            else
            {
               _loc5_ = GetNextItemID(param1.StrID);
            }
            GetDataSet(_loc4_).m_objRemap[param2][param1.StrID] = _loc5_;
         }
         return GetDataSet(_loc4_).m_objRemap[param2][param1.StrID];
      }
      
      public static function GetRemapID(param1:DataRef, param2:String) : int
      {
         var _loc5_:* = false;
         var _loc3_:int = 1;
         var _loc4_:int = param1.ID;
         if(param1.ID < 0)
         {
            _loc3_ = -1;
            _loc4_ = -_loc4_;
         }
         if(m_objData.m_strID == m_strBasePrefix)
         {
            if(param1.Mod == "" || param1.Mod == m_strBasePrefix)
            {
               if((_loc5_ = GetDataSet(m_strBasePrefix)[param2] is Dictionary) == false)
               {
                  while(GetDataSet(m_strBasePrefix)[param2].length < _loc4_)
                  {
                     GetDataSet(m_strBasePrefix)[param2].push(null);
                  }
               }
               return _loc3_ * _loc4_;
            }
            if(GetDataSet(param1.Mod).m_objRemap[param2] is Dictionary)
            {
               return _loc3_ * GetDataSet(param1.Mod).m_objRemap[param2][_loc4_];
            }
            return _loc3_ * GetDataSet(param1.Mod).m_objRemap[param2][_loc4_ - 1];
         }
         if(param1.Mod == m_strBasePrefix)
         {
            return _loc3_ * _loc4_;
         }
         if(param1.Mod == "")
         {
            return _loc3_ * SetRemapID(GetDataSet(m_objData.m_strID),param2,_loc4_);
         }
         return _loc3_ * SetRemapID(GetDataSet(param1.Mod),param2,_loc4_);
      }
      
      public static function SetRemapID(param1:DataSet, param2:String, param3:int) : int
      {
         var _loc5_:* = 0;
         var _loc6_:DataSet = null;
         if(param3 <= 0)
         {
            return param3;
         }
         var _loc4_:int = -1;
         if(param1.m_objRemap[param2] is Array)
         {
            if(param1.m_objRemap[param2][param3 - 1] == null)
            {
               _loc5_ = 0;
               while(param1.m_objRemap[param2].length < param3)
               {
                  GetDataSet(m_strBasePrefix)[param2].push(null);
                  param1.m_objRemap[param2].push(GetDataSet(m_strBasePrefix)[param2].length);
                  _loc5_++;
               }
               return int(GetDataSet(m_strBasePrefix)[param2].length);
            }
            return param1.m_objRemap[param2][param3 - 1];
         }
         if(param1.m_objRemap[param2][param3] == undefined)
         {
            _loc6_ = GetDataSet(m_strBasePrefix);
            _loc5_ = 1;
            while(_loc6_[param2][_loc5_] != undefined)
            {
               _loc5_ = (_loc5_ = _loc5_) + 1;
            }
            _loc4_ = _loc5_;
            param1.m_objRemap[param2][param3] = _loc4_;
            _loc6_[param2][_loc5_] = param3;
            return _loc4_;
         }
         return param1.m_objRemap[param2][param3];
      }
      
      public static function GetNextItemID(param1:String) : String
      {
         var _loc3_:Array = null;
         var _loc10_:Object = null;
         var _loc11_:int = 0;
         var _loc2_:Array = param1.split(".");
         var _loc4_:int = int(_loc2_[0]);
         var _loc5_:int = int(_loc2_[1]);
         var _loc6_:int = -1;
         var _loc7_:int = -1;
         var _loc8_:DataSet = GetDataSet(m_strBasePrefix);
         var _loc9_:int;
         if((_loc9_ = int(vNoRemapGroups.indexOf(_loc4_))) >= 0)
         {
            _loc6_ = _loc4_;
         }
         else
         {
            if(m_objData.m_strID == m_strBasePrefix)
            {
               return param1;
            }
            for(_loc10_ in m_objData.m_objRemap.m_dictItems)
            {
               _loc3_ = String(_loc10_).split(".");
               if(_loc2_[0] == _loc3_[0])
               {
                  _loc3_ = String(m_objData.m_objRemap.m_dictItems[_loc10_]).split(".");
                  _loc6_ = parseInt(_loc3_[0]);
                  break;
               }
            }
            if(_loc6_ == -1)
            {
               _loc11_ = 1;
               while(_loc6_ == -1)
               {
                  if(vNoRemapGroups.indexOf(_loc11_) < 0)
                  {
                     if(_loc8_.m_dictItems[_loc11_ + ".0"] == undefined && _loc8_.m_dictItems[_loc11_ + ".1"] == undefined)
                     {
                        _loc6_ = _loc11_;
                        break;
                     }
                  }
                  _loc11_++;
               }
            }
         }
         _loc11_ = _loc5_;
         while(_loc7_ == -1)
         {
            if(_loc8_.m_dictItems[_loc6_ + "." + _loc11_] == undefined)
            {
               _loc7_ = _loc11_;
               break;
            }
            _loc11_++;
         }
         GetDataSet(m_strBasePrefix).m_dictItems[_loc6_ + "." + _loc7_] = "";
         return _loc6_ + "." + _loc7_;
      }
      
      private static function RemapErrorReport() : void
      {
         var _loc3_:Object = null;
         MenuMsg("检查丢失的项目...");
         var _loc1_:Boolean = true;
         var _loc2_:DataSet = GetDataSet(m_strBasePrefix);
         for(_loc3_ in m_objData.m_objRemap.m_dictItems)
         {
            if(_loc2_.m_dictItems[m_objData.m_objRemap.m_dictItems[_loc3_]] == "")
            {
               _loc1_ = false;
               MenuMsg("项目 " + _loc3_ + " 重新映射到 " + m_objData.m_objRemap.m_dictItems[_loc3_] + " 但未定义.");
            }
         }
         if(_loc1_)
         {
            m_bNextStep = true;
         }
      }
      
      private static function ParseResponse(param1:Array) : Array
      {
         var _loc2_:DataRef = null;
         var _loc4_:Array = null;
         var _loc5_:Array = null;
         var _loc6_:uint = 0;
         var _loc7_:Number = NaN;
         var _loc8_:Array = null;
         var _loc9_:Array = null;
         var _loc10_:PlayerResponse = null;
         var _loc11_:Array = null;
         var _loc3_:uint = 0;
         while(_loc3_ < param1.length)
         {
            _loc4_ = String(param1[_loc3_]).split("=");
            _loc5_ = String(_loc4_[0]).split("+");
            _loc6_ = 0;
            while(_loc6_ < _loc5_.length)
            {
               _loc11_ = String(_loc5_[_loc6_]).split("x");
               _loc2_ = new DataRef(_loc11_[0]);
               if(_loc2_.StrID != "")
               {
                  _loc11_[0] = GetRemapIDString(_loc2_,"m_dictItems");
               }
               else if(_loc2_.ID != 0)
               {
                  _loc11_[0] = GetRemapID(_loc2_,"m_dictIngredients");
               }
               _loc5_[_loc6_] = [_loc11_[0],int(_loc11_[1])];
               _loc6_++;
            }
            _loc7_ = 1;
            _loc8_ = String(_loc4_[1]).split("x");
            _loc9_ = new Array();
            _loc7_ = Number(_loc8_[1]);
            if(_loc8_.length == 5)
            {
               _loc9_.push(Number(_loc8_[2]));
               _loc9_.push(Number(_loc8_[3]));
               _loc9_.push(Number(_loc8_[4]));
            }
            _loc2_ = new DataRef(_loc8_[0]);
            _loc8_[0] = GetRemapID(_loc2_,"m_aEncounters");
            _loc10_ = new PlayerResponse(_loc5_,_loc8_[0],_loc7_,_loc9_);
            param1[_loc3_] = _loc10_;
            _loc3_++;
         }
         return param1;
      }
      
      private static function ParseHexTypes(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:FlxHexTile = null;
         var _loc7_:Array = null;
         var _loc8_:String = null;
         var _loc9_:String = null;
         var _loc10_:String = null;
         MenuMsg("解析 hex types.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_aHexTypes");
            (_loc6_ = new FlxHexTile(null,_loc5_ - 1,0,0,false,0,2)).strName = param1["strName" + _loc3_];
            _loc6_.strDesc = param1["strDesc" + _loc3_];
            _loc6_.nDefaultCampID = GetRemapID(new DataRef(param1["nDefaultCampID" + _loc3_]),"m_vTreasures");
            _loc6_.nTerrainCost = param1["nTerrainCost" + _loc3_];
            _loc6_.nVizLimiter = param1["nVizLimiter" + _loc3_];
            _loc6_.nVizIncrease = param1["nVizIncrease" + _loc3_];
            _loc6_.nTreasureID = GetRemapID(new DataRef(param1["nTreasureID" + _loc3_]),"m_vTreasures");
            _loc6_.nCampItems = param1["nCampItems" + _loc3_];
            _loc6_.bPassable = StrToBoolean(param1["bPassable" + _loc3_]);
            _loc6_.m_nScavengeInitialID = GetRemapID(new DataRef(param1["nScavengeInitialID" + _loc3_]),"m_vTreasures");
            _loc6_.m_nScavengeItemsIDPerHour = GetRemapID(new DataRef(param1["nScavengeItemsIDPerHour" + _loc3_]),"m_vTreasures");
            _loc6_.m_nMinRange = param1["nMinRange" + _loc3_];
            _loc6_.m_nMaxRange = param1["nMaxRange" + _loc3_];
            _loc7_ = String(param1["vLightLevels" + _loc3_]).split(",");
            for each(_loc8_ in _loc7_)
            {
               _loc6_.m_vLightLevels.push(Number(_loc8_));
            }
            if((_loc9_ = String(param1["vCondIDs" + _loc3_])) != "")
            {
               _loc7_ = _loc9_.split(",");
               for each(_loc10_ in _loc7_)
               {
                  _loc6_.m_vCondIDs.push(GetRemapID(new DataRef(_loc10_),"m_aConditions"));
               }
            }
            GetDataSet(m_strBasePrefix).m_aHexTypes[_loc5_ - 1] = _loc6_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseAttackModes(param1:*) : void
      {
         var _loc3_:Array = null;
         var _loc4_:Vector.<Vector.<Number>> = null;
         var _loc5_:Vector.<Number> = null;
         var _loc6_:Array = null;
         var _loc8_:uint = 0;
         var _loc9_:int = 0;
         var _loc10_:String = null;
         var _loc11_:uint = 0;
         var _loc12_:Number = NaN;
         var _loc13_:Number = NaN;
         var _loc14_:uint = 0;
         var _loc15_:uint = 0;
         var _loc16_:String = null;
         var _loc17_:String = null;
         var _loc18_:Boolean = false;
         var _loc19_:Number = NaN;
         var _loc20_:String = null;
         var _loc21_:String = null;
         var _loc22_:Vector.<ChargeProfile> = null;
         var _loc23_:AttackMode = null;
         var _loc24_:Array = null;
         var _loc25_:DataRef = null;
         var _loc26_:int = 0;
         MenuMsg("创建 AttackModes dict。");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc7_:int = 0;
         while(_loc7_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc8_ = uint(param1["id" + _loc7_]);
            _loc9_ = GetRemapID(new DataRef("" + _loc8_),"m_aAttackModes");
            _loc10_ = param1["strName" + _loc7_];
            _loc11_ = uint(param1["nRange" + _loc7_]);
            _loc12_ = Number(param1["fDamageCut" + _loc7_]);
            _loc13_ = Number(param1["fDamageBlunt" + _loc7_]);
            _loc14_ = uint(param1["nPenetration" + _loc7_]);
            _loc15_ = uint(param1["nType" + _loc7_]);
            _loc16_ = param1["strSnd" + _loc7_];
            _loc17_ = GetRemapIDString(new DataRef(param1["strIMG" + _loc7_]),"m_dictIMG");
            _loc18_ = StrToBoolean(param1["bTransfer" + _loc7_]);
            _loc19_ = Number(param1["fMorale" + _loc7_]);
            _loc3_ = String(param1["vAttackerConditions" + _loc7_]).split(",");
            _loc4_ = new Vector.<Vector.<Number>>();
            for each(_loc20_ in _loc3_)
            {
               _loc6_ = _loc20_.split("x");
               _loc5_ = Vector.<Number>([GetRemapID(new DataRef(_loc6_[0]),"m_aConditions"),Number(_loc6_[1])]);
               _loc4_.push(_loc5_);
            }
            _loc21_ = param1["strChargeProfiles" + _loc7_];
            _loc22_ = new Vector.<ChargeProfile>();
            if(_loc21_ != "")
            {
               _loc24_ = _loc21_.split(",");
               _loc26_ = 0;
               while(_loc26_ < _loc24_.length)
               {
                  _loc25_ = new DataRef(_loc24_[_loc26_]);
                  _loc22_.push(GetDataSet(m_strBasePrefix).m_dictChargeProfiles[GetRemapID(_loc25_,"m_dictChargeProfiles")]);
                  _loc26_++;
               }
            }
            _loc23_ = new AttackMode(_loc9_,_loc10_,_loc11_,_loc12_,_loc13_,_loc22_,_loc14_,_loc15_,_loc16_,_loc18_,_loc4_,_loc17_,_loc19_);
            GetDataSet(m_strBasePrefix).m_aAttackModes[_loc9_ - 1] = _loc23_;
            _loc7_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseHeadlines(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         MenuMsg("创建 headlines vector。");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_vHeadlines");
            _loc6_ = param1["strHeadline" + _loc3_];
            GetDataSet(m_strBasePrefix).m_vHeadlines[_loc5_ - 1] = _loc6_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseDMCPlaces(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:String = null;
         var _loc9_:int = 0;
         var _loc10_:int = 0;
         var _loc11_:DMCPlace = null;
         MenuMsg("创建 DMC places。");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_vDMCPlaces");
            _loc6_ = param1["strImg" + _loc3_];
            _loc7_ = GetRemapIDString(new DataRef(param1["strImg" + _loc3_] + "_down.png"),"m_dictIMG");
            _loc8_ = GetRemapIDString(new DataRef(param1["strImg" + _loc3_] + "_on.png"),"m_dictIMG");
            _loc6_ = GetRemapIDString(new DataRef(_loc6_ + ".png"),"m_dictIMG");
            _loc9_ = int(param1["nX" + _loc3_]);
            _loc10_ = int(param1["nY" + _loc3_]);
            (_loc11_ = new DMCPlace()).m_btn = new ImgButton(_loc7_,_loc6_,_loc8_,_loc8_,_loc9_,_loc10_,null);
            _loc11_.m_nEncounterID = GetRemapID(new DataRef(param1["nEncounterID" + _loc3_]),"m_aEncounters");
            GetDataSet(m_strBasePrefix).m_vDMCPlaces[_loc5_ - 1] = _loc11_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseDatafiles(param1:*) : void
      {
         var _loc3_:String = null;
         var _loc4_:Array = null;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:String = null;
         var _loc9_:BitmapData = null;
         var _loc10_:Number = NaN;
         var _loc11_:uint = 0;
         var _loc12_:Item = null;
         MenuMsg("创建 datafiles。");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc5_:int = 0;
         while(_loc5_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc6_ = param1["strName" + _loc5_];
            _loc7_ = (_loc7_ = param1["strDesc" + _loc5_]).replace(/\r\n/gi,"\n");
            _loc8_ = GetRemapIDString(new DataRef(param1["strImg" + _loc5_]),"m_dictIMG");
            _loc9_ = GetImage(_loc8_);
            _loc10_ = parseInt(param1["fValue" + _loc5_]);
            _loc11_ = uint(param1["id" + _loc5_]);
            _loc3_ = GetRemapIDString(new DataRef("36." + _loc11_),"m_dictItems");
            _loc4_ = _loc3_.split(".");
            if(m_objData.m_strID == m_strBasePrefix)
            {
               _loc4_[1] = _loc11_;
            }
            (_loc12_ = Item(GetDataSet(m_strBasePrefix).m_dictItems["36.0"]).Clone()).strName = _loc6_;
            _loc12_.strDesc = _loc7_;
            _loc12_.fMonetaryValue = _loc10_;
            _loc12_.nSubgroupID = int(_loc4_[1]);
            _loc12_.vImageList[0] = _loc9_;
            _loc12_.vImageListNames[0] = _loc8_;
            _loc12_.m_bIgnoreSubGroupWhenStacking = true;
            GetDataSet(m_strBasePrefix).m_dictItems[_loc12_.nGroupID + "." + _loc12_.nSubgroupID] = _loc12_;
            if(m_objData.m_strID != m_strBasePrefix)
            {
               m_objData.m_objRemap.m_dictItems[_loc12_.nGroupID + "." + _loc11_] = _loc12_.nGroupID + "." + _loc12_.nSubgroupID;
            }
            _loc5_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseForbiddenHexes(param1:*) : void
      {
         var _loc4_:String = null;
         MenuMsg("创建 ForbbidenHexes");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = param1["nX" + _loc3_] + "," + param1["nY" + _loc3_];
            GetDataSet(m_strBasePrefix).m_dictForbiddenHexes[_loc4_] = 1;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseMaps(param1:*) : void
      {
         var _loc4_:String = null;
         var _loc5_:String = null;
         MenuMsg("创建 maps dict。");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = param1["strName" + _loc3_];
            _loc5_ = param1["strDef" + _loc3_];
            m_objData.m_dictMaps[_loc4_] = _loc5_;
            if(m_objData.m_strID != m_strBasePrefix)
            {
               m_objData.m_objRemap.m_dictMaps[_loc4_] = _loc4_ + m_objData.m_strID;
               GetDataSet(m_strBasePrefix).m_dictMaps[_loc4_ + m_objData.m_strID] = _loc5_;
            }
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseEncounters(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:int = 0;
         var _loc11_:Array = null;
         var _loc12_:Array = null;
         var _loc13_:Boolean = false;
         var _loc14_:Boolean = false;
         var _loc15_:Array = null;
         var _loc16_:Array = null;
         var _loc17_:SourceCreature = null;
         var _loc18_:Array = null;
         var _loc19_:FlxPoint = null;
         var _loc20_:String = null;
         var _loc21_:Array = null;
         var _loc22_:FlxPoint = null;
         var _loc23_:uint = 0;
         var _loc24_:Number = NaN;
         var _loc25_:Number = NaN;
         var _loc26_:Number = NaN;
         var _loc27_:Number = NaN;
         var _loc28_:Array = null;
         var _loc29_:uint = 0;
         var _loc30_:FlxPoint = null;
         var _loc31_:uint = 0;
         var _loc32_:Array = null;
         var _loc33_:Vector.<int> = null;
         var _loc34_:Array = null;
         var _loc35_:Vector.<int> = null;
         var _loc36_:Encounter = null;
         var _loc37_:String = null;
         MenuMsg("解析 encounter.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_aEncounters");
            _loc6_ = param1["strName" + _loc3_];
            if(m_bEmbed)
            {
               _loc37_ = (_loc37_ = param1["strDesc" + _loc3_]).replace(/\r\n\r\n/gi,"\n\n");
            }
            else
            {
               _loc37_ = (_loc37_ = (_loc37_ = param1["strDesc" + _loc3_]).replace(/\r\n/gi,"\n")).replace(/\r/gi,"\n");
            }
            _loc7_ = GetRemapIDString(new DataRef(param1["strImg" + _loc3_]),"m_dictIMG");
            _loc8_ = GetRemapID(new DataRef(param1["nItemsID" + _loc3_]),"m_vTreasures");
            _loc9_ = GetRemapID(new DataRef(param1["nTreasureID" + _loc3_]),"m_vTreasures");
            _loc10_ = GetRemapID(new DataRef(param1["nRemoveTreasureID" + _loc3_]),"m_vTreasures");
            _loc11_ = ParseResponse(String(param1["aResponses" + _loc3_]).split(","));
            _loc12_ = Encounter.MinimapStrToArray(String(param1["aMinimapHexes" + _loc3_]));
            _loc13_ = StrToBoolean(param1["bRemoveCreatures" + _loc3_]);
            _loc14_ = StrToBoolean(param1["bRemoveUsed" + _loc3_]);
            _loc15_ = String(param1["aConditions" + _loc3_]).split(",");
            _loc16_ = String(param1["aPreConditions" + _loc3_]).split(",");
            _loc17_ = GetCreatureSource(GetRemapID(new DataRef(param1["nCreatureID" + _loc3_]),"m_vCreatureTable"));
            _loc18_ = String(param1["ptCreatureHex" + _loc3_]).split(",");
            _loc19_ = new FlxPoint(_loc18_[0],_loc18_[1]);
            _loc20_ = String(param1["ptEditor" + _loc3_]);
            _loc21_ = String(param1["ptEditor" + _loc3_]).split(",");
            _loc22_ = new FlxPoint();
            if(_loc21_.length > 1)
            {
               _loc22_ = new FlxPoint(_loc21_[0],_loc21_[1]);
            }
            _loc23_ = uint(int(param1["nType" + _loc3_]));
            _loc24_ = Number(param1["fLootChance" + _loc3_]);
            _loc25_ = Number(param1["fAccidentChance" + _loc3_]);
            _loc26_ = Number(param1["fCreatureChance" + _loc3_]);
            _loc27_ = Number(param1["fPrice" + _loc3_]);
            _loc28_ = String(param1["ptTeleport" + _loc3_]).split(",");
            _loc29_ = 0;
            _loc30_ = new FlxPoint();
            if(_loc28_.length > 1)
            {
               _loc30_ = new FlxPoint(_loc28_[0],_loc28_[1]);
            }
            else
            {
               _loc29_ = uint(_loc28_[0]);
            }
            _loc31_ = 0;
            while(_loc31_ < _loc15_.length)
            {
               _loc15_[_loc31_] = GetRemapID(new DataRef(_loc15_[_loc31_]),"m_aConditions");
               _loc31_++;
            }
            _loc31_ = 0;
            while(_loc31_ < _loc16_.length)
            {
               _loc16_[_loc31_] = GetRemapID(new DataRef(_loc16_[_loc31_]),"m_aConditions");
               _loc31_++;
            }
            _loc32_ = String(param1["vLoot" + _loc3_]).split(",");
            _loc33_ = new Vector.<int>();
            _loc31_ = 0;
            while(_loc31_ < _loc32_.length)
            {
               if(_loc32_[_loc31_] != "")
               {
                  _loc33_.push(GetRemapID(new DataRef(_loc32_[_loc31_]),"m_vTreasures"));
               }
               _loc31_++;
            }
            _loc34_ = String(param1["vAccidents" + _loc3_]).split(",");
            _loc35_ = new Vector.<int>();
            _loc31_ = 0;
            while(_loc31_ < _loc34_.length)
            {
               if(_loc34_[_loc31_] != "")
               {
                  _loc35_.push(GetRemapID(new DataRef(_loc34_[_loc31_]),"m_aEncounters"));
               }
               _loc31_++;
            }
            _loc36_ = new Encounter(_loc5_,_loc6_,_loc37_,_loc7_,_loc8_,_loc11_,_loc15_,_loc16_,_loc9_,_loc10_,_loc17_,_loc19_,_loc30_,_loc29_,_loc12_,_loc13_,_loc22_,_loc23_,_loc24_,_loc25_,_loc26_,_loc35_,_loc33_,_loc27_,_loc14_);
            GetDataSet(m_strBasePrefix).m_aEncounters[_loc5_ - 1] = _loc36_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseEncounterTriggers(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         var _loc7_:int = 0;
         var _loc8_:Number = NaN;
         var _loc9_:Boolean = false;
         var _loc10_:Boolean = false;
         var _loc11_:Boolean = false;
         var _loc12_:Boolean = false;
         var _loc13_:Boolean = false;
         var _loc14_:Array = null;
         var _loc15_:Array = null;
         var _loc16_:Array = null;
         var _loc17_:Array = null;
         var _loc18_:EncounterTrigger = null;
         var _loc19_:uint = 0;
         MenuMsg("解析 enconter_trigger");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_aEncounterTriggers");
            _loc6_ = param1["strName" + _loc3_];
            _loc7_ = GetRemapID(new DataRef(param1["nEncounterID" + _loc3_]),"m_aEncounters");
            _loc8_ = Number(param1["fChance" + _loc3_]);
            _loc9_ = StrToBoolean(param1["bLocBased" + _loc3_]);
            _loc10_ = StrToBoolean(param1["bDateBased" + _loc3_]);
            _loc11_ = StrToBoolean(param1["bHexBased" + _loc3_]);
            _loc12_ = StrToBoolean(param1["bUnique" + _loc3_]);
            _loc13_ = StrToBoolean(param1["bAIPassable" + _loc3_]);
            _loc14_ = String(param1["aArea" + _loc3_]).split(",");
            _loc15_ = String(param1["dateMin" + _loc3_]).split("-");
            _loc16_ = String(param1["dateMax" + _loc3_]).split("-");
            _loc17_ = String(param1["aHexTypes" + _loc3_]).split(",");
            _loc18_ = new EncounterTrigger(_loc6_,_loc7_,_loc8_);
            _loc19_ = 0;
            while(_loc19_ < _loc17_.length)
            {
               _loc17_[_loc19_] = GetRemapID(new DataRef(_loc17_[_loc19_]),"m_aHexTypes");
               _loc19_++;
            }
            if(_loc11_)
            {
               _loc18_.HexTypes = _loc17_;
            }
            if(_loc9_)
            {
               _loc18_.SetArea(new FlxPoint(int(_loc14_[0]),int(_loc14_[1])),int(_loc14_[2]));
            }
            if(_loc10_)
            {
               _loc18_.MinDate = new Date(int(_loc15_[0]),int(_loc15_[1]),int(_loc15_[2]),int(_loc15_[3]));
               _loc18_.MaxDate = new Date(int(_loc16_[0]),int(_loc16_[1]),int(_loc16_[2]),int(_loc16_[3]));
            }
            _loc18_.m_bUnique = _loc12_;
            _loc18_.m_bAIPassable = _loc13_;
            GetDataSet(m_strBasePrefix).m_aEncounterTriggers[_loc5_ - 1] = _loc18_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseConditions(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:Boolean = false;
         var _loc9_:Number = NaN;
         var _loc10_:Boolean = false;
         var _loc11_:Boolean = false;
         var _loc12_:Boolean = false;
         var _loc13_:Boolean = false;
         var _loc14_:Boolean = false;
         var _loc15_:uint = 0;
         var _loc16_:Boolean = false;
         var _loc17_:Boolean = false;
         var _loc18_:Boolean = false;
         var _loc19_:uint = 0;
         var _loc20_:Array = null;
         var _loc21_:String = null;
         var _loc22_:Array = null;
         var _loc23_:int = 0;
         var _loc24_:Vector.<int> = null;
         var _loc25_:Array = null;
         var _loc26_:Vector.<Number> = null;
         var _loc27_:Number = NaN;
         var _loc28_:Array = null;
         var _loc29_:Array = null;
         var _loc30_:Array = null;
         var _loc31_:String = null;
         var _loc32_:Array = null;
         var _loc33_:int = 0;
         var _loc34_:PlayerCondition = null;
         var _loc35_:int = 0;
         var _loc36_:Number = NaN;
         var _loc37_:int = 0;
         MenuMsg("解析 player conditions.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_aConditions");
            _loc6_ = param1["strName" + _loc3_];
            _loc7_ = param1["strDesc" + _loc3_];
            _loc8_ = StrToBoolean(param1["bFatal" + _loc3_]);
            _loc9_ = Number(param1["fDuration" + _loc3_]);
            _loc10_ = StrToBoolean(param1["bPermanent" + _loc3_]);
            _loc11_ = StrToBoolean(param1["bStackable" + _loc3_]);
            _loc12_ = StrToBoolean(param1["bDisplay" + _loc3_]);
            _loc13_ = StrToBoolean(param1["bDisplayOther" + _loc3_]);
            _loc14_ = StrToBoolean(param1["bDisplayGameOver" + _loc3_]);
            _loc15_ = uint(param1["nColor" + _loc3_]);
            _loc16_ = StrToBoolean(param1["bReset" + _loc3_]);
            _loc17_ = StrToBoolean(param1["bRemoveAll" + _loc3_]);
            _loc18_ = StrToBoolean(param1["bRemovePostCombat" + _loc3_]);
            _loc19_ = uint(param1["nTransferRange" + _loc3_]);
            _loc20_ = String(param1["aFieldNames" + _loc3_]).split(",");
            for each(_loc21_ in _loc20_)
            {
               if(_loc21_ == "")
               {
                  _loc21_ = null;
               }
            }
            _loc22_ = String(param1["aModifiers" + _loc3_]).split(",");
            _loc23_ = 0;
            while(_loc23_ < _loc22_.length)
            {
               if((_loc35_ = int(PlayerCondition.m_aFieldNames.indexOf(_loc20_[_loc23_]))) >= 0)
               {
                  _loc22_[_loc23_] = GetRemapID(new DataRef(_loc22_[_loc23_]),PlayerCondition.m_aFieldRemaps[_loc35_][0]);
               }
               _loc36_ = Number(_loc22_[_loc23_]);
               if(isNaN(_loc36_))
               {
                  _loc36_ = 0;
               }
               _loc22_[_loc23_] = _loc36_;
               _loc23_++;
            }
            _loc24_ = new Vector.<int>();
            _loc25_ = String(param1["vIDNext" + _loc3_]).split(",");
            _loc23_ = 0;
            while(_loc23_ < _loc25_.length)
            {
               if(!(_loc25_[_loc23_] == "0" || _loc25_[_loc23_] == ""))
               {
                  _loc24_.push(GetRemapID(new DataRef(_loc25_[_loc23_]),"m_aConditions"));
               }
               _loc23_++;
            }
            _loc26_ = new Vector.<Number>();
            _loc25_ = String(param1["vChanceNext" + _loc3_]).split(",");
            for each(_loc27_ in _loc25_)
            {
               _loc26_.push(_loc27_);
            }
            _loc28_ = new Array();
            if((_loc31_ = param1["aEffects" + _loc3_]) != "")
            {
               _loc25_ = _loc31_.split(";");
               _loc23_ = 0;
               while(_loc23_ < _loc25_.length)
               {
                  if(_loc25_[_loc23_] != "")
                  {
                     _loc21_ = (_loc29_ = String(_loc25_[_loc23_]).split("="))[0];
                     _loc30_ = String(_loc29_[1]).split(",");
                     if((_loc35_ = int(PlayerCondition.m_aFieldNames.indexOf(_loc21_))) >= 0)
                     {
                        if(PlayerCondition.m_aFieldRemaps[_loc35_][1] < 0)
                        {
                           _loc37_ = 0;
                           while(_loc37_ < _loc30_.length)
                           {
                              if((_loc31_ = _loc30_[_loc37_]).indexOf(".") >= 0)
                              {
                                 _loc30_[_loc37_] = GetRemapIDString(new DataRef(_loc31_),PlayerCondition.m_aFieldRemaps[_loc35_][0]);
                              }
                              else
                              {
                                 _loc30_[_loc37_] = GetRemapID(new DataRef(_loc31_),PlayerCondition.m_aFieldRemaps[_loc35_][0]);
                              }
                              _loc37_++;
                           }
                        }
                        else if((_loc31_ = _loc30_[PlayerCondition.m_aFieldRemaps[_loc35_][1]]).indexOf(".") >= 0)
                        {
                           _loc30_[PlayerCondition.m_aFieldRemaps[_loc35_][1]] = GetRemapIDString(new DataRef(_loc31_),PlayerCondition.m_aFieldRemaps[_loc35_][0]);
                        }
                        else
                        {
                           _loc30_[PlayerCondition.m_aFieldRemaps[_loc35_][1]] = GetRemapID(new DataRef(_loc31_),PlayerCondition.m_aFieldRemaps[_loc35_][0]);
                        }
                     }
                     _loc28_.push([_loc21_,_loc30_]);
                  }
                  _loc23_++;
               }
            }
            _loc32_ = new Array();
            _loc33_ = 0;
            if((_loc31_ = param1["aThresholds" + _loc3_]) != null && _loc31_ != "")
            {
               _loc25_ = _loc31_.split(";");
               _loc23_ = 0;
               while(_loc23_ < _loc25_.length)
               {
                  if(_loc25_[_loc23_] != "")
                  {
                     _loc33_ = int((_loc29_ = String(_loc25_[_loc23_]).split("="))[0]);
                     _loc30_ = String(_loc29_[1]).split(",");
                     _loc37_ = 0;
                     while(_loc37_ < _loc30_.length)
                     {
                        _loc30_[_loc37_] = GetRemapID(new DataRef(_loc30_[_loc37_]),"m_aConditions");
                        _loc37_++;
                     }
                     _loc32_.push([_loc33_,_loc30_]);
                  }
                  _loc23_++;
               }
            }
            _loc34_ = new PlayerCondition(_loc5_,_loc6_,_loc7_,_loc20_,_loc22_,_loc28_,_loc32_,_loc8_,_loc24_,_loc9_,_loc26_,_loc10_,_loc11_,_loc12_,_loc13_,_loc14_,_loc15_,_loc16_,_loc17_,_loc18_,_loc19_);
            GetDataSet(m_strBasePrefix).m_aConditions[_loc5_ - 1] = _loc34_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseFactions(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         var _loc7_:Dictionary = null;
         var _loc8_:Array = null;
         var _loc9_:Array = null;
         var _loc10_:String = null;
         MenuMsg("解析 factions.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_dictFactions");
            _loc6_ = param1["strName" + _loc3_];
            _loc7_ = new Dictionary();
            if((_loc8_ = String(param1["dictFactions" + _loc3_]).split(","))[0] != "")
            {
               for each(_loc10_ in _loc8_)
               {
                  if((_loc9_ = _loc10_.split("="))[0] != "")
                  {
                     _loc7_[GetRemapID(new DataRef(_loc9_[0]),"m_dictFactions")] = Number(_loc9_[1]);
                  }
               }
            }
            GetDataSet(m_strBasePrefix).m_dictFactions[_loc5_] = _loc7_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseCreatures(param1:*) : void
      {
         var _loc4_:String = null;
         var _loc5_:String = null;
         var _loc6_:String = null;
         var _loc7_:int = 0;
         var _loc8_:int = 0;
         var _loc9_:Boolean = false;
         var _loc10_:int = 0;
         var _loc11_:uint = 0;
         var _loc12_:int = 0;
         var _loc13_:Array = null;
         var _loc14_:Vector.<AttackMode> = null;
         var _loc15_:Vector.<int> = null;
         var _loc16_:Vector.<Vector.<Number>> = null;
         var _loc17_:String = null;
         var _loc18_:Array = null;
         var _loc19_:Vector.<String> = null;
         var _loc20_:AICreature = null;
         var _loc21_:String = null;
         var _loc22_:String = null;
         var _loc23_:String = null;
         var _loc24_:Array = null;
         MenuMsg("解析 creatures.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = param1["strName" + _loc3_];
            _loc5_ = param1["strNamePublic" + _loc3_];
            _loc6_ = GetRemapIDString(new DataRef(param1["strImg" + _loc3_]),"m_dictIMG");
            _loc7_ = GetRemapID(new DataRef(param1["nTreasureID" + _loc3_]),"m_vTreasures");
            _loc8_ = GetRemapID(new DataRef(param1["nCorpseID" + _loc3_]),"m_vTreasures");
            _loc9_ = StrToBoolean(param1["bLooter" + _loc3_]);
            _loc10_ = GetRemapID(new DataRef(param1["nFaction" + _loc3_]),"m_dictFactions");
            _loc11_ = uint(param1["nMovesPerTurn" + _loc3_]);
            _loc12_ = GetRemapID(new DataRef(param1["id" + _loc3_]),"m_aCreatures");
            _loc13_ = String(param1["vAttackModes" + _loc3_]).split(",");
            _loc14_ = new Vector.<AttackMode>();
            if(_loc13_[0] != "")
            {
               for each(_loc21_ in _loc13_)
               {
                  _loc14_.push(GetAttackMode(GetRemapID(new DataRef(_loc21_),"m_aAttackModes")));
               }
            }
            _loc13_ = String(param1["vEncounterIDs" + _loc3_]).split(",");
            _loc15_ = new Vector.<int>();
            if(_loc13_[0] != "")
            {
               for each(_loc22_ in _loc13_)
               {
                  _loc15_.push(GetRemapID(new DataRef(_loc22_),"m_aEncounters"));
               }
            }
            _loc16_ = new Vector.<Vector.<Number>>();
            _loc17_ = param1["vBaseConditions" + _loc3_];
            if((_loc13_ = String(param1["vBaseConditions" + _loc3_]).split(","))[0] != "")
            {
               for each(_loc23_ in _loc13_)
               {
                  if((_loc24_ = _loc23_.split("="))[0] != "")
                  {
                     _loc16_.push(Vector.<Number>([GetRemapID(new DataRef(_loc24_[0]),"m_aConditions"),_loc24_[1]]));
                  }
               }
            }
            _loc18_ = String(param1["vActivities" + _loc3_]).split(",");
            _loc19_ = new Vector.<String>();
            for each(_loc21_ in _loc18_)
            {
               _loc19_.push(_loc21_);
            }
            _loc20_ = new AICreature(_loc4_,_loc5_,_loc15_,_loc6_,_loc11_,_loc7_,_loc10_,_loc14_,_loc12_,_loc16_,_loc8_,_loc19_,GetDataSet(m_strBasePrefix).m_dictFactions[_loc10_]);
            GetDataSet(m_strBasePrefix).m_aCreatures[_loc12_ - 1] = _loc20_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseCreatureSources(param1:*) : void
      {
         var _loc5_:int = 0;
         var _loc6_:int = 0;
         var _loc7_:String = null;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:int = 0;
         var _loc11_:int = 0;
         var _loc12_:int = 0;
         var _loc13_:Number = NaN;
         var _loc14_:SourceCreature = null;
         MenuMsg("创建 creature sources.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:Vector.<int> = Vector.<int>([1,2,3,4,5,6,7,8,10,11,12,13]);
         var _loc4_:int = 0;
         while(_loc4_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc5_ = int(param1["id" + _loc4_]);
            _loc6_ = GetRemapID(new DataRef("" + _loc5_),"m_vCreatureTable");
            _loc7_ = param1["strName" + _loc4_];
            _loc8_ = int(param1["nX" + _loc4_]);
            _loc9_ = int(param1["nY" + _loc4_]);
            _loc10_ = GetRemapID(new DataRef("" + param1["nCreatureID" + _loc4_]),"m_aCreatures");
            _loc11_ = int(param1["nMin" + _loc4_]);
            _loc12_ = int(param1["nMax" + _loc4_]);
            _loc13_ = Number(param1["fWeight" + _loc4_]);
            _loc14_ = new SourceCreature(_loc7_,_loc8_,_loc9_,_loc10_,_loc13_,_loc11_,_loc12_);
            GetDataSet(m_strBasePrefix).m_vCreatureTable[_loc6_ - 1] = _loc14_;
            _loc4_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseTreasureTypes(param1:*) : void
      {
         var _loc3_:DataRef = null;
         var _loc4_:Treasure = null;
         var _loc6_:int = 0;
         var _loc7_:int = 0;
         var _loc8_:String = null;
         var _loc9_:Boolean = false;
         var _loc10_:Boolean = false;
         var _loc11_:Boolean = false;
         var _loc12_:Vector.<Vector.<Treasure>> = null;
         var _loc13_:Array = null;
         var _loc14_:uint = 0;
         var _loc15_:TreasureGroup = null;
         var _loc16_:Array = null;
         var _loc17_:Vector.<Treasure> = null;
         var _loc18_:String = null;
         var _loc19_:Array = null;
         var _loc20_:Array = null;
         MenuMsg("解析 treasure types.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc5_:int = 0;
         while(_loc5_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc6_ = int(param1["id" + _loc5_]);
            _loc7_ = GetRemapID(new DataRef("" + _loc6_),"m_vTreasures");
            _loc8_ = param1["strName" + _loc5_];
            _loc9_ = StrToBoolean(param1["bNested" + _loc5_]);
            _loc10_ = StrToBoolean(param1["bSuppress" + _loc5_]);
            _loc11_ = StrToBoolean(param1["bIdentify" + _loc5_]);
            _loc12_ = new Vector.<Vector.<Treasure>>();
            _loc13_ = String(param1["aTreasures" + _loc5_]).split(",");
            _loc14_ = 0;
            while(_loc14_ < _loc13_.length)
            {
               _loc16_ = String(_loc13_[_loc14_]).split("|");
               _loc17_ = new Vector.<Treasure>();
               for each(_loc18_ in _loc16_)
               {
                  _loc19_ = _loc18_.split("x");
                  _loc20_ = String(_loc19_[2]).split("-");
                  _loc3_ = new DataRef(_loc19_[0]);
                  if(_loc3_.StrID != "")
                  {
                     _loc4_ = new Treasure(GetRemapIDString(_loc3_,"m_dictItems"),Number(_loc19_[1]),new FlxPoint(int(_loc20_[0]),int(_loc20_[1])));
                  }
                  else
                  {
                     _loc4_ = new Treasure(GetRemapID(_loc3_,"m_vTreasures").toString(),Number(_loc19_[1]),new FlxPoint(int(_loc20_[0]),int(_loc20_[1])));
                  }
                  _loc17_.push(_loc4_);
               }
               _loc12_.push(_loc17_);
               _loc14_++;
            }
            _loc15_ = new TreasureGroup(_loc8_,_loc12_,_loc9_,_loc10_,_loc11_);
            GetDataSet(m_strBasePrefix).m_vTreasures[_loc7_ - 1] = _loc15_;
            _loc5_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseItemProps(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         MenuMsg("创建 item props.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["nID" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_aItemProps");
            _loc6_ = param1["strPropertyName" + _loc3_];
            GetDataSet(m_strBasePrefix).m_aItemProps[_loc5_ - 1] = _loc6_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseContainerTypes(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         MenuMsg("解析 container types.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_aContainerTypes");
            _loc6_ = param1["strName" + _loc3_];
            GetDataSet(m_strBasePrefix).m_aContainerTypes[_loc5_ - 1] = _loc6_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseIngredients(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:String = null;
         var _loc9_:Ingredient = null;
         MenuMsg("创建 ingredients.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["nID" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_dictIngredients");
            _loc6_ = param1["strName" + _loc3_];
            _loc7_ = param1["strRequiredProps" + _loc3_];
            _loc8_ = param1["strForbidProps" + _loc3_];
            _loc9_ = new Ingredient(_loc5_,_loc6_,_loc7_,_loc8_);
            GetDataSet(m_strBasePrefix).m_dictIngredients[_loc5_] = _loc9_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseChargeProfiles(param1:*) : void
      {
         var _loc4_:ChargeProfile = null;
         var _loc5_:int = 0;
         var _loc6_:int = 0;
         MenuMsg("创建 charge profiles.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = new ChargeProfile();
            _loc5_ = int(param1["nID" + _loc3_]);
            _loc6_ = GetRemapID(new DataRef("" + _loc5_),"m_dictChargeProfiles");
            _loc4_.m_nID = _loc6_;
            _loc4_.m_strName = param1["strName" + _loc3_];
            _loc4_.m_strItemID = GetRemapIDString(new DataRef(param1["strItemID" + _loc3_]),"m_dictItems");
            _loc4_.m_fPerUse = Number(param1["fPerUse" + _loc3_]);
            _loc4_.m_fPerHour = Number(param1["fPerHour" + _loc3_]);
            _loc4_.m_fPerHourEquipped = Number(param1["fPerHourEquipped" + _loc3_]);
            _loc4_.m_fPerHex = Number(param1["fPerHex" + _loc3_]);
            _loc4_.m_bDegrade = StrToBoolean(param1["bDegrade" + _loc3_]);
            GetDataSet(m_strBasePrefix).m_dictChargeProfiles[_loc6_] = _loc4_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseGameVars(param1:*) : void
      {
         var _loc4_:String = null;
         var _loc5_:String = null;
         var _loc6_:String = null;
         MenuMsg("解析 game vars.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = param1["strName" + _loc3_];
            _loc5_ = param1["strType" + _loc3_];
            _loc6_ = param1["strValue" + _loc3_];
            switch(_loc5_)
            {
               case "int":
                  GetDataSet(m_strBasePrefix).m_dictGameVars[_loc4_] = int(_loc6_);
                  break;
               case "Number":
                  GetDataSet(m_strBasePrefix).m_dictGameVars[_loc4_] = Number(_loc6_);
                  break;
               case "Boolean":
                  GetDataSet(m_strBasePrefix).m_dictGameVars[_loc4_] = StrToBoolean(_loc6_);
                  break;
               default:
                  GetDataSet(m_strBasePrefix).m_dictGameVars[_loc4_] = _loc6_;
                  break;
            }
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseRecipes(param1:*) : void
      {
         var _loc5_:int = 0;
         var _loc6_:int = 0;
         var _loc7_:int = 0;
         var _loc8_:int = 0;
         var _loc9_:String = null;
         var _loc10_:String = null;
         var _loc11_:Number = NaN;
         var _loc12_:int = 0;
         var _loc13_:int = 0;
         var _loc14_:Boolean = false;
         var _loc15_:Boolean = false;
         var _loc16_:Boolean = false;
         var _loc17_:Boolean = false;
         var _loc18_:String = null;
         var _loc19_:String = null;
         var _loc20_:String = null;
         var _loc21_:String = null;
         var _loc22_:String = null;
         var _loc23_:Dictionary = null;
         var _loc24_:Dictionary = null;
         var _loc25_:Vector.<int> = null;
         var _loc26_:Vector.<int> = null;
         var _loc27_:Array = null;
         var _loc28_:Array = null;
         var _loc29_:uint = 0;
         var _loc30_:Recipe = null;
         var _loc31_:Item = null;
         MenuMsg("解析 recipe hints.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc5_ = int(param1["nID" + _loc3_]);
            _loc6_ = GetRemapID(new DataRef("" + _loc5_),"m_vRecipes");
            _loc7_ = GetRemapID(new DataRef(param1["nTreasureID" + _loc3_]),"m_vTreasures");
            _loc8_ = GetRemapID(new DataRef(param1["nTempTreasureID" + _loc3_]),"m_vTreasures");
            _loc9_ = param1["strName" + _loc3_];
            _loc10_ = param1["strSecretName" + _loc3_];
            _loc11_ = Number(param1["fHours" + _loc3_]);
            _loc12_ = int(param1["nReverse" + _loc3_]);
            if((_loc13_ = int(param1["nHiddenID" + _loc3_])) > 0)
            {
               _loc13_ = GetRemapID(new DataRef(param1["nHiddenID" + _loc3_]),"m_vRecipes");
            }
            _loc14_ = StrToBoolean(param1["bIdentify" + _loc3_]);
            _loc15_ = StrToBoolean(param1["bTransferComponents" + _loc3_]);
            _loc16_ = StrToBoolean(param1["bDegradeOutput" + _loc3_]);
            _loc17_ = true;
            if((_loc18_ = param1["bScrap" + _loc3_]) != null)
            {
               _loc17_ = StrToBoolean(param1["bScrap" + _loc3_]);
            }
            _loc19_ = param1["strTools" + _loc3_];
            _loc20_ = param1["strConsumed" + _loc3_];
            _loc21_ = param1["strDestroyed" + _loc3_];
            _loc22_ = param1["vAlsoTry" + _loc3_];
            _loc9_ = param1["strType" + _loc3_] + " - " + _loc9_;
            _loc23_ = new Dictionary();
            _loc24_ = new Dictionary();
            _loc25_ = new Vector.<int>();
            _loc26_ = new Vector.<int>();
            _loc27_ = _loc19_.split("+");
            _loc29_ = 0;
            while(_loc29_ < _loc27_.length)
            {
               if(_loc19_ == "")
               {
                  break;
               }
               _loc28_ = String(_loc27_[_loc29_]).split("x");
               _loc23_[GetRemapID(new DataRef(_loc28_[1]),"m_dictIngredients")] = int(_loc28_[0]);
               _loc29_++;
            }
            _loc27_ = _loc20_.split("+");
            _loc29_ = 0;
            while(_loc29_ < _loc27_.length)
            {
               if(_loc20_ == "")
               {
                  break;
               }
               _loc28_ = String(_loc27_[_loc29_]).split("x");
               _loc24_[GetRemapID(new DataRef(_loc28_[1]),"m_dictIngredients")] = int(_loc28_[0]);
               _loc29_++;
            }
            _loc27_ = _loc21_.split(",");
            _loc29_ = 0;
            while(_loc29_ < _loc27_.length)
            {
               if(_loc21_ == "")
               {
                  break;
               }
               _loc25_.push(GetRemapID(new DataRef(_loc27_[_loc29_]),"m_dictIngredients"));
               _loc29_++;
            }
            _loc27_ = _loc22_.split(",");
            _loc29_ = 0;
            while(_loc29_ < _loc27_.length)
            {
               if(_loc22_ == "")
               {
                  break;
               }
               _loc26_.push(GetRemapID(new DataRef(_loc27_[_loc29_]),"m_vRecipes"));
               _loc29_++;
            }
            _loc30_ = new Recipe(_loc6_,_loc9_,_loc10_,_loc7_,_loc11_,_loc23_,_loc24_,_loc25_,_loc12_,_loc14_,_loc15_,_loc13_,_loc26_,_loc8_,_loc16_);
            GetDataSet(m_strBasePrefix).m_vRecipes[_loc6_ - 1] = _loc30_;
            if(_loc17_)
            {
               _loc31_ = Item(GetDataSet(m_strBasePrefix).m_dictItems["9.0"]).Clone();
               _loc28_ = GetRemapIDString(new DataRef("9." + _loc6_),"m_dictItems").split(".");
               if(m_objData.m_strID == m_strBasePrefix)
               {
                  _loc28_[1] = _loc6_;
               }
               _loc31_.nSubgroupID = _loc28_[1];
               _loc31_.m_bIgnoreSubGroupWhenStacking = true;
               GetDataSet(m_strBasePrefix).m_dictItems[_loc31_.nGroupID + "." + _loc31_.nSubgroupID] = _loc31_;
               if(m_objData.m_strID != m_strBasePrefix)
               {
                  m_objData.m_objRemap.m_dictItems[_loc31_.nGroupID + "." + _loc5_] = _loc31_.nGroupID + "." + _loc31_.nSubgroupID;
               }
            }
            _loc3_++;
         }
         m_vRecipesSorted.length = 0;
         var _loc4_:Vector.<Recipe> = GetDataSet(m_strBasePrefix).m_vRecipes;
         for each(_loc30_ in _loc4_)
         {
            if(_loc30_ != null)
            {
               m_vRecipesSorted.push(_loc30_);
            }
         }
         m_vRecipesSorted = m_vRecipesSorted.sort(CompareRecipe);
         nRecipesLoaded = m_vRecipesSorted.length;
         m_bNextStep = true;
      }
      
      private static function ParseBarterHexes(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:int = 0;
         var _loc6_:Boolean = false;
         var _loc7_:int = 0;
         var _loc8_:int = 0;
         var _loc9_:int = 0;
         var _loc10_:BarterHex = null;
         MenuMsg("解析 barter hexes.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetRemapID(new DataRef("" + _loc4_),"m_vBarterHexes");
            _loc6_ = StrToBoolean(param1["bBuys" + _loc3_]);
            _loc7_ = int(param1["nX" + _loc3_]);
            _loc8_ = int(param1["nY" + _loc3_]);
            _loc9_ = GetRemapID(new DataRef(param1["nRestockTreasureID" + _loc3_]),"m_vTreasures");
            _loc10_ = new BarterHex(_loc5_,_loc6_,_loc7_,_loc8_,_loc9_);
            GetDataSet(m_strBasePrefix).m_vBarterHexes[_loc5_ - 1] = _loc10_;
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function CompareRecipe(param1:Recipe, param2:Recipe) : int
      {
         if(param1.m_nComplexity > param2.m_nComplexity)
         {
            return -1;
         }
         if(param1.m_nComplexity < param2.m_nComplexity)
         {
            return 1;
         }
         return 0;
      }
      
      public static function StrToBoolean(param1:String) : Boolean
      {
         return param1 != "0";
      }
      
      private static function ParseCampTypes(param1:*) : void
      {
         var _loc4_:int = 0;
         var _loc5_:Item = null;
         var _loc6_:Array = null;
         var _loc7_:String = null;
         var _loc8_:BitmapData = null;
         var _loc9_:Array = null;
         var _loc10_:PlayerCondition = null;
         var _loc11_:Vector.<PlayerCondition> = null;
         var _loc12_:Matrix = null;
         var _loc13_:BitmapData = null;
         var _loc14_:uint = 0;
         var _loc15_:uint = 0;
         var _loc16_:Array = null;
         MenuMsg("解析 camp types.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = int(param1["id" + _loc3_]);
            _loc5_ = GetItemDef("12.1").Clone();
            _loc6_ = [12,_loc4_];
            if(m_objData.m_strID != m_strBasePrefix)
            {
               _loc6_ = GetRemapIDString(new DataRef("12." + _loc4_),"m_dictItems").split(".");
            }
            _loc5_.nSubgroupID = _loc6_[1];
            _loc5_.strDesc = param1["strDesc" + _loc3_];
            _loc7_ = GetRemapIDString(new DataRef(param1["vImageList" + _loc3_]),"m_dictIMG");
            _loc8_ = GetImage(_loc7_);
            if(_loc5_.m_bMirrored)
            {
               (_loc12_ = new Matrix()).scale(-1,1);
               _loc12_.translate(_loc8_.width,0);
               (_loc13_ = new BitmapData(_loc8_.width,_loc8_.height,true,0)).draw(_loc8_,_loc12_);
               _loc8_ = _loc13_;
            }
            _loc5_.vImageList[0] = _loc8_;
            _loc5_.vImageListNames[0] = _loc7_;
            _loc5_.nTreasureID = GetRemapID(new DataRef(param1["nTreasureID" + _loc3_]),"m_vTreasures");
            _loc5_.aCapacities = [];
            if((_loc9_ = String(param1["aCapacities" + _loc3_]).split(","))[0] != "")
            {
               _loc14_ = uint(GUIInventorySlot.CapacityPixel);
               _loc15_ = 0;
               while(_loc15_ < _loc9_.length)
               {
                  (_loc16_ = String(_loc9_[_loc15_]).split("x"))[0] = int(_loc16_[0]) * _loc14_;
                  _loc16_[1] = int(_loc16_[1]) * _loc14_;
                  _loc5_.aCapacities.push(new FlxPoint(_loc16_[0],_loc16_[1]));
                  _loc15_++;
               }
            }
            GetDataSet(m_strBasePrefix).m_dictItems[_loc5_.nGroupID + "." + _loc5_.nSubgroupID] = _loc5_;
            if(m_objData.m_strID != m_strBasePrefix)
            {
               m_objData.m_objRemap.m_dictItems[_loc5_.nGroupID + "." + _loc4_] = _loc5_.nGroupID + "." + _loc5_.nSubgroupID;
            }
            _loc10_ = new PlayerCondition(394,"营地增益","增益来自 " + _loc5_.strDesc,new Array("m_fSleepAwareness","m_fVisibility","WetTempAdjustMod","m_fHealPerHourMod","fSleepQuality"),new Array(param1["m_fAlertness" + _loc3_],param1["m_fVisibility" + _loc3_],param1["WetTempAdjustMod" + _loc3_],param1["m_fHealPerHourMod" + _loc3_],param1["fSleepQuality" + _loc3_]),new Array(),new Array(),false,Vector.<int>([0]),0,Vector.<Number>([0]),false,false,true,false,false,0);
            _loc11_ = GetDataSet(m_strBasePrefix).m_vCampConds;
            while(_loc11_.length < _loc5_.nSubgroupID)
            {
               _loc11_.push(null);
            }
            _loc11_[_loc5_.nSubgroupID - 1] = _loc10_;
            if(m_objData.m_strID != m_strBasePrefix)
            {
               while(m_objData.m_objRemap.m_vCampConds.length < _loc4_)
               {
                  m_objData.m_objRemap.m_vCampConds.push(null);
               }
               m_objData.m_objRemap.m_vCampConds.push(_loc4_);
            }
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      private static function ParseItemTypes(param1:*) : void
      {
         var _loc3_:DataRef = null;
         var _loc5_:Item = null;
         var _loc6_:String = null;
         var _loc7_:String = null;
         var _loc8_:Array = null;
         var _loc9_:String = null;
         var _loc10_:Array = null;
         var _loc11_:Array = null;
         var _loc12_:Array = null;
         var _loc13_:Array = null;
         var _loc14_:Array = null;
         var _loc15_:Array = null;
         var _loc16_:Array = null;
         var _loc17_:int = 0;
         var _loc18_:String = null;
         var _loc19_:BitmapData = null;
         var _loc20_:Matrix = null;
         var _loc21_:BitmapData = null;
         var _loc22_:Array = null;
         var _loc23_:uint = 0;
         var _loc24_:Array = null;
         MenuMsg("解析 item types.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc4_:int = 0;
         while(_loc4_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc5_ = new Item();
            _loc6_ = param1["nGroupID" + _loc4_] + "." + param1["nSubgroupID" + _loc4_];
            _loc8_ = (_loc7_ = GetRemapIDString(new DataRef(_loc6_),"m_dictItems")).split(".");
            _loc5_.nGroupID = _loc8_[0];
            _loc5_.nSubgroupID = _loc8_[1];
            _loc5_.strName = param1["strName" + _loc4_];
            _loc5_.strDesc = param1["strDesc" + _loc4_];
            _loc5_.strDescAlt = param1["strDescAlt" + _loc4_];
            _loc5_.fWeight = param1["fWeight" + _loc4_];
            _loc5_.fMonetaryValue = param1["fMonetaryValue" + _loc4_];
            _loc5_.fMonetaryValueAlt = param1["fMonetaryValueAlt" + _loc4_];
            _loc5_.label = _loc5_.strName + " - " + _loc5_.strDesc;
            _loc5_.fDurability = param1["fDurability" + _loc4_];
            _loc5_.fDegradePerHour = param1["fDegradePerHour" + _loc4_];
            _loc5_.fDegradePerUse = param1["fDegradePerUse" + _loc4_];
            _loc5_.fEquipDegradePerHour = param1["fEquipDegradePerHour" + _loc4_];
            _loc5_.nTreasureID = GetRemapID(new DataRef(param1["nTreasureID" + _loc4_]),"m_vTreasures");
            _loc5_.m_nComponentID = GetRemapID(new DataRef(param1["nComponentID" + _loc4_]),"m_vTreasures");
            _loc5_.m_nCondID = GetRemapID(new DataRef(param1["nCondID" + _loc4_]),"m_aConditions");
            _loc5_.nFormatID = GetRemapID(new DataRef(param1["nFormatID" + _loc4_]),"m_aContainerTypes");
            _loc5_.nSlotDepth = param1["nSlotDepth" + _loc4_];
            _loc5_.m_nStackLimit = param1["nStackLimit" + _loc4_];
            _loc5_.bSocketLocked = StrToBoolean(param1["bSocketLocked" + _loc4_]);
            _loc5_.m_bMirrored = StrToBoolean(param1["bMirrored" + _loc4_]);
            if((_loc9_ = param1["vDegradeTreasureIDs" + _loc4_]) != "")
            {
               _loc8_ = _loc9_.split(",");
               _loc5_.vDegradeTreasureIDs = Vector.<int>([GetRemapID(new DataRef(_loc8_[0]),"m_vTreasures"),GetRemapID(new DataRef(_loc8_[1]),"m_vTreasures")]);
            }
            if((_loc9_ = param1["strChargeProfiles" + _loc4_]) != "")
            {
               _loc13_ = _loc9_.split(",");
               _loc17_ = 0;
               while(_loc17_ < _loc13_.length)
               {
                  _loc3_ = new DataRef(_loc13_[_loc17_]);
                  _loc5_.m_vChargeProfiles.push(GetDataSet(m_strBasePrefix).m_dictChargeProfiles[GetRemapID(_loc3_,"m_dictChargeProfiles")]);
                  _loc17_++;
               }
            }
            if((_loc9_ = param1["aEquipConditions" + _loc4_]) != "")
            {
               _loc5_.m_aEquipConditions = _loc9_.split(",");
               _loc17_ = 0;
               while(_loc17_ < _loc5_.m_aEquipConditions.length)
               {
                  _loc8_ = String(_loc5_.m_aEquipConditions[_loc17_]).split("=");
                  _loc5_.m_aEquipConditions[_loc17_] = new Array(int(_loc8_[0]),GetRemapID(new DataRef(_loc8_[1]),"m_aConditions"));
                  _loc17_++;
               }
            }
            if((_loc9_ = param1["aPossessConditions" + _loc4_]) != "")
            {
               _loc5_.m_aPossessConditions = _loc9_.split(",");
               _loc17_ = 0;
               while(_loc17_ < _loc5_.m_aPossessConditions.length)
               {
                  _loc8_ = String(_loc5_.m_aPossessConditions[_loc17_]).split("=");
                  _loc5_.m_aPossessConditions[_loc17_] = new Array(int(_loc8_[0]),GetRemapID(new DataRef(_loc8_[1]),"m_aConditions"));
                  _loc17_++;
               }
            }
            if((_loc9_ = param1["aUseConditions" + _loc4_]) != "")
            {
               _loc5_.m_aUseConditions = _loc9_.split(",");
               _loc17_ = 0;
               while(_loc17_ < _loc5_.m_aUseConditions.length)
               {
                  _loc8_ = String(_loc5_.m_aUseConditions[_loc17_]).split("=");
                  _loc5_.m_aUseConditions[_loc17_] = new Array(int(_loc8_[0]),GetRemapID(new DataRef(_loc8_[1]),"m_aConditions"));
                  _loc17_++;
               }
            }
            _loc10_ = String(param1["vImageList" + _loc4_]).split(",");
            _loc17_ = 0;
            while(_loc17_ < _loc10_.length)
            {
               _loc3_ = new DataRef(_loc10_[_loc17_]);
               _loc18_ = GetRemapIDString(_loc3_,"m_dictIMG");
               _loc19_ = GetImage(_loc18_);
               if(_loc5_.m_bMirrored)
               {
                  (_loc20_ = new Matrix()).scale(-1,1);
                  _loc20_.translate(_loc19_.width,0);
                  (_loc21_ = new BitmapData(_loc19_.width,_loc19_.height,true,0)).draw(_loc19_,_loc20_);
                  _loc19_ = _loc21_;
               }
               _loc5_.vImageList.push(_loc19_);
               _loc5_.vImageListNames.push(_loc18_);
               _loc17_++;
            }
            _loc10_ = String(param1["vSpriteList" + _loc4_]).split(",");
            _loc17_ = 0;
            while(_loc17_ < _loc10_.length)
            {
               if(_loc10_[_loc17_] == "")
               {
                  break;
               }
               _loc22_ = String(_loc10_[_loc17_]).split("=");
               _loc3_ = new DataRef(_loc22_[1]);
               _loc18_ = GetRemapIDString(_loc3_,"m_dictIMG");
               _loc19_ = GetImage(_loc18_);
               _loc5_.vSpriteList.push(_loc19_);
               _loc5_.dictSpriteUsage[int(_loc22_[0])] = _loc5_.vSpriteList.length - 1;
               _loc17_++;
            }
            _loc11_ = String(param1["vImageUsage" + _loc4_]).split(",");
            _loc5_.dictImageUsage[0] = Vector.<int>([int(_loc11_[0]),int(_loc11_[1])]);
            _loc12_ = String(param1["vEquipSlots" + _loc4_]).split(",");
            _loc17_ = 0;
            while(_loc17_ < _loc12_.length)
            {
               if((_loc22_ = String(_loc12_[_loc17_]).split("=")).length == 1)
               {
                  _loc22_ = _loc22_.concat([_loc5_.dictImageUsage[0][0],_loc5_.dictImageUsage[0][0]]);
               }
               else if(_loc22_.length == 2)
               {
                  _loc22_.push(_loc5_.dictImageUsage[0][1]);
               }
               _loc5_.vEquipSlots.push(int(_loc22_[0]));
               _loc5_.dictImageUsage[_loc5_.vEquipSlots[_loc17_]] = Vector.<int>([_loc22_[1],_loc22_[2]]);
               _loc17_++;
            }
            _loc13_ = (_loc9_ = String(param1["vProperties" + _loc4_])).split(",");
            if(_loc9_ == "")
            {
               _loc13_.length = 0;
            }
            _loc17_ = 0;
            while(_loc17_ < _loc13_.length)
            {
               _loc5_.m_vProperties.push(GetRemapID(new DataRef(_loc13_[_loc17_]),"m_aItemProps"));
               _loc17_++;
            }
            _loc5_.m_bIgnoreSubGroupWhenStacking = _loc5_.m_vProperties.indexOf(88) >= 0;
            _loc9_ = String(param1["vUseSlots" + _loc4_]);
            _loc5_.vUseSlots = Vector.<int>(_loc9_.split(","));
            if(_loc9_ == "")
            {
               _loc5_.vUseSlots.length = 0;
            }
            _loc17_ = 0;
            while(_loc17_ < _loc5_.vUseSlots.length)
            {
               _loc5_.vUseSlots[_loc17_] = int(_loc5_.vUseSlots[_loc17_]);
               _loc17_++;
            }
            if((_loc14_ = String(param1["aCapacities" + _loc4_]).split(","))[0] != "")
            {
               _loc23_ = uint(GUIInventorySlot.CapacityPixel);
               _loc17_ = 0;
               while(_loc17_ < _loc14_.length)
               {
                  (_loc24_ = String(_loc14_[_loc17_]).split("x"))[0] = int(_loc24_[0]) * _loc23_;
                  _loc24_[1] = int(_loc24_[1]) * _loc23_;
                  _loc5_.aCapacities.push(new FlxPoint(_loc24_[0],_loc24_[1]));
                  _loc17_++;
               }
            }
            if((_loc15_ = String(param1["aContentIDs" + _loc4_]).split(","))[0] != "")
            {
               _loc17_ = 0;
               while(_loc17_ < _loc15_.length)
               {
                  _loc5_.aContentIDs.push(GetRemapID(new DataRef(_loc15_[_loc17_]),"m_aContainerTypes"));
                  _loc17_++;
               }
            }
            if((_loc9_ = param1["aAttackModes" + _loc4_]) != "")
            {
               _loc5_.m_aAttackModes = _loc9_.split(",");
               _loc17_ = 0;
               while(_loc17_ < _loc5_.m_aAttackModes.length)
               {
                  _loc8_ = String(_loc5_.m_aAttackModes[_loc17_]).split("=");
                  _loc5_.m_aAttackModes[_loc17_] = new Array(int(_loc8_[0]),GetRemapID(new DataRef(_loc8_[1]),"m_aAttackModes"));
                  _loc17_++;
               }
            }
            if((_loc9_ = param1["aSwitchIDs" + _loc4_]) != "")
            {
               _loc13_ = _loc9_.split(",");
               _loc17_ = 0;
               while(_loc17_ < _loc13_.length)
               {
                  _loc8_ = String(_loc13_[_loc17_]).split("=");
                  _loc5_.m_vModeIDs.push(GetRemapIDString(new DataRef(_loc8_[1]),"m_dictItems"));
                  _loc5_.m_vModeLabels.push(_loc8_[0]);
                  _loc17_++;
               }
            }
            if((_loc16_ = String(param1["aSounds" + _loc4_]).split(","))[0] != "")
            {
               _loc17_ = 0;
               while(_loc17_ < _loc16_.length)
               {
                  _loc3_ = new DataRef(_loc16_[_loc17_],true);
                  _loc5_.m_vSounds.push(GetRemapIDString(_loc3_,"m_dictSND"));
                  _loc17_++;
               }
            }
            _loc5_.Initialize();
            GetDataSet(m_strBasePrefix).m_dictItems[_loc5_.nGroupID + "." + _loc5_.nSubgroupID] = _loc5_;
            if(m_objData.m_strID != m_strBasePrefix)
            {
               m_objData.m_objRemap.m_dictItems[_loc6_] = _loc7_;
            }
            _loc4_++;
         }
         m_bNextStep = true;
      }
      
      public static function ParseBattleMoves(param1:*) : void
      {
         var _loc4_:String = null;
         var _loc5_:String = null;
         var _loc6_:BattleMove = null;
         var _loc7_:Array = null;
         var _loc8_:int = 0;
         MenuMsg("解析 battlemoves.");
         var _loc2_:int = int(param1["nRows"]);
         m_nDebugCounter = _loc2_;
         var _loc3_:int = 0;
         while(_loc3_ < _loc2_)
         {
            m_nDebugCounter = _loc2_;
            _loc4_ = param1["strID" + _loc3_];
            _loc5_ = GetRemapIDString(new DataRef(_loc4_),"m_dictItems");
            (_loc6_ = new BattleMove(_loc5_,param1["strName" + _loc3_],param1["strSuccess" + _loc3_],Vector.<Number>(String(param1["vChanceType" + _loc3_]).split(",")))).m_strFail = param1["strFail" + _loc3_];
            _loc6_.m_strPopUp = String(param1["strPopUp" + _loc3_]).replace(/\r/gi,"");
            _loc6_.m_vUsConditions = GetNestedConditionList(param1["vUsConditions" + _loc3_]);
            _loc6_.m_vThemConditions = GetNestedConditionList(param1["vThemConditions" + _loc3_]);
            _loc6_.m_vPairConditions = GetNestedConditionList(param1["vPairConditions" + _loc3_]);
            _loc6_.m_vUsFailConditions = GetNestedConditionList(param1["vUsFailConditions" + _loc3_]);
            _loc6_.m_vThemFailConditions = GetNestedConditionList(param1["vThemFailConditions" + _loc3_]);
            _loc6_.m_vPairFailConditions = GetNestedConditionList(param1["vPairFailConditions" + _loc3_]);
            _loc7_ = String(param1["vUsPreConditions" + _loc3_]).split(",");
            _loc8_ = 0;
            while(_loc8_ < _loc7_.length)
            {
               _loc6_.m_vUsPreConditions.push(GetRemapID(new DataRef(_loc7_[_loc8_]),"m_aConditions"));
               _loc8_++;
            }
            _loc7_ = String(param1["vThemPreConditions" + _loc3_]).split(",");
            _loc8_ = 0;
            while(_loc8_ < _loc7_.length)
            {
               _loc6_.m_vThemPreConditions.push(GetRemapID(new DataRef(_loc7_[_loc8_]),"m_aConditions"));
               _loc8_++;
            }
            _loc6_.m_nSeeThem = param1["nSeeThem" + _loc3_];
            _loc6_.m_nSeeUs = param1["nSeeUs" + _loc3_];
            _loc6_.m_bAllOutOfRange = StrToBoolean(param1["bAllOutOfRange" + _loc3_]);
            _loc6_.m_bInAttackRange = StrToBoolean(param1["bInAttackRange" + _loc3_]);
            _loc6_.m_nMinCharges = param1["nMinCharges" + _loc3_];
            _loc6_.m_nMinRange = param1["nMinRange" + _loc3_];
            _loc6_.m_nMaxRange = param1["nMaxRange" + _loc3_];
            _loc6_.m_nAttackModeType = param1["nAttackModeType" + _loc3_];
            _loc7_ = String(param1["vHexTypes" + _loc3_]).split(",");
            _loc8_ = 0;
            while(_loc8_ < _loc7_.length)
            {
               _loc6_.m_vHexTypes.push(GetRemapID(new DataRef(_loc7_[_loc8_]),"m_aHexTypes"));
               _loc8_++;
            }
            _loc6_.m_fChance = param1["fChance" + _loc3_];
            _loc6_.m_fPriority = param1["fPriority" + _loc3_];
            _loc6_.m_fDetect = param1["fDetect" + _loc3_];
            _loc6_.m_fOrder = param1["fOrder" + _loc3_];
            _loc6_.m_fFatigue = param1["fFatigue" + _loc3_];
            _loc6_.m_bApproach = StrToBoolean(param1["bApproach" + _loc3_]);
            _loc6_.m_bOffense = StrToBoolean(param1["bOffense" + _loc3_]);
            _loc6_.m_bFallBack = StrToBoolean(param1["bFallBack" + _loc3_]);
            _loc6_.m_bRetreat = StrToBoolean(param1["bRetreat" + _loc3_]);
            _loc6_.m_bPosition = StrToBoolean(param1["bPosition" + _loc3_]);
            _loc6_.m_bPassive = StrToBoolean(param1["bPassive" + _loc3_]);
            GetDataSet(m_strBasePrefix).m_dictMoves[_loc6_.m_strID] = _loc6_;
            if(m_objData.m_strID != m_strBasePrefix)
            {
               m_objData.m_objRemap.m_dictMoves[_loc4_] = _loc5_;
            }
            _loc3_++;
         }
         m_bNextStep = true;
      }
      
      public static function UpdateTemplateItems() : void
      {
         var _loc2_:Item = null;
         var _loc3_:Item = null;
         MenuMsg("更新 template-based items.");
         var _loc1_:Dictionary = GetDataSet(DataHandler.m_strBasePrefix).m_dictItems;
         for each(_loc3_ in _loc1_)
         {
            if(_loc3_.nGroupID == 12)
            {
               _loc2_ = _loc1_["12.1"];
               _loc2_ = _loc2_.Clone();
               _loc2_.vImageList = _loc3_.vImageList.concat();
               _loc2_.vImageListNames = _loc3_.vImageListNames.concat();
               _loc2_.aCapacities = _loc3_.aCapacities.concat();
               _loc2_.nTreasureID = _loc3_.nTreasureID;
               _loc2_.nSubgroupID = _loc3_.nSubgroupID;
               _loc2_.strDesc = _loc3_.strDesc;
               _loc1_[_loc3_.nGroupID + "." + _loc3_.nSubgroupID] = _loc2_;
            }
            else if(_loc3_.nGroupID == 36)
            {
               _loc2_ = _loc1_["36.0"];
               _loc2_ = _loc2_.Clone();
               _loc2_.vImageList = _loc3_.vImageList.concat();
               _loc2_.vImageListNames = _loc3_.vImageListNames.concat();
               _loc2_.nSubgroupID = _loc3_.nSubgroupID;
               _loc2_.strName = _loc3_.strName;
               _loc2_.strDesc = _loc3_.strDesc;
               _loc2_.fMonetaryValue = _loc3_.fMonetaryValue;
               _loc1_[_loc3_.nGroupID + "." + _loc3_.nSubgroupID] = _loc2_;
            }
         }
         m_bNextStep = true;
      }
      
      public static function GetDataSet(param1:String) : DataSet
      {
         if(dictData[param1] != null)
         {
            return dictData[param1];
         }
         MenuMsg("错误: Mod 未找到： \"" + param1 + "\"\n");
         return null;
      }
      
      public static function GetRecipe(param1:int) : Recipe
      {
         if(GetDataSet(m_strBasePrefix).m_vRecipes.length > param1 - 1)
         {
            return GetDataSet(m_strBasePrefix).m_vRecipes[param1 - 1];
         }
         return null;
      }
      
      public static function GetEncounterTrigger(param1:int) : EncounterTrigger
      {
         return GetDataSet(m_strBasePrefix).m_aEncounterTriggers[param1];
      }
      
      public static function GetHexType(param1:int) : FlxHexTile
      {
         return GetDataSet(m_strBasePrefix).m_aHexTypes[param1];
      }
      
      public static function GetBattleMove(param1:String) : BattleMove
      {
         return GetDataSet(m_strBasePrefix).m_dictMoves[param1];
      }
      
      public static function GetBattleMovesAvail(param1:Battle, param2:CombatPair) : void
      {
         var _loc4_:BattleMove = null;
         var _loc3_:DataSet = GetDataSet(m_strBasePrefix);
         for each(_loc4_ in _loc3_.m_dictMoves)
         {
            if(_loc4_.IsAvailable(param1,param2))
            {
               param2.vAllMoves.push(_loc4_);
               if(_loc4_.m_bApproach)
               {
                  param2.vApproachMoves.push(_loc4_);
               }
               if(_loc4_.m_bFallBack)
               {
                  param2.vFallBackMoves.push(_loc4_);
               }
               if(_loc4_.m_bOffense)
               {
                  param2.vOffenseMoves.push(_loc4_);
               }
               if(_loc4_.m_bRetreat)
               {
                  param2.vRetreatMoves.push(_loc4_);
               }
               if(_loc4_.m_bPosition)
               {
                  param2.vPositionMoves.push(_loc4_);
               }
               if(_loc4_.m_bPassive)
               {
                  param2.vPassiveMoves.push(_loc4_);
               }
            }
         }
      }
      
      public static function GetIngredient(param1:int) : Ingredient
      {
         return GetDataSet(m_strBasePrefix).m_dictIngredients[param1];
      }
      
      public static function GetMap(param1:String) : String
      {
         return GetDataSet(m_strBasePrefix).m_dictMaps[param1];
      }
      
      public static function GetSound(param1:String) : Class
      {
         var _loc2_:Class = null;
         _loc2_ = GetDataSet(m_strBasePrefix).m_dictSND[param1];
         if(_loc2_ == null)
         {
            MenuMsg("错误: Unable to retrieve sound: " + param1);
            _loc2_ = GetDataSet(m_strBasePrefix).m_dictSND["cueMissing"];
         }
         return _loc2_;
      }
      
      public static function GetImage(param1:String, param2:String = "") : BitmapData
      {
         if(param1 == null)
         {
            MenuMsg("错误: Unable to retrieve image: null");
         }
         var _loc3_:Bitmap = GetDataSet(m_strBasePrefix).m_dictIMG[param2 + param1];
         if(_loc3_ == null)
         {
            MenuMsg("错误: Unable to retrieve image: " + param2 + param1);
            _loc3_ = bmpMissing;
         }
         return _loc3_.bitmapData;
      }
      
      public static function GetItem(param1:String, param2:Boolean = true) : ItemInstance
      {
         var _loc3_:Item = GetItemDef(param1);
         if(_loc3_ != null)
         {
            if(_loc3_.nGroupID == 8)
            {
               return new ItemHardware(_loc3_,0,0,param2);
            }
            if(_loc3_.nGroupID == 35 || _loc3_.nGroupID == 36)
            {
               return new ItemSoftware(_loc3_);
            }
            if(_loc3_.nGroupID == 9)
            {
               return new ItemRecipeHint(_loc3_);
            }
            if(_loc3_.nGroupID == 12)
            {
               return GetCamp(_loc3_,_loc3_.nSubgroupID);
            }
            return new ItemInstance(_loc3_,0,0,param2);
         }
         return null;
      }
      
      public static function GetItemDef(param1:String) : Item
      {
         var _loc2_:Item = null;
         var _loc3_:Array = null;
         var _loc4_:String = null;
         if(param1 != "")
         {
            _loc3_ = param1.split(".");
            if(_loc3_[0] == 7)
            {
               if(_loc3_[1] == 0)
               {
                  _loc3_[1] = Math.floor(Math.random() * GetDataSet(m_strBasePrefix).m_vHeadlines.length) + 1;
               }
               if(GetDataSet(m_strBasePrefix).m_vHeadlines.length >= _loc3_[1])
               {
                  _loc4_ = GetDataSet(m_strBasePrefix).m_vHeadlines[_loc3_[1] - 1];
                  _loc2_ = Item(GetDataSet(m_strBasePrefix).m_dictItems["7.0"]).Clone();
                  _loc2_.strDesc = _loc2_.strDesc + "\n\n" + _loc4_;
                  _loc2_.nSubgroupID = _loc3_[1];
                  _loc2_.m_bIgnoreSubGroupWhenStacking = true;
               }
            }
            else if(_loc3_[0] == 9)
            {
               if(_loc3_[1] == 0)
               {
                  _loc3_[1] = Math.floor(Math.random() * nRecipesLoaded);
               }
               if(GetDataSet(m_strBasePrefix).m_dictItems[_loc3_[0] + "." + _loc3_[1]] == null)
               {
                  _loc3_ = ["86","10"];
               }
            }
            if(_loc2_ == null)
            {
               _loc2_ = GetDataSet(m_strBasePrefix).m_dictItems[_loc3_[0] + "." + _loc3_[1]];
            }
         }
         if(_loc2_ == null)
         {
            MenuMsg("错误: Unable to retrieve item: " + param1);
            return null;
         }
         return _loc2_;
      }
      
      public static function GetCamp(param1:Item, param2:int) : ItemCamp
      {
         var _loc3_:ItemCamp = new ItemCamp(param1);
         _loc3_.Condition = GetDataSet(m_strBasePrefix).m_vCampConds[param2 - 1];
         _loc3_.Randomize();
         return _loc3_;
      }
      
      public static function GetGameVars() : Dictionary
      {
         return GetDataSet(m_strBasePrefix).m_dictGameVars;
      }
      
      public static function GetBarterHex(param1:int) : BarterHex
      {
         return GetDataSet(m_strBasePrefix).m_vBarterHexes[param1 - 1];
      }
      
      public static function GetAllBarterHexes() : Vector.<BarterHex>
      {
         return GetDataSet(m_strBasePrefix).m_vBarterHexes;
      }
      
      public static function GetTreasure(param1:int) : TreasureGroup
      {
         if(param1 < 1 || param1 > GetDataSet(m_strBasePrefix).m_vTreasures.length)
         {
            return GetDataSet(m_strBasePrefix).m_vTreasures[3 - 1];
         }
         return GetDataSet(m_strBasePrefix).m_vTreasures[param1 - 1];
      }
      
      public static function GetHeadline(param1:int) : String
      {
         if(param1 < 1 || param1 > GetDataSet(m_strBasePrefix).m_vHeadlines.length)
         {
            param1 = Math.floor(Math.random() * GetDataSet(m_strBasePrefix).m_vHeadlines.length) + 1;
         }
         return GetDataSet(m_strBasePrefix).m_vHeadlines[param1 - 1];
      }
      
      public static function GetDMCPlace(param1:int) : DMCPlace
      {
         if(param1 >= 1 && param1 <= GetDataSet(m_strBasePrefix).m_vDMCPlaces.length)
         {
            return GetDataSet(m_strBasePrefix).m_vDMCPlaces[param1 - 1];
         }
         return null;
      }
      
      public static function GetEncounter(param1:int) : Encounter
      {
         return GetDataSet(m_strBasePrefix).m_aEncounters[param1 - 1];
      }
      
      public static function GetCondition(param1:int) : PlayerCondition
      {
         var _loc2_:PlayerCondition = GetDataSet(m_strBasePrefix).m_aConditions[param1 - 1];
         if(_loc2_ == null)
         {
            _loc2_ = GetDataSet(m_strBasePrefix).m_aConditions[0];
         }
         return _loc2_.Clone();
      }
      
      public static function GetAttackMode(param1:int) : AttackMode
      {
         var _loc2_:AttackMode = GetDataSet(m_strBasePrefix).m_aAttackModes[param1 - 1];
         return new AttackMode(_loc2_.m_nID,_loc2_.m_strName,_loc2_.m_nRange,_loc2_.m_fDamageCut,_loc2_.m_fDamageBlunt,_loc2_.m_vChargeProfiles,_loc2_.m_nPenetration,_loc2_.m_nType,_loc2_.m_strSnd,_loc2_.m_bTransfer,_loc2_.m_vAttackerConditions,_loc2_.m_strIMG,_loc2_.m_fMorale);
      }
      
      public static function GetRandomRecipe() : Recipe
      {
         var _loc1_:Number = Math.random();
         var _loc2_:int = Math.floor(_loc1_ * m_vRecipesSorted.length + 1);
         return m_vRecipesSorted[_loc2_];
      }
      
      public static function GetRandomCreature(param1:FlxHexTile = null) : SourceCreature
      {
         var _loc3_:SourceCreature = null;
         var _loc4_:Number = NaN;
         var _loc5_:Array = null;
         var _loc6_:Number = NaN;
         var _loc7_:Number = NaN;
         var _loc8_:Number = NaN;
         var _loc9_:SourceCreature = null;
         var _loc2_:int = -1;
         if(param1 != null)
         {
            _loc4_ = 0;
            _loc5_ = new Array();
            _loc6_ = DM.Rand(DM.RAND_FLAT);
            _loc7_ = 1;
            _loc8_ = 0;
            for each(_loc9_ in GetDataSet(m_strBasePrefix).m_vCreatureTable)
            {
               if(!(_loc9_.x < 0 || _loc9_.y < 0))
               {
                  _loc9_.m_fTempChance = MapUtils.GetHexDistance(param1.GetHexCoords(),new FlxPoint(_loc9_.x,_loc9_.y));
                  _loc9_.m_fTempChance = _loc9_.m_fWeight / (_loc9_.m_fTempChance * _loc9_.m_fTempChance);
                  _loc4_ += _loc9_.m_fTempChance;
                  _loc5_.push(_loc9_);
               }
            }
            _loc5_ = _loc5_.sortOn("m_fTempChance",Array.DESCENDING);
            _loc7_ = 1 / _loc4_;
            for each(_loc9_ in _loc5_)
            {
               _loc8_ += _loc7_ * _loc9_.m_fTempChance;
               if(_loc6_ < _loc8_)
               {
                  return _loc9_;
               }
            }
         }
         if(dictData[m_strBasePrefix] != null)
         {
            _loc2_ = Math.floor(DM.Rand(DM.RAND_FLAT) * GetDataSet(m_strBasePrefix).m_aCreatures.length) + 1;
            if(_loc2_ == 5)
            {
               return GetRandomCreature();
            }
            return new SourceCreature("Random",0,0,_loc2_,1);
         }
         return null;
      }
      
      public static function GetCreatureSource(param1:int) : SourceCreature
      {
         if(param1 > 0 && param1 <= GetDataSet(m_strBasePrefix).m_vCreatureTable.length)
         {
            return GetDataSet(m_strBasePrefix).m_vCreatureTable[param1 - 1];
         }
         return GetDataSet(m_strBasePrefix).m_vCreatureTable[0];
      }
      
      public static function GetCreature(param1:int) : AICreature
      {
         if(param1 < 1 || param1 > GetDataSet(m_strBasePrefix).m_aCreatures.length)
         {
            return null;
         }
         var _loc2_:AICreature = GetDataSet(m_strBasePrefix).m_aCreatures[param1 - 1];
         var _loc3_:AICreature = new AICreature(_loc2_.m_strNamePrivate,_loc2_.m_strNamePublic,_loc2_.m_vEncounterIDs,_loc2_.m_strImage,_loc2_.m_nMovesPerTurn,_loc2_.m_nTreasureID,_loc2_.m_nFaction,_loc2_.m_vBaseAttackModes,_loc2_.m_nID,_loc2_.m_vBaseConditions,_loc2_.m_nCorpseID,_loc2_.m_vActivities,GetDataSet(m_strBasePrefix).m_dictFactions[_loc2_.m_nFaction]);
         _loc3_.Initialize();
         return _loc3_;
      }
      
      public static function GetNestedConditionList(param1:String) : Vector.<Vector.<Number>>
      {
         var _loc4_:Array = null;
         var _loc5_:DataRef = null;
         var _loc6_:String = null;
         var _loc7_:int = 0;
         var _loc2_:Vector.<Vector.<Number>> = new Vector.<Vector.<Number>>();
         var _loc3_:Array = param1.split("],[");
         for each(_loc6_ in _loc3_)
         {
            if(_loc6_ != "")
            {
               _loc4_ = (_loc6_ = (_loc6_ = _loc6_.replace(/\[/gi,"")).replace(/]/gi,"")).split(",");
               _loc7_ = 0;
               while(_loc7_ < _loc4_.length)
               {
                  if(_loc7_ == 0)
                  {
                     _loc5_ = new DataRef(_loc4_[_loc7_]);
                     _loc4_[_loc7_] = GetRemapID(_loc5_,"m_aConditions");
                  }
                  else
                  {
                     _loc4_[_loc7_] = Number(_loc4_[_loc7_]);
                  }
                  _loc7_++;
               }
               _loc2_.push(Vector.<Number>(_loc4_));
            }
         }
         return _loc2_;
      }
      
      public static function IsHexForbidden(param1:String) : Boolean
      {
         var _loc2_:int = int(m_dictForbiddenHexes[param1]);
         return m_dictForbiddenHexes[param1] != undefined && _loc2_ > 0;
      }
      
      public static function IsEncounterOriginal(param1:Encounter) : Boolean
      {
         if(GetDataSet(m_strBasePrefix).m_aEncounters.indexOf(param1) >= 0)
         {
            return true;
         }
         return false;
      }
      
      public static function WriteMap(param1:String, param2:FlxHexmap, param3:uint) : void
      {
         var _loc11_:Array = null;
         var _loc4_:URLRequest = new URLRequest(strServerURL + §_a_-_---§.§_a_--_--§(-1820302813));
         var _loc5_:URLVariables = new URLVariables();
         var _loc6_:uint = param2.widthInTiles;
         var _loc7_:uint = param2.heightInTiles;
         var _loc8_:Array = param2.getData();
         var _loc9_:* = "";
         var _loc10_:uint = 0;
         while(_loc10_ < _loc7_)
         {
            _loc11_ = _loc8_.slice(_loc10_ * _loc6_,(_loc10_ + 1) * _loc6_);
            _loc9_ += _loc11_.join();
            if(_loc10_ < _loc7_ - 1)
            {
               _loc9_ += "\n";
            }
            _loc10_++;
         }
         _loc5_.nID = param3;
         _loc5_.strName = param1;
         _loc5_.strDef = _loc9_;
         _loc4_.data = _loc5_;
         LoadData(_loc4_,null);
      }
      
      public static function AddEncounterTrigger(param1:EncounterTrigger) : void
      {
         var _loc2_:Vector.<FlxHexTile> = null;
         var _loc3_:String = null;
         var _loc4_:FlxHexTile = null;
         if(param1 == null)
         {
            return;
         }
         m_aEncounterTriggersRemaining.push(param1);
         if(param1.m_bLocBased && param1.m_bAIPassable == false)
         {
            _loc2_ = MapUtils.GetHexRing(param1.Location,param1.Radius);
            for each(_loc4_ in _loc2_)
            {
               if(_loc4_ != null)
               {
                  _loc3_ = _loc4_.GetHexCoords().x + "," + _loc4_.GetHexCoords().y;
                  if(m_dictForbiddenHexes[_loc3_] == undefined)
                  {
                     m_dictForbiddenHexes[_loc3_] = 1;
                  }
                  else
                  {
                     ++m_dictForbiddenHexes[_loc3_];
                  }
               }
            }
         }
      }
      
      public static function RemoveEncounterTrigger(param1:EncounterTrigger) : void
      {
         var _loc3_:Vector.<FlxHexTile> = null;
         var _loc4_:String = null;
         var _loc5_:FlxHexTile = null;
         var _loc2_:int = int(m_aEncounterTriggersRemaining.indexOf(param1));
         m_aEncounterTriggersRemaining.splice(_loc2_,1);
         if(param1.m_bLocBased && param1.m_bAIPassable == false)
         {
            _loc3_ = MapUtils.GetHexRing(param1.Location,param1.Radius);
            for each(_loc5_ in _loc3_)
            {
               if(_loc5_ != null)
               {
                  _loc4_ = _loc5_.GetHexCoords().x + "," + _loc5_.GetHexCoords().y;
                  if(m_dictForbiddenHexes[_loc4_] != undefined)
                  {
                     --m_dictForbiddenHexes[_loc4_];
                  }
               }
            }
         }
      }
      
      public static function ResetEncounterTriggers() : void
      {
         var _loc2_:Object = null;
         m_aEncounterTriggersRemaining = new Array();
         m_dictForbiddenHexes = new Dictionary();
         var _loc1_:Dictionary = GetDataSet(m_strBasePrefix).m_dictForbiddenHexes;
         for(_loc2_ in _loc1_)
         {
            m_dictForbiddenHexes[_loc2_] = _loc1_[_loc2_];
         }
      }
      
      public static function get EncounterTriggersRemaining() : Array
      {
         return m_aEncounterTriggersRemaining;
      }
      
      public static function RandomizeEncounters() : void
      {
         var _loc4_:FlxPoint = null;
         var _loc5_:FlxHexTile = null;
         var _loc6_:EncounterTrigger = null;
         var _loc7_:Vector.<int> = null;
         var _loc8_:int = 0;
         var _loc9_:PlayerCondition = null;
         var _loc10_:int = 0;
         var _loc1_:Array = MapUtils.tmapHexes.getTiles();
         var _loc2_:Vector.<FlxHexTile> = new Vector.<FlxHexTile>();
         var _loc3_:Vector.<int> = Vector.<int>([10,12,16]);
         for each(_loc5_ in _loc1_)
         {
            if(!((_loc4_ = _loc5_.GetHexCoords()).x > 50 && _loc4_.y > 180))
            {
               if(_loc3_.indexOf(_loc5_.index + 1) >= 0 && _loc5_ != PlayState.m_objInstance.tilCurrentHex)
               {
                  _loc2_.push(_loc5_);
               }
            }
         }
         for each(_loc6_ in m_aEncounterTriggersRemaining)
         {
            if(_loc6_.m_strName.indexOf("me_") >= 0)
            {
               _loc10_ = Math.floor(Math.random() * _loc2_.length);
               _loc5_ = _loc2_[_loc10_];
               _loc6_.SetArea(_loc5_.GetHexCoords(),0);
               _loc2_.splice(_loc10_,1);
            }
         }
         _loc7_ = Vector.<int>([337,373]);
         for each(_loc8_ in _loc7_)
         {
            if(PlayState.m_objInstance.sprPlayer.HasCondition(_loc8_))
            {
               return;
            }
         }
         _loc10_ = Math.floor(Math.random() * _loc7_.length);
         _loc9_ = GetCondition(_loc7_[_loc10_]);
         PlayState.m_objInstance.sprPlayer.AddCondition(_loc9_,false,false);
      }
      
      public static function SaveGame(param1:Function) : Boolean
      {
         var _loc4_:GUIInventorySlot = null;
         var _loc5_:EncounterTrigger = null;
         var _loc6_:Creature = null;
         var _loc7_:Vector.<int> = null;
         var _loc8_:Recipe = null;
         var _loc9_:Array = null;
         var _loc10_:FlxHexTile = null;
         var _loc11_:FlxText = null;
         var _loc12_:String = null;
         var _loc13_:ItemInstance = null;
         var _loc14_:Vector.<ItemInstance> = null;
         var _loc15_:ItemInstance = null;
         var _loc16_:SaveGameCreature = null;
         var _loc17_:SaveGameHex = null;
         var _loc18_:uint = 0;
         if(m_objSG == null)
         {
            m_objSG = new FlxSave();
         }
         if(!m_objSG.bind(m_strSaveGameBind))
         {
            return false;
         }
         var _loc2_:Boolean = PlayState.m_objInstance.grpMsg.m_bIgnoreMessages;
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = true;
         var _loc3_:PlayState = PlayState.m_objInstance;
         m_objSG.data.objSG = new SaveGameData();
         _loc3_.grpInventoryUI.ClearCrafting();
         m_objSG.data.objSG.m_fHours = (_loc3_.objDate.getTime() - _loc3_.objStartDate.getTime()) / 60 / 60 / 1000;
         m_objSG.data.objSG.m_bScavTut = _loc3_.bScavTut;
         if(_loc3_.sprPlayer.m_strVersion == null)
         {
            m_objSG.data.objSG.m_strVersion = m_strVersion;
         }
         else
         {
            m_objSG.data.objSG.m_strVersion = _loc3_.sprPlayer.m_strVersion;
         }
         m_objSG.data.objSG.m_strCharname = _loc3_.sprPlayer.m_strNamePrivate;
         m_objSG.data.objSG.m_strUsername = "username";
         m_objSG.data.objSG.m_bAlive = _loc3_.sprPlayer.Alive;
         m_objSG.data.objSG.m_fMoney = _loc3_.sprPlayer.Money;
         m_objSG.data.objSG.m_objPlayer = _loc3_.sprPlayer.SaveData;
         m_objSG.data.objSG.m_objPlayer.m_nID = -1;
         m_objSG.data.objSG.m_ptHex = new FlxPoint(_loc3_.tilCurrentHex.GetHexCoords().x,_loc3_.tilCurrentHex.GetHexCoords().y);
         for each(_loc4_ in _loc3_.grpInventoryUI.vSaveSlots)
         {
            _loc14_ = _loc4_.GetAllSocketedItems();
            for each(_loc15_ in _loc14_)
            {
               if(_loc15_ != null)
               {
                  m_objSG.data.objSG.m_vNonInventoryItems.push(_loc15_.SaveData);
               }
            }
         }
         m_objSG.data.objSG.m_vEventQueue = Vector.<int>(DM.m_aEventQueue.concat());
         if(_loc3_.grpInventoryUI.objEncounter != null)
         {
            m_objSG.data.objSG.m_vEventQueue.push(_loc3_.grpInventoryUI.objEncounter.m_nID - 1);
         }
         for each(_loc5_ in m_aEncounterTriggersRemaining)
         {
            m_objSG.data.objSG.m_vEncounterTriggersRemaining.push(GetDataSet(m_strBasePrefix).m_aEncounterTriggers.indexOf(_loc5_));
         }
         for each(_loc6_ in _loc3_.m_aCreatures)
         {
            if(_loc6_.Alive != false)
            {
               (_loc16_ = _loc6_.SaveData).m_nID = AICreature(_loc6_).m_nID;
               m_objSG.data.objSG.m_vCreatures.push(_loc16_);
            }
         }
         _loc7_ = new Vector.<int>();
         for each(_loc8_ in _loc3_.sprPlayer.m_vKnownRecipes)
         {
            m_objSG.data.objSG.m_vKnownRecipes.push(_loc8_.m_nID);
         }
         _loc9_ = MapUtils.tmapHexes.getTiles();
         for each(_loc10_ in _loc9_)
         {
            if(!(_loc10_.nExploredState == 2 && _loc10_.m_vOccupants.length <= 0))
            {
               (_loc17_ = _loc10_.SaveData).m_nMapIndex = _loc9_.indexOf(_loc10_);
               m_objSG.data.objSG.m_vVisibleHexes.push(_loc17_);
               _loc18_ = 0;
               while(_loc18_ < _loc10_.m_vOccupants.length)
               {
                  _loc17_.m_vOccupantIndices[_loc18_] == _loc3_.m_aCreatures.indexOf(_loc10_.m_vOccupants[_loc18_]);
                  _loc18_++;
               }
               if(_loc17_.m_fScent > 0)
               {
                  if(_loc10_.m_objScentOwner == _loc3_.sprPlayer)
                  {
                     _loc17_.m_nScentOwnerIndex = -2;
                  }
                  else
                  {
                     _loc17_.m_nScentOwnerIndex = _loc3_.m_aCreatures.indexOf(_loc10_.m_objScentOwner);
                  }
               }
            }
         }
         _loc11_ = null;
         for(_loc12_ in _loc3_.grpMinimap.m_dictLabels)
         {
            if((_loc11_ = _loc3_.grpMinimap.m_dictLabels[_loc12_]).alive)
            {
               m_objSG.data.objSG.m_dictMinimapLabels[_loc12_] = _loc11_.text;
            }
            else
            {
               m_objSG.data.objSG.m_dictMapLabels[_loc12_] = _loc11_.text;
            }
         }
         param1(m_objSG.close(100000));
         _loc13_ = _loc3_.sprPlayer.grpCampSlot.UnSocketItem(true);
         _loc3_.sprPlayer.grpCampSlot.SocketItem(_loc13_);
         PlayState.m_objInstance.grpMsg.m_bIgnoreMessages = _loc2_;
         return true;
      }
      
      public static function LoadGame() : Boolean
      {
         if(m_objSG == null)
         {
            m_objSG = new FlxSave();
         }
         var _loc1_:Boolean = m_objSG.bind(m_strSaveGameBind);
         if(!_loc1_ || m_objSG.data.objSG == null)
         {
            return false;
         }
         var _loc2_:PlayState = new PlayState();
         _loc2_.m_objSG = new SaveGameData(m_objSG.data.objSG);
         FlxG.switchState(_loc2_);
         m_objSG.close();
         return true;
      }
      
      public static function SavePrefs() : void
      {
         if(m_objPrefs == null)
         {
            m_objPrefs = new FlxSave();
         }
         if(!m_objPrefs.bind(m_strPrefsBind))
         {
            return;
         }
         m_objPrefs.data.bScavTut = PlayState.m_objInstance.bScavTut;
         m_objPrefs.close(100000);
      }
      
      public static function GetPref(param1:String) : Object
      {
         if(m_objPrefs == null || m_objPrefs.data == null)
         {
            m_objPrefs = new FlxSave();
            if(!m_objPrefs.bind(m_strPrefsBind))
            {
               return null;
            }
         }
         if(m_objPrefs.data.hasOwnProperty(param1))
         {
            return m_objPrefs.data[param1];
         }
         return null;
      }
      
      public static function DeleteSave() : void
      {
         if(m_objSG == null)
         {
            m_objSG = new FlxSave();
         }
         var _loc1_:Boolean = m_objSG.bind(m_strSaveGameBind);
         if(!_loc1_ || m_objSG.data.objSG == null)
         {
            return;
         }
         m_objSG.erase();
         m_objSG.destroy();
      }
      
      public static function CheckOwnList() : void
      {
         var _loc1_:Array = stage.root.loaderInfo.url.split("://");
         _loc1_ = String(_loc1_[1]).split("/");
         var _loc2_:String = _loc1_[0];
         var _loc3_:Array = String(§_a_-_---§.§_a_--_--§(-1820302815)).split(",");
         var _loc4_:int = 0;
         while(_loc4_ < _loc3_.length)
         {
            if(_loc2_.indexOf(_loc3_[_loc4_]) >= 0)
            {
               ThrowUp();
            }
            _loc4_++;
         }
      }
      
      public static function ParseDomains(param1:*) : void
      {
         var _loc5_:String = null;
         var _loc6_:String = null;
         var _loc2_:Array = stage.root.loaderInfo.url.split("://");
         _loc2_ = String(_loc2_[1]).split("/");
         var _loc3_:String = _loc2_[0];
         var _loc4_:int = 0;
         for(_loc5_ in param1)
         {
            _loc6_ = param1[_loc5_];
            if(_loc3_.indexOf(_loc6_) >= 0)
            {
               ThrowUp();
            }
            _loc4_++;
         }
      }
      
      private static function goToMyURL(param1:MouseEvent = null) : void
      {
         navigateToURL(new URLRequest(strProductURL));
      }
      
      public static function ThrowUp() : void
      {
         var _loc1_:Bitmap = new Bitmap(new BitmapData(stage.stageWidth,stage.stageHeight,true,4294967295));
         stage.addChild(_loc1_);
         var _loc2_:TextFormat = new TextFormat();
         _loc2_.color = 0;
         _loc2_.size = 16;
         _loc2_.align = "center";
         _loc2_.bold = true;
         _loc2_.font = "system";
         var _loc3_:TextField = new TextField();
         _loc3_.width = _loc1_.width - 16;
         _loc3_.height = _loc1_.height - 16;
         _loc3_.y = 8;
         _loc3_.multiline = true;
         _loc3_.wordWrap = true;
         _loc3_.embedFonts = true;
         _loc3_.defaultTextFormat = _loc2_;
         _loc3_.text = §_a_-_---§.§_a_--_--§(-1820302804) + strProductURL + "\n\n 去的网站上玩游戏。谢谢，玩得开心！";
         stage.addChild(_loc3_);
         _loc3_.addEventListener(MouseEvent.CLICK,goToMyURL);
         _loc1_.addEventListener(MouseEvent.CLICK,goToMyURL);
      }
      
      public static function SetRes() : void
      {
         var _loc2_:DataSet = null;
         var _loc3_:Item = null;
         var _loc4_:BattleMove = null;
         var _loc1_:int = GUIValues.GetInt("Item.zoom");
         for each(_loc2_ in dictData)
         {
            for each(_loc3_ in _loc2_.m_dictItems)
            {
               _loc3_.SetRes(_loc1_);
            }
            for each(_loc4_ in _loc2_.m_dictMoves)
            {
               _loc4_.ItemRef.SetRes(_loc1_);
            }
         }
      }
      
      public static function addEventListener(param1:String, param2:Function, param3:Boolean = false, param4:int = 0, param5:Boolean = false) : void
      {
         objEvtDispatcher.addEventListener(param1,param2,param3,param4,param5);
      }
      
      public static function dispatchEvent(param1:Event) : Boolean
      {
         return objEvtDispatcher.dispatchEvent(param1);
      }
      
      public static function removeEventListener(param1:String, param2:Function, param3:Boolean = false) : void
      {
         objEvtDispatcher.removeEventListener(param1,param2,param3);
      }
      
      public static function hasEventListener(param1:String) : Boolean
      {
         return objEvtDispatcher.hasEventListener(param1);
      }
      
      public static function willTrigger(param1:String) : Boolean
      {
         return objEvtDispatcher.willTrigger(param1);
      }
   }
}

package
{
   import flash.display.Bitmap;
   import flash.net.URLRequest;
   import flash.utils.Dictionary;
   
   public class DataSet
   {
       
      
      public var m_strID:String;
      
      public var m_aEncounters:Array;
      
      public var m_aEncounterTriggers:Array;
      
      public var m_aConditions:Array;
      
      public var m_aCreatures:Array;
      
      public var m_aHexTypes:Array;
      
      public var m_vRecipes:Vector.<Recipe>;
      
      public var m_vCampConds:Vector.<PlayerCondition>;
      
      public var m_vBarterHexes:Vector.<BarterHex>;
      
      public var m_vHeadlines:Vector.<String>;
      
      public var m_vTreasures:Vector.<TreasureGroup>;
      
      public var m_vCreatureTable:Vector.<SourceCreature>;
      
      public var m_vDMCPlaces:Vector.<DMCPlace>;
      
      public var m_aAttackModes:Array;
      
      public var m_aItemProps:Array;
      
      public var m_aContainerTypes:Array;
      
      public var m_dictFactions:Dictionary;
      
      public var m_dictIMG:Dictionary;
      
      public var m_dictItems:Dictionary;
      
      public var m_dictMaps:Dictionary;
      
      public var m_dictSND:Dictionary;
      
      public var m_dictIngredients:Dictionary;
      
      public var m_dictChargeProfiles:Dictionary;
      
      public var m_dictMoves:Dictionary;
      
      public var m_dictForbiddenHexes:Dictionary;
      
      public var m_dictGameVars:Dictionary;
      
      public var nDictIMGLength:int;
      
      public var nDictIMGLoaded:int;
      
      public var nBatchIMGLoaded:int;
      
      public var nImgBatchNext:int;
      
      public var nImgBatchCurrentMax:int;
      
      public var vImageBatches:Vector.<Vector.<String>>;
      
      public var m_objRemap:DataRemap;
      
      public function DataSet(param1:String)
      {
         super();
         this.m_strID = param1;
         this.m_dictIMG = new Dictionary();
         this.m_dictSND = new Dictionary();
         this.m_dictItems = new Dictionary();
         this.m_dictMaps = new Dictionary();
         this.m_dictMoves = new Dictionary();
         this.m_dictIngredients = new Dictionary();
         this.m_dictChargeProfiles = new Dictionary();
         this.m_dictForbiddenHexes = new Dictionary();
         this.m_dictGameVars = new Dictionary();
         this.m_aAttackModes = new Array();
         this.m_vTreasures = new Vector.<TreasureGroup>();
         this.m_vCreatureTable = new Vector.<SourceCreature>();
         this.m_vDMCPlaces = new Vector.<DMCPlace>();
         this.m_aHexTypes = new Array();
         this.m_aEncounters = new Array();
         this.m_aEncounterTriggers = new Array();
         this.m_aConditions = new Array();
         this.m_aCreatures = new Array();
         this.m_dictFactions = new Dictionary();
         this.m_vHeadlines = new Vector.<String>();
         this.m_vRecipes = new Vector.<Recipe>();
         this.m_vCampConds = new Vector.<PlayerCondition>();
         this.m_vBarterHexes = new Vector.<BarterHex>();
         this.m_aItemProps = new Array();
         this.m_aContainerTypes = new Array();
         this.ResetIMGCounters();
         this.m_objRemap = new DataRemap();
      }
      
      public function ResetIMGCounters() : void
      {
         this.nDictIMGLength = 0;
         this.nDictIMGLoaded = 0;
         this.nImgBatchNext = 0;
         this.nImgBatchCurrentMax = 0;
         this.nBatchIMGLoaded = 0;
         this.vImageBatches = new Vector.<Vector.<String>>();
      }
      
      public function ReInitialize() : void
      {
         var _loc1_:EncounterTrigger = null;
         var _loc2_:BattleMove = null;
         for each(_loc1_ in this.m_aEncounterTriggers)
         {
            DataHandler.AddEncounterTrigger(_loc1_);
         }
         for each(_loc2_ in this.m_dictMoves)
         {
            _loc2_.ItemRef = null;
         }
      }
      
      public function LoadImages(param1:String, param2:Function) : void
      {
         var _loc5_:ResourceLoader = null;
         if(this.nImgBatchNext >= this.vImageBatches.length)
         {
            param2(null,"");
            return;
         }
         var _loc3_:Vector.<String> = this.vImageBatches[this.nImgBatchNext];
         this.nImgBatchCurrentMax = _loc3_.length;
         var _loc4_:int = 0;
         while(_loc4_ < _loc3_.length)
         {
            (_loc5_ = new ResourceLoader(param2)).load(new URLRequest(param1 + _loc3_[_loc4_]));
            _loc4_++;
         }
         ++this.nImgBatchNext;
      }
      
      public function StoreImages(param1:Bitmap, param2:String) : void
      {
         var _loc3_:String = param2.substring(param2.lastIndexOf("/") + 1);
         if(param1 == null)
         {
            delete this.m_dictIMG[_loc3_];
         }
         else
         {
            this.m_dictIMG[_loc3_] = param1;
            if(this.m_strID != "0")
            {
               DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_dictIMG[_loc3_ + this.m_strID] = param1;
               this.m_objRemap.m_dictIMG[_loc3_] = _loc3_ + this.m_strID;
            }
         }
         ++this.nDictIMGLoaded;
         ++this.nBatchIMGLoaded;
      }
   }
}

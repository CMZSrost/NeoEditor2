package
{
   import flash.utils.Dictionary;
   
   public class DataRemap
   {
       
      
      public var m_aEncounters:Array;
      
      public var m_aEncounterTriggers:Array;
      
      public var m_aConditions:Array;
      
      public var m_aCreatures:Array;
      
      public var m_aHexTypes:Array;
      
      public var m_vRecipes:Array;
      
      public var m_vCampConds:Array;
      
      public var m_vBarterHexes:Array;
      
      public var m_vHeadlines:Array;
      
      public var m_vDMCPlaces:Array;
      
      public var m_vTreasures:Array;
      
      public var m_vCreatureTable:Array;
      
      public var m_aAttackModes:Array;
      
      public var m_aItemProps:Array;
      
      public var m_aContainerTypes:Array;
      
      public var m_dictFactions:Dictionary;
      
      public var m_dictIMG:Dictionary;
      
      public var m_dictItems:Dictionary;
      
      public var m_dictMaps:Dictionary;
      
      public var m_dictSND:Dictionary;
      
      public var m_dictMoves:Dictionary;
      
      public var m_dictIngredients:Dictionary;
      
      public var m_dictChargeProfiles:Dictionary;
      
      public function DataRemap()
      {
         super();
         this.Initialize();
      }
      
      public function Initialize() : void
      {
         this.m_vRecipes = new Array();
         this.m_vCampConds = new Array();
         this.m_vBarterHexes = new Array();
         this.m_aEncounters = new Array();
         this.m_aEncounterTriggers = new Array();
         this.m_aConditions = new Array();
         this.m_aCreatures = new Array();
         this.m_vHeadlines = new Array();
         this.m_vDMCPlaces = new Array();
         this.m_aHexTypes = new Array();
         this.m_aAttackModes = new Array();
         this.m_vTreasures = new Array();
         this.m_vCreatureTable = new Array();
         this.m_aItemProps = new Array();
         this.m_aContainerTypes = new Array();
         this.m_dictFactions = new Dictionary();
         this.m_dictIMG = new Dictionary();
         this.m_dictItems = new Dictionary();
         this.m_dictMaps = new Dictionary();
         this.m_dictSND = new Dictionary();
         this.m_dictMoves = new Dictionary();
         this.m_dictIngredients = new Dictionary();
         this.m_dictChargeProfiles = new Dictionary();
      }
   }
}

package
{
   public class SaveGameHex
   {
       
      
      public var m_nMapIndex:int;
      
      public var m_nExploredState:int;
      
      public var m_nIndex:int;
      
      public var m_vOccupantIndices:Vector.<int>;
      
      public var m_fScent:Number;
      
      public var m_nScentOwnerIndex:int;
      
      public var m_vItems:Vector.<SaveGameItem>;
      
      public var m_vScavengeItems:Vector.<SaveGameItem>;
      
      public var m_vCampItems:Vector.<SaveGameItem>;
      
      public var m_nCampItems:uint;
      
      public var m_fDate:Number = -1;
      
      public var m_bCampTile:Boolean;
      
      public function SaveGameHex(param1:Object = null)
      {
         var _loc2_:int = 0;
         var _loc3_:Object = null;
         super();
         this.m_vItems = new Vector.<SaveGameItem>();
         this.m_vScavengeItems = new Vector.<SaveGameItem>();
         this.m_vCampItems = new Vector.<SaveGameItem>();
         this.m_vOccupantIndices = new Vector.<int>();
         this.m_nExploredState = 0;
         if(param1 != null)
         {
            this.m_nMapIndex = param1.m_nMapIndex;
            this.m_nIndex = param1.m_nIndex;
            this.m_fScent = param1.m_fScent;
            this.m_nScentOwnerIndex = param1.m_nScentOwnerIndex;
            this.m_nCampItems = param1.m_nCampItems;
            this.m_fDate = param1.m_fDate;
            this.m_bCampTile = param1.m_bCampTile;
            this.m_nExploredState = param1.m_nExploredState;
            for each(_loc2_ in param1.m_vOccupantIndices)
            {
               this.m_vOccupantIndices.push(_loc2_);
            }
            for each(_loc3_ in param1.m_vItems)
            {
               this.m_vItems.push(new SaveGameItem(_loc3_));
            }
            for each(_loc3_ in param1.m_vScavengeItems)
            {
               this.m_vScavengeItems.push(new SaveGameItem(_loc3_));
            }
            for each(_loc3_ in param1.m_vCampItems)
            {
               this.m_vCampItems.push(new SaveGameItem(_loc3_));
            }
         }
      }
      
      public function destroy() : void
      {
         var _loc1_:int = 0;
         this.m_vOccupantIndices = null;
         if(this.m_vItems != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vItems.length)
            {
               this.m_vItems[_loc1_].destroy();
               this.m_vItems[_loc1_] = null;
               _loc1_++;
            }
            this.m_vItems = null;
         }
         if(this.m_vScavengeItems != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vScavengeItems.length)
            {
               this.m_vScavengeItems[_loc1_].destroy();
               this.m_vScavengeItems[_loc1_] = null;
               _loc1_++;
            }
            this.m_vScavengeItems = null;
         }
         if(this.m_vCampItems != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.m_vCampItems.length)
            {
               this.m_vCampItems[_loc1_].destroy();
               this.m_vCampItems[_loc1_] = null;
               _loc1_++;
            }
            this.m_vCampItems = null;
         }
      }
   }
}

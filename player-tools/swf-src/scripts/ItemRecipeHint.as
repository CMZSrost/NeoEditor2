package
{
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class ItemRecipeHint extends ItemInstance
   {
       
      
      public var m_objRecipe:Recipe;
      
      public function ItemRecipeHint(param1:Item, param2:int = 0, param3:int = 0)
      {
         var _loc6_:Number = NaN;
         super(param1,param2,param3);
         var _loc4_:int = int(param1.nSubgroupID);
         var _loc5_:int = 10;
         while(this.m_objRecipe == null && _loc5_ > 0)
         {
            _loc6_ = Math.random();
            if(_loc4_ == 0)
            {
               _loc4_ = Math.floor(_loc6_ * DataHandler.nRecipesLoaded);
            }
            if(_loc4_ == 0)
            {
               _loc4_++;
            }
            this.m_objRecipe = DataHandler.GetDataSet(DataHandler.m_strBasePrefix).m_vRecipes[_loc4_ - 1];
            _loc5_--;
            if(this.m_objRecipe == null)
            {
            }
         }
         if(this.m_objRecipe == null)
         {
            this.m_objRecipe = new Recipe(_loc4_,"Missing Recipe","",3,0,new Dictionary(),new Dictionary(),new Vector.<int>(),0,false,false,0,new Vector.<int>(),3,false);
         }
         var _loc7_:Boolean = this.m_objRecipe.m_bKnown;
         this.m_objRecipe.m_bKnown = true;
         strDesc += "\n\n" + this.m_objRecipe.m_strName + "\n\n" + this.m_objRecipe.RecipeText;
         this.m_objRecipe.m_bKnown = _loc7_;
      }
      
      override public function UpdatePossessConditions(param1:GUIInventorySlot, param2:ItemInstance, param3:Boolean = false, param4:Boolean = false) : void
      {
         super.UpdatePossessConditions(param1,param2,param3,param4);
         if(param1 != null && param1.m_sprOwner == PlayState.m_objInstance.sprPlayer && param1.m_bCarried)
         {
            if(!this.m_objRecipe.m_bKnown)
            {
               this.m_objRecipe.m_bKnown = true;
            }
            PlayState.m_objInstance.sprPlayer.AddRecipe = this.m_objRecipe.m_nID;
         }
      }
   }
}

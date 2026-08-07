package
{
   import org.flixel.FlxG;
   import org.flixel.FlxPoint;
   
   public class SaveGameWound
   {
       
      
      public var m_fBluntSeverity:Number;
      
      public var m_fCutSeverity:Number;
      
      public var m_fBluntSeverityOld:Number;
      
      public var m_fCutSeverityOld:Number;
      
      public var m_fCutArmor:Number;
      
      public var m_fBluntArmor:Number;
      
      public var m_fInfectRate:Number;
      
      public var m_fBleedRate:Number;
      
      public var m_bStaunched:Boolean;
      
      public var m_bSplinted:Boolean;
      
      public var m_fPain:Number;
      
      public var vCurrentStates:Vector.<SaveGameCondition>;
      
      public function SaveGameWound(param1:Object = null)
      {
         var _loc2_:PlayerCondition = null;
         var _loc3_:SaveGameCondition = null;
         var _loc4_:Object = null;
         super();
         this.vCurrentStates = new Vector.<SaveGameCondition>();
         if(param1 != null)
         {
            this.m_fBluntSeverity = param1.m_fBluntSeverity;
            this.m_fCutSeverity = param1.m_fCutSeverity;
            this.m_fBluntSeverityOld = param1.m_fBluntSeverityOld;
            this.m_fCutSeverityOld = param1.m_fCutSeverityOld;
            this.m_fCutArmor = param1.m_fCutArmor;
            this.m_fBluntArmor = param1.m_fBluntArmor;
            this.m_fInfectRate = param1.m_fInfectRate;
            this.m_fBleedRate = param1.m_fBleedRate;
            this.m_bStaunched = param1.m_bStaunched;
            this.m_bSplinted = param1.m_bSplinted;
            this.m_fPain = param1.m_fPain;
            if(param1 is GUIInventoryWound)
            {
               for each(_loc2_ in param1.aCurrentStates)
               {
                  _loc3_ = new SaveGameCondition();
                  _loc3_.m_fDate = _loc2_.m_objDate.getTime();
                  _loc3_.m_nID = _loc2_.m_nID;
                  _loc3_.m_nStacked = _loc2_.m_nStacked;
                  this.vCurrentStates.push(_loc3_);
               }
            }
            else
            {
               for each(_loc4_ in param1.vCurrentStates)
               {
                  _loc3_ = new SaveGameCondition();
                  _loc3_.m_fDate = _loc4_.m_fDate;
                  _loc3_.m_nID = _loc4_.m_nID;
                  _loc3_.m_nStacked = _loc4_.m_nStacked;
                  this.vCurrentStates.push(_loc3_);
               }
            }
         }
      }
      
      public function destroy() : void
      {
         this.vCurrentStates = null;
      }
   }
}

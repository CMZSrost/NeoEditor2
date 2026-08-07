package
{
   public interface IConditionOwner
   {
       
      
      function GetCondition(param1:uint) : PlayerCondition;
      
      function HasCondition(param1:uint) : Boolean;
      
      function AddCondition(param1:PlayerCondition, param2:Boolean = true, param3:Boolean = true) : void;
      
      function RemoveCondition(param1:PlayerCondition) : void;
   }
}

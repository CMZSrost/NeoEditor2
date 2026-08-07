package
{
   import org.flixel.FlxPoint;
   
   public class GUIFitItemResult
   {
      
      public static const RESULT_CANNOT_FIT:uint = 0;
      
      public static const RESULT_CAN_SOCKET_SWAP:uint = 1;
      
      public static const RESULT_CAN_FIT_SWAP:uint = 2;
      
      public static const RESULT_CAN_STACK_PARTIAL:uint = 3;
      
      public static const RESULT_CAN_SOCKET:uint = 4;
      
      public static const RESULT_CAN_STACK_FULL:uint = 5;
      
      public static const RESULT_CAN_FIT:uint = 6;
      
      public static const RESULT_CAN_FIT_SUB:uint = 7;
      
      public static const RESULT_CAN_SOCKET_PARTIAL:uint = 8;
       
      
      public var m_nResult:uint;
      
      public var m_grpSlot:GUIInventorySlot;
      
      public var m_objItem:ItemInstance;
      
      public var m_ptPos:FlxPoint;
      
      public var m_nNextX:int;
      
      public function GUIFitItemResult()
      {
         super();
      }
      
      public function destroy() : void
      {
         this.m_nResult = 0;
         this.m_nNextX = 0;
         this.m_grpSlot = null;
         this.m_objItem = null;
         this.m_ptPos = null;
      }
   }
}

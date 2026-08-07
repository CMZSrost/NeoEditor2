package
{
   import org.flixel.FlxPoint;
   
   public class TextFloaty
   {
       
      
      public var m_strText:String;
      
      public var m_ptPos:FlxPoint;
      
      public var m_nColor:int;
      
      public function TextFloaty(param1:String, param2:FlxPoint, param3:int = -1)
      {
         super();
         if(param3 < 0)
         {
            param3 = int(GUIMessageWindow.COLOR_DEFAULT);
         }
         this.m_strText = param1;
         this.m_ptPos = new FlxPoint(param2.x,param2.y);
         this.m_nColor = param3;
      }
      
      public function destroy() : void
      {
         this.m_strText = null;
         this.m_ptPos = null;
      }
   }
}

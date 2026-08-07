package
{
   import flash.ui.Mouse;
   import org.flixel.FlxGame;
   
   public class NEOScavenger extends FlxGame
   {
       
      
      public function NEOScavenger()
      {
         super(1360 / 1,768 / 1,MenuState,1,DataHandler.nFPS,DataHandler.nFPS,Mouse.supportsNativeCursor);
      }
   }
}

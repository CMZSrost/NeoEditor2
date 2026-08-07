package
{
   import flash.display.Bitmap;
   import flash.events.Event;
   import flash.ui.Mouse;
   import org.flixel.FlxG;
   import org.flixel.FlxState;
   
   public class MenuState extends FlxState
   {
       
      
      private var m_grpMenu:GUIEscMenu;
      
      public var m_grpLetterbox:GUILetterbox;
      
      public function MenuState()
      {
         super();
      }
      
      override public function create() : void
      {
         if(!Mouse.supportsNativeCursor)
         {
            FlxG.mouse.show();
         }
         FlxG.mouse.loadBitmap(new Bitmap(DataHandler.GetImage("CurDefault.png")));
         FlxG.bgColor = 4278190080;
         FlxG.sounds.callAll("stop");
         DataHandler.stage.align = GUIValues.GetString("strAlignMode");
         FlxG.stage.showDefaultContextMenu = false;
         this.m_grpLetterbox = new GUILetterbox();
         this.m_grpMenu = new GUIEscMenu();
         add(this.m_grpMenu);
         add(this.m_grpLetterbox);
         FlxG.stage.root.addEventListener(Event.FULLSCREEN,this.m_grpMenu.HandlerFullscreen);
         FlxG.stage.root.addEventListener(Event.RESIZE,this.m_grpMenu.HandlerResize);
         this.m_grpMenu.Show();
      }
      
      override public function destroy() : void
      {
         FlxG.stage.root.removeEventListener(Event.FULLSCREEN,this.m_grpMenu.HandlerFullscreen);
         FlxG.stage.root.removeEventListener(Event.RESIZE,this.m_grpMenu.HandlerResize);
         super.destroy();
      }
      
      public function ShowMsg(param1:String) : void
      {
         if(this.m_grpMenu != null)
         {
            this.m_grpMenu.ShowMsg(param1);
         }
      }
   }
}

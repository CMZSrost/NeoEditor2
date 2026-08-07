package
{
   import org.flixel.FlxG;
   import org.flixel.FlxGroup;
   import org.flixel.FlxPoint;
   import org.flixel.FlxSprite;
   
   public class GUILetterbox extends FlxGroup
   {
       
      
      private var m_sprTitleFrameL:FlxSprite;
      
      private var m_sprTitleFrameR:FlxSprite;
      
      private var m_sprTitleFrameT:FlxSprite;
      
      private var m_sprTitleFrameB:FlxSprite;
      
      public function GUILetterbox()
      {
         super();
         var _loc1_:FlxPoint = GUIValues.GetPoint("offset");
         this.m_sprTitleFrameL = new FlxSprite(_loc1_.x - FlxG.width,_loc1_.y);
         this.m_sprTitleFrameL.makeGraphic(FlxG.width,FlxG.height,4278190080);
         this.m_sprTitleFrameR = new FlxSprite(_loc1_.x + GUIValues.GetInt("width"),_loc1_.y);
         this.m_sprTitleFrameR.pixels = this.m_sprTitleFrameL.pixels;
         this.m_sprTitleFrameT = new FlxSprite(0,_loc1_.y - this.m_sprTitleFrameL.height);
         this.m_sprTitleFrameT.pixels = this.m_sprTitleFrameL.pixels;
         this.m_sprTitleFrameB = new FlxSprite(0,_loc1_.y + GUIValues.GetInt("height"));
         this.m_sprTitleFrameB.pixels = this.m_sprTitleFrameL.pixels;
         add(this.m_sprTitleFrameL);
         add(this.m_sprTitleFrameR);
         add(this.m_sprTitleFrameT);
         add(this.m_sprTitleFrameB);
         setAll("scrollFactor",new FlxPoint(0,0));
         setAll("cameras",[FlxG.camera]);
      }
      
      override public function destroy() : void
      {
         this.m_sprTitleFrameL = DataHandler.DestroyObject(this.m_sprTitleFrameL);
         this.m_sprTitleFrameR = DataHandler.DestroyObject(this.m_sprTitleFrameR);
         this.m_sprTitleFrameT = DataHandler.DestroyObject(this.m_sprTitleFrameT);
         this.m_sprTitleFrameB = DataHandler.DestroyObject(this.m_sprTitleFrameB);
      }
      
      public function SetRes() : void
      {
         var _loc1_:FlxPoint = GUIValues.GetPoint("offset");
         this.m_sprTitleFrameL.x = _loc1_.x - FlxG.width;
         this.m_sprTitleFrameL.y = _loc1_.y;
         this.m_sprTitleFrameR.x = _loc1_.x + GUIValues.GetInt("width");
         this.m_sprTitleFrameR.y = _loc1_.y;
         this.m_sprTitleFrameT.x = 0;
         this.m_sprTitleFrameT.y = _loc1_.y - this.m_sprTitleFrameL.height;
         this.m_sprTitleFrameB.x = 0;
         this.m_sprTitleFrameB.y = _loc1_.y + GUIValues.GetInt("height");
      }
   }
}

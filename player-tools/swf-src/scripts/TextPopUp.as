package
{
   import flash.display.BitmapData;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import org.flixel.*;
   
   public class TextPopUp extends FlxGroup
   {
       
      
      private var m_sprIMG:FlxSprite;
      
      private var m_bmpIMGNull:BitmapData;
      
      private var m_txtPopUp:FlxText;
      
      private var m_sprBG:FlxSprite;
      
      public var m_sprPeek:FlxSprite;
      
      private var m_nPadding:int;
      
      private var m_nTintColor:uint;
      
      private var ptOffset:FlxPoint;
      
      private var ptTemp:FlxPoint;
      
      public var strTextLast:String;
      
      public function TextPopUp()
      {
         super();
         this.ptTemp = new FlxPoint();
         this.ptOffset = new FlxPoint();
         this.m_nPadding = 2;
         this.m_nTintColor = 3422552064;
         this.m_txtPopUp = new FlxText(0,0,GUIValues.GetInt("TextPopUp.width"),"Pop up text");
         this.m_txtPopUp.setFormat(GUIValues.GetString("strTinyFontName"),GUIValues.GetInt("nTinyFontSize"),GUIValues.GetInt("nBodyFontColor"),"left",GUIValues.GetInt("nBodyFontShadowColor"));
         this.m_bmpIMGNull = DataHandler.GetImage("blank.png");
         this.m_sprIMG = new FlxSprite();
         this.m_sprIMG.pixels = this.m_bmpIMGNull;
         this.m_sprBG = new FlxSprite();
         this.m_sprBG.makeGraphic(1,1,this.m_nTintColor);
         this.m_sprPeek = new FlxSprite();
         this.m_sprPeek.makeGraphic(1,1,this.m_nTintColor);
         add(this.m_sprBG);
         add(this.m_sprPeek);
         add(this.m_sprIMG);
         add(this.m_txtPopUp);
         setAll("scrollFactor",new FlxPoint());
         setAll("cameras",[FlxG.camera]);
      }
      
      override public function destroy() : void
      {
         this.m_sprIMG = DataHandler.DestroyObject(this.m_sprIMG);
         this.m_bmpIMGNull = null;
         this.m_txtPopUp = DataHandler.DestroyObject(this.m_txtPopUp);
         this.m_sprBG = DataHandler.DestroyObject(this.m_sprBG);
         this.m_sprPeek = DataHandler.DestroyObject(this.m_sprPeek);
      }
      
      public function UpdateInfo(param1:String, param2:BitmapData = null) : void
      {
         if(this.m_txtPopUp.text != param1)
         {
            this.m_txtPopUp.text = param1;
            this.strTextLast = param1;
         }
         if(param2 == null)
         {
            this.m_sprIMG.pixels = this.m_bmpIMGNull;
         }
         else
         {
            this.m_sprIMG.pixels = param2;
         }
         this.m_sprBG.scale.x = this.m_sprIMG.width + this.m_txtPopUp.width + this.m_nPadding * 3;
         this.m_sprBG.scale.y = Math.max(this.m_sprIMG.height,this.m_txtPopUp.height) + this.m_nPadding * 2;
         if(this.m_sprPeek.visible)
         {
            this.m_sprBG.scale.y += this.m_sprPeek.height;
         }
         this.Move(this.m_sprBG.x - this.m_sprBG.scale.x / 2,this.m_sprBG.y - this.m_sprBG.scale.y / 2);
      }
      
      public function Move(param1:Number, param2:Number) : void
      {
         if(visible == false)
         {
            return;
         }
         this.ptOffset = GUIValues.GetPoint("offset",this.ptOffset);
         this.ptTemp.x = this.ptOffset.x + GUIValues.GetInt("width");
         this.ptTemp.y = this.ptOffset.y + GUIValues.GetInt("height") - GUIValues.GetInt("PlayState.camMsg.minheight");
         if(param1 + this.m_sprBG.scale.x > this.ptTemp.x)
         {
            param1 = param1 - this.m_sprBG.scale.x - 16;
         }
         if(param2 + this.m_sprBG.scale.y > this.ptTemp.y)
         {
            param2 = this.ptTemp.y - this.m_sprBG.scale.y;
         }
         this.m_sprBG.x = param1 + this.m_sprBG.scale.x / 2;
         this.m_sprIMG.x = param1 + this.m_nPadding;
         this.m_txtPopUp.x = this.m_sprIMG.x + this.m_sprIMG.width + this.m_nPadding;
         this.m_sprBG.y = param2 + this.m_sprBG.scale.y / 2;
         this.m_sprIMG.y = param2 + this.m_nPadding;
         this.m_txtPopUp.y = param2 + this.m_nPadding;
         this.m_sprPeek.x = param1 + this.m_nPadding;
         this.m_sprPeek.y = param2 + Math.max(this.m_sprIMG.height,this.m_txtPopUp.height) + this.m_nPadding;
      }
      
      public function AddPeek(param1:ItemInstance) : void
      {
         var _loc2_:ItemInstance = null;
         this.m_sprPeek.makeGraphic(FlxPoint(param1.ItemDefinition.aCapacities[0]).x,FlxPoint(param1.ItemDefinition.aCapacities[0]).y,3422552064);
         this.m_sprPeek.pixels.copyPixels(DataHandler.GetImage("GUIGrid.png"),new Rectangle(0,0,FlxPoint(param1.ItemDefinition.aCapacities[0]).x,FlxPoint(param1.ItemDefinition.aCapacities[0]).y),new Point(0,0));
         this.m_sprPeek.pixels = GUIValues.ScaleBitmapData(this.m_sprPeek.pixels,GUIValues.m_fItemZoom);
         this.m_sprPeek.visible = true;
         this.m_sprPeek.dirty = true;
         for each(_loc2_ in param1.vItems)
         {
            this.m_sprPeek.pixels.copyPixels(_loc2_.m_sprIMG.pixels,_loc2_.m_sprIMG.pixels.rect,new Point(_loc2_.ptOffset.x,_loc2_.ptOffset.y),null,null,true);
            if(_loc2_.m_sprStackBG.visible != false)
            {
               this.m_sprPeek.pixels.fillRect(new Rectangle(_loc2_.ptOffset.x + _loc2_.m_sprStackBG.x - _loc2_.m_sprIMG.x - _loc2_.m_sprStackBG.scale.x / 2,_loc2_.ptOffset.y + _loc2_.m_sprStackBG.y - _loc2_.m_sprIMG.y - _loc2_.m_sprStackBG.scale.y / 2,_loc2_.m_sprStackBG.scale.x,_loc2_.m_sprStackBG.scale.y),4278190080);
               this.m_sprPeek.pixels.copyPixels(_loc2_.m_txtStackAmount.pixels,_loc2_.m_txtStackAmount.pixels.rect,new Point(_loc2_.ptOffset.x + _loc2_.m_txtStackAmount.x - _loc2_.m_sprIMG.x,_loc2_.ptOffset.y + _loc2_.m_txtStackAmount.y - _loc2_.m_sprIMG.y),null,null,true);
            }
         }
      }
      
      public function ClearPeek() : void
      {
         this.m_sprPeek.visible = false;
      }
      
      public function Show() : void
      {
         setAll("visible",true);
      }
      
      public function Hide() : void
      {
         setAll("visible",false);
      }
      
      public function SetRes() : void
      {
         this.m_txtPopUp.setFormat(GUIValues.GetString("strBodyFontName"),GUIValues.GetInt("nBodyFontSize"),GUIValues.GetInt("nBodyFontColor"));
         this.m_txtPopUp.SetWidth(GUIValues.GetInt("TextPopUp.width"));
      }
   }
}

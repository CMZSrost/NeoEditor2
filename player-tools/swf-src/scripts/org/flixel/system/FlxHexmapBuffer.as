package org.flixel.system
{
   import flash.display.BitmapData;
   import flash.geom.Rectangle;
   import org.flixel.FlxCamera;
   import org.flixel.FlxG;
   import org.flixel.FlxU;
   
   public class FlxHexmapBuffer extends FlxTilemapBuffer
   {
       
      
      public function FlxHexmapBuffer(param1:Number, param2:Number, param3:uint, param4:uint, param5:FlxCamera = null, param6:int = 0, param7:int = 0)
      {
         super(param1,param2,param3,param4,param5);
         if(param5 == null)
         {
            param5 = FlxG.camera;
         }
         columns = FlxU.ceil(param5.width / param6) + 2;
         if(columns > param3)
         {
            columns = param3;
         }
         rows = FlxU.ceil(param5.height / param7) + 2;
         if(rows > param4)
         {
            rows = param4;
         }
         _pixels = new BitmapData((columns - 1) * param6 + param1 + param6 / 2,param7 * (rows - 1) + param2,true,0);
         width = _pixels.width;
         height = _pixels.height;
         _flashRect = new Rectangle(0,0,width,height);
         dirty = true;
      }
   }
}

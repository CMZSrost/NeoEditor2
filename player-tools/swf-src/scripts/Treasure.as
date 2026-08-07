package
{
   import org.flixel.FlxPoint;
   
   public class Treasure
   {
       
      
      public var strItemID:String;
      
      public var fChance:Number;
      
      public var ptAmount:FlxPoint;
      
      public function Treasure(param1:String, param2:Number, param3:FlxPoint)
      {
         super();
         this.strItemID = param1;
         this.fChance = param2;
         this.ptAmount = param3;
         this.ptAmount.y -= this.ptAmount.x;
         if(this.ptAmount.y < 0)
         {
            this.ptAmount.y = 0;
         }
      }
   }
}

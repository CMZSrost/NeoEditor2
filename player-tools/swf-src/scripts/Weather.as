package
{
   public class Weather
   {
       
      
      public var fTemp:Number;
      
      public var fPrecipChance:Number;
      
      public var fOvercastChance:Number;
      
      public var bPrecip:Boolean;
      
      public var bOvercast:Boolean;
      
      public var objDate:Date;
      
      public function Weather(param1:Number, param2:Number, param3:Number, param4:Boolean, param5:Boolean, param6:Date)
      {
         super();
         this.fTemp = param1;
         this.fPrecipChance = param2;
         this.fOvercastChance = param3;
         this.bPrecip = param4;
         this.bOvercast = param5;
         this.objDate = param6;
      }
      
      public function destroy() : void
      {
         this.objDate = null;
      }
   }
}

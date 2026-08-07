package
{
   import org.flixel.*;
   
   public class VFX extends FlxGroup
   {
       
      
      public var aSprites:Array;
      
      public var aParams:Array;
      
      public var fPerFrame:Function;
      
      public var fCleanup:Function;
      
      public var m_bNeedsCleanup:Boolean;
      
      public function VFX(param1:Array, param2:Array, param3:Function, param4:Function)
      {
         super();
         this.aSprites = param1;
         this.aParams = param2;
         this.fPerFrame = param3;
         this.fCleanup = param4;
         this.m_bNeedsCleanup = false;
         var _loc5_:int = 0;
         while(_loc5_ < this.aSprites.length)
         {
            add(this.aSprites[_loc5_]);
            _loc5_++;
         }
      }
      
      override public function destroy() : void
      {
         var _loc1_:int = 0;
         if(this.aSprites != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aSprites.length)
            {
               FlxSprite(this.aSprites[_loc1_]).destroy();
               this.aSprites[_loc1_] = null;
               _loc1_++;
            }
            this.aSprites = null;
         }
         if(this.aParams != null)
         {
            _loc1_ = 0;
            while(_loc1_ < this.aParams.length)
            {
               this.aParams[_loc1_] = null;
               _loc1_++;
            }
            this.aParams = null;
         }
         this.fPerFrame = null;
         this.fCleanup = null;
         super.destroy();
      }
      
      public function Cleanup() : void
      {
         var _loc1_:int = 0;
         while(_loc1_ < this.aSprites.length)
         {
            remove(this.aSprites[_loc1_]);
            _loc1_++;
         }
         this.fPerFrame = null;
         this.fCleanup(this);
      }
      
      override public function update() : void
      {
         super.update();
         if(this.fPerFrame != null)
         {
            this.fPerFrame(this);
         }
         if(this.m_bNeedsCleanup)
         {
            this.Cleanup();
         }
      }
   }
}

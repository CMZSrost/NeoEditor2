package org.flixel
{
   import org.flixel.plugin.TimerManager;
   
   public class FlxTimer
   {
       
      
      public var time:Number;
      
      public var loops:uint;
      
      public var paused:Boolean;
      
      public var finished:Boolean;
      
      protected var _callback:Function;
      
      protected var _timeCounter:Number;
      
      protected var _loopsCounter:uint;
      
      public function FlxTimer()
      {
         super();
         this.time = 0;
         this.loops = 0;
         this._callback = null;
         this._timeCounter = 0;
         this._loopsCounter = 0;
         this.paused = false;
         this.finished = false;
      }
      
      public static function get manager() : TimerManager
      {
         return FlxG.getPlugin(TimerManager) as TimerManager;
      }
      
      public function destroy() : void
      {
         this.stop();
         this._callback = null;
      }
      
      public function update() : void
      {
         this._timeCounter += FlxG.elapsed;
         while(this._timeCounter >= this.time && !this.paused && !this.finished)
         {
            this._timeCounter -= this.time;
            ++this._loopsCounter;
            if(this.loops > 0 && this._loopsCounter >= this.loops)
            {
               this.stop();
            }
            if(this._callback != null)
            {
               this._callback(this);
            }
         }
      }
      
      public function start(param1:Number = 1, param2:uint = 1, param3:Function = null) : FlxTimer
      {
         var _loc4_:TimerManager;
         if((_loc4_ = manager) != null)
         {
            _loc4_.add(this);
         }
         if(this.paused)
         {
            this.paused = false;
            return this;
         }
         this.paused = false;
         this.finished = false;
         this.time = param1;
         this.loops = param2;
         this._callback = param3;
         this._timeCounter = 0;
         this._loopsCounter = 0;
         return this;
      }
      
      public function stop() : void
      {
         this.finished = true;
         var _loc1_:TimerManager = manager;
         if(_loc1_ != null)
         {
            _loc1_.remove(this);
         }
      }
      
      public function get timeLeft() : Number
      {
         return this.time - this._timeCounter;
      }
      
      public function get loopsLeft() : int
      {
         return this.loops - this._loopsCounter;
      }
      
      public function get progress() : Number
      {
         if(this.time > 0)
         {
            return this._timeCounter / this.time;
         }
         return 0;
      }
   }
}

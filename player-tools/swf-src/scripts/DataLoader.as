package
{
   import flash.events.Event;
   import flash.events.EventDispatcher;
   import flash.events.IOErrorEvent;
   import flash.events.SecurityErrorEvent;
   import flash.net.URLLoader;
   import flash.net.URLLoaderDataFormat;
   import flash.net.URLRequest;
   import flash.utils.Dictionary;
   import org.flixel.*;
   
   public class DataLoader extends EventDispatcher
   {
      
      private static var _map:Dictionary = new Dictionary();
       
      
      private var _loader:URLLoader;
      
      private var _data:*;
      
      private var _callback:Function;
      
      private var _url:URLRequest;
      
      public function DataLoader(param1:Function = null)
      {
         super();
         this._data = null;
         this._callback = param1;
         this._loader = new URLLoader();
         this._loader.dataFormat = URLLoaderDataFormat.VARIABLES;
         this._loader.addEventListener(IOErrorEvent.IO_ERROR,this.handleError);
         this._loader.addEventListener(SecurityErrorEvent.SECURITY_ERROR,this.handleError);
         this._loader.addEventListener(Event.COMPLETE,this.handleComplete);
      }
      
      public function load(param1:URLRequest) : void
      {
         this._url = param1;
         this._loader.load(param1);
      }
      
      private function dispatchError(param1:Event) : void
      {
         this.cleanUp();
         dispatchEvent(new Event(Event.COMPLETE));
         var _loc2_:String = unescape("Failed " + this._url.url + ":\n" + URLLoader(param1.target).data);
         if(this._callback is Function)
         {
            this._callback(param1);
         }
      }
      
      private function dispatchSuccess() : void
      {
         this.cleanUp();
         dispatchEvent(new Event(Event.COMPLETE));
         if(this._callback is Function)
         {
            this._callback(this._data);
         }
      }
      
      private function handleError(param1:Event) : void
      {
         this.dispatchError(param1);
      }
      
      private function handleComplete(param1:Event) : void
      {
         var e:Event = param1;
         var success:Boolean = false;
         try
         {
            this._data = e.target.data;
            success = true;
         }
         catch(err:TypeError)
         {
            success = false;
         }
         catch(_loc_e_:*)
         {
            var _loc4_:* = §§pop();
            §§push(0);
            if(success)
            {
               this.dispatchSuccess();
            }
            else
            {
               this.dispatchError(e);
            }
            switch(§§pop())
            {
               case 0:
                  throw _loc4_;
               default:
                  return;
            }
         }
      }
      
      public function get data() : *
      {
         return this._data;
      }
      
      private function cleanUp() : void
      {
         if(this._loader)
         {
            this._loader.removeEventListener(IOErrorEvent.IO_ERROR,this.handleError);
            this._loader.removeEventListener(SecurityErrorEvent.SECURITY_ERROR,this.handleError);
            this._loader.removeEventListener(Event.COMPLETE,this.handleComplete);
         }
         if(_map[this])
         {
            delete _map[this];
         }
      }
   }
}

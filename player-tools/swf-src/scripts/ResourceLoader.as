package
{
   import flash.display.Loader;
   import flash.display.LoaderInfo;
   import flash.events.Event;
   import flash.events.EventDispatcher;
   import flash.events.IOErrorEvent;
   import flash.net.URLRequest;
   import flash.utils.Dictionary;
   
   public class ResourceLoader extends EventDispatcher
   {
      
      private static var _map:Dictionary = new Dictionary();
       
      
      private var _loader:Loader;
      
      private var _data:LoaderInfo;
      
      private var _callback:Function;
      
      private var _url:String;
      
      public function ResourceLoader(param1:Function = null)
      {
         super();
         this._data = null;
         this._callback = param1;
         this._loader = new Loader();
         this._loader.contentLoaderInfo.addEventListener(Event.COMPLETE,this.handleComplete);
         this._loader.contentLoaderInfo.addEventListener(IOErrorEvent.IO_ERROR,this.handleError);
      }
      
      public function load(param1:URLRequest) : void
      {
         this._url = param1.url;
         this._loader.load(param1);
      }
      
      private function dispatchError(param1:Event) : void
      {
         this.cleanUp();
         dispatchEvent(new Event(Event.COMPLETE));
         var _loc2_:String = unescape("Failed " + this._url + ":\n" + param1.target);
         if(this._callback is Function)
         {
            this._callback(null,this._url);
         }
      }
      
      private function dispatchSuccess() : void
      {
         this.cleanUp();
         dispatchEvent(new Event(Event.COMPLETE));
         if(this._callback is Function)
         {
            this._callback(this._data.content,this._url);
         }
      }
      
      private function handleError(param1:Event) : void
      {
      }
      
      private function handleComplete(param1:Event) : void
      {
         var e:Event = param1;
         var success:Boolean = false;
         try
         {
            this._data = e.target.loader.contentLoaderInfo;
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
      
      public function get data() : LoaderInfo
      {
         return this._data;
      }
      
      private function cleanUp() : void
      {
         if(this._loader)
         {
            this._loader.contentLoaderInfo.removeEventListener(Event.COMPLETE,this.handleComplete);
            this._loader.contentLoaderInfo.removeEventListener(IOErrorEvent.IO_ERROR,this.handleError);
         }
         if(_map[this])
         {
            delete _map[this];
         }
      }
   }
}

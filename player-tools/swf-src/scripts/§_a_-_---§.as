package
{
   import flash.display.Sprite;
   import flash.utils.ByteArray;
   import flash.utils.Endian;
   
   public class §_a_-_---§ extends Sprite
   {
      
      private static var §_a_--_-§:Class;
      
      private static var §_a_--_§:Class;
      
      private static var §_a_-__§:Class;
      
      private static var §_a_-____§:Array;
      
      private static var §_a_----§:Array;
      
      private static var §_a_-___-§:Boolean = false;
      
      private static var §_a_--§:int;
      
      {
         var _loc1_:* = true;
         var _loc2_:* = false;
         if(_loc1_)
         {
            §_a_--_-§ = §_a_-_-__§;
            if(!_loc2_)
            {
               if(!_loc1_)
               {
                  _loc1_ = _loc1_;
                  _loc2_ = _loc2_;
                  this = §_a_-_---§;
                  loop0:
                  while(true)
                  {
                     §_a_----§ = new Array();
                     if(_loc1_)
                     {
                        if(_loc1_)
                        {
                           if(!_loc2_)
                           {
                              if(_loc2_)
                              {
                                 _loc2_ = _loc2_;
                                 this = §_a_-_---§;
                                 _loc1_ = _loc1_;
                                 while(true)
                                 {
                                    §_a_--_§ = §_a_-_§;
                                    if(_loc1_)
                                    {
                                       if(!_loc1_)
                                       {
                                          this = §_a_-_---§;
                                          _loc2_ = _loc2_;
                                          this = §_a_-_---§;
                                          loop4:
                                          while(true)
                                          {
                                             §_a_-____§ = new Array();
                                             addr70:
                                             while(!_loc1_)
                                             {
                                                _loc1_ = _loc1_;
                                                this = §_a_-_---§;
                                                this = §_a_-_---§;
                                                while(true)
                                                {
                                                   §_a_-___-§ = false;
                                                   addr84:
                                                   while(true)
                                                   {
                                                      if(_loc2_)
                                                      {
                                                         this = §_a_-_---§;
                                                         this = §_a_-_---§;
                                                         _loc2_ = _loc2_;
                                                         while(true)
                                                         {
                                                            §_a_-__§ = §_a_---§;
                                                            addr98:
                                                            while(true)
                                                            {
                                                               if(_loc2_)
                                                               {
                                                                  _loc2_ = _loc2_;
                                                                  this = §_a_-_---§;
                                                                  _loc1_ = _loc1_;
                                                                  break;
                                                               }
                                                               continue loop4;
                                                            }
                                                         }
                                                         addr95:
                                                      }
                                                      return;
                                                   }
                                                   _loc1_ = _loc1_;
                                                   this = §_a_-_---§;
                                                   this = §_a_-_---§;
                                                }
                                             }
                                             continue loop0;
                                          }
                                       }
                                       §§goto(addr95);
                                    }
                                    §§goto(addr70);
                                 }
                                 addr49:
                              }
                              §§goto(addr81);
                           }
                           §§goto(addr98);
                        }
                        §§goto(addr70);
                     }
                     §§goto(addr84);
                  }
               }
               §§goto(addr49);
            }
            §§goto(addr98);
         }
         §§goto(addr84);
      }
      
      public function §_a_-_---§()
      {
         var _loc1_:Boolean = false;
         var _loc2_:Boolean = true;
         if(!_loc1_)
         {
            super();
         }
      }
      
      private static function §_a_-_--§() : void
      {
         var _loc7_:* = false;
         var _loc8_:* = true;
         var _loc1_:ByteArray = new §_a_--_-§() as ByteArray;
         var _loc2_:* = new §_a_--_§() as ByteArray;
         var _loc3_:* = new §_a_-__§() as ByteArray;
         if(_loc8_)
         {
            _loc3_.endian = Endian.LITTLE_ENDIAN;
            if(!_loc7_)
            {
               §_a_--§ = _loc3_.readInt();
            }
         }
         var _loc4_:* = _loc2_.readByte();
         §§push(0);
         if(!_loc8_)
         {
            §§push((§§pop() - 100 + 1) * 119);
         }
         var _loc5_:* = §§pop();
         if(!_loc7_)
         {
            while(true)
            {
               §§push(_loc5_);
               if(_loc8_)
               {
                  if(§§pop() >= _loc4_)
                  {
                     if(_loc8_)
                     {
                        §§push(_loc1_.readInt());
                        if(!_loc7_)
                        {
                           break;
                        }
                        §§goto(addr89);
                     }
                     §§push(0);
                     if(_loc7_)
                     {
                        §§push(-(§§pop() - 82 + 1 + 1 + 15 + 1) + 1);
                     }
                     addr89:
                  }
                  §_a_-__-_§(_loc2_);
                  if(_loc8_)
                  {
                     _loc5_++;
                  }
                  continue;
                  var _loc6_:* = §§pop();
                  if(_loc8_)
                  {
                     loop1:
                     while(_loc6_ < _loc4_)
                     {
                        if(_loc7_)
                        {
                           _loc2_ = _loc2_;
                           this = §_a_-_---§;
                           _loc4_ = _loc4_;
                           loop2:
                           while(true)
                           {
                              _loc6_++;
                              if(!_loc7_)
                              {
                                 if(_loc8_)
                                 {
                                    continue loop1;
                                 }
                                 _loc7_ = _loc7_;
                                 _loc3_ = _loc3_;
                                 _loc4_ = _loc4_;
                                 while(true)
                                 {
                                    §_a_--__§(_loc1_,§_a_----§[_loc6_ % §_a_----§.length]);
                                 }
                              }
                              while(true)
                              {
                                 if(!_loc8_)
                                 {
                                    break loop2;
                                 }
                                 continue loop2;
                              }
                           }
                           _loc2_ = _loc2_;
                           _loc5_ = _loc5_;
                           _loc8_ = _loc8_;
                           continue;
                        }
                        §§goto(addr120);
                     }
                     if(_loc8_)
                     {
                        if(_loc7_)
                        {
                        }
                        §_a_-___-§ = true;
                     }
                  }
                  return;
               }
               break;
            }
            _loc4_ = §§pop();
         }
         §§goto(addr77);
      }
      
      private static function §_a_--__§(param1:ByteArray, param2:ByteArray) : void
      {
         var _loc6_:Boolean = false;
         var _loc7_:Boolean = true;
         var _loc3_:int = param1.readInt();
         var _loc4_:ByteArray = new ByteArray();
         if(_loc7_)
         {
            §§push(param1);
            §§push(_loc4_);
            §§push(0);
            if(!_loc7_)
            {
               §§push((-(§§pop() - 1 + 7) + 1 + 1) * 15 * 64);
            }
            §§pop().readBytes(§§pop(),§§pop(),_loc3_);
         }
         var _loc5_:§_a_-_-_§;
         (_loc5_ = new §_a_-_-_§(param2)).§_a_---_§(_loc4_);
         if(_loc7_)
         {
            §§push(_loc4_);
            §§push(0);
            if(_loc6_)
            {
               §§push(-(§§pop() * 10 - 1 - 37) + 1);
            }
            §§pop().position = §§pop();
            if(_loc7_)
            {
               §_a_-____§.push(_loc4_.readUTFBytes(_loc4_.length));
            }
         }
      }
      
      private static function §_a_-__-_§(param1:ByteArray) : void
      {
         var _loc3_:* = false;
         var _loc4_:* = true;
         var _loc2_:* = new ByteArray();
         if(!_loc3_)
         {
            §§push(param1);
            §§push(_loc2_);
            §§push(0);
            if(!_loc4_)
            {
               §§push(-(§§pop() - 83 - 1));
            }
            §§pop().readBytes(§§pop(),§§pop(),16);
            if(!_loc3_)
            {
               addr27:
               if(!_loc4_)
               {
                  _loc4_ = _loc4_;
                  _loc2_ = _loc2_;
                  _loc4_ = _loc4_;
                  loop1:
                  while(true)
                  {
                     §_a_----§.push(_loc2_);
                     if(!_loc3_)
                     {
                        if(!_loc4_)
                        {
                           _loc3_ = _loc3_;
                           _loc2_ = _loc2_;
                           this = §_a_-_---§;
                           while(true)
                           {
                              §§push(_loc2_);
                              §§push(0);
                              if(!_loc4_)
                              {
                                 §§push(-((§§pop() + 1) * 32) * 47);
                              }
                              §§pop().position = §§pop();
                              addr66:
                              while(true)
                              {
                                 if(_loc3_)
                                 {
                                    _loc3_ = _loc3_;
                                    _loc2_ = _loc2_;
                                    this = §_a_-_---§;
                                    break;
                                 }
                                 continue loop1;
                              }
                           }
                           addr55:
                        }
                        return;
                     }
                     §§goto(addr66);
                  }
               }
               §§goto(addr55);
            }
            §§goto(addr66);
         }
         §§goto(addr27);
      }
      
      public static function §_a_--_--§(param1:int) : String
      {
         var _loc2_:Boolean = false;
         var _loc3_:Boolean = true;
         if(!_loc2_)
         {
            if(!§_a_-___-§)
            {
               if(_loc3_)
               {
                  §_a_-_--§();
               }
            }
         }
         §§push(§_a_-____§);
         §§push(param1);
         if(!_loc2_)
         {
            §§push(§§pop() ^ §_a_--§);
         }
         return §§pop()[§§pop()];
      }
   }
}

using System;
using System.Collections;
using System.Collections.Generic;

namespace bhl {

public class VM
{
  static VM _instance;
  public static VM instance 
  {
    get {
      if(_instance == null)
        _instance = new VM();
      return _instance;
    }
  }
}

} //namespace bhl

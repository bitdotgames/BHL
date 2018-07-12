<?php

class mtg_cs_templater
{
  function tpl_struct()
  {
    $TPL = <<<EOD
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!
%includes%

namespace %namespace% {

public %type_name% %class% %parent% 
{
  %fields%

  static public %new_method% uint STATIC_CLASS_ID = %class_id%;

  public %virt_method% uint CLASS_ID() 
  {
    return %class_id%; 
  }

  %commented_in_pod_begin%
  public %class%()
  {
    reset();
  }
  %commented_in_pod_end%

  public %virt_method% void reset() 
  {
    %fields_reset%
  }

  public %virt_method% void copy(IMetaStruct other)
  {
    copyFrom((%class%)other);
  }

  public void copyFrom(%class% other)
  {
    reset();

    %copy_fields%
  }

  public %virt_method% IMetaStruct clone()
  {
    %class% copy = new %class%();
    copy.copy(this);
    return copy;
  }

  %commented_in_child_begin%
  public %virt_method% MetaIoError write(IDataWriter writer) 
  {
    MetaIoError err = writer.BeginArray(getFieldsCount());
    if(err != MetaIoError.SUCCESS)
      return err;
    
    err = writeFields(writer);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    return writer.EndArray();
  }
  %commented_in_child_end%

  public %virt_method% MetaIoError writeFields(IDataWriter writer) 
  {
    MetaIoError err = MetaIoError.SUCCESS;
    %write_buffer%
    return err;
  }
  
  %commented_in_child_begin%
  public %virt_method% MetaIoError read(IDataReader reader) 
  {
    MetaIoError err = MetaIoError.SUCCESS;

    err = reader.BeginArray();
    if(err != MetaIoError.SUCCESS)
      return err == MetaIoError.DATA_MISSING ? MetaIoError.DATA_MISSING0 : err;
    
    err = readFields(reader);
    if(err != MetaIoError.SUCCESS)
      return err;
    
    err = reader.EndArray();
    if(err != MetaIoError.SUCCESS)
      return err;
    
    return err;
  }
  %commented_in_child_end%

  public %virt_method% MetaIoError readFields(IDataReader reader) 
  {
    MetaIoError err = MetaIoError.SUCCESS;
    reset();
    %read_buffer%
    return err;
  }
  
  public %virt_method% int getFieldsCount() 
  {
    return %fields_count%; 
  }

  %ext_methods%
}

} //namespace %namespace%

EOD;
    return $TPL;
  }

  function tpl_enum()
  {
    $TPL = <<<EOD
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT! 

namespace %namespace% {

public enum %class% {
  %fields%
}

} //namespace %namespace%
 
EOD;
    return $TPL; 
  }

  function tpl_rpc()
  {
    $TPL = <<<EOD
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

namespace %namespace% {

public class %class% : IRpc
{
  public IRpcError error = null;
  public %req_class% req = new %req_class%();
  public %rsp_class% rsp = new %rsp_class%();

  public int getCode() 
  {
    return %code%;
  }

  public void setError(IRpcError error)
  {
    this.error = error;
  }

  public IRpcError getError()
  {
    return error;
  }

  public IMetaStruct getRequest()
  {
    return req as IMetaStruct;
  }

  public IMetaStruct getResponse()
  {
    return rsp as IMetaStruct;
  }
}

} //namespace %namespace%

EOD;
    return $TPL;
  }

  function tpl_bundle()
  {
    $TPL = <<<EOD
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!
using System.Collections.Generic;
using bhl;

%units_src%

namespace %namespace% {

public class AutogenBundle {

  static public IRpc createRpc(int code) 
  {
    switch(code) 
    {
      %create_rpc_by_id%
      default: 
      {
        return null;
      }
    }
  }

  static public IMetaStruct createById(int crc) 
  {
    //NOTE: reflection based creation, not reliable?
    //var type = id2type(crc);
    //if(type == null)
    //  return null;
    //return (IMetaStruct)System.Activator.CreateInstance(type);
    
    //NOTE: faster alternative
    switch(crc)
    {
      %create_struct_by_crc28%
      default: 
      {
        return null;
      }
    }
  }

  static public System.Type id2type(int crc) 
  {
    switch(crc) 
    {
      %id2type%
      default: 
      {
        return null;
      }
    }
  }
}

} // namespace %namespace%

EOD;
    
    return $TPL;
  }
}

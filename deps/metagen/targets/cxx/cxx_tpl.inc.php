<?php

class mtg_cxx_templater
{
  function tpl_packet_header($in_bundle = false)
  {
    $HEADER = <<<EOD
#ifndef PACKET_%type%_H
#define PACKET_%type%_H

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

#include "gambit/net.h"
#include "gambit/types.h"
#include "gambit/fixed_string.h"
#include "gambit/array.h"
#include "gambit/vector.h"
#include "gambit/string.h"
#include "gambit/data.h"
%includes%

EOD;

    $FOOTER = <<<EOD

#endif

EOD;

    $BODY = <<<EOD

namespace game {

struct %type% : public NetPacket 
{
  GAME_RTTI_EX(%type%, NetPacket);

  enum { 
    CLASS_ID = %classid%, 
    FIELDS_COUNT = %fields_count%, // legacy + 2/*from NetPacket*/,
  };

  %type%(Allocator* = NULL);
  virtual ~%type%();

  %fields%

  virtual size_t getFieldsCount() const { return FIELDS_COUNT; }

protected:
  virtual DataError  _read(GameReader&);
  virtual DataError  _write(GameWriter&) const; 
};

} //namespace game

EOD;
    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }

  function tpl_packet_impl($in_bundle = false)
  {
    $HEADER = <<<EOD
#include "%class%.h"
#include "gambit/json.h"
#include "gambit/log.h"

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

EOD;

    $FOOTER = <<<EOD
EOD;

    $BODY = <<<EOD
namespace game {
  
%type%::%type%(Allocator* alloc)
  : NetPacket(%code%, "%type%", alloc)
    %fields_init%
{
  %fields_construct%
}

%type%::~%type%()
{
  %fields_destruct%
}

DataError %type%::_read(GameReader &reader)
{
  //settings optional fields(if any) to default values
  %optional_set%

  %read_buffer%

  return DATA_OK;
}

DataError %type%::_write(GameWriter& writer) const
{
  %write_buffer%

  return DATA_OK;
}

} //namespace game

EOD;

    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }

  function tpl_enum($in_bundle = false)
  {
    $HEADER = <<<EOD
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

EOD;

    $FOOTER = <<<EOD

EOD;

    $BODY = <<<EOD
#ifndef ENUM_%type%_H
#define ENUM_%type%_H

#include "gambit/types.h"

namespace game {

struct %type% 
{
  enum Value
  {
    %fields%
  };

  enum
  {
    ValuesCount = %fields_count%
  };

  static const char* toStr(Value v)
  {
    switch(v)
    {
      %v2str%
    }
    return NULL;
  }

  static bool isValid(int v)
  {
    switch(v)
    {
      %v2bool%
    }
    return false;
  }

  static bool convert(int v1, Value* v2)
  {
    if(!isValid(v1))
     return false;
   
    *v2 = static_cast<Value>(v1);
    return true;
  } 

  static const char* getStrByIdx(int idx)
  {
    static const char* names[] = { %field_names% };
    return names[idx];
  }

  static Value getValueByIdx(int idx)
  {
    static Value values[] = { %field_values% };
    return values[idx];
  }
};

} //namespace game

#endif

EOD;

    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }

  function tpl_struct_header($in_bundle = false)
  {
    $HEADER = <<<EOD
#include "gambit/meta.h"
#include "gambit/types.h"
#include "gambit/fixed_string.h"
#include "gambit/array.h"
#include "gambit/vector.h"
#include "gambit/string.h"
#include "gambit/data.h"
%includes%

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

EOD;

    $FOOTER = <<<EOD

EOD;

    $BODY = <<<EOD
#ifndef STRUCT_%type%_H
#define STRUCT_%type%_H

namespace game {

struct %type% : public %parent%
{
  GAME_RTTI_EX(%type%, %parent%);

  enum { 
    CLASS_ID = %classid%, 
    FIELDS_COUNT = %fields_count%,
  };

  %type%(Allocator* = NULL);
  virtual ~%type%();

  bool operator==(const %type%&) const;

  %fields%

  virtual size_t getFieldsCount() const { return FIELDS_COUNT; }

protected:
  virtual DataError  _read(GameReader&);
  virtual DataError  _write(GameWriter&) const;
};

} //namespace game

#endif

EOD;

    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }


  function tpl_struct_impl($in_bundle = false)
  {
    $HEADER = <<<EOD
#include "%type%.h"
#include "gambit/json.h"
#include "gambit/log.h"

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

EOD;

    $FOOTER = <<<EOD

EOD;

    $BODY = <<<EOD
namespace game {
  
%type%::%type%(Allocator* alloc)
 : %parent%(alloc)
   %fields_init%
{
  %fields_construct%
}

%type%::~%type%()
{
  %fields_destruct%
}

DataError %type%::_read(GameReader &reader)
{
  GAME_CHECK_READ(%parent%::_read(reader), "Parent '%parent%' read error");

  //settings optional fields(if any) to default values
  %optional_set%

  %read_buffer%

  return DATA_OK;
}

DataError %type%::_write(GameWriter& writer) const
{
  GAME_CHECK_WRITE(%parent%::_write(writer), "Parent '%parent%' write error");

  %write_buffer%

  return DATA_OK;
}

bool %type%::operator==(const %type% & rhs) const
{
  if(!(static_cast<%parent%>(*this) == static_cast<%parent%>(rhs)))
    return false;

  %compare_fields%

  return true;
}

} //namespace game

EOD;

    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }

  function tpl_POD_header($in_bundle = false)
  {
    $HEADER = <<<EOD
#include "gambit/types.h"
#include "gambit/array.h"
#include "gambit/fixed_string.h"
#include "gambit/data.h"
%includes%

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

EOD;

    $FOOTER = <<<EOD

EOD;

    $BODY = <<<EOD
#ifndef STRUCT_%type%_H
#define STRUCT_%type%_H

namespace game {

struct %type%
{
  enum { 
    CLASS_ID = %classid%, 
    FIELDS_COUNT = %fields_count%,
  };

  DataError _read(GameReader&);
  DataError read(GameReader&); 
  DataError write(GameWriter&) const; 

  bool operator==(const %type%&) const;

  %fields%
};

} //namespace game

#endif

EOD;

    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }

  function tpl_POD_impl($in_bundle = false)
  {
    $HEADER = <<<EOD
#include "%type%.h"
#include "gambit/json.h"
#include "gambit/log.h"

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

EOD;

    $FOOTER = <<<EOD

EOD;

    $BODY = <<<EOD
namespace game {

DataError %type%::_read(GameReader &reader)
{
  %read_buffer%

  return DATA_OK;
}

DataError %type%::read(GameReader &reader)
{
  GAME_CHECK_READ(reader.beginArray(NULL), "Begin %type%");

  //settings optional fields(if any) to default values
  %optional_set%

  GAME_CHECK_READ(_read(reader), "Body %type%");

  GAME_CHECK_READ(reader.endArray(), "End %type%");

  return DATA_OK;
}

DataError %type%::write(GameWriter& writer) const
{
  GAME_CHECK_WRITE(writer.beginArray(FIELDS_COUNT), "Begin %type%");

  %write_buffer%

  GAME_CHECK_READ(writer.endArray(), "End %type%");

  return DATA_OK;
}

bool %type%::operator==(const %type%& rhs) const
{
  %compare_fields%

  return true;
}

} //namespace game

EOD;

    if($in_bundle)
      return $BODY;
    else
      return $HEADER . $BODY . $FOOTER;
  }

  function tpl_bundle_header()
  {
    $TPL = <<<EOD
#ifndef __GAME_AUTOGEN_H
#define __GAME_AUTOGEN_H

#include "gambit/meta.h"
#include "gambit/net.h"
#include "gambit/types.h"
#include "gambit/array.h"
#include "gambit/fixed_string.h"
#include "gambit/vector.h"
#include "gambit/string.h"

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

%bundle%

#endif //__GAME_AUTOGEN_H

EOD;
    
    return $TPL;
  }

  function tpl_bundle_impl()
  {
    $TPL = <<<EOD
#include "autogen.h"
#include "gambit/crc.h"
#include "gambit/json.h"
#include "gambit/log.h"
#include "gambit/memory.h"

//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

namespace game {

MetaBaseStruct* MetaFactory::createById(u32 crc, Allocator* allocator)
{
  if(!allocator)
    allocator = &memory();

  %create_struct_by_crc28%
  return NULL;
}

MetaBaseStruct* MetaFactory::createByStr(const char* name, Allocator* allocator)
{
  return createById(crc28(name), allocator);
}

MetaBaseStruct* MetaFactory::createRPCResponse(u32 id, Allocator* allocator)
{
  if(!allocator)
    allocator = &memory();

  %create_struct_by_rpcid%
  return NULL;
}

} //namespace game

%bundle%

EOD;
    
    return $TPL;
  }

}


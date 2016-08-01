<?php

class mtg_as3_templater
{
  function tpl_struct()
  {
    $TPL = <<<EOD
package %package_name%.packet {

import flash.utils.*;

import com.bitgames.util.net.util.PacketFieldReader;
import com.bitgames.util.net.util.PacketFieldWriter;
import com.bitgames.util.net.util.NetStructBase;
import com.bitgames.util.net.util.NetStructFieldDesc;
import com.bitgames.util.net.util.FieldNumber;
%includes%

public class %type% %parent_type%
{
  public static const CLASS_ID : int = %class_id%;
  public static const CLASS_NAME : String = "%type%";

%private_fields%

%private_field_getters%
%private_field_setters%

  public function %type%()
  {  
    super();
    // %parent_construct%

%private_fields_init%
  }

  override public function getClassId() : int
  {
    return CLASS_ID;
  }

  override public function getClassName() : String
  {
    return CLASS_NAME;
  }

  override protected function createClassFieldsPropsDesc() : Vector.<NetStructFieldDesc>
  {// virtual
    return new <NetStructFieldDesc>[
         %fields_desc%
      ];
  }


  override protected function getFieldName(field_num : uint) : String
  {//virtual
    return '"' + [%fields_list%][field_num] + '"';
  }

  override protected function doReadFields(reader : PacketFieldReader, field_num : FieldNumber) : void
  {// virtual
    // super.doReadFields(reader, field_num);
%fill_private_fields%
  }

  override protected function doWriteFields(writer : PacketFieldWriter, field_num : FieldNumber) : void
  {// virtual
    // super.doWriteFields(writer, field_num);
%private_fill_buffer%
  } 

  public static function read(reader : PacketFieldReader, is_virtual : Boolean = false) : %type%
  {
    var tmp : %type%;

    if(is_virtual)
    {
      var klass_name : String = reader.nextAsString();
      var klass : Class = Class(getDefinitionByName("%package_name%.packet." + klass_name));
      tmp = new klass();
    }
    else
      tmp = new %type%();

    tmp.readFields(reader);
    return tmp;
  }
}

} //end of package

EOD;
    return $TPL;
  }

  function tpl_packet()
  {
    $TPL = <<<EOD
package %package_name%.packet {

import com.bitgames.util.net.BasePacket;
import com.bitgames.util.net.util.PacketFieldReader;
import com.bitgames.util.net.util.PacketFieldWriter;
import com.bitgames.util.net.util.FieldNumber;
%includes%

public class %class% extends BasePacket
{
  %fields%

  public function %class%(bytes : * = null)
  {  
    super(%code%, "%class%", bytes);        
  }

  public function getClassName() : String
  {
    return "%type%";
  }

  public static function getFieldName(idx : int) : String
  {
    return '"' + [%fields_list%][idx] + '"';
  }

%read_guard_begin%
  override protected function readFields(reader : PacketFieldReader) : void
  {
    var field_num : FieldNumber = new FieldNumber();
    try {
    %fill_fields%
    } catch(ex : *) {throw "Unable extract field " + %class%.getFieldName(field_num.num) + ":" + ex;}
  } 
%read_guard_end%

%write_guard_begin%
  override protected function setDefaultFieldValues() : void
  {
    %fields_init%
  }

  override protected function writeFields(writer : PacketFieldWriter) : void
  {
    var field_num : FieldNumber = new FieldNumber();
    try {
    %fill_buffer%
    } catch(ex : *) {throw "Unable add field " + %class%.getFieldName(field_num.num) + ":" + ex;}
  } 
%write_guard_end%
}

} //end of package

EOD;
    return $TPL;
  }

  function tpl_handler()
  {
    $TPL = <<<EOD
package %package_name% {
import gme.net.BaseHandler;  //TODO: use valid path

public class %class% extends BaseHandler
{
 // public function execute(pckt)
 // {
 //   // UNCOMMENT AND IMPLEMENT PACKET'S HANDLING HERE
 // }
}

} //end of package
 
EOD;
    return $TPL;
  }

  function tpl_map()
  {
    $TPL = <<<EOD
//DO NOT TOUCH THIS FILE IT'S GENERATED AUTOMATICALLY

package %package_name% {

import com.bitgames.util.net.BasePacket;
import com.bitgames.util.net.IPacketFactory;
%imports%

public class %class% implements IPacketFactory 
{
  %references%

  public function %class%()
  {}

  public function createPacket(code : int, data : Object) : BasePacket
  {
    switch(code)
    {
      %map%
      default: 
        throw "Could not map packet to code '" + code + "'";
    }
  }
}

} //end of package

EOD;
  return $TPL;
  }
}
